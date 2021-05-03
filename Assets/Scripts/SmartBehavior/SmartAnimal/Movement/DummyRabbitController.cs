﻿using AnimalsV2;
using UnityEngine;

public class DummyRabbitController : AnimalController
{
    
    new void Awake()
    {
        animalModel = new RabbitModel();
        base.Awake();
        animalModel.currentHydration = animalModel.traits.maxHydration;
        animalModel.currentEnergy = animalModel.traits.maxEnergy;
        animalModel.reproductiveUrge = 100f;
        fsm.SetDefaultState(idleState);
        fsm.ChangeState(idleState);
    }

    public override void onObjectSpawn()
    {
        //Do nothing
    }

    public override void ChangeModifiers(State state)
    {
        //Do nothing
    }

    public override void UpdateParameters()
    {
        //do nothing
    }

    protected override void SetPhenotype()
    {
        //do nothing
    }

    public override Vector3 getNormalizedScale()
    {
        return new Vector3(1f, 1f, 1f);
    }
    
    public override string GetObjectLabel()
    {
        return "DummyRabbit";
    }
}
