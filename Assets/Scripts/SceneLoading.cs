using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
public class SceneLoading : MonoBehaviour
{
    public static SceneLoading Instance { get; private set; }
    
    public string rhythmSceneName = "AlphaRhythm";
    public string fishingSceneName = "rodBobberMech";
    [Header("Overlay Lifecycle")]
    public bool preloadRhythmOverlayOnStart = true;
    public bool keepRhythmOverlayLoaded = true;
    [Header("Tension Overlay Trigger")]
    public bool driveOverlayFromBobberTension = false;
    public BobberArcCaster tensionSource;

    public static GameObject MigratedFish;
    
    private bool isRhythmLoaded = false;
    private bool isRhythmVisible = false;
    private bool tensionStateInitialized = false;
    private bool wasInTensionLastFrame = false;
    private bool isRhythmTransitioning = false;
    private readonly Dictionary<int, bool> rhythmRootDefaultActive = new Dictionary<int, bool>();
    private readonly List<GameObject> rhythmRoots = new List<GameObject>();

    private void Start()
    {
        if (preloadRhythmOverlayOnStart && keepRhythmOverlayLoaded && !isRhythmLoaded)
        {
            StartCoroutine(PreloadRhythmOverlay());
        }
    }

    void Update()
    {
        // press 1 to reload the scene, might break everything not sure
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(fishingSceneName);
        }

        // 3 to load overlay, 4 to unload
        if (Keyboard.current.digit3Key.wasPressedThisFrame && !isRhythmVisible)
        {
            StartRhythmEncounter();
        }
        
        if (Keyboard.current.digit4Key.wasPressedThisFrame && isRhythmVisible)
        {
            EndRhythmEncounter();
        }

