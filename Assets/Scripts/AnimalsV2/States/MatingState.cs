/*
 * Authors: Johan A.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AnimalsV2.States
{
    public class MatingState : State
    {
        private AnimalController animalController;

        public Action<GameObject> onMate;
        
        private float timeLeft = 3.0f;

        public MatingState(AnimalController animalController, FiniteStateMachine finiteStateMachine) : base(
            animalController, finiteStateMachine)
        {
            currentStateAnimation = StateAnimation.Mating;
        }

        public override void Enter()
        {
            base.Enter();
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
                Mate(GetFoundMate());
            }
            else
            {
                finiteStateMachine.GoToDefaultState();
            }
        }

        public void Mate(GameObject target)
        {
            onMate?.Invoke(target);
            finiteStateMachine.ChangeState(animal.wanderState);
        }
        
        public override string ToString()
        {
            return "Mating";
        }

        public override bool MeetRequirements()
        {
            return GetFoundMate() != null && FoundMateIsClose();
        }
        
        private GameObject GetFoundMate()
        {
            List<GameObject> allNearbyFriendly = animal.heardFriendlyTargets.Concat(animal.visibleFriendlyTargets).ToList();

            foreach(GameObject potentialMate in allNearbyFriendly)
            {
                if (potentialMate.TryGetComponent(out AnimalController potentialMateAnimalController))
                {
                    if (potentialMateAnimalController.animalModel.WantingOffspring)
                    {
                        return potentialMateAnimalController.gameObject;
                    }
                }
            }

            return null;
        }

        private bool FoundMateIsClose()
        {
            return Vector3.Distance(GetFoundMate().transform.position, animal.transform.position) <= 2f;
        }
    }
}