using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YG
{
    public class LBPlayerDataYG : MonoBehaviour
    {
        public ImageLoadYG imageLoad;

        [Serializable]
        public struct TextLegasy
        {
            public Text rank, name, score;
        }
        public TextLegasy textLegasy;

        [Serializable]
        public struct TextMP
        {
            public TMP_Text rank, name, score;
        }
        public TextMP textMP;
        [Space(10)]
        public MonoBehaviour[] topPlayerActivityComponents = new MonoBehaviour[0];
        public MonoBehaviour[] currentPlayerActivityComponents = new MonoBehaviour[0];

        public class Data
        {
            public string rank;
            public string name;
            public string score;
            public string photoUrl;
            public bool inTop;
            public bool currentPlayer;
            public Sprite photoSprite;
        }

        [HideInInspector]
        public Data data = new Data();

        private void Awake()
        {
            ResolveTextBindings();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveTextBindings();
        }
#endif

        public void UpdateEntries()
        {
            ResolveTextBindings();

            if (textLegasy.rank && data.rank != null) textLegasy.rank.text = data.rank.ToString();
            if (textLegasy.name && data.name != null) textLegasy.name.text = data.name;
            if (textLegasy.score && data.score != null) textLegasy.score.text = data.score.ToString();

            if (textMP.rank && data.rank != null) textMP.rank.text = data.rank.ToString();
            if (textMP.name && data.name != null) textMP.name.text = data.name;
            if (textMP.score && data.score != null) textMP.score.text = data.score.ToString();
            if (imageLoad)
            {
                if (data.photoSprite)
                {
                    imageLoad.SetTexture(data.photoSprite.texture);
                }
                else if (data.photoUrl == null)
                {
                    imageLoad.ClearTexture();
                }
                else
                {
                    imageLoad.Load(data.photoUrl);
                }
            }

            if (topPlayerActivityComponents.Length > 0)
            {
                if (data.inTop)
                {
                    ActivityMomoObjects(topPlayerActivityComponents, true);
                }
                else
                {
                    ActivityMomoObjects(topPlayerActivityComponents, false);
                }
            }

            if (currentPlayerActivityComponents.Length > 0)
            {
                if (data.currentPlayer)
                {
                    ActivityMomoObjects(currentPlayerActivityComponents, true);
                }
                else
                {
                    ActivityMomoObjects(currentPlayerActivityComponents, false);
                }
            }

            void ActivityMomoObjects(MonoBehaviour[] objects, bool activity)
            {
                for (int i = 0; i < objects.Length; i++)
                {
                    objects[i].enabled = activity;
                }
            }
        }

        private void ResolveTextBindings()
        {
            TryBindLegacy(ref textLegasy.rank, "Rank");
            TryBindLegacy(ref textLegasy.name, "Name");
            TryBindLegacy(ref textLegasy.score, "Score");

            TryBindTMP(ref textMP.rank, "Rank");
            TryBindTMP(ref textMP.name, "Name");
            TryBindTMP(ref textMP.score, "Score");
        }

        private void TryBindLegacy(ref Text target, string markerName)
        {
            if (target != null)
                return;

            Transform marker = FindChildRecursive(transform, markerName);
            if (marker != null)
                target = marker.GetComponentInChildren<Text>(true);
        }

        private void TryBindTMP(ref TMP_Text target, string markerName)
        {
            if (target != null)
                return;

            Transform marker = FindChildRecursive(transform, markerName);
            if (marker != null)
                target = marker.GetComponentInChildren<TMP_Text>(true);
        }

        private Transform FindChildRecursive(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
                    return child;

                Transform nested = FindChildRecursive(child, childName);
                if (nested != null)
                    return nested;
            }

            return null;
        }
    }
}