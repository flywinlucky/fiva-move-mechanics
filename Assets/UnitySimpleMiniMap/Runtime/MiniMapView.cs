using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace Arikan
{
    public class MiniMapView : MonoBehaviour
    {
        [Header("RectTransform Roots")]
        public RectTransform centeredDotCanvas;
        public RectTransform otherDotCanvas;
        [Header("Defult Sprite")]
        public Sprite defaultSprite;
        [Header("Default Dot Prefab")]
        public Image uiDotPrefab;
#if ODIN_INSPECTOR
        [Required]
#endif
        [Header("Bounds Object")]
        public MiniMapBounds miniMapBounds;

        [Header("Centered Target Behavior")]
        public bool followCenteredTargetPosition = true;
        public bool rotateMapWithCenteredTarget = false;
        public bool rotateDotsWithTargets = false;

        [Header("Projection")]
        public bool swapWorldAxes = false;
        public bool invertAxisX = false;
        public bool invertAxisY = false;
        [Range(0.05f, 3f)]
        public float mapZoom = 1f;

        [Header("Dot Clamp")]
        public bool clampDotsToMapRect = true;
        [Min(0f)]
        public float clampPadding = 2f;

        private Dictionary<Transform, RectTransform> redDotMap = new Dictionary<Transform, RectTransform>();
        private KeyValuePair<Transform, RectTransform> mainMap = new KeyValuePair<Transform, RectTransform>();

        private void OnEnable()
        {
            if (miniMapBounds == null)
            {
                miniMapBounds = FindObjectOfType<MiniMapBounds>();
            }
        }

#if ODIN_INSPECTOR
        [Button]
#endif
        /// <summary>
        /// Follow target over the minimap, returns Generated MiniMap Image object
        /// </summary>
        public Image FollowCentered(Transform target, Sprite icon = null)
        {
            if (centeredDotCanvas == null)
            {
                throw new NullReferenceException("[MiniMapView] centeredDotCanvas is null");
            }
            if (uiDotPrefab == null)
            {
                throw new NullReferenceException("[MiniMapView] uiDotPrefab is null");
            }
            if (target.lossyScale.x != 1)
            {
                Debug.LogWarning("[MiniMapView] target.lossyScale != 1, this causes wrong positions over minimap", target);
            }
            if (mainMap.Key != null)
            {
                UnfollowTarget(mainMap.Key);
            }

            var uiDot = Instantiate(uiDotPrefab, centeredDotCanvas);
            uiDot.sprite = icon ?? defaultSprite;
            mainMap = new KeyValuePair<Transform, RectTransform>(target, uiDot.transform as RectTransform);
            return uiDot;
        }

#if ODIN_INSPECTOR
        [Button]
#endif
        /// <summary>
        /// Follow target over the minimap, returns Generated MiniMap Image object
        /// </summary>
        public Image Follow(Transform target, Sprite icon = null)
        {
            if (otherDotCanvas == null)
            {
                throw new NullReferenceException("[MiniMapView] otherDotCanvas is null");
            }
            if (uiDotPrefab == null)
            {
                throw new NullReferenceException("[MiniMapView] uiDotPrefab is null");
            }
            UnfollowTarget(target);

            var uiDot = Instantiate(uiDotPrefab, otherDotCanvas);
            uiDot.sprite = icon ?? defaultSprite;
            redDotMap.Add(target, uiDot.transform as RectTransform);
            return uiDot;
        }

#if ODIN_INSPECTOR
        [Button]
#endif
        public void UnfollowTarget(Transform target)
        {
            if (mainMap.Key == target)
            {
                if (mainMap.Value != null)
                    Destroy(mainMap.Value.gameObject);
                mainMap = new KeyValuePair<Transform, RectTransform>();

                if (otherDotCanvas != null)
                {
                    otherDotCanvas.localPosition = Vector2.zero;
                    otherDotCanvas.localEulerAngles = Vector3.zero;
                }
            }
            else if (redDotMap.TryGetValue(target, out var redDot))
            {
                if (redDot != null)
                    Destroy(redDot.gameObject);
                redDotMap.Remove(target);
            }
        }

#if ODIN_INSPECTOR
        [Button]
#endif
        public void ClearTargets()
        {
            if (mainMap.Key != null)
            {
                UnfollowTarget(mainMap.Key);
            }
            foreach (var redDot in redDotMap.ToList())
            {
                UnfollowTarget(redDot.Key);
            }
        }

        private void Update()
        {
            if (mainMap.Key != null)
            {
                var target = mainMap.Key;
                var redDot = mainMap.Value;

                TranslateReverse(target, redDot);
            }

            foreach (var pair in redDotMap)
            {
                var target = pair.Key;
                var redDot = pair.Value;

                if (target != null)
                {
                    Translate(target, redDot);
                }
            }
        }

        Vector2 WorldToMiniMapLocalPosition(Transform worldObj)
        {
            var worldBounds = miniMapBounds.GetWorldRect();

            float worldSizeX = Mathf.Max(0.0001f, Mathf.Abs(worldBounds.size.x));
            float worldSizeZ = Mathf.Max(0.0001f, Mathf.Abs(worldBounds.size.z));

            var relative = worldObj.position - worldBounds.center;
            relative.y = 0f;

            Vector2 mapped = new Vector2(
                relative.x * (otherDotCanvas.sizeDelta.x / worldSizeX),
                relative.z * (otherDotCanvas.sizeDelta.y / worldSizeZ));

            return ApplyProjectionOptions(mapped);
        }

        Vector2 ApplyProjectionOptions(Vector2 position)
        {
            if (swapWorldAxes)
                position = new Vector2(position.y, position.x);

            if (invertAxisX)
                position.x = -position.x;

            if (invertAxisY)
                position.y = -position.y;

            return position * Mathf.Max(0.05f, mapZoom);
        }

        Vector2 ClampDotPosition(Vector2 localPosition, RectTransform canvas, RectTransform dot)
        {
            if (!clampDotsToMapRect || canvas == null)
                return localPosition;

            Vector2 canvasSize = canvas.rect.size;
            if (canvasSize.x <= 0.0001f || canvasSize.y <= 0.0001f)
                canvasSize = canvas.sizeDelta;

            Vector2 half = canvasSize * 0.5f;
            float dotHalfWidth = 0f;
            float dotHalfHeight = 0f;

            if (dot != null)
            {
                dotHalfWidth = dot.rect.width * 0.5f;
                dotHalfHeight = dot.rect.height * 0.5f;

                if (dotHalfWidth <= 0.0001f)
                    dotHalfWidth = dot.sizeDelta.x * 0.5f;
                if (dotHalfHeight <= 0.0001f)
                    dotHalfHeight = dot.sizeDelta.y * 0.5f;
            }

            float pad = Mathf.Max(0f, clampPadding);
            float maxX = Mathf.Max(0f, half.x - dotHalfWidth - pad);
            float maxY = Mathf.Max(0f, half.y - dotHalfHeight - pad);

            localPosition.x = Mathf.Clamp(localPosition.x, -maxX, maxX);
            localPosition.y = Mathf.Clamp(localPosition.y, -maxY, maxY);
            return localPosition;
        }


        public void Translate(Transform worldObj, RectTransform dot)
        {
            Vector2 mappedPosition = WorldToMiniMapLocalPosition(worldObj);
            dot.localPosition = ClampDotPosition(mappedPosition, otherDotCanvas, dot);

            if (rotateDotsWithTargets)
                dot.localEulerAngles = new Vector3(0, 0, -worldObj.eulerAngles.y);
            else
                dot.localEulerAngles = Vector3.zero;
        }

        public void TranslateReverse(Transform worldObj, RectTransform dot)
        {
            if (followCenteredTargetPosition)
            {
                Vector2 centeredPosition = WorldToMiniMapLocalPosition(worldObj);
                otherDotCanvas.localPosition = -centeredPosition;
            }
            else
            {
                otherDotCanvas.localPosition = Vector2.zero;
            }

            if (rotateMapWithCenteredTarget)
                otherDotCanvas.localEulerAngles = new Vector3(0, 0, -worldObj.eulerAngles.y);
            else
                otherDotCanvas.localEulerAngles = Vector3.zero;

            if (dot != null)
            {
                dot.localPosition = ClampDotPosition(Vector2.zero, centeredDotCanvas, dot);

                if (rotateDotsWithTargets)
                    dot.localEulerAngles = new Vector3(0, 0, -worldObj.eulerAngles.y);
                else
                    dot.localEulerAngles = Vector3.zero;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (miniMapBounds != null)
            {
                var worldBounds = miniMapBounds.GetWorldRect();
                Gizmos.DrawWireCube(worldBounds.center, worldBounds.size);
            }
        }
    }

}
