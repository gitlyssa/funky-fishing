using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class FishingSessionHud : MonoBehaviour
{
    [Header("Toggle")]
    [SerializeField] private bool hudEnabled = true;

    [Header("Scoring")]
    [SerializeField] private int perfectPoints = 100;
    [SerializeField] private int goodPoints = 70;
    [SerializeField] private int missPoints = 0;

    [Header("Display")]
    [SerializeField] private bool hideDuringRhythmMode = true;
    [SerializeField] private bool hideWhenPausedInPondLevel1 = true;
    [SerializeField] private string pondLevelSceneName = "Pond_Level_1";
    [SerializeField] private Vector2 panelSize = new Vector2(308f, 290f);
    [SerializeField] private Vector2 panelOffset = new Vector2(-24f, -24f);
    [SerializeField] private int fontSize = 22;
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color textColor = Color.white;

    private Canvas canvas;
    private TextMeshProUGUI hudText;
    private BobberArcCaster cachedBobberArcCaster;

    private bool runActive;
    private bool wasRhythmVisible;
    private int runScore;
    private int runPerfect;
    private int runGood;
    private int runMiss;
    private int runCombo;
    private int runBestCombo;

    private static int sessionFishCaught;
    private static int sessionHighScore;
    private static int sessionTotalScore;
    private static int sessionTotalPerfect;
    private static int sessionTotalGood;
    private static int sessionTotalMiss;
    private static int sessionBestCombo;
    private static int sessionRunsCompleted;

    private static int lastCatchScore;
    private static int lastCatchPerfect;
    private static int lastCatchGood;
    private static int lastCatchMiss;
    private static int lastCatchBestCombo;
    private static float lastCatchAccuracy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSessionStats()
    {
        sessionFishCaught = 0;
        sessionHighScore = 0;
        sessionTotalScore = 0;
        sessionTotalPerfect = 0;
        sessionTotalGood = 0;
        sessionTotalMiss = 0;
        sessionBestCombo = 0;
        sessionRunsCompleted = 0;

        lastCatchScore = 0;
        lastCatchPerfect = 0;
        lastCatchGood = 0;
        lastCatchMiss = 0;
        lastCatchBestCombo = 0;
        lastCatchAccuracy = 0f;
    }

    private void Awake()
    {
        EnsureUi();
        ApplyHudVisibility(false);
        RefreshHudText();
    }

    private void OnEnable()
    {
        RhythmJudge.OnNoteJudged += HandleNoteJudged;
    }

    private void OnDisable()
    {
        RhythmJudge.OnNoteJudged -= HandleNoteJudged;
    }

    private void OnValidate()
    {
        ApplyHudVisibility(IsRhythmVisible());
    }

    private void Update()
    {
        bool rhythmVisible = IsRhythmVisible();

        if (!hudEnabled)
        {
            ApplyHudVisibility(rhythmVisible);
            wasRhythmVisible = rhythmVisible;
            return;
        }

        ApplyHudVisibility(rhythmVisible);

        if (rhythmVisible && !wasRhythmVisible)
            BeginCatchRun();

        if (!rhythmVisible && wasRhythmVisible)
            CompleteCatchRun();

        wasRhythmVisible = rhythmVisible;
    }

    private void HandleNoteJudged(RhythmJudge.JudgeRating rating)
    {
        if (!hudEnabled)
            return;

        if (!runActive)
            BeginCatchRun();

        switch (rating)
        {
            case RhythmJudge.JudgeRating.Perfect:
                runPerfect++;
                runCombo++;
                runScore += perfectPoints;
                break;

            case RhythmJudge.JudgeRating.Good:
                runGood++;
                runCombo++;
                runScore += goodPoints;
                break;

            default:
                runMiss++;
                runCombo = 0;
                runScore += missPoints;
                break;
        }

        if (runCombo > runBestCombo)
            runBestCombo = runCombo;
    }

    private void BeginCatchRun()
    {
        runActive = true;
        runScore = 0;
        runPerfect = 0;
        runGood = 0;
        runMiss = 0;
        runCombo = 0;
        runBestCombo = 0;
    }

    private void CompleteCatchRun()
    {
        if (!runActive)
            return;

        int judgedNotes = runPerfect + runGood + runMiss;
        float accuracy = judgedNotes > 0
            ? ((runPerfect + (runGood * 0.7f)) / judgedNotes) * 100f
            : 0f;

        lastCatchScore = runScore;
        lastCatchPerfect = runPerfect;
        lastCatchGood = runGood;
        lastCatchMiss = runMiss;
        lastCatchBestCombo = runBestCombo;
        lastCatchAccuracy = accuracy;

        sessionFishCaught++;
        sessionRunsCompleted++;
        sessionTotalScore += runScore;
        sessionTotalPerfect += runPerfect;
        sessionTotalGood += runGood;
        sessionTotalMiss += runMiss;

        if (runScore > sessionHighScore)
            sessionHighScore = runScore;

        if (runBestCombo > sessionBestCombo)
            sessionBestCombo = runBestCombo;

        runActive = false;
        RefreshHudText();
    }

    private bool IsRhythmVisible()
    {
        if (SceneLoading.Instance != null)
            return SceneLoading.Instance.IsRhythmVisible;

        if (cachedBobberArcCaster == null)
            cachedBobberArcCaster = FindObjectOfType<BobberArcCaster>();

        if (cachedBobberArcCaster == null)
            return false;

        return cachedBobberArcCaster.CurrentState == BobberArcCaster.State.Tension;
    }

    private void RefreshHudText()
    {
        if (hudText == null)
            return;

        int avgScore = sessionFishCaught > 0
            ? Mathf.RoundToInt((float)sessionTotalScore / sessionFishCaught)
            : 0;

        hudText.text =
            "Fishing Session\n" +
            $"Last Catch: {lastCatchScore} pts ({GetLetterGrade(lastCatchAccuracy)})\n" +
            $"High Score: {sessionHighScore} pts\n" +
            $"Fish Caught: {sessionFishCaught}\n" +
            $"Session Score: {sessionTotalScore}\n" +
            $"Avg / Catch: {avgScore}\n" +
            $"Best Combo: {sessionBestCombo}\n" +
            $"Last Combo: {lastCatchBestCombo}\n" +
            $"Last P/G/M: {lastCatchPerfect}/{lastCatchGood}/{lastCatchMiss}\n" +
            $"Last Accuracy: {lastCatchAccuracy:F1}%";
    }

    public void SetHudEnabled(bool enabled)
    {
        hudEnabled = enabled;
        ApplyHudVisibility(IsRhythmVisible());
    }

    private string GetLetterGrade(float accuracy)
    {
        if (accuracy >= 95f) return "S";
        if (accuracy >= 85f) return "A";
        if (accuracy >= 75f) return "B";
        if (accuracy >= 65f) return "C";
        return "D";
    }

    private void EnsureUi()
    {
        if (canvas != null && hudText != null)
            return;

        GameObject canvasGo = new GameObject(
            "FishingSessionCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);

        RectTransform panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = panelOffset;
        panelRect.sizeDelta = panelSize;

        Image panelImage = panelGo.GetComponent<Image>();
        panelImage.color = panelColor;

        GameObject textGo = new GameObject("SessionText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(panelGo.transform, false);

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 10f);
        textRect.offsetMax = new Vector2(-10f, -10f);

        hudText = textGo.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            hudText.font = TMP_Settings.defaultFontAsset;

        hudText.fontSize = fontSize;
        hudText.color = textColor;
        hudText.alignment = TextAlignmentOptions.TopRight;
        hudText.textWrappingMode = TextWrappingModes.Normal;
        hudText.raycastTarget = false;
    }

    private void ApplyHudVisibility(bool rhythmVisible)
    {
        if (canvas == null)
            return;

        bool hideForPause = hideWhenPausedInPondLevel1 && IsPausedInPondLevel();
        bool shouldShow = hudEnabled && (!hideDuringRhythmMode || !rhythmVisible) && !hideForPause;
        canvas.enabled = shouldShow;
    }

    private bool IsPausedInPondLevel()
    {
        if (Time.timeScale > 0f)
            return false;

        Scene activeScene = SceneManager.GetActiveScene();
        return activeScene.name == pondLevelSceneName;
    }
}
