using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.ControlBall.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.GoToHome.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities;
using RobustFSM.Base;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.TacklePlayer.MainState
{
    public class TackleMainState : BState
    {
        bool _isTackleSuccessful;
        float _waitTime;
        bool _isDuelWindowActive;
        float _duelElapsed;
        Player _carrier;
        Player _duelUserPlayer;
        int _duelStartDefendTapSequence;
        bool _duelWidgetClosedByDefendTap;

        const float DuelWindowDurationSeconds = 0.7f;
        const float FailedTackleResolveDelay = 0.15f;
        const float CarrierDuelLockExtraSeconds = 0.2f;
        const float DuelBreakDistanceBuffer = 0.15f;
        const float NoDefendTapAttackerWinChance = 0.8f;
        const float AIAttackerMinWinChance = 0.7f;
        const float UserTakeTapGuaranteedWinChance = 0.85f;

        public override void Enter()
        {
            base.Enter();

            _carrier = Ball.Instance != null ? Ball.Instance.Owner : null;
            _isDuelWindowActive = false;
            _duelElapsed = 0f;
            _duelUserPlayer = null;
            _duelStartDefendTapSequence = MobileControlsInput.GetDefendTapSequence();
            _duelWidgetClosedByDefendTap = false;

            if (_carrier == null || _carrier == Owner)
            {
                _isTackleSuccessful = false;
                _waitTime = FailedTackleResolveDelay;
                return;
            }

            // If user initiated TAKE with a fresh tap, count that tap for this duel window.
            if (Owner.IsUserControlled
                && !_carrier.IsUserControlled
                && MobileControlsInput.WasDefendTappedRecently(0.2f))
            {
                _duelStartDefendTapSequence = Mathf.Max(0, _duelStartDefendTapSequence - 1);
            }

            // Prevent concurrent duels on the same carrier from resolving ownership in opposite directions.
            if (Time.time < _carrier.TackleDuelLockUntil)
            {
                _isTackleSuccessful = false;
                _waitTime = FailedTackleResolveDelay;
                return;
            }

            float duelLockDuration = DuelWindowDurationSeconds + CarrierDuelLockExtraSeconds;
            _carrier.TackleDuelLockUntil = Mathf.Max(_carrier.TackleDuelLockUntil, Time.time + duelLockDuration);

            _duelUserPlayer = Owner.IsUserControlled ? Owner : (_carrier.IsUserControlled ? _carrier : null);
            if (_duelUserPlayer != null)
            {
                _isDuelWindowActive = true;
                _duelElapsed = 0f;
                _duelWidgetClosedByDefendTap = false;
                _duelUserPlayer.SetDefendWidgetState(true, 0f);
                return;
            }

            ResolveTackleDuel();
        }

        public override void Execute()
        {
            base.Execute();

            if (_isDuelWindowActive)
            {
                if (!IsCarrierStillValidForDuel())
                {
                    CancelCurrentDuel();
                    return;
                }

                if (!_duelWidgetClosedByDefendTap && DidUserTapDefendDuringDuel())
                {
                    _duelWidgetClosedByDefendTap = true;
                    HideAndResetDuelWidget();
                }

                _duelElapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(_duelElapsed / DuelWindowDurationSeconds);

                if (_duelUserPlayer != null && !_duelWidgetClosedByDefendTap)
                    _duelUserPlayer.SetDefendWidgetState(true, progress);

                if (_duelElapsed < DuelWindowDurationSeconds)
                    return;

                _isDuelWindowActive = false;
                ClearDefendWidget();
                ResolveTackleDuel();
                return;
            }

            //decrement time
            _waitTime -= Time.deltaTime;

            //if time if exhausted trigger approprite state transation
            if(_waitTime <= 0)
            {
                if (_isTackleSuccessful && Ball.Instance != null && Ball.Instance.Owner == Owner)
                    SuperMachine.ChangeState<ControlBallMainState>();
                else
                    SuperMachine.ChangeState<GoToHomeMainState>();
            }
        }

        public override void Exit()
        {
            base.Exit();
            ClearDefendWidget();
        }

        void ResolveTackleDuel()
        {
            if (_carrier == null || !IsCarrierStillValidForDuel())
            {
                ReleaseCarrierDuelLockEarly();
                _isTackleSuccessful = false;
                _waitTime = FailedTackleResolveDelay;
                return;
            }

            // Dynamic duel result feels more natural than fixed 50/50.
            float tackleSuccessChance = EvaluateTackleSuccessChance(_carrier);

            // If user carrier does not tap defend at least once during this duel, attacker gets a strong edge.
            if (_carrier.IsUserControlled && !DidUserTapDefendDuringDuel())
                tackleSuccessChance = Mathf.Max(tackleSuccessChance, NoDefendTapAttackerWinChance);

            MatchDifficultyProfile runtimeProfile = GetRuntimeDifficultyProfile();

            // In TAKE mode, one or more taps should give user a strong minimum steal chance.
            if (Owner.IsUserControlled && !_carrier.IsUserControlled && DidUserTapDefendDuringDuel())
                tackleSuccessChance = Mathf.Max(
                    tackleSuccessChance,
                    Mathf.Max(runtimeProfile.UserTakeTapMinWinChance, UserTakeTapGuaranteedWinChance));

            // Requested balance: when AI attacks user carrier, keep a stable 70% minimum steal chance.
            if (!Owner.IsUserControlled && _carrier.IsUserControlled)
                tackleSuccessChance = Mathf.Max(tackleSuccessChance, AIAttackerMinWinChance);

            _isTackleSuccessful = Random.value <= tackleSuccessChance;

            // Successful tackles resolve slightly faster for responsive casual feel.
            _waitTime = _isTackleSuccessful ? 0.18f : FailedTackleResolveDelay;

            if (_isTackleSuccessful)
            {
                ActionUtility.Invoke_Action(_carrier.OnTackled);

                // Re-attach ball immediately to the tackle winner to avoid detached/loose-frame state.
                if (Ball.Instance != null)
                {
                    Ball.Instance.Owner = Owner;
                    if (Ball.Instance.Rigidbody != null)
                        Ball.Instance.Rigidbody.isKinematic = true;

                    Owner.PlaceBallInfronOfMe();
                }
            }
        }

        bool DidUserTapDefendDuringDuel()
        {
            return MobileControlsInput.GetDefendTapSequence() > _duelStartDefendTapSequence;
        }

        bool IsCarrierStillValidForDuel()
        {
            return Ball.Instance != null
                && _carrier != null
                && Ball.Instance.Owner == _carrier
                && IsWithinDuelDistance();
        }

        bool IsWithinDuelDistance()
        {
            if (_carrier == null)
                return false;

            MatchDifficultyProfile runtimeProfile = GetRuntimeDifficultyProfile();
            float attackerScale = (!Owner.IsUserControlled && _carrier.IsUserControlled)
                ? Mathf.Clamp(runtimeProfile.AITackleEngageDistanceScale, 0.4f, 1.1f)
                : 1f;

            float attackerTackleReach = Mathf.Max(0.75f, Owner.BallTacklableDistance * attackerScale + Owner.Radius);
            float carrierBodyAllowance = Mathf.Max(0.15f, _carrier.Radius * 0.65f);
            float maxDuelDistance = attackerTackleReach + carrierBodyAllowance + DuelBreakDistanceBuffer;

            // Keep user-initiated TAKE duels stable while both players move at speed.
            if (Owner.IsUserControlled && !_carrier.IsUserControlled && DidUserTapDefendDuringDuel())
                maxDuelDistance += 0.22f;

            Vector3 delta = Owner.Position - _carrier.Position;
            delta.y = 0f;
            return delta.sqrMagnitude <= (maxDuelDistance * maxDuelDistance);
        }

        void CancelCurrentDuel()
        {
            _isDuelWindowActive = false;
            ClearDefendWidget();
            _isTackleSuccessful = false;
            _waitTime = FailedTackleResolveDelay;
            ReleaseCarrierDuelLockEarly();
        }

        void ReleaseCarrierDuelLockEarly()
        {
            if (_carrier == null)
                return;

            float shortLock = Time.time + 0.1f;
            if (_carrier.TackleDuelLockUntil > shortLock)
                _carrier.TackleDuelLockUntil = shortLock;
        }

        void ClearDefendWidget()
        {
            if (_duelUserPlayer != null)
                _duelUserPlayer.ResetDefendWidget();
        }

        void HideAndResetDuelWidget()
        {
            if (_duelUserPlayer == null)
                return;

            _duelUserPlayer.SetDefendWidgetState(false, 0f);
        }

        float EvaluateTackleSuccessChance(Player carrier)
        {
            if (carrier == null)
                return 0f;

            float chance = 0.55f;
            MatchDifficultyProfile runtimeProfile = GetRuntimeDifficultyProfile();

            // Mild assist for user feel without making tackles unfair.
            if (Owner.IsUserControlled && !carrier.IsUserControlled)
                chance += 0.1f;
            else if (!Owner.IsUserControlled && carrier.IsUserControlled)
                chance -= 0.05f;

            if (Owner.IsUserControlled && !carrier.IsUserControlled)
            {
                chance += runtimeProfile.UserDuelControlBonus;

                // Simulate occasional AI defensive input miss.
                if (Random.value <= runtimeProfile.AIDefendMissChance)
                    chance += 0.10f;
            }
            else if (!Owner.IsUserControlled && carrier.IsUserControlled)
            {
                chance -= runtimeProfile.UserDuelControlBonus * 0.75f;
            }

            if (MatchManager.Instance != null)
            {
                MatchDifficulty difficulty = MatchManager.Instance.Difficulty;
                runtimeProfile = MatchManager.Instance.RuntimeDifficultyProfile;

                if (!Owner.IsUserControlled && carrier.IsUserControlled)
                {
                    chance -= (runtimeProfile.AIPlayerInterceptionAssist * 0.55f);
                    chance -= (runtimeProfile.AIErrorChanceBase * 0.20f);

                    if (difficulty == MatchDifficulty.Casual)
                        chance -= 0.04f;
                    else if (difficulty == MatchDifficulty.Normal)
                        chance += 0.01f;
                }
                else if (Owner.IsUserControlled && !carrier.IsUserControlled)
                {
                    chance += (runtimeProfile.AIPlayerInterceptionAssist * 0.30f);

                    if (difficulty == MatchDifficulty.Casual)
                        chance += 0.03f;
                    else if (difficulty == MatchDifficulty.Normal)
                        chance += 0.01f;
                }
            }

            // Mobile defend mini-battle: taps improve user chance to win the current duel.
            if (Owner.IsUserControlled && !carrier.IsUserControlled)
            {
                float userAttackBonus = MobileControlsInput.ConsumeDefendDuelBonus(
                    userIsAttacker: true,
                    userStamina01: Owner.CurrentStaminaNormalized,
                    opponentStamina01: carrier.CurrentStaminaNormalized);

                chance += userAttackBonus;
            }
            else if (!Owner.IsUserControlled && carrier.IsUserControlled)
            {
                float userRetainBonus = MobileControlsInput.ConsumeDefendDuelBonus(
                    userIsAttacker: false,
                    userStamina01: carrier.CurrentStaminaNormalized,
                    opponentStamina01: Owner.CurrentStaminaNormalized);

                chance -= userRetainBonus;
            }

            float tackleReach = Mathf.Max(0.75f, Owner.BallTacklableDistance + Owner.Radius);
            float distanceToCarrier = Vector3.Distance(Owner.Position, carrier.Position);
            float proximity = 1f - Mathf.Clamp01(distanceToCarrier / tackleReach);
            chance += proximity * 0.2f;

            float attackerSpeed = ResolvePlayerSpeed(Owner);
            float carrierSpeed = ResolvePlayerSpeed(carrier);
            float speedAdvantage = attackerSpeed - carrierSpeed;
            chance += Mathf.Clamp(speedAdvantage / 12f, -0.12f, 0.12f);

            // Tackles from behind are easier; from in front are harder.
            Vector3 toAttacker = Owner.Position - carrier.Position;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude > 0.0001f)
            {
                float facingDot = Vector3.Dot(carrier.transform.forward.normalized, toAttacker.normalized);
                if (facingDot < -0.2f)
                    chance += 0.08f;
                else if (facingDot > 0.4f)
                    chance -= 0.08f;
            }

            return Mathf.Clamp(chance, 0.2f, 0.85f);
        }

        float ResolvePlayerSpeed(Player player)
        {
            if (player == null)
                return 0f;

            if (player.RPGMovement != null)
                return Mathf.Max(0f, player.RPGMovement.CurrentSpeed);

            return Mathf.Max(0f, player.ActualSpeed);
        }

        MatchDifficultyProfile GetRuntimeDifficultyProfile()
        {
            if (MatchManager.Instance != null)
                return MatchManager.Instance.RuntimeDifficultyProfile;

            return new MatchDifficultyProfile
            {
                AITackleEngageDistanceScale = 0.78f,
                UserDuelControlBonus = 0.10f,
                AIDefendMissChance = 0.20f,
                UserTakeTapMinWinChance = 0.90f
            };
        }

        Player Owner => ((InFieldPlayerFSM)SuperMachine).Owner;
    }
}
