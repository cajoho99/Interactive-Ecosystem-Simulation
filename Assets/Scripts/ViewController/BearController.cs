﻿using UnityEngine;

public class BearController : AnimalController
{
    // Start is called before the first frame update
    void Awake()
    {
        base.Awake();
        animalModel = new BearModel();
    }

    
}
