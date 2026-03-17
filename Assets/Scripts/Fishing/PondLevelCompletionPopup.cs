using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PondLevelCompletionPopup : MonoBehaviour
{
    private static bool sceneHookRegistered;

    [Header("Scene Scope")]
    [SerializeField] private string pondSceneName = "Pond_Level_1";

    [Header("Copy")]
    [SerializeField] private string titleMessage = "Pond Cleared!";
    [SerializeField] private string subtitleMessage =
        "You caught every fish in the pond.\nHere are your results:";
    [SerializeField] private string restartButtonLabel = "Restart Level";

    [Header("Style")]
    [SerializeField] private Vector2 panelSize = new Vector2(880f, 560f);
    [SerializeField] private Color backdropColor = new Color(0f, 0f, 0f, 0.68f);
    [SerializeField] private Color panelColor = new Color(0.08f, 0.08f, 0.08f, 0.95f);
    [SerializeField] private Color buttonColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private int titleFontSize = 52;
    [SerializeField] private int bodyFontSize = 32;
    [SerializeField] private int buttonFontSize = 30;

    private PondManager pondManager;
    private BobberArcCaster bobberArcCaster;

    private Canvas canvas;
    private GameObject canvasRoot;
    private TextMeshProUGUI bodyText;
    private Button restartButton;

    private bool popupShown;
    private bool restarting;
    private bool cursorStateCached;
    private bool cachedCursorVisible;
    private CursorLockMode cachedCursorLockMode;
    private readonly List<PauseManager> disabledPauseManagers = new List<PauseManager>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        if (sceneHookRegistered)
            return;

        sceneHookRegistered = true;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TrySpawnForPondScene();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void TrySpawnAfterInitialSceneLoad()
    {
        TrySpawnForPondScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Pond_Level_1")
            return;

        TrySpawnForPondScene();
    }

    private static void TrySpawnForPondScene()
    {
        Scene pondScene = SceneManager.GetSceneByName("Pond_Level_1");
        if (!pondScene.IsValid() || !pondScene.isLoaded)
            return;

        if (FindObjectOfType<PondLevelCompletionPopup>() != null)
            return;

        GameObject go = new GameObject("PondLevelCompletionPopup");
        SceneManager.MoveGameObjectToScene(go, pondScene);
        go.AddComponent<PondLevelCompletionPopup>();
    }

    private void Awake()
    {
        if (!IsInPondSceneContext())
        {
            Destroy(gameObject);
            return;
        }

        BuildUi();
        SetPopupVisible(false);
    }

    private void Update()
    {
        if (restarting)
            return;

        if (!IsInPondSceneContext())
        {
            Destroy(gameObject);
            return;
        }

        if (!popupShown)
        {
            if (CanShowPopupNow())
                ShowPopup();
            return;
        }

        EnsureSelection();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnDestroy()
    {
        if (popupShown && !restarting)
            Time.timeScale = 1f;

        RestorePauseManagers();
        RestoreCursorState();
    }

    private bool IsInPondSceneContext()
    {
        Scene pondScene = SceneManager.GetSceneByName(pondSceneName);
        return pondScene.IsValid() &&
               pondScene.isLoaded &&
               gameObject.scene == pondScene;
    }

    private bool CanShowPopupNow()
    {
        if (IsRhythmVisible())
            return false;

        ResolveReferences();
        if (pondManager == null || pondManager.fishList == null)
            return false;

        if (pondManager.HasRemainingOrPendingFish)
            return false;

        if (bobberArcCaster != null &&
            bobberArcCaster.CurrentState == BobberArcCaster.State.Tension)
        {
            return false;
        }

        if (bobberArcCaster != null && bobberArcCaster.IsSuccessSequenceActive)
            return false;

        return true;
    }

    private bool IsRhythmVisible()
    {
        return SceneLoading.Instance != null && SceneLoading.Instance.IsRhythmVisible;
    }

    private void ResolveReferences()
    {
        Scene pondScene = gameObject.scene;

        if (pondManager == null || pondManager.gameObject.scene != pondScene)
            pondManager = FindSceneObject<PondManager>(pondScene);

        if (bobberArcCaster == null || bobberArcCaster.gameObject.scene != pondScene)
            bobberArcCaster = FindSceneObject<BobberArcCaster>(pondScene);
    }

    private static T FindSceneObject<T>(Scene scene) where T : Component
    {
        T[] objects = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < objects.Length; i++)
        {
            T candidate = objects[i];
            if (candidate != null && candidate.gameObject.scene == scene)
                return candidate;
        }

        return null;
    }

    private void ShowPopup()
    {
        popupShown = true;
        CacheCursorState();
        DisablePauseManagers();
        Time.timeScale = 0f;
        RefreshSummaryText();
        SetPopupVisible(true);
        SetInitialSelection();
    }

    private void RestartLevel()
    {
        if (restarting)
            return;

        restarting = true;
        FishingSessionHud.ResetSessionForLevelRestart();
        Time.timeScale = 1f;
        RestorePauseManagers();
        RestoreCursorState();
        XboxFishingInput.BlockGameplayInputForRealtimeSeconds(0.35f);
        SceneTransitionManager.LoadSceneWithLoading(pondSceneName);
    }

    private void RefreshSummaryText()
    {
        if (bodyText == null)
            return;

        FishingSessionHud.SessionSummary s = FishingSessionHud.GetSessionSummary();
        string lastGrade = FishingSessionHud.GetLetterGradeForAccuracy(s.LastCatchAccuracy);
        string lastResult = s.LastCatchSucceeded ? "Caught" : "Escaped";

        bodyText.text =
            $"{subtitleMessage}\n\n" +
            $"Fish Caught: {s.FishCaught}\n" +
            $"Catch Attempts: {s.CatchAttempts}\n" +
            $"Session Score: {s.SessionScore}\n" +
            $"High Score: {s.HighScore}\n" +
            $"Best Combo: {s.SessionBestCombo}\n" +
            $"Avg / Attempt: {s.AverageScorePerAttempt}\n\n" +
            $"Last Attempt: {s.LastCatchScore} pts ({lastGrade}) [{lastResult}]\n" +
            $"Last Combo: {s.LastCatchBestCombo}\n" +
            $"Last P/G/M: {s.LastCatchPerfect}/{s.LastCatchGood}/{s.LastCatchMiss}\n" +
            $"Last Accuracy: {s.LastCatchAccuracy:F1}%";
    }

    private void BuildUi()
    {
        GameObject root = new GameObject(
            "PondCompletionCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        canvasRoot = root;

        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1400;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backdrop.transform.SetParent(root.transform, false);
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        backdrop.GetComponent<Image>().color = backdropColor;

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(backdrop.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = panelColor;

        TextMeshProUGUI titleText = CreateText(
            "TitleText",
            panel.transform,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -28f),
            new Vector2(panelSize.x - 80f, 80f),
            titleFontSize,
            TextAlignmentOptions.Center,
            titleMessage);
        titleText.color = textColor;

        bodyText = CreateText(
            "BodyText",
            panel.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -10f),
            new Vector2(panelSize.x - 100f, panelSize.y - 220f),
            bodyFontSize,
            TextAlignmentOptions.TopLeft,
            string.Empty);
        bodyText.color = textColor;
        bodyText.enableAutoSizing = true;
        bodyText.fontSizeMin = 20;
        bodyText.fontSizeMax = bodyFontSize;

        GameObject buttonObject = new GameObject(
            "RestartButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(panel.transform, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 24f);
        buttonRect.sizeDelta = new Vector2(360f, 68f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = buttonColor;

        restartButton = buttonObject.GetComponent<Button>();
        restartButton.onClick.AddListener(RestartLevel);

        TextMeshProUGUI buttonText = CreateText(
            "ButtonText",
            buttonObject.transform,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            buttonFontSize,
            TextAlignmentOptions.Center,
            restartButtonLabel);
        buttonText.color = new Color(0.18f, 0.18f, 0.18f, 1f);
    }

    private static TextMeshProUGUI CreateText(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        int fontSize,
        TextAlignmentOptions alignment,
        string textValue)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
        text.text = textValue;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private void SetPopupVisible(bool visible)
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(visible);
    }

    private void SetInitialSelection()
    {
        EventSystem evt = EventSystem.current;
        if (evt == null || restartButton == null)
            return;

        evt.SetSelectedGameObject(null);
        evt.SetSelectedGameObject(restartButton.gameObject);
    }

    private void EnsureSelection()
    {
        EventSystem evt = EventSystem.current;
        if (evt == null || restartButton == null)
            return;

        if (evt.currentSelectedGameObject != null)
            return;

        evt.SetSelectedGameObject(restartButton.gameObject);
    }

    private void DisablePauseManagers()
    {
        disabledPauseManagers.Clear();
        PauseManager[] pauseManagers = FindObjectsOfType<PauseManager>(true);
        for (int i = 0; i < pauseManagers.Length; i++)
        {
            PauseManager manager = pauseManagers[i];
            if (manager == null || !manager.enabled)
                continue;

            manager.enabled = false;
            disabledPauseManagers.Add(manager);
        }
    }

    private void RestorePauseManagers()
    {
        for (int i = 0; i < disabledPauseManagers.Count; i++)
        {
            PauseManager manager = disabledPauseManagers[i];
            if (manager != null)
                manager.enabled = true;
        }
        disabledPauseManagers.Clear();
    }

    private void CacheCursorState()
    {
        if (cursorStateCached)
            return;

        cachedCursorVisible = Cursor.visible;
        cachedCursorLockMode = Cursor.lockState;
        cursorStateCached = true;
    }

    private void RestoreCursorState()
    {
        if (!cursorStateCached)
            return;

        Cursor.visible = cachedCursorVisible;
        Cursor.lockState = cachedCursorLockMode;
        cursorStateCached = false;
    }
}
