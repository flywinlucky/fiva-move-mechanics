using System;
using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.ChaseBall.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.ControlBall.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.GoToHome.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.ReceiveBall.SubStates;
using RobustFSM.Base;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.ReceiveBall.MainState
{
    /// <summary>
    /// The player steers to the pass target and waits for the ball
    /// there. If ball comes within range the player controls the ball. If
    /// the player receives a message that the team has lost control he
    /// goes back to home
    /// </summary>
    public class ReceiveBallMainState : BHState
    {
        float _ballTime;
        Vector3 _baseReceiveTarget;
        Vector3 _liveReceiveTarget;

        const float _maxAdaptiveOffset = 7f;
        const float _liveTargetSmoothing = 8f;
        const float _predictionTimeCap = 2.5f;

        public override void AddStates()
        {
            base.AddStates();

            //add the state
            AddState<SteerToReceiveTarget>();
            AddState<WaitForBallAtReceiveTarget>();

            //set the initial state
            SetInitialState<SteerToReceiveTarget>();
        }

        public override void Enter()
        {
            base.Enter();

            // set me as the ball owner
            // Ball.Instance.Owner = Owner;

            //register to some player events
            Owner.OnBecameTheClosestPlayerToBall += Instance_OnBecameTheClosestPlayerToBall;
            Owner.OnTeamLostControl += Instance_OnTeamLostControl;
        }

        public override void Execute()
        {
            base.Execute();

            // decrement ball trap time
            if(_ballTime > 0)
                _ballTime -= Time.deltaTime;

            UpdateLiveReceiveTarget();

            //trap the ball if it is now in a trapping distance
             if (Owner.IsBallWithinControlableDistance())
                 Machine.ChangeState<ControlBallMainState>();
        }

        public override void ManualExecute()
        {
            base.ManualExecute();

            // if we have exhausted ball time, chase down ball
            if (_ballTime <= -0.5f)
                Machine.ChangeState<ChaseBallMainState>();
        }

        public override void Exit()
        {
            base.Exit();

            //stop listing to some player events
            Owner.OnBecameTheClosestPlayerToBall -= Instance_OnBecameTheClosestPlayerToBall;
            Owner.OnTeamLostControl -= Instance_OnTeamLostControl;
        }

        private void Instance_OnBecameTheClosestPlayerToBall()
        {
            Machine.ChangeState<ChaseBallMainState>();
        }

        private void Instance_OnTeamLostControl()
        {
            SuperMachine.ChangeState<GoToHomeMainState>();
        }

        public void SetSteeringTarget(float ballTime, Vector3 position)
        {
            _ballTime = Mathf.Max(0.5f, ballTime + 0.35f);

            _baseReceiveTarget = position;
            _liveReceiveTarget = position;
            GetState<SteerToReceiveTarget>().SteeringTarget = _liveReceiveTarget;
        }

        void UpdateLiveReceiveTarget()
        {
            Ball ball = Ball.Instance;
            Rigidbody ballRigidbody = ball != null ? ball.Rigidbody : null;
            if (ball == null || ballRigidbody == null)
            {
                _liveReceiveTarget = _baseReceiveTarget;
                return;
            }

            float desiredPredictionTime = Mathf.Clamp(_ballTime, 0.1f, _predictionTimeCap);
            Vector3 predicted = PredictInterceptPoint(ball, ballRigidbody, desiredPredictionTime);

            Vector3 planarOffset = predicted - _baseReceiveTarget;
            planarOffset.y = 0f;
            float offsetMagnitude = planarOffset.magnitude;
            if (offsetMagnitude > _maxAdaptiveOffset)
            {
                predicted = _baseReceiveTarget + planarOffset.normalized * _maxAdaptiveOffset;
            }

            predicted.y = Owner.Position.y;

            float blendWeight = Mathf.Clamp01(_ballTime / 1.2f);
            Vector3 desiredTarget = Vector3.Lerp(_baseReceiveTarget, predicted, blendWeight);
            _liveReceiveTarget = Vector3.Lerp(_liveReceiveTarget, desiredTarget, Time.deltaTime * _liveTargetSmoothing);

            GetState<SteerToReceiveTarget>().SteeringTarget = _liveReceiveTarget;
        }

        Vector3 PredictInterceptPoint(Ball ball, Rigidbody ballRigidbody, float fallbackTime)
        {
            Vector3 position = ball.Position;
            Vector3 velocity = ballRigidbody.velocity;
            float gravityY = Physics.gravity.y;

            float predictionTime = fallbackTime;
            if (ball.HeightAbovePitch > 0.35f || velocity.y > 0.5f)
            {
                float timeToGround = EstimateTimeToGround(position.y, velocity.y, gravityY);
                if (timeToGround > 0f)
                    predictionTime = Mathf.Min(predictionTime, timeToGround);
            }

            Vector3 predicted = position + velocity * predictionTime + 0.5f * Physics.gravity * predictionTime * predictionTime;
            predicted.y = 0f;
            return predicted;
        }

        float EstimateTimeToGround(float height, float verticalVelocity, float gravityY)
        {
            if (height <= 0f)
                return 0f;

            if (Mathf.Abs(gravityY) <= 0.0001f)
                return verticalVelocity < 0f ? (height / -verticalVelocity) : -1f;

            float discriminant = (verticalVelocity * verticalVelocity) - (2f * gravityY * height);
            if (discriminant < 0f)
                return -1f;

            float sqrt = Mathf.Sqrt(discriminant);
            float t1 = (-verticalVelocity + sqrt) / gravityY;
            float t2 = (-verticalVelocity - sqrt) / gravityY;

            float result = float.MaxValue;
            if (t1 > 0f)
                result = t1;
            if (t2 > 0f)
                result = Mathf.Min(result, t2);

            return result == float.MaxValue ? -1f : result;
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
