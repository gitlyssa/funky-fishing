using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public sealed class EventSystemSingletonGuard : MonoBehaviour
{
    private const string GuardObjectName = "__EventSystemSingletonGuard";
    private const float PeriodicCheckIntervalSeconds = 0.5f;

    private static bool isBootstrapped;
    private static bool hasLoggedDuplicateWarning;
    private float nextCheckAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        isBootstrapped = false;
        hasLoggedDuplicateWarning = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (isBootstrapped)
            return;

        isBootstrapped = true;
        GameObject guardObject = new GameObject(GuardObjectName);
        DontDestroyOnLoad(guardObject);
        guardObject.AddComponent<EventSystemSingletonGuard>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        EnforceSingleEventSystem("Guard enabled");
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void Start()
    {
        EnforceSingleEventSystem("Guard start");
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheckAt)
            return;

        nextCheckAt = Time.unscaledTime + PeriodicCheckIntervalSeconds;
        EnforceSingleEventSystem("Periodic check");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnforceSingleEventSystem($"Scene loaded: {scene.name} ({mode})");
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
    {
        EnforceSingleEventSystem($"Active scene changed: {previousScene.name} -> {newScene.name}");
    }

    private static void EnforceSingleEventSystem(string reason)
    {
        EventSystem[] allEventSystems = FindObjectsOfType<EventSystem>(true);
        if (allEventSystems == null || allEventSystems.Length == 0)
        {
            CreateFallbackEventSystem(reason);
            return;
        }

        EventSystem keeper = ChooseKeeper(allEventSystems);
        if (keeper == null)
            return;

        int activeCount = 0;
        for (int i = 0; i < allEventSystems.Length; i++)
        {
            EventSystem evt = allEventSystems[i];
            if (evt != null && evt.enabled && evt.gameObject.activeInHierarchy)
                activeCount++;
        }

        StringBuilder disabledSummary = null;
        int disabledCount = 0;
        for (int i = 0; i < allEventSystems.Length; i++)
        {
            EventSystem evt = allEventSystems[i];
            if (evt == null || evt == keeper)
                continue;

            bool changed = false;
            if (evt.enabled)
            {
                evt.enabled = false;
                changed = true;
            }
            if (evt.gameObject.activeSelf)
            {
                evt.gameObject.SetActive(false);
                changed = true;
            }

            if (!changed)
                continue;

            if (disabledSummary == null)
                disabledSummary = new StringBuilder();

            disabledSummary.AppendLine(DescribeEventSystem(evt));
            disabledCount++;
        }

        if (!keeper.gameObject.activeSelf)
            keeper.gameObject.SetActive(true);
        if (!keeper.enabled)
            keeper.enabled = true;

        if (!hasLoggedDuplicateWarning && (disabledCount > 0 || activeCount > 1))
        {
            string disabledDetails = disabledSummary != null ? disabledSummary.ToString().TrimEnd() : "(none)";
            Debug.LogWarning(
                $"[EventSystemSingletonGuard] {reason}. Found {allEventSystems.Length} EventSystems " +
                $"(active: {activeCount}). Keeping: {DescribeEventSystem(keeper)}. Disabled {disabledCount} duplicate(s):\n{disabledDetails}");
            hasLoggedDuplicateWarning = true;
        }
    }

    private static EventSystem ChooseKeeper(EventSystem[] allEventSystems)
    {
        if (EventSystem.current != null &&
            EventSystem.current.enabled &&
            EventSystem.current.gameObject.activeInHierarchy)
        {
            return EventSystem.current;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        EventSystem best = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < allEventSystems.Length; i++)
        {
            EventSystem evt = allEventSystems[i];
            if (evt == null)
                continue;

            int score = 0;
            if (evt.gameObject.scene == activeScene)
                score += 100;
            if (evt.enabled && evt.gameObject.activeInHierarchy)
                score += 10;
            if (evt.GetComponent<InputSystemUIInputModule>() != null)
                score += 1;

            if (score > bestScore)
            {
                best = evt;
                bestScore = score;
            }
        }

        return best;
    }

    private static void CreateFallbackEventSystem(string reason)
    {
        GameObject eventSystemObject = new GameObject("EventSystem");
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.isLoaded)
            SceneManager.MoveGameObjectToScene(eventSystemObject, activeScene);

        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();

        Debug.LogWarning(
            $"[EventSystemSingletonGuard] {reason}. No EventSystem was found, so a fallback EventSystem was created in scene '{activeScene.name}'.");
    }

    private static string DescribeEventSystem(EventSystem evt)
    {
        if (evt == null)
            return "(null)";

        string sceneName = evt.gameObject.scene.IsValid() ? evt.gameObject.scene.name : "(no scene)";
        return $"{sceneName}/{BuildHierarchyPath(evt.transform)} (activeSelf: {evt.gameObject.activeSelf}, activeInHierarchy: {evt.gameObject.activeInHierarchy}, enabled: {evt.enabled})";
    }

    private static string BuildHierarchyPath(Transform node)
    {
        if (node == null)
            return "(missing)";

        StringBuilder builder = new StringBuilder(node.name);
        Transform current = node.parent;
        while (current != null)
        {
            builder.Insert(0, '/');
            builder.Insert(0, current.name);
            current = current.parent;
        }

        return builder.ToString();
    }
}
