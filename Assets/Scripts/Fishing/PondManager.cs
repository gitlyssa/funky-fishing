using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.Audio;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PondManager : MonoBehaviour
{
    // array of fish prefabs
    public GameObject[] fishPrefabs;
    // list of spawned fish
    public List<GameObject> fishList;

    public int radius = 30;
    public int waterlevel = 0;
    [Header("Spawning")]
    [Min(0)] public int initialFishCount = 10;
    public TextMeshProUGUI FishCaughtText;
    private bool fishCaughtTextActive = false;

    public float catchRadius = 1.5f;
    [Header("Input")]
    public bool enableKeyboardCatchAttempt = true;
    public KeyCode catchAttemptKey = KeyCode.Space;
    [Header("Collisions")]
    public bool disableFishFishCollisions = true;
    private bool _lastDisableFishFishCollisions;

    public GameObject playerBobber;
    public GameManager gameManager;
    Vector3 pondCenter;
    private readonly List<GameObject> hiddenFishDuringTension = new List<GameObject>();

    private void IgnoreFishCollisionsFor(GameObject fish)
    {
        if (fish == null)
            return;

        Collider[] fishColliders = fish.GetComponentsInChildren<Collider>(true);
        if (fishColliders == null || fishColliders.Length == 0)
            return;

        for (int i = 0; i < fishList.Count; i++)
        {
            GameObject otherFish = fishList[i];
            if (otherFish == null || otherFish == fish)
                continue;

            Collider[] otherColliders = otherFish.GetComponentsInChildren<Collider>(true);
            if (otherColliders == null || otherColliders.Length == 0)
                continue;

            for (int a = 0; a < fishColliders.Length; a++)
            {
                Collider c1 = fishColliders[a];
                if (c1 == null) continue;

                for (int b = 0; b < otherColliders.Length; b++)
                {
                    Collider c2 = otherColliders[b];
                    if (c2 == null) continue;
                    Physics.IgnoreCollision(c1, c2, disableFishFishCollisions);
                }
            }
        }
    }

    private void ApplyFishFishCollisionSetting()
    {
        for (int i = 0; i < fishList.Count; i++)
        {
            GameObject fishA = fishList[i];
            if (fishA == null) continue;

            Collider[] collidersA = fishA.GetComponentsInChildren<Collider>(true);
            if (collidersA == null || collidersA.Length == 0) continue;

            for (int j = i + 1; j < fishList.Count; j++)
            {
                GameObject fishB = fishList[j];
                if (fishB == null) continue;

                Collider[] collidersB = fishB.GetComponentsInChildren<Collider>(true);
                if (collidersB == null || collidersB.Length == 0) continue;

                for (int a = 0; a < collidersA.Length; a++)
                {
                    Collider c1 = collidersA[a];
                    if (c1 == null) continue;

                    for (int b = 0; b < collidersB.Length; b++)
                    {
                        Collider c2 = collidersB[b];
                        if (c2 == null) continue;
                        Physics.IgnoreCollision(c1, c2, disableFishFishCollisions);
                    }
                }
            }
        }
    }

    // public AudioSource bobberSound;
    // public AudioResource bobberSplashClip;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {   
        // bobberSound.resource = bobberSplashClip;
        gameManager = FindObjectOfType<GameManager>();
        pondCenter = transform.position;
        fishList = new List<GameObject>();
        _lastDisableFishFishCollisions = disableFishFishCollisions;
        // Spawn initial fish at random positions in pond.
        for (int i = 0; i < initialFishCount; i++)
        {   

            // x^2 + z^2 < radius^2
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            Vector3 randomPosition = new Vector3(pondCenter.x + randomCircle.x, 
            waterlevel, 
            pondCenter.z + randomCircle.y);

            SpawnFish(-1, randomPosition);
        }

        ApplyFishFishCollisionSetting();
    }

    // Update is called once per frame
    void Update()
    {
        // check for input key r to spawn random fish at random position in pond

        if (Input.GetKeyDown(KeyCode.R))
        {
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            Vector3 randomPosition = new Vector3(pondCenter.x + randomCircle.x, 
            waterlevel, 
            pondCenter.z + randomCircle.y);

            SpawnFish(-1, randomPosition);
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            if (fishList.Count > 0)
            {
                int randomIndex = Random.Range(0, fishList.Count);
                RemoveFish(randomIndex);
            }
        }

        if (enableKeyboardCatchAttempt && Input.GetKeyDown(catchAttemptKey))
        {
            Debug.Log("Attempting to catch fish...");
            if (fishCaughtTextActive)
            {
                FishCaughtText.gameObject.SetActive(false);
                fishCaughtTextActive = false;
            }
            CatchFish(playerBobber);
        }

        if (_lastDisableFishFishCollisions != disableFishFishCollisions)
        {
            ApplyFishFishCollisionSetting();
            _lastDisableFishFishCollisions = disableFishFishCollisions;
        }
    }
    void SpawnFish(int fishIndex, Vector3 position)
    {
        if (fishIndex < 0)
        {
            // spawn random fish
            int randomIndex = Random.Range(0, fishPrefabs.Length);
            fishIndex = randomIndex;
        }
        GameObject fish = Instantiate(fishPrefabs[fishIndex], position, Quaternion.identity);
        SceneManager.MoveGameObjectToScene(fish, gameObject.scene);
        //append to fish list
        fish.GetComponent<FishMovement>().pondManager = this;
        fishList.Add(fish);
        IgnoreFishCollisionsFor(fish);
    }

    void RemoveFish(int fishIndex)
    {
        hiddenFishDuringTension.Remove(fishList[fishIndex]);
        Destroy(fishList[fishIndex]);
        fishList.RemoveAt(fishIndex);
    }

    public bool RemoveFish(GameObject fish)
    {
        if (fish == null)
            return false;

        hiddenFishDuringTension.Remove(fish);
        fishList.Remove(fish);
        Destroy(fish);
        return true;
    }

    public void HideFishForTension(GameObject hookedFish)
    {
        hiddenFishDuringTension.Clear();

        for (int i = 0; i < fishList.Count; i++)
        {
            GameObject fish = fishList[i];
            if (fish == null || fish == hookedFish)
                continue;

            if (!fish.activeSelf)
                continue;

            fish.SetActive(false);
            hiddenFishDuringTension.Add(fish);
        }
    }

    public void RestoreFishAfterTension()
    {
        for (int i = 0; i < hiddenFishDuringTension.Count; i++)
        {
            GameObject fish = hiddenFishDuringTension[i];
            if (fish != null)
                fish.SetActive(true);
        }

        hiddenFishDuringTension.Clear();
    }

