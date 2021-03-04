﻿using UnityEngine;
using UnityEngine.AI;

namespace AnimalsV2.States
{
    public class GoToMate : State
    {
        public GoToMate(AnimalController animal, FiniteStateMachine finiteStateMachine) : base(animal, finiteStateMachine)
        {
        }

        public override void Enter()
        {
            base.Enter();
            currentStateAnimation = StateAnimation.Walking;
        }

        public override void HandleInput()
        {
            base.HandleInput();
            
        }

        public override void LogicUpdate()
        {
            base.LogicUpdate();
            if (MeetRequirements())
            {
                GameObject foundMate = GetFoundMate();
                if (foundMate != null && animal.agent.isActiveAndEnabled)
                {
                    Vector3 pointToRunTo =
                        NavigationUtilities.RunToFromPoint(animal.transform, foundMate.transform.position, true);
                    //Move the animal using the navmeshagent.
                    NavMeshHit hit;
                    NavMesh.SamplePosition(pointToRunTo, out hit, 5, 1 << NavMesh.GetAreaFromName("Walkable"));
                    animal.agent.SetDestination(hit.position);
                    if (Vector3.Distance(animal.gameObject.transform.position, foundMate.transform.position) <= 2f)
                    {
                        finiteStateMachine.ChangeState(animal.matingState);
                    }    
                }
                
            }
            else
            {
                finiteStateMachine.ChangeState(animal.wanderState);
            }
        }
        
        

        public override string ToString()
        {
            return "Going To Mate";
        }

        public override bool MeetRequirements()
        {
            return animal.visibleFriendlyTargets.Count > 0 && !(finiteStateMachine.CurrentState is Mating) && GetFoundMate() != null;
        }

        private GameObject GetFoundMate()
        {
            foreach(GameObject potentialMate in animal.visibleFriendlyTargets)
            {
                if (potentialMate.TryGetComponent(out AnimalController potentialMateAnimalController) && potentialMateAnimalController.animalModel.WantingOffspring)
                {
                    return potentialMateAnimalController.gameObject;
                }
            }

            return null;
        }
    }
}