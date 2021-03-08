﻿using System;
using Model;

public class RabbitModel : AnimalModel,IEdible
{

    public RabbitModel() : base(new Traits(1f, 20, 100, 50, 3,10,10,10,10,120,10,3, Traits.BehaviorType.Herbivore, Traits.Species.Rabbit),0)
    {
        // Rabbit specific initialization 
    }
    
    public RabbitModel(Traits traits, int generation) : base(traits, generation)
    {
        
    }

    public override AnimalModel Mate(AnimalModel otherParent)
    {
        Traits childTraits = traits.Crossover(otherParent.traits);
        //TODO logic for determining generation
        return new RabbitModel(childTraits, 0);
    }

    public float GetEaten()
    {
        return traits.maxEnergy;
    }

    public override bool CanEat<T>(T obj)
    {
        return obj is PlantModel;
    }

    public override bool IsSameSpecies<T>(T obj)
    {
        return obj is RabbitModel;
    }
}