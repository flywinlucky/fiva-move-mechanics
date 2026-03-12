using System;
using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.KickBall.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Enums;
using RobustFSM.Base;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.ControlBall.SubStates
{
    public class ManualControl : BState
    {
        Vector3 RefObjectForward;             // The current forward direction of the camera
        Transform _refObject;                 // A reference to the main camera in the scenes transform

        private void EnsureReferenceObject()
        {
            if (_refObject == null)
            {
                _refObject = Camera.main != null ? Camera.main.transform : Owner.transform;
            }
        }

        public override void Enter()
        {
            base.Enter();

            // enable the user controlled icon
            if (Owner.IconUserControlled != null)
                Owner.IconUserControlled.SetActive(true);

            // set the ref object
            EnsureReferenceObject();
        }

        public override void Execute()
        {
            base.Execute();

            //capture input
            float horizontalRot = Input.GetAxisRaw("Horizontal");
            float verticalRot = Input.GetAxisRaw("Vertical");

            //calculate the direction to rotate to
            Vector3 input = new Vector3(horizontalRot, 0f, verticalRot);

            //ensure we always have a valid movement reference
            EnsureReferenceObject();

            // calculate camera relative direction to move:
            RefObjectForward = Vector3.Scale(_refObject.forward, new Vector3(1, 0, 1)).normalized;
            Vector3 refObjectRight = Vector3.Scale(_refObject.right, new Vector3(1, 0, 1)).normalized;
            Vector3 Movement = input.z * RefObjectForward + input.x * refObjectRight;
            if (Movement.sqrMagnitude > 1f)
                Movement.Normalize();

            bool passPressed = Input.GetButtonDown("Pass/Press") || Input.GetKeyDown(KeyCode.N);
            if (passPressed)
            {
                // set the direction of movement
                Vector3 direction = Movement.sqrMagnitude <= 0.0001f ? Owner.transform.forward : Movement;

                // find pass in direction
                bool canPass = Owner.CanPassInDirection(direction);

                // go to kick ball if can pass
                if(canPass)
                {
                    //go to kick-ball state
                    Owner.KickType = KickType.Pass;
                    SuperMachine.ChangeState<KickBallMainState>();
                }
            }
            else if (Input.GetButtonDown("Shoot"))
            {
                // check if I can score
                bool canScore = Owner.CanScore(false, true);

                // shoot if I can score
                if (canScore)
                {
                    //go to kick-ball state
                    Owner.KickType = KickType.Shot;
                    SuperMachine.ChangeState<KickBallMainState>();
                }
                else
                {
                    // reconsider shot without considering the shot
                    // safety
                    canScore = Owner.CanScore(false, false);

                    // shoot if I can score
                    if (canScore)
                    {
                        //go to kick-ball state
                        Owner.KickType = KickType.Shot;
                        SuperMachine.ChangeState<KickBallMainState>();
                    }
                }
            }
            else
            {
                //process if any key down
                if (input == Vector3.zero)
                {
                    Owner.RPGMovement.SetMoveDirection(Vector3.zero);

                    if (Owner.RPGMovement.Steer == true)
                        Owner.RPGMovement.SetSteeringOff();

                    if (Owner.RPGMovement.Track == true)
                        Owner.RPGMovement.SetTrackingOff();
                }
                else
                {
                    // set the movement
                    Vector3 moveDirection = Movement.sqrMagnitude <= 0.0001f ? Vector3.zero : Movement;
                    Owner.RPGMovement.SetMoveDirection(moveDirection);
                    Owner.RPGMovement.SetRotateFaceDirection(Movement);

                    // set the steering to on
                    if (Owner.RPGMovement.Steer == false)
                        Owner.RPGMovement.SetSteeringOn();

                    if (Owner.RPGMovement.Track == false)
                        Owner.RPGMovement.SetTrackingOn();
                }
            }
        }

        public override void Exit()
        {
            base.Exit();

            // disable the user controlled icon
            if (Owner.IconUserControlled != null)
                Owner.IconUserControlled.SetActive(false);
        }

        public Player Owner
        {
            get
            {
                return ((InFieldPlayerFSM)SuperMachine).Owner;
            }
        }
    }
}
