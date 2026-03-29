using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.PickOutThreat.MainState;
using RobustFSM.Base;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.PickOutThreat.SubStates
{
    public class SteerToThreat : BState
    {
        float _waitTime;
        Vector3 _steeringTarget;

        Player _newThreat;
        Vector3 _defensiveErrorOffset;

        MatchDifficultyProfile GetDifficultyProfile()
        {
            if (MatchManager.Instance != null)
                return MatchManager.Instance.CurrentDifficultyProfile;

            return new MatchDifficultyProfile
            {
                AIDefensiveGapChance = 0.1f
            };
        }

        void RefreshDefensiveErrorOffset()
        {
            _defensiveErrorOffset = Vector3.zero;

            if (Owner == null || Owner.IsUserControlled || Threat == null)
                return;

            MatchDifficultyProfile profile = GetDifficultyProfile();
            if (Random.value > profile.AIDefensiveGapChance)
                return;

            Vector3 toGoal = Owner.TeamGoal.Position - Threat.Position;
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude <= 0.0001f)
                toGoal = Vector3.forward;

            Vector3 lateral = Vector3.Cross(Vector3.up, toGoal.normalized);
            if (lateral.sqrMagnitude <= 0.0001f)
                lateral = Vector3.right;

            float lateralSign = Random.value <= 0.5f ? -1f : 1f;
            float lateralGap = Random.Range(0.35f, 1.05f);
            float depthGap = Random.Range(-0.25f, 0.6f);

            _defensiveErrorOffset = (lateral * lateralSign * lateralGap) + (toGoal.normalized * depthGap);
        }

        public override void Enter()
        {
            base.Enter();

            //set wait time
            _waitTime = 2f;

            //set steering target
            Threat = ((PickOutThreatMainState)Machine).Threat;

            if (Threat == null)
                Machine.ChangeState<SteerToHome>();
            else
            {
                RefreshDefensiveErrorOffset();
                _steeringTarget = Threat.Position;

                // set threat is picked out
                Threat.SupportSpot.SetIsPickedOut(Owner);

                //set steering on
                Owner.RPGMovement.SetMoveTarget(_steeringTarget);
                Owner.RPGMovement.SetRotateFacePosition(_steeringTarget);
                Owner.RPGMovement.SetSteeringOn();
                Owner.RPGMovement.SetTrackingOn();
            }
        }

        public override void Execute()
        {
            base.Execute();

            //check if now at target and switch to wait for ball
            if (Owner.IsAtTarget(_steeringTarget))
                Machine.ChangeState<WaitAtTarget>();
        }

        public override void ManualExecute()
        {
            base.ManualExecute();

            _waitTime -= 1;

            if (_waitTime <= 0)
            {
                //reset the wait time
                _waitTime = 2;

                //get the steering target
                _steeringTarget = GetSteeringTarget();

                // get the support spot
                _newThreat = ((PickOutThreatMainState)Machine).Threat;

                if(_newThreat != Threat)
                {
                    // set is threat is picked out to true
                    Threat.SupportSpot.SetIsNotPickedOut();

                    // if there is no threat then go to home
                    if(_newThreat == null)
                    {
                        Threat = null;
                        Machine.ChangeState<SteerToHome>();
                    }
                    else
                    {
                        // update threat to new threat
                        Threat = _newThreat;

                        RefreshDefensiveErrorOffset();

                        // pick out the new threat
                        Threat.SupportSpot.SetIsPickedOut(Owner);

                        // update the steering target
                        _steeringTarget = GetSteeringTarget();
                    }
                }

                //update the steering to target
                Owner.RPGMovement.SetMoveTarget(_steeringTarget);
                Owner.RPGMovement.SetRotateFacePosition(_steeringTarget);
            }
        }

        public override void Exit()
        {
            base.Exit();

            //set steering off
            Owner.RPGMovement.SetSteeringOff();
            Owner.RPGMovement.SetTrackingOff();
        }

        public Vector3 GetSteeringTarget()
        {
            //find direction to goal
            Vector3 directionOfThreatToGoal = Owner.TeamGoal.Position - Threat.Position;

            //the spot is somewhere between the threat and my goal
            Vector3 steeringTarget = Threat.Position
                + directionOfThreatToGoal.normalized
                * (Owner.ThreatTrackDistance + Owner.Radius);

            steeringTarget += _defensiveErrorOffset;

            // return result
            return steeringTarget;
        }

        public Player Owner
        {
            get
            {
                return ((InFieldPlayerFSM)SuperMachine).Owner;
            }
        }

        public Player Threat { get; set; }
    }
}
