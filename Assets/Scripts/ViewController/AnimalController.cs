﻿using System;
using System.Collections;
using System.Collections.Generic;
using AnimalsV2;
using AnimalsV2.States;
using AnimalsV2.States.AnimalsV2.States;
using DataCollection;
using Model;
using UnityEngine;
using UnityEngine.AI;
using ViewController;
using Random = System.Random;

public abstract class AnimalController : MonoBehaviour
{
    public static Random random = new Random();
    
    public AnimalModel animalModel;

    [HideInInspector] public TickEventPublisher tickEventPublisher;

    public Action<State> stateChange;

    // decisionMaker subscribes to these actions
    public Action<GameObject> actionPerceivedHostile;
    public Action actionDeath;

    //Subscribed to by animalBrainAgent.
    public event EventHandler<OnBirthEventArgs> onBirth;

    public class OnBirthEventArgs : EventArgs
    {
        public GameObject child;
    }

    [HideInInspector] public NavMeshAgent agent;

    public FiniteStateMachine fsm;
    private AnimationController animationController;
    
    // Add a data handler
    private DataHandler dh;

    public enum CauseOfDeath
    {
        Hydration,
        Eaten,
        Health,
        Hunger,
    };
    
    //States
    public FleeingState fleeingState;
    public GoToFood goToFoodState;
    public Wander wanderState;
    public Idle idleState;
    public GoToWater goToWaterState;
    public MatingState matingState;
    public Dead deadState;
    public DrinkingState drinkingState;
    public EatingState eatingState;
    public GoToMate goToMate;
    public Waiting waitingState;

    //Constants
    private const float WalkingSpeed = 0.3f;
    private const float JoggingSpeed = 0.5f;
    private const float RunningSpeed = 1f;

    //Modifiers
    [HideInInspector] public float energyModifier;
    [HideInInspector] public float hydrationModifier;
    [HideInInspector] public float reproductiveUrgeModifier = 0.3f;
    [HideInInspector] public float speedModifier = JoggingSpeed; //100% of maxSpeed in model
    
    //Timescale stuff
    private float baseAcceleration;
    private float baseAngularSpeed;


    //target lists
    public List<GameObject> visibleHostileTargets = new List<GameObject>();
    public List<GameObject> visibleFriendlyTargets = new List<GameObject>();
    public List<GameObject> visibleFoodTargets = new List<GameObject>();
    public List<GameObject> visibleWaterTargets = new List<GameObject>();

    
    public  List<GameObject> heardHostileTargets = new List<GameObject>();
    public  List<GameObject> heardFriendlyTargets = new List<GameObject>();
    public  List<GameObject> heardPreyTargets = new List<GameObject>();


    public bool IsControllable { get; set; } = false;

    //used for ml, so that it does not spawn a lot of children that might interfere with training
    public bool isInfertile = false;

    public void Awake()
    {
        //Create the FSM.
        fsm = new FiniteStateMachine();

        goToFoodState = new GoToFood(this, fsm);
        fleeingState = new FleeingState(this, fsm);
        wanderState = new Wander(this, fsm);
        idleState = new Idle(this, fsm);
        goToWaterState = new GoToWater(this, fsm);
        matingState = new MatingState(this, fsm);
        deadState = new Dead(this, fsm);
        drinkingState = new DrinkingState(this, fsm);
        eatingState = new EatingState(this, fsm);
        goToMate = new GoToMate(this, fsm);
        waitingState = new Waiting(this, fsm);
        fsm.Initialize(wanderState);

        animationController = new AnimationController(this);
    }