        UpdateTensionDrivenOverlay();
    }

     void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    public void StartRhythmEncounter(GameObject fishToMigrate = null)
    {
        if (isRhythmTransitioning)
            return;

        if (isRhythmLoaded && keepRhythmOverlayLoaded)
        {
            if (fishToMigrate != null)
            {
                MigratedFish = fishToMigrate;
                Scene rhythmScene = SceneManager.GetSceneByName(rhythmSceneName);
                if (rhythmScene.IsValid() && rhythmScene.isLoaded)
                {
                    PrepareFishForRhythmScene(fishToMigrate, rhythmScene);
                }
            }

            SetRhythmOverlayVisible(true);
            return;
        }

        if (fishToMigrate != null)
        {
            MigratedFish = fishToMigrate;
            StartCoroutine(LoadRhythmFishAdditive(fishToMigrate));
        }
        else
        {
            StartCoroutine(LoadRhythmAdditive());
        }
        
    }

    public void EndRhythmEncounter()
    {
        if (isRhythmTransitioning)
            return;

        if (keepRhythmOverlayLoaded && isRhythmLoaded)
        {
            SetRhythmOverlayVisible(false);
            if (MigratedFish != null)
            {
                Destroy(MigratedFish);
                MigratedFish = null;
            }
            return;
        }

        if (isRhythmLoaded)
        {
            StartCoroutine(UnloadRhythm());
        }
    }

    private IEnumerator PreloadRhythmOverlay()
    {
        isRhythmTransitioning = true;
        AsyncOperation op = SceneManager.LoadSceneAsync(rhythmSceneName, LoadSceneMode.Additive);
        if (op != null) yield return op;

        isRhythmLoaded = true;
        Scene rhythmScene = SceneManager.GetSceneByName(rhythmSceneName);
        CacheRhythmRoots(rhythmScene);
        SetRhythmOverlayVisible(false);
        isRhythmTransitioning = false;
    }

    private IEnumerator LoadRhythmAdditive()
    {
        isRhythmTransitioning = true;
        isRhythmLoaded = true;
        AsyncOperation op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(rhythmSceneName, LoadSceneMode.Additive);
        if (op != null) yield return op;

        Scene rhythmScene = SceneManager.GetSceneByName(rhythmSceneName);
        CacheRhythmRoots(rhythmScene);
        SetRhythmOverlayVisible(true);
        isRhythmTransitioning = false;
        
        Debug.Log("Rhythm Overlay ON.");
    }

    private IEnumerator LoadRhythmFishAdditive(GameObject fish)
    {
        isRhythmTransitioning = true;
        isRhythmLoaded = true;
        AsyncOperation op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(rhythmSceneName, LoadSceneMode.Additive);
        if (op != null) yield return op;

        Scene rhythmScene = SceneManager.GetSceneByName(rhythmSceneName);
        CacheRhythmRoots(rhythmScene);
        PrepareFishForRhythmScene(fish, rhythmScene);
        SetRhythmOverlayVisible(true);
        isRhythmTransitioning = false;
        
        Debug.Log($"Migrated {fish.name} to Rhythm Scene.");
    }

    private IEnumerator UnloadRhythm()
    {
        isRhythmTransitioning = true;
        if (MigratedFish != null) 
        {
            Destroy(MigratedFish);
            MigratedFish = null;
        }
        
        AsyncOperation op = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(rhythmSceneName);
        if (op != null) yield return op;

        isRhythmLoaded = false;
        isRhythmVisible = false;
        rhythmRoots.Clear();
        rhythmRootDefaultActive.Clear();
        isRhythmTransitioning = false;
        Debug.Log("Rhythm Unloaded");
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    private void PrepareFishForRhythmScene(GameObject fish, Scene rhythmScene)
    {
        fish.transform.SetParent(null);
        SceneManager.MoveGameObjectToScene(fish, rhythmScene);

        if (fish.TryGetComponent<MonoBehaviour>(out var fishAI))
        {
            fishAI.enabled = false;
        }

        foreach (Collider col in fish.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }
        foreach (Rigidbody rb in fish.GetComponentsInChildren<Rigidbody>())
        {
            rb.isKinematic = true;
        }

        int rhythmOverlayLayer = LayerMask.NameToLayer("RhythmOverlay");
        if (rhythmOverlayLayer < 0)
        {
            rhythmOverlayLayer = 0;
            Debug.LogWarning("RhythmOverlay layer not found. Falling back fish layer to Default.");
        }
        SetLayerRecursively(fish, rhythmOverlayLayer);
    }

    private void CacheRhythmRoots(Scene rhythmScene)
    {
        rhythmRoots.Clear();
        rhythmRootDefaultActive.Clear();

        if (!rhythmScene.IsValid() || !rhythmScene.isLoaded)
            return;

        GameObject[] roots = rhythmScene.GetRootGameObjects();
        foreach (GameObject root in roots)
        {
            if (root == null) continue;
            rhythmRoots.Add(root);
            rhythmRootDefaultActive[root.GetInstanceID()] = root.activeSelf;
        }
    }

    private void SetRhythmOverlayVisible(bool visible)
    {
        Scene rhythmScene = SceneManager.GetSceneByName(rhythmSceneName);
        if ((!rhythmScene.IsValid() || !rhythmScene.isLoaded) || rhythmRoots.Count == 0)
        {
            CacheRhythmRoots(rhythmScene);
        }

        foreach (GameObject root in rhythmRoots)
        {
            if (root == null) continue;
            if (visible)
            {
                bool defaultActive = true;
                rhythmRootDefaultActive.TryGetValue(root.GetInstanceID(), out defaultActive);
                root.SetActive(defaultActive);
            }
            else
            {
                root.SetActive(false);
            }
        }

        isRhythmVisible = visible;
    }

    private void UpdateTensionDrivenOverlay()
    {
        if (!driveOverlayFromBobberTension)
            return;

        if (tensionSource == null)
        {
            tensionSource = FindObjectOfType<BobberArcCaster>();
            if (tensionSource == null)
            {
                tensionStateInitialized = false;
                return;
            }
        }

        bool inTension = tensionSource.CurrentState == BobberArcCaster.State.Tension;
        if (!tensionStateInitialized)
        {
            wasInTensionLastFrame = inTension;
            tensionStateInitialized = true;
            return;
        }

        if (inTension == wasInTensionLastFrame)
            return;

        wasInTensionLastFrame = inTension;
        if (inTension)
            StartRhythmEncounter();
        else
            EndRhythmEncounter();
    }
}
