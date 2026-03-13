using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
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

            // Ignore trigger while the ball is controlled by a player to avoid false goal events.
            if (Ball.Instance != null && Ball.Instance.Owner != null)
                return;

            //invoke that the wall has collided with the ball
            Action temp = OnCollidedWithBall;
            if (temp != null)
                temp.Invoke();
        }
    }
}
