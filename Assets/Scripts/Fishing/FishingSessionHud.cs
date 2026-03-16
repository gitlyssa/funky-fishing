using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class FishingSessionHud : MonoBehaviour
{
    public enum CatchGradeRank
    {
        D = 0,
        C = 1,
        B = 2,
        A = 3,
        S = 4
    }

    public struct SessionSummary
    {
        public int FishCaught;
        public int CatchAttempts;
        public int HighScore;
        public int SessionScore;
        public int SessionBestCombo;
        public int SessionRunsCompleted;
        public int LastCatchScore;
        public int LastCatchBestCombo;
        public int LastCatchPerfect;
        public int LastCatchGood;
        public int LastCatchMiss;
        public float LastCatchAccuracy;
        public bool LastCatchSucceeded;
        public int AverageScorePerAttempt;
    }

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
    [SerializeField] private bool hideInTutorialLevel = true;
    [SerializeField] private string tutorialSceneName = "Tutorial_Level";
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
    private static bool lastCatchSucceeded;

    private static bool pendingCatchOutcomeRegistered;
    private static bool pendingCatchSucceeded;
    private static FishingSessionHud activeInstance;
    private static int minimumSuccessfulCatchGradeRank = (int)CatchGradeRank.C;

    public static float LastCatchAccuracy => lastCatchAccuracy;
    public const float GradeSMinAccuracy = 95f;
    public const float GradeAMinAccuracy = 85f;
    public const float GradeBMinAccuracy = 75f;
    public const float GradeCMinAccuracy = 65f;
    public static int MinimumSuccessfulCatchGradeRank
    {
        get => minimumSuccessfulCatchGradeRank;
        set => minimumSuccessfulCatchGradeRank = Mathf.Clamp(value, (int)CatchGradeRank.D, (int)CatchGradeRank.S);
    }

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
        lastCatchSucceeded = false;
        pendingCatchOutcomeRegistered = false;
        pendingCatchSucceeded = false;
        activeInstance = null;
        minimumSuccessfulCatchGradeRank = (int)CatchGradeRank.C;
    }

    public static void ResetSessionForLevelRestart()
    {
        ResetSessionStats();
    }

    private void Awake()
    {
        activeInstance = this;
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

    private void OnDestroy()
    {
        if (activeInstance == this)
            activeInstance = null;
    }

    private void OnValidate()
    {
        ApplyHudVisibility(IsRhythmVisible());
    }

    private void Update()
    {
        bool rhythmVisible = IsRhythmVisible();
        ApplyHudVisibility(rhythmVisible);

        if (rhythmVisible && !wasRhythmVisible)
            BeginCatchRun();

        if (!rhythmVisible && wasRhythmVisible)
            CompleteCatchRun();

        wasRhythmVisible = rhythmVisible;
    }

    private void HandleNoteJudged(RhythmJudge.JudgeRating rating)
    {
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
        pendingCatchOutcomeRegistered = false;
    }

    private void CompleteCatchRun()
    {
        if (!runActive)
            return;

        float accuracy = CalculateAccuracy(runPerfect, runGood, runMiss);
        bool catchSucceeded = pendingCatchOutcomeRegistered
            ? pendingCatchSucceeded
            : IsSuccessfulCatchAccuracy(accuracy);
        pendingCatchOutcomeRegistered = false;

        lastCatchScore = runScore;
        lastCatchPerfect = runPerfect;
        lastCatchGood = runGood;
        lastCatchMiss = runMiss;
        lastCatchBestCombo = runBestCombo;
        lastCatchAccuracy = accuracy;
        lastCatchSucceeded = catchSucceeded;

        sessionRunsCompleted++;
        sessionTotalPerfect += runPerfect;
        sessionTotalGood += runGood;
        sessionTotalMiss += runMiss;

        SessionTopScoresTracker.TryRecordScore(runScore, out _);

        if (catchSucceeded)
        {
            sessionFishCaught++;
            sessionTotalScore += runScore;

            if (runScore > sessionHighScore)
                sessionHighScore = runScore;
        }

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

        int avgScore = sessionRunsCompleted > 0
            ? Mathf.RoundToInt((float)sessionTotalScore / sessionRunsCompleted)
            : 0;
        string lastResult = lastCatchSucceeded ? "Caught" : "Escaped";

        hudText.text =
            "Fishing Session\n" +
            $"Last Attempt: {lastCatchScore} pts ({GetLetterGradeForAccuracy(lastCatchAccuracy)}) [{lastResult}]\n" +
            $"High Score: {sessionHighScore} pts\n" +
            $"Caught / Attempts: {sessionFishCaught}/{sessionRunsCompleted}\n" +
            $"Session Score: {sessionTotalScore}\n" +
            $"Avg / Attempt: {avgScore}\n" +
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

    public static SessionSummary GetSessionSummary()
    {
        int avgScore = sessionRunsCompleted > 0
            ? Mathf.RoundToInt((float)sessionTotalScore / sessionRunsCompleted)
            : 0;

        return new SessionSummary
        {
            FishCaught = sessionFishCaught,
            CatchAttempts = sessionRunsCompleted,
            HighScore = sessionHighScore,
            SessionScore = sessionTotalScore,
            SessionBestCombo = sessionBestCombo,
            SessionRunsCompleted = sessionRunsCompleted,
            LastCatchScore = lastCatchScore,
            LastCatchBestCombo = lastCatchBestCombo,
            LastCatchPerfect = lastCatchPerfect,
            LastCatchGood = lastCatchGood,
            LastCatchMiss = lastCatchMiss,
            LastCatchAccuracy = lastCatchAccuracy,
            LastCatchSucceeded = lastCatchSucceeded,
            AverageScorePerAttempt = avgScore
        };
    }

    public static void RegisterCatchOutcome(bool catchSucceeded)
    {
        pendingCatchOutcomeRegistered = true;
        pendingCatchSucceeded = catchSucceeded;
    }

    public static float GetCurrentRunAccuracyOrLast()
    {
        if (activeInstance != null && activeInstance.runActive)
            return CalculateAccuracy(activeInstance.runPerfect, activeInstance.runGood, activeInstance.runMiss);

        return lastCatchAccuracy;
    }

    public static bool IsSuccessfulCatchAccuracy(float accuracy)
    {
        return GetGradeRankForAccuracy(accuracy) >= minimumSuccessfulCatchGradeRank;
    }

    public static string GetLetterGradeForAccuracy(float accuracy)
    {
        return GetGradeLetterForRank(GetGradeRankForAccuracy(accuracy));
    }

    public static int GetGradeRankForAccuracy(float accuracy)
    {
        if (accuracy >= GradeSMinAccuracy) return (int)CatchGradeRank.S;
        if (accuracy >= GradeAMinAccuracy) return (int)CatchGradeRank.A;
        if (accuracy >= GradeBMinAccuracy) return (int)CatchGradeRank.B;
        if (accuracy >= GradeCMinAccuracy) return (int)CatchGradeRank.C;
        return (int)CatchGradeRank.D;
    }

    public static string GetGradeLetterForRank(int rank)
    {
        switch (Mathf.Clamp(rank, (int)CatchGradeRank.D, (int)CatchGradeRank.S))
        {
            case (int)CatchGradeRank.S: return "S";
            case (int)CatchGradeRank.A: return "A";
            case (int)CatchGradeRank.B: return "B";
            case (int)CatchGradeRank.C: return "C";
            default: return "D";
        }
    }

    private static float CalculateAccuracy(int perfect, int good, int miss)
    {
        int judgedNotes = perfect + good + miss;
        if (judgedNotes <= 0)
            return 0f;

        return ((perfect + (good * 0.7f)) / judgedNotes) * 100f;
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
        bool hideForTutorial = hideInTutorialLevel && IsInTutorialLevel();
        bool shouldShow = hudEnabled && (!hideDuringRhythmMode || !rhythmVisible) && !hideForPause && !hideForTutorial;
        canvas.enabled = shouldShow;
    }

    private bool IsPausedInPondLevel()
    {
        if (Time.timeScale > 0f)
            return false;

        Scene activeScene = SceneManager.GetActiveScene();
        return activeScene.name == pondLevelSceneName;
    }

    private bool IsInTutorialLevel()
    {
        Scene tutorialScene = SceneManager.GetSceneByName(tutorialSceneName);
        return tutorialScene.IsValid() &&
               tutorialScene.isLoaded &&
               gameObject.scene == tutorialScene;
    }
}
