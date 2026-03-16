using System;
using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.KickBall.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Enums;
using RobustFSM.Base;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.ControlBall.SubStates
{
    public class ManualControl : BState
    {
        const float PassPreviewScanInterval = 0.5f;

        Vector3 RefObjectForward;             // The current forward direction of the camera
        Transform _refObject;                 // A reference to the main camera in the scenes transform
        Player _previewPassReceiver;
        float _nextPreviewScanTime;
        bool _hasCachedPassTarget;

        bool IsUserControlIconVisible()
        {
            return Owner.IconUserControlled == null || Owner.IconUserControlled.activeSelf;
        }

        void SyncPreviewVisibilityWithControlIcon()
        {
            if (_previewPassReceiver == null)
                return;

            _previewPassReceiver.SetCanPassPreviewVisible(IsUserControlIconVisible());
        }

        void SetUserControlIconVisible(bool visible)
        {
            if (Owner.IconUserControlled != null)
                Owner.IconUserControlled.SetActive(visible);

            // Also activate/deactivate the ball-control indicator on the owner
            if (Owner.IconCanPassPlayer != null)
                Owner.IconCanPassPlayer.SetActive(visible);

            SyncPreviewVisibilityWithControlIcon();
        }

        void SetPreviewPassReceiver(Player receiver)
        {
            if (_previewPassReceiver == receiver)
                return;

            if (_previewPassReceiver != null)
                _previewPassReceiver.SetCanPassPreviewVisible(false);

            _previewPassReceiver = receiver;

            if (_previewPassReceiver != null)
                _previewPassReceiver.SetCanPassPreviewVisible(IsUserControlIconVisible());
        }

        void ClearPreviewPassReceiver()
        {
            if (_previewPassReceiver != null)
                _previewPassReceiver.SetCanPassPreviewVisible(false);

            _previewPassReceiver = null;
        }

        bool ScanPassPreview(Vector3 direction, bool forceScan)
        {
            if (!forceScan && Time.time < _nextPreviewScanTime)
                return _hasCachedPassTarget;

            _nextPreviewScanTime = Time.time + PassPreviewScanInterval;

            _hasCachedPassTarget = Owner.CanPassInDirection(direction, false);
            SetPreviewPassReceiver(_hasCachedPassTarget ? Owner.PassReceiver : null);

            return _hasCachedPassTarget;
        }

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

        public override void Enter()
        {
            base.Enter();

            // enable the user controlled icon
            SetUserControlIconVisible(true);

            // set the ref object
            EnsureReferenceObject();

            ClearPreviewPassReceiver();
            _nextPreviewScanTime = 0f;
            _hasCachedPassTarget = false;
        }

        public override void Execute()
        {
            base.Execute();

            //capture input
            Vector2 movementAxes = GetMovementInputAxes();
            float horizontalRot = movementAxes.x;
            float verticalRot = movementAxes.y;

            //calculate the direction to rotate to
            Vector3 input = new Vector3(horizontalRot, 0f, verticalRot);

            //ensure we always have a valid movement reference
            EnsureReferenceObject();

            // keep receiver preview visibility in sync with user-control icon state.
            SyncPreviewVisibilityWithControlIcon();

            // calculate camera relative direction to move:
            RefObjectForward = Vector3.Scale(_refObject.forward, new Vector3(1, 0, 1)).normalized;
            Vector3 refObjectRight = Vector3.Scale(_refObject.right, new Vector3(1, 0, 1)).normalized;
            Vector3 Movement = input.z * RefObjectForward + input.x * refObjectRight;
            if (Movement.sqrMagnitude > 1f)
                Movement.Normalize();

            bool isMoving = Movement.sqrMagnitude > 0.0001f;
            bool wantsSprint = isMoving
                && ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    || MobileControlsInput.IsSprintHeld());
            Owner.ApplySprintToMovement(wantsSprint, isMoving, 0.95f);

            Vector3 direction = Movement.sqrMagnitude <= 0.0001f ? Owner.transform.forward : Movement;
            bool canPassInDirection = ScanPassPreview(direction, false);

            bool passPressed = Input.GetKeyDown(KeyCode.N)
                || MobileControlsInput.ConsumePassPressed();
            if (passPressed)
            {
                canPassInDirection = ScanPassPreview(direction, true);

                // go to kick ball if can pass
                if(canPassInDirection)
                {
                    //go to kick-ball state
                    Owner.KickType = KickType.Pass;
                    SuperMachine.ChangeState<KickBallMainState>();
                }
            }
            else if (Input.GetButtonDown("Shoot") || MobileControlsInput.ConsumeShootPressed())
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

            ClearPreviewPassReceiver();
            _hasCachedPassTarget = false;

            Owner.ResetSprintState(0.95f);

            // disable the user controlled icon
            SetUserControlIconVisible(false);
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
