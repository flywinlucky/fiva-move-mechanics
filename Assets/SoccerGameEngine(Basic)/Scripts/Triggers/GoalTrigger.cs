using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Enums;
using System;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.Triggers
{
    public class GoalTrigger : MonoBehaviour
    {
        public Goal Goal;// { get; set; }

        public Action OnCollidedWithBall;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Ball"))
                return;

            // Ignore only when goalkeeper is actively controlling ball to avoid false goal events.
            if (Ball.Instance != null
                && Ball.Instance.Owner != null
                && Ball.Instance.Owner.PlayerType == PlayerTypes.Goalkeeper)
                return;

            //invoke that the wall has collided with the ball
            Action temp = OnCollidedWithBall;
            if (temp != null)
                temp.Invoke();
        }
    }
}
