using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.ControlBall.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.TacklePlayer.MainState;
using RobustFSM.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.ChaseBall.SubStates
{
    public class ManualChase : BState
    {
        Vector3 refObjectForward;
        Transform _refObject;

        private void EnsureReferenceObject()
        {
            if (_refObject == null)
            {
                _refObject = Camera.main != null ? Camera.main.transform : Owner.transform;
            }
        }

        Vector2 GetMovementInputAxes()
        {
            Vector2 keyboardInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (!MobileControlsInput.IsEnabled)
                return keyboardInput;

            Vector2 mobileInput = MobileControlsInput.ReadMovementInput();
            if (mobileInput.sqrMagnitude > 0.0001f)
                return mobileInput;

            return keyboardInput;
        }

        /// <summary>
        /// The steering target
        /// </summary>
        public Vector3 SteeringTarget { get; set; }

        public override void Enter()
        {
            base.Enter();

            // enable the user controlled icon
            if (Owner.IconUserControlled != null)
                Owner.IconUserControlled.SetActive(true);

            // enable the ball-control indicator on the owner
            if (Owner.IconCanPassPlayer != null)
                Owner.IconCanPassPlayer.SetActive(true);

            //get the steering target
            SteeringTarget = Ball.Instance.NormalizedPosition;

            // set the ref object
            EnsureReferenceObject();

            //reset move/tracking from previous state
            Owner.RPGMovement.SetSteeringOff();
            Owner.RPGMovement.SetTrackingOff();
            Owner.RPGMovement.SetMoveDirection(Vector3.zero);
        }

        public override void Execute()
        {
            base.Execute();

            //check if ball is within control distance
            if (Ball.Instance.Owner != null
                && Ball.Instance.Owner != Owner
                && Owner.IsBallWithinControlableDistance())
            {
                //tackle player
                SuperMachine.ChangeState<TackleMainState>();
                return;
            }
            else if (Owner.IsBallWithinControlableDistance())
            {
                // control ball
                SuperMachine.ChangeState<ControlBallMainState>();
                return;
            }

            //capture input
            Vector2 movementAxes = GetMovementInputAxes();
            float horizontal = movementAxes.x;
            float vertical = movementAxes.y;
            Vector3 input = new Vector3(horizontal, 0f, vertical);

            //ensure we always have a valid movement reference
            EnsureReferenceObject();

            //calculate camera relative movement
            refObjectForward = Vector3.Scale(_refObject.forward, new Vector3(1f, 0f, 1f)).normalized;
            Vector3 refObjectRight = Vector3.Scale(_refObject.right, new Vector3(1f, 0f, 1f)).normalized;
            Vector3 movement = input.z * refObjectForward + input.x * refObjectRight;
            if (movement.sqrMagnitude > 1f)
                movement.Normalize();

            bool isMoving = movement.sqrMagnitude > 0.0001f;
            bool wantsSprint = isMoving
                && ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    || MobileControlsInput.IsSprintHeld());
            Owner.ApplySprintToMovement(wantsSprint, isMoving);

            if (input == Vector3.zero)
            {
                Owner.RPGMovement.SetMoveDirection(Vector3.zero);

                if (Owner.RPGMovement.Steer)
                    Owner.RPGMovement.SetSteeringOff();

                if (Owner.RPGMovement.Track)
                    Owner.RPGMovement.SetTrackingOff();
            }
            else
            {
                Owner.RPGMovement.SetMoveDirection(movement);
                Owner.RPGMovement.SetRotateFaceDirection(movement);

                if (!Owner.RPGMovement.Steer)
                    Owner.RPGMovement.SetSteeringOn();

                if (!Owner.RPGMovement.Track)
                    Owner.RPGMovement.SetTrackingOn();
            }
        }

        public override void Exit()
        {
            base.Exit();

            Owner.ResetSprintState();

            // disable the user controlled icon
            if (Owner.IconUserControlled != null)
                Owner.IconUserControlled.SetActive(false);

            // disable the ball-control indicator
            if (Owner.IconCanPassPlayer != null)
                Owner.IconCanPassPlayer.SetActive(false);

            //set the steering to on
            Owner.RPGMovement.SetSteeringOff();
            Owner.RPGMovement.SetTrackingOff();
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
