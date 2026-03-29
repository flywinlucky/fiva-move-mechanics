using System;
using Patterns.Singleton;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.Managers
{
    public class SoundManager : Singleton<SoundManager>
    {
        [Header("Audio Sources")]
        public AudioSource _matchStartAS;

        public AudioSource _passAS;

        public AudioSource _shotAS;

        public AudioSource _netImpactAS;

        public AudioSource _crowdAS;

        public AudioSource _loseBallAS;

        public AudioSource _gainControlAS;

        public AudioSource _goalkeeperCatchAS;

        public AudioSource _postHitAS;

        public AudioSource _matchAmbience;

        [Header("Clip Arrays")]
        public AudioClip[] matchStartWhistle;

        public AudioClip[] passSounds;

        public AudioClip[] shotSounds;

        public AudioClip[] netImpactSounds;

        public AudioClip[] crowdCheerSounds;

        public AudioClip[] loseBallSounds;

        public AudioClip[] gainControlSounds;

        public AudioClip[] goalkeeperCatchSounds;

        public AudioClip[] postHitSounds;

        [Header("Randomization")]
        [SerializeField]
        [Range(0.7f, 1.3f)]
        float _minRandomPitch = 0.9f;

        [SerializeField]
        [Range(0.7f, 1.3f)]
        float _maxRandomPitch = 1.1f;

        [SerializeField]
        [Range(0f, 0.2f)]
        float _passSuppressAfterShotSeconds = 0.08f;

        [Header("Category Volumes")]
        [SerializeField]
        [Range(0f, 1.5f)]
        float _matchStartVolume = 0.95f;

        [SerializeField]
        [Range(0f, 1.5f)]
        float _passVolume = 0.72f;

        [SerializeField]
        [Range(0f, 1.5f)]
        float _shotVolume = 0.96f;

        [SerializeField]
        [Range(0f, 1.5f)]
        float _netImpactVolume = 1.0f;

        [SerializeField]
        [Range(0f, 1.5f)]
        float _crowdVolume = 0.90f;

        [SerializeField]
        [Range(0f, 1.5f)]
        float _loseBallVolume = 0.82f;

        [SerializeField]
        [Range(0f, 1.5f)]
        float _gainControlVolume = 0.80f;

        [SerializeField]
        [Range(0f, 1.5f)]
        float _goalkeeperCatchVolume = 0.88f;

        [SerializeField]
        [Range(0f, 1.5f)]
        float _postHitVolume = 1.05f;

        [SerializeField]
        bool _avoidImmediateClipRepeat = true;

        float _lastShotSfxTime = -10f;

        int _lastMatchStartIndex = -1;
        int _lastPassIndex = -1;
        int _lastShotIndex = -1;
        int _lastNetImpactIndex = -1;
        int _lastCrowdIndex = -1;
        int _lastLoseBallIndex = -1;
        int _lastGainControlIndex = -1;
        int _lastGoalKeeperCatchIndex = -1;
        int _lastPostHitIndex = -1;

        bool PlayRandomClip(AudioSource source, AudioClip[] clipArray, ref int lastIndex, float volume = 1.0f)
        {
            if (source == null || clipArray == null || clipArray.Length == 0)
                return false;

            int randomIndex = UnityEngine.Random.Range(0, clipArray.Length);
            if (_avoidImmediateClipRepeat && clipArray.Length > 1 && randomIndex == lastIndex)
                randomIndex = (randomIndex + UnityEngine.Random.Range(1, clipArray.Length)) % clipArray.Length;

            AudioClip clip = clipArray[randomIndex];
            if (clip == null)
                return false;

            lastIndex = randomIndex;

            float originalPitch = source.pitch;
            source.pitch = UnityEngine.Random.Range(
                Mathf.Min(_minRandomPitch, _maxRandomPitch),
                Mathf.Max(_minRandomPitch, _maxRandomPitch));

            source.PlayOneShot(clip, Mathf.Clamp01(volume));
            source.pitch = originalPitch;
            return true;
        }

        bool PlayRandomClip(AudioSource source, AudioClip[] clipArray, float volume = 1.0f)
        {
            int discard = -1;
            return PlayRandomClip(source, clipArray, ref discard, volume);
        }

        [ContextMenu("Apply Recommended Mix Preset")]
        public void ApplyRecommendedMixPreset()
        {
            _minRandomPitch = 0.92f;
            _maxRandomPitch = 1.08f;
            _passSuppressAfterShotSeconds = 0.08f;

            _matchStartVolume = 0.95f;
            _passVolume = 0.72f;
            _shotVolume = 0.96f;
            _netImpactVolume = 1.0f;
            _crowdVolume = 0.90f;
            _loseBallVolume = 0.82f;
            _gainControlVolume = 0.80f;
            _goalkeeperCatchVolume = 0.88f;
            _postHitVolume = 1.05f;
        }

        public void PlayAmbienceLoop(bool play)
        {
            if (_matchAmbience == null)
                return;

            if (play)
            {
                if (!_matchAmbience.isPlaying)
                    _matchAmbience.Play();
            }
            else
            {
                if (_matchAmbience.isPlaying)
                    _matchAmbience.Stop();
            }
        }

        // Trigger from match flow (ex: pre-kickoff countdown / match start)
        public void PlayMatchStart()
        {
            PlayRandomClip(_matchStartAS, matchStartWhistle, ref _lastMatchStartIndex, _matchStartVolume);
        }

        // Trigger from pass systems (ex: Ball.OnBallLaunched, filtered against shot window)
        public void PlayPass()
        {
            PlayRandomClip(_passAS, passSounds, ref _lastPassIndex, _passVolume);
        }

        // Trigger from shot systems (ex: Ball.OnBallShot)
        public void PlayShot()
        {
            _lastShotSfxTime = Time.time;
            PlayRandomClip(_shotAS, shotSounds, ref _lastShotIndex, _shotVolume);
        }

        // Trigger when goal is scored / net is hit
        public void PlayGoal()
        {
            PlayRandomClip(_netImpactAS, netImpactSounds, ref _lastNetImpactIndex, _netImpactVolume);
            PlayRandomClip(_crowdAS, crowdCheerSounds, ref _lastCrowdIndex, _crowdVolume);
        }

        // Trigger when user team loses possession
        public void PlayBallLost()
        {
            PlayRandomClip(_loseBallAS, loseBallSounds, ref _lastLoseBallIndex, _loseBallVolume);
        }

        // Trigger when user team recovers possession / control
        public void PlayBallRecovered()
        {
            PlayRandomClip(_gainControlAS, gainControlSounds, ref _lastGainControlIndex, _gainControlVolume);
        }

        // Trigger when goalkeeper secures the ball
        public void PlayGKCatch()
        {
            PlayRandomClip(_goalkeeperCatchAS, goalkeeperCatchSounds, ref _lastGoalKeeperCatchIndex, _goalkeeperCatchVolume);
        }

        public void PlayShotSound(float flightTime, float velocity, Vector3 initial, Vector3 target)
        {
            PlayShot();
        }

        public void PlayBallKickedSound(float flightTime, float velocity, Vector3 initial, Vector3 target)
        {
            if (Time.time - _lastShotSfxTime <= _passSuppressAfterShotSeconds)
                return;

            PlayPass();
        }

        public void PlayGoalScoredSound(string message)
        {
            PlayGoal();
        }

        public void PlayPostHitSound(Vector3 worldPoint)
        {
            PlayRandomClip(_postHitAS, postHitSounds, ref _lastPostHitIndex, _postHitVolume);
        }
    }
}
