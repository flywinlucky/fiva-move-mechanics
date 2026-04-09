using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement; 
using YG;
#if InterstitialAdv_yg
using YG.Insides;
#endif

public class RestartCurrentScene : MonoBehaviour
{
    bool _isWaitingForAd;
    int _pendingSceneIndex = -1;
    string _pendingSceneName;
    Coroutine _adFallbackRoutine;

    [SerializeField]
    [Min(0.5f)]
    float adCallbackTimeoutSeconds = 5f;

    void OnDisable()
    {
#if InterstitialAdv_yg
        UnsubscribeInterEvents();
#endif
        if (_adFallbackRoutine != null)
        {
            StopCoroutine(_adFallbackRoutine);
            _adFallbackRoutine = null;
        }

        _isWaitingForAd = false;
        _pendingSceneIndex = -1;
        _pendingSceneName = null;
    }

    void SaveUserDataBeforeSceneLoad()
    {
        if (UserData.Instance != null)
            UserData.Instance.Save();
    }

    public void RestartGame()
    {
        SaveUserDataBeforeSceneLoad();
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        RequestSceneLoadWithInterstitial(currentSceneIndex, null);
    }

    public void SwitchScene(string sceneName)
    {
        SaveUserDataBeforeSceneLoad();
        RequestSceneLoadWithInterstitial(-1, sceneName);
    }

    void RequestSceneLoadWithInterstitial(int sceneIndex, string sceneName)
    {
        if (_isWaitingForAd)
            return;

        _pendingSceneIndex = sceneIndex;
        _pendingSceneName = sceneName;

#if InterstitialAdv_yg
        if (!YG2.nowAdsShow)
        {
            _isWaitingForAd = true;
            SubscribeInterEvents();

            // Force timer completion so ad can be requested right now for scene transitions.
            YGInsides.ResetTimerInterAdv();
            YG2.InterstitialAdvShow();

            if (_adFallbackRoutine != null)
                StopCoroutine(_adFallbackRoutine);

            _adFallbackRoutine = StartCoroutine(AdCallbackTimeoutFallback());
            return;
        }
#endif

        LoadPendingScene();
    }

#if InterstitialAdv_yg
    void SubscribeInterEvents()
    {
        YG2.onCloseInterAdvWasShow += OnInterClosed;
        YG2.onErrorInterAdv += OnInterError;
    }

    void UnsubscribeInterEvents()
    {
        YG2.onCloseInterAdvWasShow -= OnInterClosed;
        YG2.onErrorInterAdv -= OnInterError;
    }

    void OnInterClosed(bool wasShown)
    {
        StopAdFallbackRoutine();
        UnsubscribeInterEvents();
        _isWaitingForAd = false;
        LoadPendingScene();
    }

    void OnInterError()
    {
        StopAdFallbackRoutine();
        UnsubscribeInterEvents();
        _isWaitingForAd = false;
        LoadPendingScene();
    }

    IEnumerator AdCallbackTimeoutFallback()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, adCallbackTimeoutSeconds));

        if (!_isWaitingForAd)
        {
            _adFallbackRoutine = null;
            yield break;
        }

        UnsubscribeInterEvents();
        _isWaitingForAd = false;
        _adFallbackRoutine = null;
        LoadPendingScene();
    }

    void StopAdFallbackRoutine()
    {
        if (_adFallbackRoutine == null)
            return;

        StopCoroutine(_adFallbackRoutine);
        _adFallbackRoutine = null;
    }
#endif

    void LoadPendingScene()
    {
        if (!string.IsNullOrWhiteSpace(_pendingSceneName))
        {
            string sceneToLoad = _pendingSceneName;
            _pendingSceneName = null;
            _pendingSceneIndex = -1;
            SceneManager.LoadScene(sceneToLoad);
            return;
        }

        if (_pendingSceneIndex >= 0)
        {
            int sceneToLoad = _pendingSceneIndex;
            _pendingSceneIndex = -1;
            SceneManager.LoadScene(sceneToLoad);
        }
    }
}