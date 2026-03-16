using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Arikan
{
    public class MiniMapDemo : MonoBehaviour
    {
        [Header("MiniMap")]
        public MiniMapView miniMapView;
        public Transform centeredTarget;
        public Sprite centeredTargetSprite;
        public Color centeredTargetColor = Color.white;

        [Header("MiniMap Projection")]
        public bool swapWorldAxes = true;
        public bool invertMapX = false;
        public bool invertMapY = false;
        [Range(0.05f, 3f)]
        public float mapZoom = 0.78f;
        public bool clampDotsInsideMap = true;
        [Min(0f)]
        public float clampPadding = 2f;

        [Header("Dot Size")]
        public bool overrideDotSizes = true;
        [Range(0.1f, 4f)]
        public float dotScaleMultiplier = 1f;

        [Header("Live Size Sliders (0..5)")]
        [Range(0f, 5f)]
        public float centeredSizeSlider = 1f;
        [Range(0f, 5f)]
        public float team1SizeSlider = 1f;
        [Range(0f, 5f)]
        public float team2SizeSlider = 1f;
        [Range(0f, 5f)]
        public float ballSizeSlider = 1f;

        [Header("Team 1")]
        public List<Transform> team1Targets = new List<Transform>();
        public Sprite team1Sprite;
        public Color team1Color = new Color(0.15f, 0.55f, 1f, 1f);

        [Header("Team 2")]
        public List<Transform> team2Targets = new List<Transform>();
        public Sprite team2Sprite;
        public Color team2Color = new Color(1f, 0.35f, 0.25f, 1f);

        [Header("Ball")]
        public Transform ballTarget;
        public Sprite ballSprite;
        public Color ballColor = Color.white;

        [Header("Camera Follow (Optional)")]
        public Camera followCamera;
        public Transform cameraFollowTarget;
        public Vector3 cameraOffset = new Vector3(0f, 18f, -14f);
        [Range(0.1f, 20f)]
        public float cameraFollowLerp = 8f;
        public bool cameraLookAtTarget = true;
        [Range(0.1f, 20f)]
        public float cameraRotationLerp = 8f;

        HashSet<Transform> _followedTargets = new HashSet<Transform>();
        Image _centeredImage;
        Image _ballImage;
        readonly List<Image> _team1Images = new List<Image>();
        readonly List<Image> _team2Images = new List<Image>();

        static readonly Vector2 CenteredBaseDotSize = new Vector2(22f, 22f);
        static readonly Vector2 Team1BaseDotSize = new Vector2(16f, 16f);
        static readonly Vector2 Team2BaseDotSize = new Vector2(16f, 16f);
        static readonly Vector2 BallBaseDotSize = new Vector2(14f, 14f);


        void Start()
        {
            if (miniMapView == null)
                miniMapView = FindObjectOfType<MiniMapView>();

            if (miniMapView == null)
            {
                Debug.LogError("[MiniMapDemo] MiniMapView not found.", this);
                return;
            }

            if (followCamera == null)
                followCamera = Camera.main;

            if (cameraFollowTarget == null)
                cameraFollowTarget = centeredTarget;

            ApplyMiniMapProjectionSettings();

            miniMapView.followCenteredTargetPosition = centeredTarget != null;
            miniMapView.rotateMapWithCenteredTarget = false;
            miniMapView.rotateDotsWithTargets = false;

            miniMapView.ClearTargets();
            _followedTargets.Clear();
            _centeredImage = null;
            _ballImage = null;
            _team1Images.Clear();
            _team2Images.Clear();

            if (centeredTarget != null)
            {
                var centeredImage = miniMapView.FollowCentered(centeredTarget, centeredTargetSprite);
                SetImageVisual(centeredImage, centeredTargetColor, CenteredBaseDotSize);
                _centeredImage = centeredImage;
                _followedTargets.Add(centeredTarget);
            }

            RegisterTeamTargets(team1Targets, team1Sprite, team1Color, Team1BaseDotSize, _team1Images);
            RegisterTeamTargets(team2Targets, team2Sprite, team2Color, Team2BaseDotSize, _team2Images);
            RegisterBallTarget();
            ApplyLiveDotSizes();
        }

        void LateUpdate()
        {
            UpdateCameraFollow();
            ApplyLiveDotSizes();
        }

        void OnValidate()
        {
            ApplyMiniMapProjectionSettings();
        }

        void ApplyMiniMapProjectionSettings()
        {
            if (miniMapView == null)
                return;

            miniMapView.swapWorldAxes = swapWorldAxes;
            miniMapView.invertAxisX = invertMapX;
            miniMapView.invertAxisY = invertMapY;
            miniMapView.mapZoom = Mathf.Max(0.05f, mapZoom);
            miniMapView.clampDotsToMapRect = clampDotsInsideMap;
            miniMapView.clampPadding = Mathf.Max(0f, clampPadding);
        }

        void RegisterTeamTargets(List<Transform> targets, Sprite sprite, Color color, Vector2 dotSize, List<Image> imageBuffer)
        {
            if (targets == null)
                return;

            foreach (Transform target in targets)
            {
                Image image = RegisterTarget(target, sprite, color, dotSize);
                if (image != null && imageBuffer != null)
                    imageBuffer.Add(image);
            }
        }

        void RegisterBallTarget()
        {
            _ballImage = RegisterTarget(ballTarget, ballSprite, ballColor, BallBaseDotSize);
        }

        Image RegisterTarget(Transform target, Sprite sprite, Color color, Vector2 dotSize)
        {
            if (target == null)
                return null;

            if (_followedTargets.Contains(target))
                return null;

            Image image = miniMapView.Follow(target, sprite);
            SetImageVisual(image, color, dotSize);
            _followedTargets.Add(target);
            return image;
        }

        void ApplyLiveDotSizes()
        {
            if (!overrideDotSizes)
                return;

            ApplyImageSize(_centeredImage, CenteredBaseDotSize, centeredSizeSlider);
            ApplyImageSize(_ballImage, BallBaseDotSize, ballSizeSlider);
            ApplyImageSizeList(_team1Images, Team1BaseDotSize, team1SizeSlider);
            ApplyImageSizeList(_team2Images, Team2BaseDotSize, team2SizeSlider);
        }

        void ApplyImageSizeList(List<Image> images, Vector2 baseSize, float sliderValue)
        {
            if (images == null)
                return;

            for (int i = images.Count - 1; i >= 0; i--)
            {
                if (images[i] == null)
                {
                    images.RemoveAt(i);
                    continue;
                }

                ApplyImageSize(images[i], baseSize, sliderValue);
            }
        }

        void ApplyImageSize(Image image, Vector2 baseSize, float sliderValue)
        {
            if (image == null)
                return;

            float baseScale = Mathf.Max(0f, sliderValue);
            Vector2 scaledSize = baseSize * baseScale;
            ApplyDotSize(image, scaledSize);
        }

        void SetImageVisual(Image image, Color color, Vector2 dotSize)
        {
            if (image == null)
                return;

            image.color = color;
            ApplyDotSize(image, dotSize);
        }

        void ApplyDotSize(Image image, Vector2 dotSize)
        {
            if (!overrideDotSizes || image == null)
                return;

            float scale = Mathf.Max(0.1f, dotScaleMultiplier);
            Vector2 finalSize = new Vector2(
                Mathf.Max(0f, dotSize.x),
                Mathf.Max(0f, dotSize.y)) * scale;

            RectTransform rect = image.rectTransform;
            if (rect != null)
                rect.sizeDelta = finalSize;
        }

        void UpdateCameraFollow()
        {
            if (followCamera == null || cameraFollowTarget == null)
                return;

            Vector3 desiredPosition = cameraFollowTarget.position + cameraOffset;
            float followT = Mathf.Clamp01(Time.deltaTime * cameraFollowLerp);
            followCamera.transform.position = Vector3.Lerp(followCamera.transform.position, desiredPosition, followT);

            if (!cameraLookAtTarget)
                return;

            Vector3 lookDirection = cameraFollowTarget.position - followCamera.transform.position;
            lookDirection.y = Mathf.Min(lookDirection.y, -0.01f);
            if (lookDirection.sqrMagnitude <= 0.0001f)
                return;

            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            float rotT = Mathf.Clamp01(Time.deltaTime * cameraRotationLerp);
            followCamera.transform.rotation = Quaternion.Slerp(followCamera.transform.rotation, desiredRotation, rotT);
        }
    }
}
