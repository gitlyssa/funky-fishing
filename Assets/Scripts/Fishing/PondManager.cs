using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.Audio;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PondManager : MonoBehaviour
{
    [System.Serializable]
    public class FishSpawnEntry
    {
        public GameObject prefab;
        [Min(0)] public int count = 1;
    }

    // array of fish prefabs
    public GameObject[] fishPrefabs;
    [Header("Spawn Plan (Preferred)")]
    public List<FishSpawnEntry> initialSpawnPlan = new List<FishSpawnEntry>();
    [Header("Boss Spawn Plan")]
    public List<FishSpawnEntry> bossSpawnPlan = new List<FishSpawnEntry>();

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
    private bool _bossWaveSpawned;

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

        // Preferred startup behavior: spawn exact counts per fish type.
        bool usedSpawnPlan = SpawnInitialFishFromPlan();

        // Backward-compatible fallback: if no plan is configured, use old random count spawning.
        if (!usedSpawnPlan)
        {
            for (int i = 0; i < initialFishCount; i++)
                SpawnFish(GetRandomSpawnPrefab(), GetRandomSpawnPosition());
        }

        TrySpawnBossWaveIfNeeded();
        ApplyFishFishCollisionSetting();
    }

    // Update is called once per frame
    void Update()
    {
        // check for input key r to spawn random fish at random position in pond

        if (Input.GetKeyDown(KeyCode.R))
        {
            SpawnFish(GetRandomSpawnPrefab(), GetRandomSpawnPosition());
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
    private bool SpawnInitialFishFromPlan()
    {
        return SpawnFishFromPlan(initialSpawnPlan);
    }

    private bool SpawnFishFromPlan(List<FishSpawnEntry> spawnPlan)
    {
        bool spawnedAny = false;
        if (spawnPlan == null || spawnPlan.Count == 0)
            return false;

        for (int i = 0; i < spawnPlan.Count; i++)
        {
            FishSpawnEntry entry = spawnPlan[i];
            if (entry == null || entry.prefab == null)
                continue;

            int count = Mathf.Max(0, entry.count);
            for (int n = 0; n < count; n++)
            {
                SpawnFish(entry.prefab, GetRandomSpawnPosition());
                spawnedAny = true;
            }
        }

        return spawnedAny;
    }

    private bool HasAnyLiveFish()
    {
        if (fishList == null || fishList.Count == 0)
            return false;

        for (int i = 0; i < fishList.Count; i++)
        {
            if (fishList[i] != null)
                return true;
        }

        return false;
    }

    private bool HasValidBossSpawnPlan()
    {
        if (bossSpawnPlan == null || bossSpawnPlan.Count == 0)
            return false;

        for (int i = 0; i < bossSpawnPlan.Count; i++)
        {
            FishSpawnEntry entry = bossSpawnPlan[i];
            if (entry != null && entry.prefab != null && entry.count > 0)
                return true;
        }

        return false;
    }

    private void TrySpawnBossWaveIfNeeded()
    {
        if (_bossWaveSpawned || !HasValidBossSpawnPlan() || HasAnyLiveFish())
            return;

        bool spawnedAny = SpawnFishFromPlan(bossSpawnPlan);
        if (!spawnedAny)
            return;

        _bossWaveSpawned = true;
        ApplyFishFishCollisionSetting();
        Debug.Log("PondManager spawned boss fish wave.");
    }

    public bool HasPendingBossWave => !_bossWaveSpawned && HasValidBossSpawnPlan();
    public bool HasRemainingOrPendingFish => HasAnyLiveFish() || HasPendingBossWave;

    private Vector3 GetRandomSpawnPosition()
    {
        Vector2 randomCircle = Random.insideUnitCircle * radius;
        return new Vector3(
            pondCenter.x + randomCircle.x,
            waterlevel,
            pondCenter.z + randomCircle.y);
    }

    private GameObject GetRandomSpawnPrefab()
    {
        if (TryGetWeightedSpawnPrefabFromPlan(out GameObject plannedPrefab))
            return plannedPrefab;

        if (fishPrefabs == null || fishPrefabs.Length == 0)
            return null;

        int randomIndex = Random.Range(0, fishPrefabs.Length);
        return fishPrefabs[randomIndex];
    }

    private bool TryGetWeightedSpawnPrefabFromPlan(out GameObject prefab)
    {
        prefab = null;
        if (initialSpawnPlan == null || initialSpawnPlan.Count == 0)
            return false;

        int totalWeight = 0;
        for (int i = 0; i < initialSpawnPlan.Count; i++)
        {
            FishSpawnEntry entry = initialSpawnPlan[i];
            if (entry == null || entry.prefab == null)
                continue;
            totalWeight += Mathf.Max(0, entry.count);
        }

        if (totalWeight <= 0)
            return false;

        int pick = Random.Range(0, totalWeight);
        for (int i = 0; i < initialSpawnPlan.Count; i++)
        {
            FishSpawnEntry entry = initialSpawnPlan[i];
            if (entry == null || entry.prefab == null)
                continue;

            int weight = Mathf.Max(0, entry.count);
            if (pick < weight)
            {
                prefab = entry.prefab;
                return true;
            }
            pick -= weight;
        }

        return false;
    }

    void SpawnFish(GameObject fishPrefab, Vector3 position)
    {
        if (fishPrefab == null)
        {
            Debug.LogWarning("PondManager could not spawn fish: no prefab available.");
            return;
        }

        GameObject fish = Instantiate(fishPrefab, position, Quaternion.identity);
        SceneManager.MoveGameObjectToScene(fish, gameObject.scene);

        // Keep spawn data sane: hooked fish should carry rhythm data.
        if (fish.GetComponent<RhythmProfile>() == null && fish.GetComponentInChildren<RhythmProfile>(true) == null)
        {
            Debug.LogWarning(
                $"Spawned fish prefab '{fishPrefab.name}' without RhythmProfile. " +
                "Fish-specific beatmap/music switching will not occur.");
        }

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
        TrySpawnBossWaveIfNeeded();
    }

    public bool RemoveFish(GameObject fish)
    {
        if (fish == null)
            return false;

        hiddenFishDuringTension.Remove(fish);
        fishList.Remove(fish);
        Destroy(fish);
        TrySpawnBossWaveIfNeeded();
        return true;
    }

    public bool UnregisterFish(GameObject fish)
    {
        if (fish == null)
            return false;

        hiddenFishDuringTension.Remove(fish);
        bool removed = fishList.Remove(fish);
        if (removed)
            TrySpawnBossWaveIfNeeded();
        return removed;
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
            TrySpawnBossWaveIfNeeded();
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
