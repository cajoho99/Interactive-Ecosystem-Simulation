﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DataCollection;
using DefaultNamespace;
using Menus;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public class ObjectPooler : MonoBehaviour
{
    /// <summary>
    /// A Pool has a tag for the contained element, rabbit. A prefab and an amount of that object to start with (size)
    /// </summary>
    [Serializable]
    public class Pool
    {
        public string tag;
        public GameObject prefab;
        public int size;
    }

    public static ObjectPooler Instance
    {
        get
        {
            if (instance == null)
                instance = FindObjectOfType(typeof(ObjectPooler)) as ObjectPooler;

            return instance;
        }
        set { instance = value; }
    }

    public static ObjectPooler instance;
    
    public List<Pool> pools;
    public Dictionary<string, Queue<GameObject>> poolDictionary;
    public Dictionary<string, Stack<GameObject>> stackDictionary;
    private bool showCanvasForAll;
    private bool isFinishedPlacing;
    private DataHandler dh;

    private void Awake()
    {
        instance = this;
        poolDictionary = new Dictionary<string, Queue<GameObject>>();
        stackDictionary = new Dictionary<string, Stack<GameObject>>();
        foreach (Pool pool in pools)
        {
            Queue<GameObject> objectPool = new Queue<GameObject>();
            Stack<GameObject> objectStack = new Stack<GameObject>();
            poolDictionary.Add(pool.tag, objectPool);
            stackDictionary.Add(pool.tag, objectStack);
        }
        showCanvasForAll = OptionsMenu.alwaysShowParameterUI;
        isFinishedPlacing = false;
    }

    private void Start()
    {
        dh = FindObjectOfType<DataHandler>();
    }

    /// <summary>
    /// Called when the terrain generator is finished placing all animals and object. 
    /// </summary>
    public void HandleFinishedSpawning()
    {
        if (!isFinishedPlacing)
        {
            GameObject groupObject = new GameObject("Pool");
            foreach (Pool pool in pools)
            {
                string objTag = pool.tag;
                if (stackDictionary[objTag].Count > 0)
                {
                    for (int i = stackDictionary[objTag].Count; i < pool.size; i++)
                    {
                        GameObject obj = Instantiate(pool.prefab, groupObject.transform, true);
                        obj.SetActive(false);
                        poolDictionary[objTag].Enqueue(obj);
                    }

                    for (int i = 0; i < stackDictionary[objTag].Count - 1; i++) poolDictionary[objTag].Enqueue(stackDictionary[objTag].Pop());
                }
            }

            isFinishedPlacing = true;
        }
    }

    /// <summary>
    /// When the terrain generator makes a new animal, the pool handles the animals start method (onObjectSpawn)
    /// and subscribes to the animal mating and death. 
    /// </summary>
    /// <param name="objectToSpawn"> The pooled object the terrain generator will spawn. </param>
    /// <param name="tag"> Tag of the animal, must match names in terrain generator. </param>
    public void HandleAnimalInstantiated(GameObject objectToSpawn, string tag)
    {
        if (poolDictionary != null && poolDictionary.ContainsKey(tag))
        {
            objectToSpawn.SetActive(true);
            objectToSpawn.GetComponent<IPooledObject>()?.onObjectSpawn();
            if (objectToSpawn.TryGetComponent(out AnimalController animalController))
            {
                dh.LogNewAnimal(animalController.animalModel);
                animalController.deadState.onDeath += HandleDeadAnimal;
                animalController.SpawnNew += HandleBirthAnimal;
                animalController.parameterUI.gameObject.SetActive(showCanvasForAll);
            }

            if (stackDictionary != null && stackDictionary.ContainsKey(tag))
            {
                stackDictionary[tag].Push(objectToSpawn);
            }
        }
        
    }

    /// <summary>
    /// Make more space in the queue and disable the animal.
    /// </summary>
    /// <param name="animalController"> Controller of the deceased animal. </param>
    /// <param name="gotEaten"></param>
    public void HandleDeadAnimal(AnimalController animalController, bool gotEaten)
    {
        if(gotEaten) StartCoroutine(HandleDeadAnimalDelay(animalController, 0f));
        else StartCoroutine(HandleDeadAnimalDelay(animalController, 5f));
    }

    private IEnumerator HandleDeadAnimalDelay(AnimalController animalController, float delay)
    {
        yield return new WaitForSeconds(delay / Time.timeScale);
        if (animalController != null)
        {
            GameObject animalObj;
            (animalObj = animalController.gameObject).SetActive(false);
            AnimalModel.CauseOfDeath cause;
            AnimalModel am = animalController.animalModel;
            if (am.currentEnergy <= 0) cause = AnimalModel.CauseOfDeath.Energy;
            else if (am.currentHydration <= 0) cause = AnimalModel.CauseOfDeath.Hydration;
            else if (am.age >= am.traits.ageLimit) cause = AnimalModel.CauseOfDeath.Age;
            else if (am.currentHealth <= 0) cause = AnimalModel.CauseOfDeath.Health;
            else cause = AnimalModel.CauseOfDeath.Eaten;
            dh.LogDeadAnimal(am, cause, (transform.position - animalController.startVector).magnitude);
            bool isSmart = animalObj.GetComponent<AnimalBrainAgent>();
            
            /*
            switch (animalController.animalModel)
            {
                case RabbitModel _:
                    if(isSmart) poolDictionary["SmartRabbit"].Enqueue(animalObj);
                    else poolDictionary["Rabbit Brown"].Enqueue(animalObj);
                    break;
                case WolfModel _:
                    if(isSmart) poolDictionary["SmartWolf"].Enqueue(animalObj);
                    else poolDictionary["Wolf Grey"].Enqueue(animalObj);
                    break;
                case DeerModel _:
                    if(isSmart) poolDictionary["SmartDeer"].Enqueue(animalObj);
                    else poolDictionary["Deer"].Enqueue(animalObj);
                    break;
                case BearModel _:
                    if(isSmart) poolDictionary["SmartBear"].Enqueue(animalObj);
                    else poolDictionary["Bear"].Enqueue(animalObj);
                    break;
            }
            */
            //Debug.Log(animalObj.name.Replace("(Clone)", "").Trim());
            
            poolDictionary[animalObj.name.Replace("(Clone)", "").Trim()].Enqueue(animalObj);
        }
        
    }

    /// <summary>
    /// Handles animal birth by instantiating the animal as previously using the pool
    /// </summary>
    /// <param name="childModel"> Model to spawn animal with </param>
    /// <param name="pos"> Where the animal is to be spawned </param>
    /// <param name="energy"> The energy of the child </param>
    /// <param name="hydration"> The hydration of the child </param>
    /// <param name="isSmart"> If the animals has ML brain </param>
    private void HandleBirthAnimal(AnimalModel childModel, Vector3 pos, float energy, float hydration, string tag)
    {
        GameObject child = null;
        
        /*
        switch (childModel)
        {
            case RabbitModel _:
                if(isSmart) child = SpawnFromPool("SmartRabbit", pos, Quaternion.identity);
                else child = SpawnFromPool("Rabbit Brown", pos, Quaternion.identity);
                break;
            case WolfModel _:
                if(isSmart) child = SpawnFromPool("SmartWolf", pos, Quaternion.identity);
                else child = SpawnFromPool("Wolf Grey", pos, Quaternion.identity);
                break;
            case DeerModel _:
                if(isSmart) child = SpawnFromPool("SmartDeer", pos, Quaternion.identity);
                else child = SpawnFromPool("Deer", pos, Quaternion.identity);
                break;
            case BearModel _:
                if(isSmart) child = SpawnFromPool("SmartBear", pos, Quaternion.identity);
                else child = SpawnFromPool("Bear", pos, Quaternion.identity);
                break;
        }
        */
        Debug.Log(tag.Replace("(Clone)", "").Trim());

        child = SpawnFromPool(tag.Replace("(Clone)", "").Trim(), pos, Quaternion.identity);
        
        if (child != null)
        {
            AnimalController childController = child.GetComponent<AnimalController>();
            childController.animalModel = childModel;
            //Debug.Log(childController.animalModel.generation);
            childController.animalModel.currentEnergy = energy;
            childController.animalModel.currentHydration = hydration;
            childController.parameterUI.gameObject.SetActive(showCanvasForAll);

            // update the childs speed (in case of mutation).
            childController.animalModel.traits.maxSpeed = 1;
            dh.LogNewAnimal(childModel);
        }
    }

    /// <summary>
    /// Activates an object from the given pool named "tag". 
    /// </summary>
    /// <param name="tag"> Name of the pool to spawn from, ie Rabbits</param>
    /// <param name="position"> Where the object is to be spawned </param>
    /// <param name="rotation"> Rotation of the object</param>
    /// <returns></returns>
    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
    {
        if (!poolDictionary[tag].Any())
        {
            // Empty queue, make more
            foreach (var pool in pools)
            {
                if (pool.tag.Equals(tag))
                {
                    pool.size += 20;
                    for (int i = 0; i < 20; i++)
                    {
                        GameObject obj = Instantiate(pool.prefab);
                        obj.SetActive(false);
                        poolDictionary[tag].Enqueue(obj);
                    }
                }
            }
        }

        if (poolDictionary != null && poolDictionary.ContainsKey(tag))
        {
            GameObject objectToSpawn = poolDictionary[tag].Dequeue();

            if (objectToSpawn != null)
            {
                objectToSpawn.transform.position = position;
                objectToSpawn.transform.rotation = rotation;
                objectToSpawn.SetActive(true);
                //TODO Maintain list of all components for more performance
                objectToSpawn.GetComponent<IPooledObject>()?.onObjectSpawn();

                objectToSpawn.GetComponent<AnimalController>().deadState.onDeath += HandleDeadAnimal;
                objectToSpawn.GetComponent<AnimalController>().SpawnNew += HandleBirthAnimal;

                return objectToSpawn;
            }
        }

        return null;
    }
}