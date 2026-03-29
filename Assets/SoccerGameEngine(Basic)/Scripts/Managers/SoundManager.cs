using System;
using Patterns.Singleton;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.Managers
{
    public class SoundManager : Singleton<SoundManager>
    {
        public AudioSource _ballKickAS;

        public AudioSource _goalAS;

        public AudioSource _postHitAS;

        public AudioSource _matchAmbience;

        public void PlayBallKickedSound(float flightTime, float velocity, Vector3 initial, Vector3 target)
        {
            _ballKickAS.Play();
        }

        public void PlayGoalScoredSound(string message)
        {
            _goalAS.Play();
        }

        public void PlayPostHitSound(Vector3 worldPoint)
        {
            if (_postHitAS == null)
                return;

            float initialVolume = _postHitAS.volume;
            _postHitAS.volume = Mathf.Clamp01(initialVolume * 1.2f);
            _postHitAS.Play();
            _postHitAS.volume = initialVolume;
        }
    }
}
