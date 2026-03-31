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

        public override void Enter()
        {
            base.Enter();

            Player carrier = Ball.Instance != null ? Ball.Instance.Owner : null;

            // Dynamic duel result feels more natural than fixed 50/50.
            float tackleSuccessChance = EvaluateTackleSuccessChance(carrier);
            _isTackleSuccessful = carrier != null && Random.value <= tackleSuccessChance;

            // Successful tackles resolve slightly faster for responsive casual feel.
            _waitTime = _isTackleSuccessful ? 0.18f : 0.25f;

            //if tackle is successful, then message the ball owner
            //that he has been tackled
            if(_isTackleSuccessful)
            {
                ActionUtility.Invoke_Action(carrier.OnTackled);
            }
        }

        public override void Execute()
        {
            base.Execute();

            //decrement time
            _waitTime -= Time.deltaTime;

            //if time if exhausted trigger approprite state transation
            if(_waitTime <= 0)
            {
                if (_isTackleSuccessful)
                    SuperMachine.ChangeState<ControlBallMainState>();
                else
                    SuperMachine.ChangeState<GoToHomeMainState>();
            }
        }

        float EvaluateTackleSuccessChance(Player carrier)
        {
            if (carrier == null)
                return 0f;

            float chance = 0.55f;

            // Mild assist for user feel without making tackles unfair.
            if (Owner.IsUserControlled && !carrier.IsUserControlled)
                chance += 0.1f;
            else if (!Owner.IsUserControlled && carrier.IsUserControlled)
                chance -= 0.05f;

            if (MatchManager.Instance != null)
            {
                MatchDifficulty difficulty = MatchManager.Instance.Difficulty;
                if (!Owner.IsUserControlled && carrier.IsUserControlled)
                {
                    if (difficulty == MatchDifficulty.Casual)
                        chance -= 0.12f;
                    else if (difficulty == MatchDifficulty.Normal)
                        chance -= 0.05f;

                    // QTE block button: without taps user keeps ball ~5%, with multiple taps up to ~70%.
                    int blockTapCount = MobileControlsInput.ConsumeTakeKickOffBlockPressCount();
                    float keepBallChance = blockTapCount <= 0
                        ? 0.05f
                        : Mathf.Lerp(0.35f, 0.70f, Mathf.Clamp01(blockTapCount / 4f));

                    float tackleSuccessFromQte = 1f - keepBallChance;
                    chance = Mathf.Lerp(chance, tackleSuccessFromQte, 0.9f);
                }
                else if (Owner.IsUserControlled && !carrier.IsUserControlled)
                {
                    if (difficulty == MatchDifficulty.Casual)
                        chance += 0.08f;
                    else if (difficulty == MatchDifficulty.Normal)
                        chance += 0.04f;
                }
            }

            float tackleReach = Mathf.Max(0.75f, Owner.BallControlDistance + Owner.Radius + 0.35f);
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

        Player Owner => ((InFieldPlayerFSM)SuperMachine).Owner;
    }
}
