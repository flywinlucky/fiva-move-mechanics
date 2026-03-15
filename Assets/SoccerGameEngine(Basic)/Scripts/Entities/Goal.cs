using Assets.SoccerGameEngine_Basic_.Scripts.Triggers;
using System;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.Entities
{
    public class Goal : MonoBehaviour
    {
        [SerializeField]
        GoalMouth _goalMouth;

        [SerializeField]
        GoalTrigger _goalTrigger;

        [SerializeField]
        Transform _shotTargetReferencePoint;

        [Header("Shot Targeting")]

        [SerializeField]
        [Range(0f, 1f)]
        float _shotCornerChance = 0.35f;

        [SerializeField]
        [Range(0f, 0.45f)]
        float _goalTriggerInset = 0.1f;

        /// <summary>
        /// Action raised when goal collides with the ball
        /// </summary>
        public Action OnCollideWithBall;

        ///ToDo::Speak about why you put them here as an initialization
        public Vector3 BottomLeftRelativePosition { get; set; }
        public Vector3 BottomRightRelativePosition { get; set; }
        public Vector3 Position { get => transform.position; }
        public Vector3 ShotTargetReferencePoint
        {
            get
            {
                if (_shotTargetReferencePoint != null)
                    return _shotTargetReferencePoint.position;

                return transform.position;
            }
        }

        private void Awake()
        {
            //init some data 
            BottomLeftRelativePosition = transform.InverseTransformPoint(_goalMouth._pointBottomLeft.position);
            BottomRightRelativePosition = transform.InverseTransformPoint(_goalMouth._pointBottomRight.position);

            _goalTrigger.Goal = this;

            //listen to the goal-trigger events
            _goalTrigger.OnCollidedWithBall += Instance_OnCollidedWithBall;
        }

        private void Instance_OnCollidedWithBall()
        {
            //raise the on collision with ball event
            Action temp = OnCollideWithBall;
            if (temp != null)
                temp.Invoke();
        }

        public bool IsPositionWithinGoalMouthFrustrum(Vector3 position)
        {
            //find the relative position to goal
            Vector3 relativePosition = transform.InverseTransformPoint(position);

            //find the relative position of each goal mouth
            Vector3 pointBottomLeftRelativePosition = transform.InverseTransformPoint(_goalMouth._pointBottomLeft.position);
            Vector3 pointBottomRightRelativePosition = transform.InverseTransformPoint(_goalMouth._pointBottomRight.position);
            Vector3 pointTopLeftRelativePosition = transform.InverseTransformPoint(_goalMouth._pointTopLeft.position);

            //check if the x- coordinate of the relative position lies within the goal mouth
            bool isPositionWithTheXCoordinates = relativePosition.x > pointBottomLeftRelativePosition.x && relativePosition.x < pointBottomRightRelativePosition.x;
            bool isPositionWithTheYCoordinates = relativePosition.y > pointBottomLeftRelativePosition.y && relativePosition.y < pointTopLeftRelativePosition.y;

            //the result is the combination of the two tests
            return isPositionWithTheXCoordinates && isPositionWithTheYCoordinates;
        }

        public Vector3 GetRandomShotTarget()
        {
            Vector3 target;

            bool preferCorner = UnityEngine.Random.value <= _shotCornerChance;
            if (preferCorner && TryGetRandomCornerShotTarget(out target))
                return target;

            if (TryGetRandomGoalTriggerShotTarget(out target))
                return target;

            if (TryGetRandomCornerShotTarget(out target))
                return target;

            return GetFallbackRandomShotTarget();
        }

        bool TryGetRandomGoalTriggerShotTarget(out Vector3 target)
        {
            target = Vector3.zero;

            if (_goalTrigger == null)
                return false;

            BoxCollider triggerCollider = _goalTrigger.GetComponent<BoxCollider>();
            if (triggerCollider == null)
                return false;

            Vector3 half = triggerCollider.size * 0.5f;
            float inset = Mathf.Clamp(_goalTriggerInset, 0f, 0.45f);

            float xMin = triggerCollider.center.x - half.x + half.x * inset;
            float xMax = triggerCollider.center.x + half.x - half.x * inset;
            float yMin = triggerCollider.center.y - half.y + half.y * inset;
            float yMax = triggerCollider.center.y + half.y - half.y * inset;

            if (xMin > xMax)
            {
                float temp = xMin;
                xMin = xMax;
                xMax = temp;
            }

            if (yMin > yMax)
            {
                float temp = yMin;
                yMin = yMax;
                yMax = temp;
            }

            float x = UnityEngine.Random.Range(xMin, xMax);
            float y = UnityEngine.Random.Range(yMin, yMax);
            float z = triggerCollider.center.z;

            Vector3 localPoint = new Vector3(x, y, z);
            target = triggerCollider.transform.TransformPoint(localPoint);
            return true;
        }

        bool TryGetRandomCornerShotTarget(out Vector3 target)
        {
            target = Vector3.zero;

            Transform[] corners = new Transform[9];
            int cornerCount = 0;

            if (_shotTargetReferencePoint != null)
            {
                int childCount = _shotTargetReferencePoint.childCount;
                if (childCount > 0)
                {
                    for (int i = 0; i < childCount && cornerCount < corners.Length; i++)
                    {
                        Transform child = _shotTargetReferencePoint.GetChild(i);
                        if (child != null)
                            corners[cornerCount++] = child;
                    }
                }
                else
                {
                    corners[cornerCount++] = _shotTargetReferencePoint;
                }
            }

            if (_goalMouth._pointBottomLeft != null && cornerCount < corners.Length)
                corners[cornerCount++] = _goalMouth._pointBottomLeft;
            if (_goalMouth._pointBottomRight != null && cornerCount < corners.Length)
                corners[cornerCount++] = _goalMouth._pointBottomRight;
            if (_goalMouth._pointTopLeft != null && cornerCount < corners.Length)
                corners[cornerCount++] = _goalMouth._pointTopLeft;
            if (_goalMouth._pointTopRight != null && cornerCount < corners.Length)
                corners[cornerCount++] = _goalMouth._pointTopRight;

            if (cornerCount == 0)
                return false;

            int randomIndex = UnityEngine.Random.Range(0, cornerCount);
            target = corners[randomIndex].position;
            return true;
        }

        Vector3 GetFallbackRandomShotTarget()
        {
            Vector3 refShotTarget = transform.InverseTransformPoint(ShotTargetReferencePoint);

            float randomXPosition = UnityEngine.Random.Range(BottomLeftRelativePosition.x,
                BottomRightRelativePosition.x);

            Vector3 goalLocalTarget = new Vector3(randomXPosition, refShotTarget.y, refShotTarget.z);
            return transform.TransformPoint(goalLocalTarget);
        }

    }

    [Serializable]
    public struct GoalMouth
    {
        public Transform _pointBottomLeft;
        public Transform _pointBottomRight;
        public Transform _pointTopLeft;
        public Transform _pointTopRight;
    }
}
