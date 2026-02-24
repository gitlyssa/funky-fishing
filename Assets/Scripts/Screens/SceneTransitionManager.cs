using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Scenes")]
    [SerializeField] private string loadingSceneName = "LoadingScreen";
    [SerializeField] private string warmupSceneName = "Pond_Level_1";

    [Header("Timing")]
    [SerializeField] private float minLoadingScreenDuration = 0.6f;
    [SerializeField] private float warmupTimeoutSeconds = 15f;

    private bool isTransitioning;
    private string pendingActiveSceneName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void LoadSceneWithLoading(string targetSceneName)
    {
        EnsureInstance().StartTransition(targetSceneName);
    }

    private static SceneTransitionManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject go = new GameObject("SceneTransitionManager");
        Instance = go.AddComponent<SceneTransitionManager>();
        DontDestroyOnLoad(go);
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void StartTransition(string targetSceneName)
    {
        if (isTransitioning)
            return;

        StartCoroutine(TransitionRoutine(targetSceneName));
    }

    private IEnumerator TransitionRoutine(string targetSceneName)
    {
        isTransitioning = true;
        Time.timeScale = 1f;
        float transitionStartTime = Time.unscaledTime;

        AsyncOperation loadLoadingSceneOp = SceneManager.LoadSceneAsync(loadingSceneName, LoadSceneMode.Single);
        if (loadLoadingSceneOp == null)
        {
            Debug.LogError($"Could not load loading scene '{loadingSceneName}'.");
            isTransitioning = false;
            yield break;
        }

        while (!loadLoadingSceneOp.isDone)
            yield return null;

        LoadingScreenController loadingUi = LoadingScreenController.GetOrCreate();
        loadingUi.SetStatus("Loading...");
        loadingUi.SetProgress(0f);

        pendingActiveSceneName = targetSceneName;
        AsyncOperation loadTargetSceneOp = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);
        if (loadTargetSceneOp == null)
        {
            Debug.LogError($"Could not load target scene '{targetSceneName}'.");
            pendingActiveSceneName = null;
            isTransitioning = false;
            yield break;
        }

        loadTargetSceneOp.allowSceneActivation = false;

        while (loadTargetSceneOp.progress < 0.9f)
        {
            float normalized = Mathf.Clamp01(loadTargetSceneOp.progress / 0.9f);
            loadingUi.SetProgress(normalized * 0.85f);
            yield return null;
        }

        loadingUi.SetStatus("Preparing world...");
        loadingUi.SetProgress(0.88f);

        loadTargetSceneOp.allowSceneActivation = true;
        while (!loadTargetSceneOp.isDone)
            yield return null;

        Scene targetScene = SceneManager.GetSceneByName(targetSceneName);
        if (targetScene.IsValid() && targetScene.isLoaded)
            SceneManager.SetActiveScene(targetScene);
        pendingActiveSceneName = null;

        yield return null;

        float warmupStartTime = Time.unscaledTime;
        while (!IsTargetReady(targetSceneName))
        {
            if (Time.unscaledTime - warmupStartTime > warmupTimeoutSeconds)
            {
                Debug.LogWarning(
                    $"Timed out waiting for warmup signal in scene '{targetSceneName}'. " +
                    "Continuing transition.");
                break;
            }

            float warmupProgress = Mathf.InverseLerp(0f, warmupTimeoutSeconds, Time.unscaledTime - warmupStartTime);
            loadingUi.SetProgress(Mathf.Lerp(0.88f, 0.98f, warmupProgress));
            yield return null;
        }

        while (Time.unscaledTime - transitionStartTime < minLoadingScreenDuration)
            yield return null;

        loadingUi.SetStatus("Ready");
        loadingUi.SetProgress(1f);
        yield return null;

        AsyncOperation unloadLoadingSceneOp = SceneManager.UnloadSceneAsync(loadingSceneName);
        if (unloadLoadingSceneOp != null)
        {
            while (!unloadLoadingSceneOp.isDone)
                yield return null;
        }

        targetScene = SceneManager.GetSceneByName(targetSceneName);
        if (targetScene.IsValid() && targetScene.isLoaded)
            SceneManager.SetActiveScene(targetScene);

        isTransitioning = false;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.IsNullOrEmpty(pendingActiveSceneName))
            return;

        if (!scene.name.Equals(pendingActiveSceneName))
            return;

        SceneManager.SetActiveScene(scene);
    }

    private bool IsTargetReady(string targetSceneName)
    {
        Scene targetScene = SceneManager.GetSceneByName(targetSceneName);
        if (!targetScene.IsValid() || !targetScene.isLoaded)
            return false;

        if (!targetSceneName.Equals(warmupSceneName))
            return true;

        return SceneLoading.Instance != null && SceneLoading.Instance.HasCompletedInitialPreload;
    }
}