    protected void Start()
    {
        // Init the NavMesh agent
        agent = GetComponent<NavMeshAgent>();
        agent.autoBraking = true;

        animalModel.currentSpeed = animalModel.traits.maxSpeed * speedModifier * animalModel.traits.size;

        //Can be used later.
        baseAngularSpeed = agent.angularSpeed;
        baseAcceleration = agent.acceleration;
        
        agent.speed = animalModel.currentSpeed * Time.timeScale;
        agent.acceleration *= Time.timeScale;
        agent.angularSpeed *= Time.timeScale;

        //dh.LogNewAnimal(animalModel);
        
        //Debug.Log(agent.autoBraking);
        tickEventPublisher = FindObjectOfType<global::TickEventPublisher>();
        EventSubscribe();

        SetPhenotype();
    }

    /* /\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\ */
    /*                                   Parameter handlers                                   */
    /* \/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/ */

    /// <summary>
    /// Parameter levels are to constantly be ticking down.
    /// Using tickEvent so as to not need a separate yielding tick thread for each animal.
    /// Instead just use one global event publisher that handles the ticking,
    /// then on each tick decrement each of the meters.
    ///
    /// Important to unsubscribe from the event publisher on death, however!
    /// </summary>
    public virtual void ChangeModifiers(State state)
    {
        switch (state)
        {
            case GoToFood _:
                //TODO bad practice, hard coded values, this is temporary
                if (animalModel is BearModel || animalModel is WolfModel)
                    HighEnergyState();
                else
                    MediumEnergyState();
                break;
            case GoToWater _:
                MediumEnergyState();
                break;
            case GoToMate _:
                MediumEnergyState();
                break;
            case FleeingState _:
                HighEnergyState();
                break;
            case EatingState _:
                LowEnergyState();
                break;
            case DrinkingState _:
                LowEnergyState();
                break;
            case MatingState _:
                HighEnergyState();
                break;
            case SearchingState _:
                MediumEnergyState();
                break;
            case Idle _:
                LowEnergyState();
                break;
            case Wander _:
                LowEnergyState();
                break;
            case Dead _:
                energyModifier = 0f;
                hydrationModifier = 0f;
                reproductiveUrgeModifier = 0f;
                speedModifier = 0f;
                break;
            default:
                energyModifier = 0.1f;
                hydrationModifier = 0.05f;
                reproductiveUrgeModifier = 0.2f;

                speedModifier = JoggingSpeed;
                //Debug.Log("varying parameters depending on state: Wander");
                break;
        }
    }
    // if (animalModel is WolfModel)
        // {
        //     Debug.Log(speedModifier);
        // }

    private void HighEnergyState()
    {
        energyModifier = 1f;
        hydrationModifier = 1f;
        reproductiveUrgeModifier = 0f;
        speedModifier = RunningSpeed;
    }
    private void MediumEnergyState()
    {
        energyModifier = 0.35f;
        hydrationModifier = 0.5f;
        reproductiveUrgeModifier = 1f;
        speedModifier = JoggingSpeed;
    }
    private void LowEnergyState()
    {
        energyModifier = 0.15f;
        hydrationModifier = 0.25f;
        reproductiveUrgeModifier = 1f;
        speedModifier = WalkingSpeed;
    }
    public virtual void UpdateParameters()
    {
        //The age will increase 2 per 2 seconds.
        animalModel.age += 1;
        
        // speed
        animalModel.currentSpeed = animalModel.traits.maxSpeed * speedModifier;
        //TODO, maybe move from here?
        agent.speed = animalModel.currentSpeed;
        
        // energy
        animalModel.currentEnergy -= (animalModel.age + animalModel.currentSpeed + 
            animalModel.traits.viewRadius / 10 + animalModel.traits.hearingRadius / 10)
                                     * animalModel.traits.size * energyModifier;
        
        // hydration
        animalModel.currentHydration -= animalModel.traits.size * 
                                        (1 + 
                                         animalModel.currentSpeed / animalModel.traits.endurance * 
                                         hydrationModifier);
        
        // reproductive urge
        animalModel.reproductiveUrge += 0.2f * reproductiveUrgeModifier;
        animalModel.currentSpeed = animalModel.traits.maxSpeed * speedModifier * animalModel.traits.size;
        agent.speed = animalModel.currentSpeed * Time.timeScale;
    }


