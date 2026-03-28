using System;
using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.ChaseBall.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.Team.Attack.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.Team.Wait.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Enums;
using RobustFSM.Base;
using RobustFSM.Interfaces;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.Team.Defend.MainState
{
    /// <summary>
    /// The team drops into it's own half and tries to place players between itself and the
    /// goal in the hope of making it difficult for the opposition to score
    /// </summary>
    public class DefendMainState : BState
    {
        float _lengthPitch = 90;
        TeamPlayer _closestPlayerToBall;
        bool _isHoldingShapeAgainstGoalkeeper;
        float _nextClosestPlayerSwitchTime;

        public override void Enter()
        {
            base.Enter();

            _isHoldingShapeAgainstGoalkeeper = false;
            _nextClosestPlayerSwitchTime = 0f;

            //listen to some team events
            //Owner.OnBallLaunched += Instance_OnBallLaunched;
            Owner.OnGainPossession += Instance_OnGainPossession;
            Owner.OnMessagedToStop += Instance_OnMessagedToStop;

            // init the players home positions
            Owner.Players.ForEach(tM => ActionUtility.Invoke_Action(tM.Player.OnInstructedToGoToHome));
        }

        private void Instance_OnBallLaunched(float flightTime, float velocity, Vector3 initial, Vector3 target)
        {
            // get the current closest player to ball
            TeamPlayer currClosestPlayerToPoint = Owner.GetClosestPlayerToPoint(target);

            //update new player to attack ball
            if(currClosestPlayerToPoint != _closestPlayerToBall)
            {
                bool shouldSwitch = Owner.ShouldSwitchClosestPlayer(_closestPlayerToBall,
                    currClosestPlayerToPoint,
                    target);
                if (!shouldSwitch)
                    return;

                bool hasCurrentChaser = _closestPlayerToBall != null && _closestPlayerToBall.Player != null;
                if (hasCurrentChaser && Time.time < _nextClosestPlayerSwitchTime)
                    return;

                // message the closest player to go out of chaseball
                if(_closestPlayerToBall != null)
                    _closestPlayerToBall.Player.Invoke_OnIsNoLongerTheClosestPlayerToBall();

                // update to new closest player
                _closestPlayerToBall = currClosestPlayerToPoint;

                // raise the new player to say he is now the new closest player to ball
                _closestPlayerToBall.Player.Invoke_OnBecameTheClosestPlayerToBall();
                _nextClosestPlayerSwitchTime = Time.time + Owner.ClosestPlayerSwitchCooldownSeconds;
            }
        }

        private void TriggerPlayerToChaseBall()
        {
            if (IsGoalkeeperHoldingBall())
            {
                CancelCurrentChaser();
                return;
            }

            // get the current closest player to ball
            TeamPlayer currClosestPlayerToPoint = Owner.GetClosestPlayerToPoint(Ball.Instance.NormalizedPosition);
            if (currClosestPlayerToPoint == null || currClosestPlayerToPoint.Player == null)
                return;

            //update new player to attack ball
            if (currClosestPlayerToPoint != _closestPlayerToBall)
            {
                bool shouldSwitch = Owner.ShouldSwitchClosestPlayer(_closestPlayerToBall,
                    currClosestPlayerToPoint,
                    Ball.Instance.NormalizedPosition);
                if (!shouldSwitch)
                    return;

                bool hasCurrentChaser = _closestPlayerToBall != null && _closestPlayerToBall.Player != null;
                if (hasCurrentChaser && Time.time < _nextClosestPlayerSwitchTime)
                    return;

                // message the closest player to go out of chaseball
                if (_closestPlayerToBall != null && _closestPlayerToBall.Player != null)
                    _closestPlayerToBall.Player.Invoke_OnIsNoLongerTheClosestPlayerToBall();

                // update to new closest player
                _closestPlayerToBall = currClosestPlayerToPoint;

                // raise the new player to say he is now the new closest player to ball
                _closestPlayerToBall.Player.Invoke_OnBecameTheClosestPlayerToBall();
                _nextClosestPlayerSwitchTime = Time.time + Owner.ClosestPlayerSwitchCooldownSeconds;
            }
            else if(currClosestPlayerToPoint != null 
                && currClosestPlayerToPoint.Player.InFieldPlayerFSM.IsCurrentState<ChaseBallMainState>() == false)
            {
                // raise the new player to say he is now the new closest player to ball
                if (_closestPlayerToBall != null && _closestPlayerToBall.Player != null)
                    _closestPlayerToBall.Player.Invoke_OnBecameTheClosestPlayerToBall();
            }
        }

        bool IsGoalkeeperHoldingBall()
        {
            Player ballOwner = Ball.Instance.Owner;
            return ballOwner != null && ballOwner.PlayerType == PlayerTypes.Goalkeeper;
        }

        void CancelCurrentChaser()
        {
            if (_closestPlayerToBall != null && _closestPlayerToBall.Player != null)
                _closestPlayerToBall.Player.Invoke_OnIsNoLongerTheClosestPlayerToBall();

            _closestPlayerToBall = null;
            _nextClosestPlayerSwitchTime = 0f;
        }

        public override void ManualExecute()
        {
            base.ManualExecute();

            bool isGoalkeeperHoldingBall = IsGoalkeeperHoldingBall();
            if (isGoalkeeperHoldingBall)
            {
                if (_isHoldingShapeAgainstGoalkeeper == false)
                {
                    Owner.Players.ForEach(tM => ActionUtility.Invoke_Action(tM.Player.OnInstructedToGoToHome));
                    _isHoldingShapeAgainstGoalkeeper = true;
                }
            }
            else
            {
                _isHoldingShapeAgainstGoalkeeper = false;
            }

            // trigger closest player to ball to chase ball
            TriggerPlayerToChaseBall();

            //loop through each player and update it's position
            foreach (TeamPlayer teamPlayer in Owner.Players)
            {
                //find the percentage to move the player upfield
                Vector3 ballGoalLocalPosition = Owner.Goal.transform.InverseTransformPoint(Ball.Instance.transform.position);
                float playerMovePercentage = Mathf.Clamp01((ballGoalLocalPosition.z / _lengthPitch) - 0.35f);

                //move the home position a similar percentage up the field
                Vector3 currentPlayerHomePosition = Vector3.Lerp(teamPlayer.DefendingHomePosition.transform.position,
                    teamPlayer.AttackingHomePosition.position,
                    playerMovePercentage);

                //update the current player home position position
                if (Vector3.Distance(currentPlayerHomePosition, teamPlayer.CurrentHomePosition.position) >= 2)
                    teamPlayer.CurrentHomePosition.position = currentPlayerHomePosition;
            }
        }

        public override void Exit()
        {
            base.Exit();

            CancelCurrentChaser();

            _isHoldingShapeAgainstGoalkeeper = false;

            //stop listening to some team events
            //Owner.OnBallLaunched -= Instance_OnBallLaunched;
            Owner.OnGainPossession -= Instance_OnGainPossession;
            Owner.OnMessagedToStop -= Instance_OnMessagedToStop;
        }

        private void Instance_OnGainPossession()
        {
            Machine.ChangeState<AttackMainState>();
        }

        private void Instance_OnMessagedToStop()
        {
            SuperMachine.ChangeState<WaitMainState>();
        }

        public Scripts.Entities.Team Owner
        {
            get
            {
                return ((TeamFSM)SuperMachine).Owner;
            }
        }
    }
}
