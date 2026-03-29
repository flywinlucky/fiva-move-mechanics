using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Controllers;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.SoccerGameEngine_Basic_.Scripts.Managers
{
    /// <summary>
    /// Manages the entire game
    /// </summary>
    public class GameManager : MonoBehaviour
    {
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

        private void Awake()
        {
            // register the game manager to some events
            Ball.Instance.OnBallLaunched += SoundManager.Instance.PlayBallKickedSound;
            MatchManager.Instance.OnGoalScored += SoundManager.Instance.PlayGoalScoredSound;
            MatchManager.Instance.OnPostHit += SoundManager.Instance.PlayPostHitSound;

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

        private void Instance_OnPostHit(Vector3 worldPoint)
        {
            if (_cameraController == null)
                _cameraController = FindObjectOfType<CameraController>();

            if (_cameraController != null)
                _cameraController.Shake(0.14f, 0.22f);
        }

        private void Instance_OnMatchOver(string message)
        {
            StopSuddenDeathPanelAutoHide();
            SetSuddenDeathPanelActive(false);
            _matchOverPanel.TxtInfo.text = message;
            _matchOverPanel.Root.gameObject.SetActive(true);
        }

        private void Instance_OnMatchPlayStart()
        {
            StopSuddenDeathPanelAutoHide();
            SetSuddenDeathPanelActive(false);
            _matchOnPanel.Root.gameObject.SetActive(true);
        }

        private void Instance_OnMatchPlayStop()
        {
            _matchOnPanel.Root.gameObject.SetActive(false);
        }

        private void Instance_OnTick(int half, int minutes, int seconds)
        {
            //set the ui
            _matchOnPanel.TxtTime.text = string.Format("{0}:{1}",
                minutes.ToString("00"),
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
            if (_delayedMatchStartCoroutine != null)
                StopCoroutine(_delayedMatchStartCoroutine);

            _delayedMatchStartCoroutine = StartCoroutine(DelayedMatchStart());
        }

        IEnumerator DelayedMatchStart()
        {
            yield return new WaitForSeconds(3f);
            ActionUtility.Invoke_Action(OnMessageSwitchToMatchOn);
            _delayedMatchStartCoroutine = null;
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
        public Text TxtScores;

        public Text TxtTime;

        public Transform Root;
    }

    [Serializable]
    public struct MatchOverPanel
    {
        public Text TxtInfo;

        public Transform Root;
    }

    [Serializable]
    public struct SuddenDeathPanel
    {
        public Text TxtInfo;

        public Transform Root;
    }
}