    protected void EventSubscribe()
    {
        if (tickEventPublisher)
        {
            // every 2 sec
            tickEventPublisher.onParamTickEvent += UpdateParameters;
            tickEventPublisher.onParamTickEvent += HandleDeathStatus;
            // every 0.5 sec
            tickEventPublisher.onSenseTickEvent += fsm.UpdateStatesLogic;    
        }

        fsm.OnStateEnter += ChangeModifiers;

        eatingState.onEatFood += EatFood;

        drinkingState.onDrinkWater += DrinkWater;

        matingState.onMate += Mate;

        animationController.EventSubscribe();
    }
    protected void EventUnsubscribe()
    {
        if (tickEventPublisher)
        {
            // every 2 sec
            tickEventPublisher.onParamTickEvent -= UpdateParameters;
            tickEventPublisher.onParamTickEvent -= HandleDeathStatus;
            // every 0.5 sec
            tickEventPublisher.onSenseTickEvent -= fsm.UpdateStatesLogic;    
        }
        

        if (fsm != null)
        {
            fsm.OnStateEnter -= ChangeModifiers;
        }

        eatingState.onEatFood -= EatFood;

        drinkingState.onDrinkWater -= DrinkWater;

        matingState.onMate -= Mate;

        animationController.EventUnsubscribe();
    }

    //Set animals size based on traits.
    private void SetPhenotype()
    {
        gameObject.transform.localScale = getNormalizedScale() * animalModel.traits.size;
    }
    public void EatFood(GameObject food, float currentEnergy)
    {

        if (food != null && food.GetComponent<AnimalController>()?.animalModel is IEdible edibleAnimal &&
            animalModel.CanEat(edibleAnimal))
        { 
            animalModel.currentEnergy += edibleAnimal.GetEaten();
            Destroy(food);
        }

        if (food != null && food.GetComponent<PlantController>()?.plantModel is IEdible ediblePlant && animalModel.CanEat(ediblePlant))
        {
            animalModel.currentEnergy += ediblePlant.GetEaten();
            Destroy(food);
        }
    }

    public void DrinkWater(GameObject water, float currentHydration)
    {
        if (water != null && water.gameObject.CompareTag("Water") && !animalModel.HydrationFull)
        {
            animalModel.currentHydration = animalModel.traits.maxHydration;
        }

    }

    void Mate(GameObject target)
    {
        if (isInfertile) return;
    
        AnimalController targetAnimalController = null;
    
        if (target != null)
        { 
            targetAnimalController = target.GetComponent<AnimalController>();

        }
        
        Random rng = new System.Random();
        
        // make sure target has an AnimalController,
        // that its animalModel is same species, and neither animal is already carrying
        if (targetAnimalController != null && targetAnimalController.animalModel.IsSameSpecies(animalModel) &&
            targetAnimalController.animalModel.WantingOffspring)
        {

            // higher max urge gives greater potential for more offspring. (1-8 offspring)
            int offspringCount = Math.Max(1, rng.Next((int) animalModel.traits.maxReproductiveUrge / 5 + 1));
            
            // higher max urge => lower gestation time.
            float gestationTime = Mathf.Max(1, 100 / animalModel.traits.maxReproductiveUrge);
            
            float childEnergy = animalModel.currentEnergy * 0.3f +
                                targetAnimalController.animalModel.currentEnergy * 0.3f;
            childEnergy /= offspringCount;
            float childHydration = animalModel.currentHydration * 0.25f +
                                   targetAnimalController.animalModel.currentHydration * 0.25f;

            
            
            // Expend energy and give it to child(ren)
            animalModel.currentEnergy *= 0.7f;
            targetAnimalController.animalModel.currentEnergy *= 0.7f;
            animalModel.currentHydration *= 0.7f;
            targetAnimalController.animalModel.currentHydration *= 0.7f;

            // Reset both reproductive urges. 
            animalModel.reproductiveUrge = 0f;
            targetAnimalController.animalModel.reproductiveUrge = 0f;


            animalModel.isPregnant = true;
            for (int i = 1; i <= offspringCount; i++)
                // Wait some time before giving birth
                StartCoroutine(GiveBirth(childEnergy, childHydration, gestationTime, targetAnimalController));
        
        }
    }
    