public GameObject GetClosestFish(GameObject bobber)
{
    if (bobber == null)
        return null;

    GameObject closestFish = null;
    float closestDistance = Mathf.Infinity;
    Vector3 bobberPos = bobber.transform.position;

    foreach (GameObject fish in fishList)
    {
        if (fish == null || !fish.activeInHierarchy)
            continue;

        Collider fishCollider = fish.GetComponent<Collider>();
        if (fishCollider == null)
            fishCollider = fish.GetComponentInChildren<Collider>();
        if (fishCollider == null) continue;

        // Closest point on fish collider to bobber
        Vector3 closestPoint = fishCollider.ClosestPoint(bobberPos);

        // 2D distance (ignore Y)
        float distance = Vector2.Distance(
            new Vector2(bobberPos.x, bobberPos.z),
            new Vector2(closestPoint.x, closestPoint.z)
        );

        // Only fish inside cast radius
        if (distance <= catchRadius && distance < closestDistance)
        {
            closestDistance = distance;
            closestFish = fish;
        }
    }

    return closestFish;
}

    void CatchFish(GameObject bobber)
    {
        GameObject fish = GetClosestFish(bobber);
        if (fish != null)
        {   
            // // add force throwing fish upwards
            // Rigidbody fishRb = fish.GetComponent<Rigidbody>();
            // if (fishRb != null)
            // {   
            //     // fishRb.isKinematic = true;
            //     fishRb.useGravity = true;
            //     fishRb.AddForce(Vector3.up * 15f, ForceMode.Impulse);
            // }


            fishList.Remove(fish);
            // Destroy(fish);
            
            FishCaughtText.gameObject.SetActive(true);
            fishCaughtTextActive = true;

            Debug.Log("Fish caught!");
            
            SceneLoading.Instance.StartRhythmEncounter(fish);

            // playerBobber.GetComponent<BobberScript>().Reset();
            // gameManager.HookFish();
        }
        else
        {
            Debug.Log("No fish caught.");
        }
    }
}
