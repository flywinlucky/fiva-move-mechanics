using Patterns.Singleton;
using UnityEngine;
 

namespace Assets.FootballGameEngine_Indie.Scripts.Controllers
{
    public class CameraController : Singleton<CameraController>
    {
        [SerializeField]
        bool _canAutoFollowTarget = true;

        [SerializeField]
        float _distanceFollow = 15f;

        [Header("Limite Orizontale (X)")]
        [SerializeField]
        float _distanceMinXDisplacement = -20f;
        [SerializeField]
        float _distanceMaxXDisplacement = 20f;

        [Header("Limite Verticale (Z)")]
        [SerializeField]
        float _distanceMinZDisplacement = -30f;
        [SerializeField]
        float _distanceMaxZDisplacement = 30f;

        [Header("Setări Înălțime")]
        [SerializeField]
        float _height = 15f;

        [SerializeField]
        float _speedFollow = 3f;

        [SerializeField]
        private float _lockDamping = 0.9f; // Controlează puterea efectului de lock (0 la 1)

        [SerializeField]
        Transform _target;

        float _cameraHeight;
        private Vector3 _velocity = Vector3.zero; // Pentru smooth damping
        private Camera mainCamera;

        public override void Awake()
        {
            mainCamera = GetComponent<Camera>();
            base.Awake();

            // Setăm înălțimea inițială bazată pe offset
            _cameraHeight = _height;
        }

        private void LateUpdate()
        {
            // Doar dacă avem permisiunea de urmărire și o țintă validă
            if (_canAutoFollowTarget && _target != null)
            {
                // Calculăm următoarea poziție dorită
                Vector3 nextPosition = CalculateNextPosition(_target.position);

                // Mișcare fluidă SmoothDamp
                Vector3 currentPosition = transform.position;
                Vector3 smoothedPosition = Vector3.SmoothDamp(currentPosition, nextPosition, ref _velocity, _speedFollow * Time.deltaTime);

                // Aplicăm efectul de lock prin blending (Lerp între poziția curentă și cea calculată)
                smoothedPosition = Vector3.Lerp(currentPosition, smoothedPosition, _lockDamping);

                // Aplicăm poziția finală
                transform.position = smoothedPosition;
            }
        }

        public Vector3 CalculateNextPosition()
        {
            return CalculateNextPosition(_target.position);
        }

        public Vector3 CalculateNextPosition(Vector3 refPosition)
        {
            // Punctul de plecare este poziția țintei
            Vector3 nextPosition = refPosition;

            // Aplicăm offset-ul pe X (distanța de urmărire)
            nextPosition.x = refPosition.x + _distanceFollow;
            
            // Setăm înălțimea fixă a camerei
            nextPosition.y = _cameraHeight;

            // Aplicăm limitele (Clamping) pe axa X
            nextPosition.x = Mathf.Clamp(nextPosition.x, _distanceMinXDisplacement, _distanceMaxXDisplacement);

            // Aplicăm limitele (Clamping) pe axa Z
            // Verificăm poziția țintei pe Z și o limităm între valorile setate manual
            nextPosition.z = Mathf.Clamp(refPosition.z, _distanceMinZDisplacement, _distanceMaxZDisplacement);

            return nextPosition;
        }

        public bool CanAutoFollowTarget { get => _canAutoFollowTarget; set => _canAutoFollowTarget = value; }
        public Vector3 Position { get => transform.position; set => transform.position = value; }
        public Transform Target { get => _target; set => _target = value; }
    }
}