    IEnumerator GiveBirth(float childEnergy, float childHydration, float laborTime, AnimalController otherParentAnimalController)
    {
        yield return new WaitForSeconds(laborTime);
        //Instantiate here
        
        GameObject child = Instantiate(gameObject, transform.position,
            transform.rotation); //NOTE CHANGE SO THAT PREFAB IS USED
        
        // Generate the offspring traits
        AnimalModel childModel = animalModel.Mate(otherParentAnimalController.animalModel);
        child.GetComponent<AnimalController>().animalModel = childModel;
        child.GetComponent<AnimalController>().animalModel.currentEnergy = childEnergy;
        child.GetComponent<AnimalController>().animalModel.currentHydration = childHydration;   

        // update the childs speed (in case of mutation).
        child.GetComponent<AnimalController>().animalModel.traits.maxSpeed = 1;
        
        animalModel.isPregnant = false;
        //Debug.Log(child.GetComponent<AnimalController>().animalModel.generation);
        onBirth?.Invoke(this,new OnBirthEventArgs{child = child});

    }

    /* \/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/ */
    
    public void DestroyGameObject(float delay)
    {
        Destroy(gameObject, delay);
    }
    

    //should be refactored so that this logic is in AnimalModel
    private void HandleDeathStatus()
    {
        if (!animalModel.IsAlive)
        {
            CauseOfDeath cause;
            if (animalModel.currentEnergy == 0) cause = CauseOfDeath.Hunger;
            if (animalModel.currentHealth == 0) cause = CauseOfDeath.Health;
            if (animalModel.currentHydration == 0) cause = CauseOfDeath.Hydration;
            else cause = CauseOfDeath.Eaten;
            //dh.LogDeadAnimal(animalModel, cause);

            // invoke death state with method HandleDeath() in decisionmaker
            actionDeath?.Invoke();
            
            //Stop animal from giving birth once dead.
            StopCoroutine("GiveBirth");
            StopAllCoroutines();

            // unsubscribe all events because we want only want to invoke it once.
            //actionDeath = null;
        }
    }

    public void OnDestroy()
    {
        StopAllCoroutines();
        EventUnsubscribe();
    }


    //General method that takes unknown gameobject as input and interacts with the given gameobject depending on what it is. It can be to e.g. consume or to mate
    // It is not guaranteed that the statechange will happen since meetrequirements has to be true for given statechange.
    public void Interact(GameObject target)
    {

        //Dont stop to interact if we are fleeing.
        if (fsm.currentState is FleeingState) return;
        
        //Debug.Log(gameObject.name);
        switch (target.tag)
        {
            case "Water":
                drinkingState.SetTarget(target);
                fsm.ChangeState(drinkingState);
                break;
            case "Plant":
                if (target.TryGetComponent(out PlantController plantController) && animalModel.CanEat(plantController.plantModel))
                {
                    eatingState.SetTarget(target);
                    fsm.ChangeState(eatingState);   
                }
                break;
            case "Animal":
                if (target.TryGetComponent(out AnimalController otherAnimalController))
                {
                    AnimalModel otherAnimalModel = otherAnimalController.animalModel;
                    //if we can eat the other animal we try to do so
                    if (animalModel.CanEat(otherAnimalModel))
                    {
                        eatingState.SetTarget(target);
                        fsm.ChangeState(eatingState);
                    } else if (animalModel.IsSameSpecies(otherAnimalModel))
                    {
                        matingState.SetTarget(target);
                        fsm.ChangeState(matingState);
                    }
                }
                break;
        }

    }

    public abstract Vector3 getNormalizedScale();
}