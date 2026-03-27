using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
public class SceneLoading : MonoBehaviour
{
    public static SceneLoading Instance { get; private set; }
    public bool HasCompletedInitialPreload { get; private set; }
    public bool IsRhythmVisible => isRhythmVisible;
    
    public string rhythmSceneName = "AlphaRhythm";
    public string fishingSceneName = "rodBobberMech";
    [Header("Overlay Lifecycle")]
    public bool preloadRhythmOverlayOnStart = true;
    public bool keepRhythmOverlayLoaded = true;
    [Header("Tension Overlay Trigger")]
    public bool driveOverlayFromBobberTension = false;
    public BobberArcCaster tensionSource;
    [Header("Rhythm UI Targets")]
    public string scoringCircleObjectName = "ScoringCircle";

    public static GameObject MigratedFish;
    
    private bool isRhythmLoaded = false;
    private bool isRhythmVisible = false;
    private bool tensionStateInitialized = false;
    private bool wasInTensionLastFrame = false;
    private bool isRhythmTransitioning = false;
    private readonly Dictionary<int, bool> rhythmRootDefaultActive = new Dictionary<int, bool>();
    private readonly List<GameObject> rhythmRoots = new List<GameObject>();
    private GameObject scoringCircleObject;
    private bool hasLoggedMissingScoringCircle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        MigratedFish = null;
    }

    private void Start()
    {
        HasCompletedInitialPreload = false;

        if (preloadRhythmOverlayOnStart && keepRhythmOverlayLoaded && !isRhythmLoaded)
        {
            StartCoroutine(PreloadRhythmOverlay());
            return;
        }

        HasCompletedInitialPreload = true;
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
            StartRhythmEncounter(ResolveFishForRhythmEncounter());
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
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void StartRhythmEncounter(GameObject fishToMigrate = null)
    {
        RhythmProfile rhythmProfile = null;
        if (fishToMigrate != null)
        {
            rhythmProfile = fishToMigrate.GetComponent<RhythmProfile>();
            if (rhythmProfile == null)
                rhythmProfile = fishToMigrate.GetComponentInChildren<RhythmProfile>(true);
            if (rhythmProfile != null)
            {
                string beatmapName = rhythmProfile.beatmapFile != null ? rhythmProfile.beatmapFile.name : "null";
                string musicEventInfo = rhythmProfile.musicEvent.IsNull ? "null" : rhythmProfile.musicEvent.ToString();
                Debug.Log($"Fish '{fishToMigrate.name}' RhythmProfile values -> beatmapFile: {beatmapName}, musicEvent: {musicEventInfo}");
            }
            else
            {
                Debug.LogWarning($"Fish '{fishToMigrate.name}' does not have a RhythmProfile component.");
            }
        }

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
            ShowScoringCircleForRhythm();
            ApplyRhythmProfileToRhythmSystems(rhythmProfile);
            return;
        }

        if (fishToMigrate != null)
        {
            MigratedFish = fishToMigrate;
            StartCoroutine(LoadRhythmFishAdditive(fishToMigrate, rhythmProfile));
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

        if (VisualConductor.Instance != null)
        {
            VisualConductor.Instance.StopAndReset();
            Debug.Log("Stopped Visual Conductor.");
        }

        if (isRhythmLoaded)
        {
            StartCoroutine(UnloadRhythm());
        }
    }

    public void HideScoringCircleForCatchSequence()
    {
        if (!TryResolveScoringCircle(out GameObject scoringCircle))
            return;

        scoringCircle.SetActive(false);
    }

    private IEnumerator PreloadRhythmOverlay()
    {
        isRhythmTransitioning = true;
        AsyncOperation op = SceneManager.LoadSceneAsync(rhythmSceneName, LoadSceneMode.Additive);
        if (op != null) yield return op;

        isRhythmLoaded = true;
        Scene rhythmScene = SceneManager.GetSceneByName(rhythmSceneName);
        CacheRhythmRoots(rhythmScene);
        SetRhythmRootsToDefaultActive();

        // Let the additive scene run one frame under LoadingScreen so rhythm setup
        // completes before gameplay starts.
        yield return null;

        SetRhythmOverlayVisible(false);
        isRhythmTransitioning = false;
        HasCompletedInitialPreload = true;
    }

    private void SetRhythmRootsToDefaultActive()
    {
        for (int i = 0; i < rhythmRoots.Count; i++)
        {
            GameObject root = rhythmRoots[i];
            if (root == null)
                continue;

            bool defaultActive = true;
            rhythmRootDefaultActive.TryGetValue(root.GetInstanceID(), out defaultActive);
            root.SetActive(defaultActive);
        }
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
        ShowScoringCircleForRhythm();
        isRhythmTransitioning = false;
        
        Debug.Log("Rhythm Overlay ON.");
    }

    private IEnumerator LoadRhythmFishAdditive(GameObject fish, RhythmProfile rhythmProfile)
    {
        isRhythmTransitioning = true;
        isRhythmLoaded = true;
        AsyncOperation op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(rhythmSceneName, LoadSceneMode.Additive);
        if (op != null) yield return op;

        Scene rhythmScene = SceneManager.GetSceneByName(rhythmSceneName);
        CacheRhythmRoots(rhythmScene);
        PrepareFishForRhythmScene(fish, rhythmScene);
        SetRhythmOverlayVisible(true);
        ShowScoringCircleForRhythm();
        ApplyRhythmProfileToRhythmSystems(rhythmProfile);
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

    private void ShowScoringCircleForRhythm()
    {
        if (!TryResolveScoringCircle(out GameObject scoringCircle))
            return;

        scoringCircle.SetActive(true);
    }

    private bool TryResolveScoringCircle(out GameObject scoringCircle)
    {
        scoringCircle = scoringCircleObject;
        if (scoringCircle != null)
            return true;

        Scene rhythmScene = SceneManager.GetSceneByName(rhythmSceneName);
        if (!rhythmScene.IsValid() || !rhythmScene.isLoaded)
            return false;

        GameObject[] roots = rhythmScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
                continue;

            Transform found = FindChildRecursiveByName(root.transform, scoringCircleObjectName);
            if (found == null)
                continue;

            scoringCircleObject = found.gameObject;
            scoringCircle = scoringCircleObject;
            return true;
        }

        if (!hasLoggedMissingScoringCircle)
        {
            hasLoggedMissingScoringCircle = true;
            Debug.LogWarning(
                $"SceneLoading could not find '{scoringCircleObjectName}' in rhythm scene '{rhythmSceneName}'.");
        }

        return false;
    }

    private static Transform FindChildRecursiveByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform found = FindChildRecursiveByName(child, targetName);
            if (found != null)
                return found;
        }

        return null;
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

            if (inTension && !isRhythmVisible)
                StartRhythmEncounter(ResolveFishForRhythmEncounter());
            else if (!inTension && isRhythmVisible)
                EndRhythmEncounter();
            return;
        }

        if (inTension == wasInTensionLastFrame)
        {
            // Self-heal in case the edge transition was missed during initialization.
            if (inTension && !isRhythmVisible && !isRhythmTransitioning)
                StartRhythmEncounter(ResolveFishForRhythmEncounter());
            else if (!inTension && isRhythmVisible && !isRhythmTransitioning)
                EndRhythmEncounter();
            return;
        }

        wasInTensionLastFrame = inTension;
        if (inTension)
            StartRhythmEncounter(ResolveFishForRhythmEncounter());
        else
            EndRhythmEncounter();
    }

    private GameObject ResolveFishForRhythmEncounter()
    {
        if (tensionSource == null)
            tensionSource = FindObjectOfType<BobberArcCaster>();

        if (tensionSource != null && tensionSource.HookedFish != null)
            return tensionSource.HookedFish;

        PondManager pondManager = FindObjectOfType<PondManager>();
        if (pondManager != null && pondManager.playerBobber != null)
        {
            GameObject closestFish = pondManager.GetClosestFish(pondManager.playerBobber);
            if (closestFish != null)
                return closestFish;
        }

        return null;
    }

    private void ApplyRhythmProfileToRhythmSystems(RhythmProfile rhythmProfile)
    {
        if (rhythmProfile == null)
            return;

        RhythmConductor conductor = FindObjectOfType<RhythmConductor>();
        if (conductor != null)
            conductor.SetBeatmapFile(rhythmProfile.beatmapFile);

        if (VisualConductor.Instance != null)
        {
            VisualConductor.Instance.LoadVisualScript(rhythmProfile.visualScriptFile);
        }

        RhythmMusicPlayer musicPlayer = FindObjectOfType<RhythmMusicPlayer>();
        if (musicPlayer != null)
            musicPlayer.SetMusicEvent(rhythmProfile.musicEvent);
    }
}
