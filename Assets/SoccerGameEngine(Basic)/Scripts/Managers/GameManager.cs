using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Controllers;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Enums;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Assets.SoccerGameEngine_Basic_.Scripts.Managers
{
    /// <summary>
    /// Manages the entire game
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        enum MatchResultType
        {
            Win,
            Loss,
            Draw,
            Unknown
        }

        [SerializeField]
        MatchInfoPanel _matchInfoPanel;

        [SerializeField]
        MatchOverPanel _matchOverPanel;

        [SerializeField]
        MatchOnPanel _matchOnPanel;

        [SerializeField]
        CameraController _cameraController;

        [SerializeField]
        SuddenDeathPanel _suddenDeathPanel;

        [Header("Flow Timing")]
        [SerializeField]
        [Min(0f)]
        float _initialKickOffDelaySeconds = 2f;

        [SerializeField]
        bool _waitForCinematicBeforeMatchStart = true;

        [SerializeField]
        CinematicCameraSystem _cinematicCameraSystem;

        [Header("Mobile Controls")]
        public CanvasGroup mobileCanvasPanel;

        [SerializeField]
        [Min(0f)]
        float _mobileControlsFadeDuration = 0.25f;

        [SerializeField]
        [Min(0f)]
        float _mobileControlsLeadInSeconds = 2f;

        [Header("Ranked Trophy Rewards")]
        [SerializeField]
        bool _enableTrophyRewards = true;

        [SerializeField]
        [Range(10, 60)]
        int _baseWinTrophies = 30;

        [SerializeField]
        [Range(5, 50)]
        int _baseLossTrophies = 25;

        [SerializeField]
        [Range(40f, 200f)]
        float _trophyDiffScale = 80f;

        [SerializeField]
        [Range(5, 60)]
        int _minWinReward = 15;

        [SerializeField]
        [Range(10, 80)]
        int _maxWinReward = 45;

        [SerializeField]
        [Range(0, 30)]
        int _minLossPenalty = 8;

        [SerializeField]
        [Range(10, 50)]
        int _maxLossPenalty = 38;

        [SerializeField]
        [Range(0, 20)]
        int _maxDrawDelta = 12;

        /// <summary>
        /// Event raised when continuing to second half
        /// </summary>
        public Action OnContinueToSecondHalf;

        /// <summary>
        /// Event raised when switching to match on
        /// </summary>
        public Action OnMessageSwitchToMatchOn;

        Coroutine _delayedMatchStartCoroutine;
        Coroutine _suddenDeathPanelAutoHideCoroutine;
        bool _matchRewardApplied;
        bool _teamsStagedForKickOff;

        private void Awake()
        {
            SoundManager.Instance.PlayAmbienceLoop(false);

            // register the game manager to some events
            Ball.Instance.OnBallLaunched += SoundManager.Instance.PlayBallKickedSound;
            Ball.Instance.OnBallShot += SoundManager.Instance.PlayShotSound;
            MatchManager.Instance.OnGoalScored += SoundManager.Instance.PlayGoalScoredSound;
            MatchManager.Instance.OnPostHit += SoundManager.Instance.PlayPostHitSound;
            MatchManager.Instance.OnMatchPlayStart += SoundManager.Instance.PlayMatchStart;

            if (MatchManager.Instance.TeamAway != null)
            {
                MatchManager.Instance.TeamAway.OnLostPossession += Instance_OnTeamAwayLostPossession;
                MatchManager.Instance.TeamAway.OnGainPossession += Instance_OnTeamAwayGainPossession;
            }

            if (MatchManager.Instance.TeamHome != null)
            {
                MatchManager.Instance.TeamHome.OnLostPossession += Instance_OnTeamHomeLostPossession;
                MatchManager.Instance.TeamHome.OnGainPossession += Instance_OnTeamHomeGainPossession;
            }

            //register managers to listen to me
            OnContinueToSecondHalf += MatchManager.Instance.Instance_OnContinueToSecondHalf;
            OnMessageSwitchToMatchOn += MatchManager.Instance.Instance_OnMessagedSwitchToMatchOn;

            //listen to match manager events
            MatchManager.Instance.OnBroadcastHalfStart += Instance_OnBroadcastHalfStart;
            MatchManager.Instance.OnBroadcastMatchStart += Instance_OnBroadcastMatchStart;
            MatchManager.Instance.OnEnterWaitForMatchOnInstruction += Instance_OnEnterWaitForMatchOnInstruction;
            MatchManager.Instance.OnExitMatchOver += Instance_OnExitMatchOver;
            MatchManager.Instance.OnExitWaitForMatchOnInstruction += Instance_OnExitWaitForMatchOnInstruction;
            MatchManager.Instance.OnFinishBroadcastHalfStart += _Instance_OnFinishBroadcastHalfStart;
            MatchManager.Instance.OnFinishBroadcastMatchStart += Instance_OnFinishBroadcastMatchStart;
            MatchManager.Instance.OnGoalScored += Instance_OnGoalScored;
            MatchManager.Instance.OnPostHit += Instance_OnPostHit;
            MatchManager.Instance.OnEnterSuddenDeath += Instance_OnEnterSuddenDeath;
            MatchManager.Instance.OnMatchOver += Instance_OnMatchOver;
            MatchManager.Instance.OnMatchPlayStart += Instance_OnMatchPlayStart;
            MatchManager.Instance.OnMatchPlayStop += Instance_OnMatchPlayStop;
            MatchManager.Instance.OnTick += Instance_OnTick;

            Instance_OnMessageSwitchToMatchOn();
        }

        private void Instance_OnBroadcastHalfStart(string message)
        {
            ShowInfoPanel(message);
        }

        private void Instance_OnBroadcastMatchStart(string message)
        {
            ShowInfoPanel(message);
        }

        private void Instance_OnEnterWaitForMatchOnInstruction()
        {

        }

        private void Instance_OnExitMatchOver()
        {
            _matchOverPanel.Root.gameObject.SetActive(false);
        }

        private void Instance_OnExitWaitForMatchOnInstruction()
        {

        }

        private void _Instance_OnFinishBroadcastHalfStart()
        {
            HideInfoPanel();
        }

        private void Instance_OnFinishBroadcastMatchStart()
        {
            HideInfoPanel();
        }

        private void Instance_OnGoalScored(string message)
        {
            //show the text
            _matchOnPanel.TxtScores.text = message;
        }

        void Instance_OnTeamAwayLostPossession()
        {
            if (MatchManager.Instance.TeamAway != null && MatchManager.Instance.TeamAway.IsUserControlled)
                SoundManager.Instance.PlayBallLost();
        }

        void Instance_OnTeamHomeLostPossession()
        {
            if (MatchManager.Instance.TeamHome != null && MatchManager.Instance.TeamHome.IsUserControlled)
                SoundManager.Instance.PlayBallLost();
        }

        void Instance_OnTeamAwayGainPossession()
        {
            if (MatchManager.Instance.TeamAway != null && MatchManager.Instance.TeamAway.IsUserControlled)
                SoundManager.Instance.PlayBallRecovered();
        }

        void Instance_OnTeamHomeGainPossession()
        {
            if (MatchManager.Instance.TeamHome != null && MatchManager.Instance.TeamHome.IsUserControlled)
                SoundManager.Instance.PlayBallRecovered();
        }

        private void Instance_OnPostHit(Vector3 worldPoint)
        {
            if (_cameraController == null)
                _cameraController = FindObjectOfType<CameraController>();

            //if (_cameraController != null)
                //_cameraController.Shake(0.14f, 0.22f);
        }

        private void Instance_OnMatchOver(string message)
        {
            StopSuddenDeathPanelAutoHide();
            SetSuddenDeathPanelActive(false);
            _matchOverPanel.TxtInfo.text = message;

            int trophyDelta = ApplyMatchTrophyReward(message);
            UpdateMatchOverRewardText(trophyDelta);

            _matchOverPanel.Root.gameObject.SetActive(true);
        }

        private void Instance_OnMatchPlayStart()
        {
            StopSuddenDeathPanelAutoHide();
            SetSuddenDeathPanelActive(false);
            _matchOnPanel.Root.gameObject.SetActive(true);
            SoundManager.Instance.PlayAmbienceLoop(true);
        }

        private void Instance_OnMatchPlayStop()
        {
            _matchOnPanel.Root.gameObject.SetActive(false);
            SoundManager.Instance.PlayAmbienceLoop(false);
        }

        private void Instance_OnTick(int half, int minutes, int seconds)
        {
            //set the ui
            _matchOnPanel.TxtTime.text = string.Format("{0}:{1}",
                minutes.ToString(),
                seconds.ToString("00"));
        }

        private void HideInfoPanel()
        {
            _matchInfoPanel.Root.gameObject.SetActive(false);
        }

        private void Instance_OnEnterSuddenDeath()
        {
            SetSuddenDeathPanelActive(true);

            StopSuddenDeathPanelAutoHide();
            _suddenDeathPanelAutoHideCoroutine = StartCoroutine(AutoHideSuddenDeathPanel());
        }

        void SetSuddenDeathPanelActive(bool isActive)
        {
            if (_suddenDeathPanel.Root != null)
                _suddenDeathPanel.Root.gameObject.SetActive(isActive);
        }

        IEnumerator AutoHideSuddenDeathPanel()
        {
            yield return new WaitForSeconds(3f);

            SetSuddenDeathPanelActive(false);
            _suddenDeathPanelAutoHideCoroutine = null;
        }

        void StopSuddenDeathPanelAutoHide()
        {
            if (_suddenDeathPanelAutoHideCoroutine == null)
                return;

            StopCoroutine(_suddenDeathPanelAutoHideCoroutine);
            _suddenDeathPanelAutoHideCoroutine = null;
        }

        public void Instance_OnContinueToSecondHalf()
        {
            ActionUtility.Invoke_Action(OnContinueToSecondHalf);
        }

        private void Instance_OnMessageSwitchToMatchOn()
        {
            SoundManager.Instance.PlayMatchStart();

            if (_delayedMatchStartCoroutine != null)
                StopCoroutine(_delayedMatchStartCoroutine);

            _delayedMatchStartCoroutine = StartCoroutine(DelayedMatchStart());
        }

        IEnumerator DelayedMatchStart()
        {
            _teamsStagedForKickOff = false;

            yield return FadeMobileControls(false);

            if (_waitForCinematicBeforeMatchStart)
            {
                if (_cinematicCameraSystem == null)
                    _cinematicCameraSystem = FindObjectOfType<CinematicCameraSystem>();

                if (_cinematicCameraSystem != null)
                {
                    _cinematicCameraSystem.onTransitionToGoalCamera.AddListener(Instance_OnCinematicSwitchedToGoalCamera);

                    if (!_cinematicCameraSystem.IsPlaying)
                        _cinematicCameraSystem.PlaySequence();

                    while (_cinematicCameraSystem != null && _cinematicCameraSystem.IsPlaying)
                    {
                        if (!_teamsStagedForKickOff && _cinematicCameraSystem.HasEnteredGoalShot)
                            StageTeamsForKickOffWithoutStartingMatch();

                        yield return null;
                    }

                    if (_cinematicCameraSystem != null)
                        _cinematicCameraSystem.onTransitionToGoalCamera.RemoveListener(Instance_OnCinematicSwitchedToGoalCamera);

                    if (!_teamsStagedForKickOff)
                        StageTeamsForKickOffWithoutStartingMatch();
                }
                else if (_initialKickOffDelaySeconds > 0f)
                {
                    yield return new WaitForSeconds(_initialKickOffDelaySeconds);
                }
            }
            else if (_initialKickOffDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(_initialKickOffDelaySeconds);
            }

            yield return FadeMobileControls(true);

            if (_mobileControlsLeadInSeconds > 0f)
                yield return new WaitForSeconds(_mobileControlsLeadInSeconds);

            ActionUtility.Invoke_Action(OnMessageSwitchToMatchOn);
            _delayedMatchStartCoroutine = null;
        }

        IEnumerator FadeMobileControls(bool show)
        {
            if (mobileCanvasPanel == null)
                yield break;

            float target = show ? 1f : 0f;
            float duration = Mathf.Max(0f, _mobileControlsFadeDuration);

            if (show)
            {
                mobileCanvasPanel.interactable = true;
                mobileCanvasPanel.blocksRaycasts = true;
            }

            if (duration <= 0f)
            {
                mobileCanvasPanel.alpha = target;
                mobileCanvasPanel.interactable = show;
                mobileCanvasPanel.blocksRaycasts = show;
                yield break;
            }

            float start = mobileCanvasPanel.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                mobileCanvasPanel.alpha = Mathf.Lerp(start, target, t);
                yield return null;
            }

            mobileCanvasPanel.alpha = target;
            mobileCanvasPanel.interactable = show;
            mobileCanvasPanel.blocksRaycasts = show;
        }

        void Instance_OnCinematicSwitchedToGoalCamera()
        {
            StageTeamsForKickOffWithoutStartingMatch();
        }

        void StageTeamsForKickOffWithoutStartingMatch()
        {
            if (_teamsStagedForKickOff || MatchManager.Instance == null)
                return;

            _teamsStagedForKickOff = true;

            PlaceBallAtCentreSpot();
            StageTeamAtKickOffPositions(MatchManager.Instance.TeamAway);
            StageTeamAtKickOffPositions(MatchManager.Instance.TeamHome);
        }

        void PlaceBallAtCentreSpot()
        {
            if (Ball.Instance == null || MatchManager.Instance.TransformCentreSpot == null)
                return;

            Ball.Instance.Owner = null;
            Ball.Instance.Rigidbody.isKinematic = false;
            Ball.Instance.Trap();
            Ball.Instance.Position = MatchManager.Instance.TransformCentreSpot.position;
        }

        void StageTeamAtKickOffPositions(Team team)
        {
            if (team == null || team.Players == null)
                return;

            team.ControllingPlayer = null;
            ActionUtility.Invoke_Action(team.OnInstructPlayersToWait);

            Vector3 centerSpot = MatchManager.Instance != null && MatchManager.Instance.TransformCentreSpot != null
                ? MatchManager.Instance.TransformCentreSpot.position
                : Vector3.zero;

            TeamPlayer kickOffTaker = null;
            if (team.HasKickOff)
            {
                kickOffTaker = team.Players
                    .Where(tM => tM != null && tM.Player != null && tM.Player.PlayerType == PlayerTypes.InFieldPlayer)
                    .OrderBy(tM => Vector3.Distance(tM.Player.Position, centerSpot))
                    .FirstOrDefault();

                if (kickOffTaker == null)
                {
                    kickOffTaker = team.Players
                        .Where(tM => tM != null && tM.Player != null)
                        .LastOrDefault();
                }
            }

            foreach (TeamPlayer teamPlayer in team.Players)
            {
                if (teamPlayer == null || teamPlayer.Player == null || teamPlayer.CurrentHomePosition == null || teamPlayer.KickOffHomePosition == null)
                    continue;

                teamPlayer.CurrentHomePosition.position = teamPlayer.KickOffHomePosition.position;
                teamPlayer.Player.Position = teamPlayer.CurrentHomePosition.position;
                teamPlayer.Player.Rotation = teamPlayer.KickOffHomePosition.rotation;
                teamPlayer.Player.HomeRegion = teamPlayer.CurrentHomePosition;
            }

            if (kickOffTaker == null || kickOffTaker.Player == null || MatchManager.Instance == null || MatchManager.Instance.TransformCentreSpot == null)
                return;

            Vector3 kickoffPosition = MatchManager.Instance.TransformCentreSpot.position
                + (team.Goal.transform.forward * (kickOffTaker.Player.BallControlDistance + kickOffTaker.Player.Radius));

            kickOffTaker.CurrentHomePosition.position = kickoffPosition;
            kickOffTaker.Player.Position = kickoffPosition;

            if (team.KickOffRefDirection != null)
            {
                team.KickOffRefDirection.position = kickoffPosition;
                kickOffTaker.Player.HomeRegion = team.KickOffRefDirection;
                kickOffTaker.Player.transform.rotation = team.KickOffRefDirection.rotation;
            }
        }

        /// <summary>
        /// Quits this game
        /// </summary>
        public void Quit()
        {
            //pasue the editor
#if UNITYEDITOR
            Debug.Break();
#endif
            //quit the game
            Application.Quit();

        }

        /// <summary>
        /// Restart this instance
        /// </summary>
        public void Restart()
        {
            //reload the current scene
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void ShowInfoPanel(string message)
        {
            _matchInfoPanel.TxtInfo.text = message;
            _matchInfoPanel.Root.gameObject.SetActive(true);
        }

        int ApplyMatchTrophyReward(string matchMessage)
        {
            if (_matchRewardApplied || !_enableTrophyRewards)
                return 0;

            _matchRewardApplied = true;

            UserData userData = UserData.Instance;
            if (userData == null || userData.Data == null)
                return 0;

            MatchResultType result = ResolveResult(matchMessage);
            if (result == MatchResultType.Unknown)
                return 0;

            int userTrophies = Mathf.Max(0, userData.Data.trophies);
            int opponentTrophies = GetOpponentTrophies();
            int trophyDelta = CalculateTrophyDelta(result, userTrophies, opponentTrophies);

            if (trophyDelta > 0)
                userData.AddTrophies(trophyDelta);
            else if (trophyDelta < 0)
                userData.SpendTrophies(-trophyDelta);

            int userGoals = GetUserGoals();
            userData.RegisterMatchPlayed(result == MatchResultType.Win, userGoals);

            // Save immediately so fast exits after match do not drop progress.
            userData.Save();
            return trophyDelta;
        }

        void UpdateMatchOverRewardText(int delta)
        {
            if (_matchOverPanel.TxtTrophyReward == null)
                return;

            if (delta > 0)
                _matchOverPanel.TxtTrophyReward.text = $"+{delta}";
            else if (delta < 0)
                _matchOverPanel.TxtTrophyReward.text = $"{delta}";
            else
                _matchOverPanel.TxtTrophyReward.text = "0";
        }

        MatchResultType ResolveResult(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return MatchResultType.Unknown;

            if (message.IndexOf("You Won", StringComparison.OrdinalIgnoreCase) >= 0)
                return MatchResultType.Win;

            if (message.IndexOf("You Lost", StringComparison.OrdinalIgnoreCase) >= 0)
                return MatchResultType.Loss;

            if (message.IndexOf("Draw", StringComparison.OrdinalIgnoreCase) >= 0)
                return MatchResultType.Draw;

            return MatchResultType.Unknown;
        }

        int CalculateTrophyDelta(MatchResultType result, int userTrophies, int opponentTrophies)
        {
            int diff = opponentTrophies - userTrophies;
            int scaled = Mathf.RoundToInt(diff / Mathf.Max(1f, _trophyDiffScale));

            if (result == MatchResultType.Win)
                return Mathf.Clamp(_baseWinTrophies + scaled, _minWinReward, _maxWinReward);

            if (result == MatchResultType.Loss)
            {
                int loss = Mathf.Clamp(_baseLossTrophies - scaled, _minLossPenalty, _maxLossPenalty);
                return -loss;
            }

            if (result == MatchResultType.Draw)
                return Mathf.Clamp(scaled, -_maxDrawDelta, _maxDrawDelta);

            return 0;
        }

        int GetOpponentTrophies()
        {
            AiData aiData = AiData.Instance;
            if (aiData != null)
                return Mathf.Max(0, aiData.AiTrophies);

            return 0;
        }

        int GetUserGoals()
        {
            if (MatchManager.Instance == null)
                return 0;

            if (MatchManager.Instance.TeamHome != null && MatchManager.Instance.TeamHome.IsUserControlled)
                return Mathf.Max(0, MatchManager.Instance.TeamHome.Goals);

            if (MatchManager.Instance.TeamAway != null && MatchManager.Instance.TeamAway.IsUserControlled)
                return Mathf.Max(0, MatchManager.Instance.TeamAway.Goals);

            return 0;
        }

        public void ReturnToMainMenu(string sceneName)
        {
            if (!string.IsNullOrWhiteSpace(sceneName) && UserData.Instance != null)
                UserData.Instance.Save();

            if (!string.IsNullOrWhiteSpace(sceneName))
                SceneManager.LoadScene(sceneName);
        }
    }

    [Serializable]
    public struct MainPanel
    {
        public Transform Root;
    }

    [Serializable]
    public struct MatchInfoPanel
    {
        public Text TxtInfo;

        public Transform Root;
    }

    [Serializable]
    public struct MatchOnPanel
    {
        public TMP_Text TxtScores;

        public TMP_Text TxtTime;

        public Transform Root;
    }

    [Serializable]
    public struct MatchOverPanel
    {
        public Text TxtInfo;

        public TMP_Text TxtTrophyReward;

        public Transform Root;
    }

    [Serializable]
    public struct SuddenDeathPanel
    {
        public Text TxtInfo;

        public Transform Root;
    }
}
