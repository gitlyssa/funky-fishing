using System.Collections.Generic;
using System.IO;
using TMPro;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class RhythmTutorialCoach : MonoBehaviour
{
    private static bool sceneHookRegistered;
    private static RhythmTutorialCoach activeInstance;

    private enum FlowState
    {
        WaitingForRhythm,
        IntroHowToPlayGate,
        DirectionInfoGate,
        DirectionPracticeActive,
        SequenceInfoGate,
        SequencePracticeActive,
        SuccessGate,
        Complete
    }

    private enum PendingAdvanceAction
    {
        None,
        NextDirection,
        SequenceInfo,
        SuccessGate
    }

    [Header("Scene Scope")]
    [SerializeField] private string tutorialSceneName = "Tutorial_Level";
    [SerializeField] private string rhythmSceneName = "AlphaRhythm";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Practice")]
    [SerializeField, Min(1)] private int requiredSuccessfulUpNotes = 3;
    [SerializeField] private float tutorialBpm = 92f;
    [SerializeField, Min(1)] private int tutorialGroupSize = 3;
    [SerializeField, Min(0)] private int tutorialGroupRestBeats = 3;
    [SerializeField, Min(1)] private int tutorialNoteSpacingBeats = 2;
    [SerializeField, Min(1)] private int tutorialSequenceNoteSpacingBeats = 1;
    [SerializeField, Min(0f)] private float resumeInputBlockSeconds = 0.3f;

    [Header("Left Step Override")]
    [SerializeField] private string leftPracticeOverrideBeatmapAssetPath = "Assets/FMOD/FunkyFishing/TutorialMus_beatmap.csv";
    [SerializeField] private string leftPracticeOverrideMusicEventPath = "event:/Music/TutorialMus";

    [Header("Copy")]
    [SerializeField, TextArea(3, 8)] private string howToPlayMessage =
        "You didn't think it be that easy, did you?\n\n" +
        "To make your funky catch you'll have to tire the fish out with your rhythm skills.\n\n" +
        "Rhythm tutorial:\n\n" +
        "Notes move toward the hit ring. Flick in the same direction as the incoming note right as it reaches the ring.\n\n" +
        "- Xbox: flick the Left Stick\n" +
        "- Joy-Con: flick the joycon in the direction of the beats\n\n" +
        "Press confirm to continue.";
    [SerializeField, TextArea(3, 8)] private string upPracticeMessage =
        "Let's give this a go!\n\n" +        
        "Step 1:\n\n" +
        "Hit 3 successful Up notes in a row.\n\n" +
        "Press confirm to start.";
    [SerializeField, TextArea(3, 8)] private string leftPracticeMessage =
        "Nice. Next:\n\n" +
        "Step 2:\n\n" +
        "Hit 3 successful Left notes in a row.\n\n" +
        "Press confirm to start.";
    [SerializeField, TextArea(3, 8)] private string rightPracticeMessage =
        "Great. Next:\n\n" +
        "Step 3:\n\n" +
        "Hit 3 successful Right notes in a row.\n\n" +
        "Press confirm to start.";
    [SerializeField, TextArea(3, 8)] private string sequencePracticeMessage =
        "One more thing:\n\n" +
        "Hit this sequence in order:\n\n" +
        "Left -> Right -> Up\n\n" +
        "Press confirm to start.";
    [SerializeField, TextArea(3, 8)] private string successMessage =
        "That concludes the Funky Fishing tutorial.\n\n" +
        "Press continue to return to the Main Menu.";

    [Header("Style")]
    [SerializeField] private Vector2 panelSize = new Vector2(980f, 520f);
    [SerializeField] private Vector2 directionPanelSizeWithImage = new Vector2(760f, 520f);
    [SerializeField] private Color backdropColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color panelColor = new Color(0.05f, 0.05f, 0.05f, 0.92f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private int fontSize = 40;
    [SerializeField] private int minFontSize = 24;
    [SerializeField] private Vector2 progressOffset = new Vector2(-24f, -24f);
    [SerializeField] private int progressFontSize = 30;

    [Header("Direction Tutorial Images")]
    [SerializeField] private Vector2 directionTutorialImageMaxSize = new Vector2(560f, 460f);
    [SerializeField] private Vector2 directionTutorialImageOffset = new Vector2(680f, 0f);
    [SerializeField] private float directionTutorialImageLeftShift = 140f;
    [SerializeField] private string upTutorialImageResourcePath = "Tutorial/tut4";
    [SerializeField] private string upTutorialImageEditorAssetPath = "Assets/Images/Tutorial/tut4.jpg";
    [SerializeField] private string leftTutorialImageResourcePath = "Tutorial/tut5";
    [SerializeField] private string leftTutorialImageEditorAssetPath = "Assets/Images/Tutorial/tut5.jpg";
    [SerializeField] private string rightTutorialImageResourcePath = "Tutorial/tut6";
    [SerializeField] private string rightTutorialImageEditorAssetPath = "Assets/Images/Tutorial/tut6.jpg";

    private FlowState flowState = FlowState.WaitingForRhythm;
    private bool wasRhythmVisible;
    private bool gateActive;
    private bool practiceModeInitialized;
    private int currentDirectionStepIndex;
    private int consecutiveSuccessfulNotes;
    private int sequenceProgress;
    private PendingAdvanceAction pendingAdvanceAction;

    private static readonly FlickDirection[] DirectionSteps =
    {
        FlickDirection.Up,
        FlickDirection.Left,
        FlickDirection.Right
    };
    private static readonly FlickDirection[] FinalSequenceStep =
    {
        FlickDirection.Left,
        FlickDirection.Right,
        FlickDirection.Up
    };

    private RhythmConductor conductor;
    private RhythmMusicPlayer musicPlayer;
    private RhythmPerformanceHud rhythmPerformanceHud;
    private FishingSessionHud fishingSessionHud;
    private Canvas gateCanvas;
    private GameObject gateWindowRoot;
    private RectTransform gatePanelRect;
    private TextMeshProUGUI gateText;
    private TextMeshProUGUI progressText;
    private RectTransform directionTutorialImageRect;
    private Image directionTutorialImage;
    private Sprite upTutorialSprite;
    private Sprite leftTutorialSprite;
    private Sprite rightTutorialSprite;
    private bool loggedMissingUpTutorialImage;
    private bool loggedMissingLeftTutorialImage;
    private bool loggedMissingRightTutorialImage;
    private GameObject rhythmStartTutorialUi;
    private GameObject upPracticeTutorialUi;
    private GameObject leftPracticeTutorialUi;
    private GameObject rightPracticeTutorialUi;
    private GameObject sequenceTutorialUi;
    private GameObject endTutorialUi;

    private bool cursorStateCached;
    private bool cachedCursorVisible;
    private CursorLockMode cachedCursorLockMode;
    private readonly List<PauseManager> disabledPauseManagers = new List<PauseManager>();
    private bool conflictingHudSuppressed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        if (sceneHookRegistered)
            return;

        sceneHookRegistered = true;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TrySpawnForTutorialRhythmContext();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void TrySpawnAfterInitialSceneLoad()
    {
        TrySpawnForTutorialRhythmContext();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Tutorial_Level")
            return;

        TrySpawnForTutorialRhythmContext();
    }

    private static void TrySpawnForTutorialRhythmContext()
    {
        Scene tutorialScene = SceneManager.GetSceneByName("Tutorial_Level");
        if (!tutorialScene.IsValid() || !tutorialScene.isLoaded)
            return;

        if (FindObjectOfType<RhythmTutorialCoach>() != null)
            return;

        GameObject go = new GameObject("RhythmTutorialCoach");
        SceneManager.MoveGameObjectToScene(go, tutorialScene);
        go.AddComponent<RhythmTutorialCoach>();
    }

    private void Awake()
    {
        if (!IsTutorialSceneLoaded())
        {
            Destroy(gameObject);
            return;
        }

        if (activeInstance != null && activeInstance != this)
        {
            Destroy(gameObject);
            return;
        }

        activeInstance = this;

        BuildUi();
        SetGateVisible(false);
        ResolveSceneTutorialUi();
        ResolveRhythmReferences();
    }

    private void OnEnable()
    {
        RhythmJudge.OnDetailedNoteJudged += HandleDetailedNoteJudged;
    }

    private void OnDisable()
    {
        RhythmJudge.OnDetailedNoteJudged -= HandleDetailedNoteJudged;
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
            activeInstance = null;

        if (conductor != null)
            conductor.SetTutorialReelsSuppressed(false);
        if (musicPlayer != null)
            musicPlayer.SetTutorialPlaybackSuppressed(false);

        StopPracticeMode();
        HideAllSceneTutorialUi();
        RestoreConflictingHud();
        RestorePauseManagers();
        RestoreCursorState();
        DestroyDirectionTutorialSprites();
        if (gateActive)
            Time.timeScale = 1f;
    }

    private void Update()
    {
        if (!IsTutorialSceneLoaded())
        {
            Destroy(gameObject);
            return;
        }

        ResolveRhythmReferences();
        bool rhythmVisible = IsRhythmVisible();

        ProcessPendingAdvanceAction();

        if (rhythmVisible && flowState == FlowState.WaitingForRhythm)
        {
            BeginIntro();
        }
        else if (!rhythmVisible && wasRhythmVisible)
        {
            HandleRhythmEncounterEnded();
        }
        wasRhythmVisible = rhythmVisible;

        if (!gateActive)
            return;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (WasConfirmPressedThisFrame())
            AdvanceGate();
    }

    private void BeginIntro()
    {
        ResolveRhythmReferences();
        if (conductor != null)
            conductor.SetTutorialReelsSuppressed(true);
        if (musicPlayer != null)
            musicPlayer.SetTutorialPlaybackSuppressed(true);

        currentDirectionStepIndex = 0;
        consecutiveSuccessfulNotes = 0;
        sequenceProgress = 0;
        practiceModeInitialized = false;
        pendingAdvanceAction = PendingAdvanceAction.None;
        SuppressConflictingHud();
        flowState = FlowState.IntroHowToPlayGate;
        PauseForGate();
        SetGateMessage(howToPlayMessage);
        SetProgressVisible(false);
        SetGateVisible(true);
    }

    private void AdvanceGate()
    {
        if (flowState == FlowState.IntroHowToPlayGate)
        {
            flowState = FlowState.DirectionInfoGate;
            InitializePracticeModeIfNeeded();
            if (conductor != null)
                conductor.SetTutorialUpPracticeSpawnPaused(true);
            ResumeGameKeepGateOpen();
            SetGateMessage(BuildDirectionInfoMessage());
            RefreshProgress();
            SetProgressVisible(true);
            return;
        }

        if (flowState == FlowState.DirectionInfoGate)
        {
            SetGateVisible(false);
            CloseOverlayGateWithoutPause();
            if (musicPlayer != null)
                musicPlayer.SetTutorialPlaybackSuppressed(false);
            ApplyLeftPracticeRhythmOverrideIfNeeded();
            BeginActivePractice();
            return;
        }

        if (flowState == FlowState.SequenceInfoGate)
        {
            SetGateVisible(false);
            CloseOverlayGateWithoutPause();
            BeginSequencePractice();
            return;
        }

        if (flowState == FlowState.SuccessGate)
        {
            SetGateVisible(false);
            CloseOverlayGateWithoutPause();
            StopPracticeMode();
            RestoreConflictingHud();
            SetProgressVisible(false);
            flowState = FlowState.Complete;
            ReturnToMainMenu();
        }
    }

    private void InitializePracticeModeIfNeeded()
    {
        if (flowState == FlowState.Complete || practiceModeInitialized)
            return;

        consecutiveSuccessfulNotes = 0;

        if (conductor != null)
            conductor.StartTutorialDirectionalPracticeMode(
                CurrentTargetDirection(),
                tutorialBpm,
                tutorialGroupSize,
                tutorialGroupRestBeats,
                tutorialNoteSpacingBeats);
        if (musicPlayer != null)
            musicPlayer.SetTutorialLoopMode(true);

        if (conductor != null)
            practiceModeInitialized = true;
    }

    private void BeginActivePractice()
    {
        if (flowState == FlowState.Complete)
            return;

        InitializePracticeModeIfNeeded();
        flowState = FlowState.DirectionPracticeActive;
        if (conductor != null)
            conductor.SetTutorialUpPracticeSpawnPaused(false);
        RefreshProgress();
        SetProgressVisible(true);
    }

    private void BeginSequencePractice()
    {
        if (flowState == FlowState.Complete)
            return;

        sequenceProgress = 0;
        flowState = FlowState.SequencePracticeActive;

        if (conductor != null)
            conductor.StartTutorialSequencePracticeMode(
                FinalSequenceStep,
                tutorialBpm,
                FinalSequenceStep.Length,
                tutorialGroupRestBeats,
                tutorialSequenceNoteSpacingBeats);
        if (conductor != null)
            conductor.SetTutorialUpPracticeSpawnPaused(false);
        if (musicPlayer != null)
            musicPlayer.SetTutorialLoopMode(true);

        practiceModeInitialized = true;
        RefreshProgress();
        SetProgressVisible(true);
    }

    private void ApplyLeftPracticeRhythmOverrideIfNeeded()
    {
        if (CurrentTargetDirection() != FlickDirection.Up)
            return;

        ResolveRhythmReferences();

        if (conductor != null)
        {
            TextAsset overrideBeatmap = LoadLeftPracticeOverrideBeatmap();
            if (overrideBeatmap != null)
                conductor.SetBeatmapFile(overrideBeatmap);
            else
                Debug.LogWarning(
                    $"RhythmTutorialCoach: Could not load left practice override beatmap at '{leftPracticeOverrideBeatmapAssetPath}'.");
        }

        if (musicPlayer != null)
        {
            EventReference overrideEvent = RuntimeManager.PathToEventReference(leftPracticeOverrideMusicEventPath);
            if (!overrideEvent.IsNull)
                musicPlayer.SetMusicEvent(overrideEvent);
            else
                Debug.LogWarning(
                    $"RhythmTutorialCoach: Could not resolve left practice override music event '{leftPracticeOverrideMusicEventPath}'.");
        }
    }

    private TextAsset LoadLeftPracticeOverrideBeatmap()
    {
#if UNITY_EDITOR
        TextAsset editorAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(leftPracticeOverrideBeatmapAssetPath);
        if (editorAsset != null)
            return editorAsset;
#endif

        string relativePath = leftPracticeOverrideBeatmapAssetPath;
        const string assetsPrefix = "Assets/";
        if (relativePath.StartsWith(assetsPrefix))
            relativePath = relativePath.Substring(assetsPrefix.Length);

        string absolutePath = Path.Combine(Application.dataPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(absolutePath))
            return null;

        return new TextAsset(File.ReadAllText(absolutePath));
    }

    private void StopPracticeMode()
    {
        if (conductor != null)
            conductor.StopTutorialUpPracticeMode(false);
        if (musicPlayer != null)
            musicPlayer.SetTutorialLoopMode(false);
        practiceModeInitialized = false;
    }

    private void EnterSuccessGate()
    {
        if (flowState != FlowState.DirectionPracticeActive &&
            flowState != FlowState.SequencePracticeActive)
            return;

        if (conductor != null)
            conductor.SetTutorialUpPracticeSpawnPaused(true);
        flowState = FlowState.SuccessGate;
        OpenOverlayGateWithoutPause();
        SetGateMessage(successMessage);
        SetGateVisible(true);
        RefreshProgress();
    }

    private void EnterNextDirectionInfoGate()
    {
        if (currentDirectionStepIndex >= DirectionSteps.Length - 1)
        {
            QueuePendingAdvanceAction(PendingAdvanceAction.SequenceInfo);
            return;
        }

        currentDirectionStepIndex++;
        consecutiveSuccessfulNotes = 0;
        flowState = FlowState.DirectionInfoGate;

        if (conductor != null)
            conductor.StartTutorialDirectionalPracticeMode(
                CurrentTargetDirection(),
                tutorialBpm,
                tutorialGroupSize,
                tutorialGroupRestBeats,
                tutorialNoteSpacingBeats);
        if (conductor != null)
            conductor.SetTutorialUpPracticeSpawnPaused(true);

        OpenOverlayGateWithoutPause();
        SetGateMessage(BuildDirectionInfoMessage());
        SetProgressVisible(true);
        SetGateVisible(true);
        RefreshProgress();
    }

    private void EnterSequenceInfoGate()
    {
        sequenceProgress = 0;
        flowState = FlowState.SequenceInfoGate;

        if (conductor != null)
            conductor.StartTutorialSequencePracticeMode(
                FinalSequenceStep,
                tutorialBpm,
                FinalSequenceStep.Length,
                tutorialGroupRestBeats,
                tutorialSequenceNoteSpacingBeats);
        if (conductor != null)
            conductor.SetTutorialUpPracticeSpawnPaused(true);

        OpenOverlayGateWithoutPause();
        SetGateMessage(BuildSequenceInfoMessage());
        SetProgressVisible(true);
        SetGateVisible(true);
        RefreshProgress();
    }

    private void HandleDetailedNoteJudged(
        RhythmJudge.JudgeRating rating,
        RhythmArcNote.NoteType noteType,
        FlickDirection direction)
    {
        if (flowState != FlowState.DirectionPracticeActive && flowState != FlowState.SequencePracticeActive)
            return;

        if (flowState == FlowState.DirectionPracticeActive)
        {
            FlickDirection targetDirection = CurrentTargetDirection();
            if (noteType != RhythmArcNote.NoteType.Flick || direction != targetDirection)
                return;

            if (rating == RhythmJudge.JudgeRating.Perfect || rating == RhythmJudge.JudgeRating.Good)
                consecutiveSuccessfulNotes++;
            else
                consecutiveSuccessfulNotes = 0;

            RefreshProgress();
            if (consecutiveSuccessfulNotes >= requiredSuccessfulUpNotes)
                QueuePendingAdvanceAction(PendingAdvanceAction.NextDirection);
            return;
        }

        HandleSequenceJudgement(rating, noteType, direction);
    }

    private void HandleRhythmEncounterEnded()
    {
        StopPracticeMode();
        HideAllSceneTutorialUi();
        RestoreConflictingHud();
        SetProgressVisible(false);

        if (gateActive)
        {
            SetGateVisible(false);
            if (flowState == FlowState.IntroHowToPlayGate)
                ResumeFromGate();
            else
                CloseOverlayGateWithoutPause();
        }

        if (flowState != FlowState.Complete)
            flowState = FlowState.WaitingForRhythm;
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;

        if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
    }

    private void PauseForGate()
    {
        CacheCursorState();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        DisablePauseManagers();
        Time.timeScale = 0f;
        if (musicPlayer != null)
            musicPlayer.PauseRhythmForGamePause();

        gateActive = true;
    }

    private void ResumeFromGate()
    {
        gateActive = false;
        Time.timeScale = 1f;
        if (musicPlayer != null)
            musicPlayer.ResumeRhythmFromGamePause();
        XboxFishingInput.BlockGameplayInputForRealtimeSeconds(resumeInputBlockSeconds);
        RestorePauseManagers();
        RestoreCursorState();
    }

    private void OpenOverlayGateWithoutPause()
    {
        CacheCursorState();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        DisablePauseManagers();
        gateActive = true;
    }

    private void ResumeGameKeepGateOpen()
    {
        Time.timeScale = 1f;
        if (musicPlayer != null)
            musicPlayer.ResumeRhythmFromGamePause();
        XboxFishingInput.BlockGameplayInputForRealtimeSeconds(resumeInputBlockSeconds);
        DisablePauseManagers();
        gateActive = true;
    }

    private void CloseOverlayGateWithoutPause()
    {
        gateActive = false;
        RestorePauseManagers();
        RestoreCursorState();
        XboxFishingInput.BlockGameplayInputForRealtimeSeconds(resumeInputBlockSeconds);
    }

    private void BuildUi()
    {
        GameObject canvasGo = new GameObject(
            "RhythmTutorialGateCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        gateCanvas = canvasGo.GetComponent<Canvas>();
        gateCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        gateCanvas.sortingOrder = 1100;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backdrop.transform.SetParent(canvasGo.transform, false);
        gateWindowRoot = backdrop;
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        backdrop.GetComponent<Image>().color = backdropColor;

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(backdrop.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        gatePanelRect = panelRect;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = panelColor;

        GameObject tutorialImageGo = new GameObject("DirectionTutorialImage", typeof(RectTransform), typeof(Image));
        tutorialImageGo.transform.SetParent(backdrop.transform, false);
        directionTutorialImageRect = tutorialImageGo.GetComponent<RectTransform>();
        directionTutorialImageRect.anchorMin = new Vector2(0.5f, 0.5f);
        directionTutorialImageRect.anchorMax = new Vector2(0.5f, 0.5f);
        directionTutorialImageRect.pivot = new Vector2(0.5f, 0.5f);
        directionTutorialImageRect.sizeDelta = directionTutorialImageMaxSize;
        directionTutorialImageRect.anchoredPosition = directionTutorialImageOffset;

        directionTutorialImage = tutorialImageGo.GetComponent<Image>();
        directionTutorialImage.color = Color.white;
        directionTutorialImage.preserveAspect = true;
        directionTutorialImage.raycastTarget = false;
        directionTutorialImage.enabled = false;

        GameObject textGo = new GameObject("TutorialText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(panel.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(40f, 28f);
        textRect.offsetMax = new Vector2(-40f, -28f);

        gateText = textGo.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            gateText.font = TMP_Settings.defaultFontAsset;
        gateText.enableAutoSizing = true;
        gateText.fontSizeMax = fontSize;
        gateText.fontSizeMin = minFontSize;
        gateText.fontSize = fontSize;
        gateText.color = textColor;
        gateText.alignment = TextAlignmentOptions.Center;
        gateText.textWrappingMode = TextWrappingModes.Normal;
        gateText.raycastTarget = false;

        GameObject progressGo = new GameObject("ProgressText", typeof(RectTransform), typeof(TextMeshProUGUI));
        progressGo.transform.SetParent(canvasGo.transform, false);
        RectTransform progressRect = progressGo.GetComponent<RectTransform>();
        progressRect.anchorMin = new Vector2(1f, 1f);
        progressRect.anchorMax = new Vector2(1f, 1f);
        progressRect.pivot = new Vector2(1f, 1f);
        progressRect.anchoredPosition = progressOffset;
        progressRect.sizeDelta = new Vector2(500f, 90f);

        progressText = progressGo.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            progressText.font = TMP_Settings.defaultFontAsset;
        progressText.fontSize = progressFontSize;
        progressText.color = textColor;
        progressText.alignment = TextAlignmentOptions.TopRight;
        progressText.textWrappingMode = TextWrappingModes.NoWrap;
        progressText.raycastTarget = false;
        SetProgressVisible(false);
        RefreshGateVisuals();
    }

    private void SetGateMessage(string message)
    {
        if (gateText != null)
            gateText.text = message;

        if (!gateActive)
            return;

        bool showingCustomTutorialUi = UpdateSceneTutorialUiVisibility();
        if (gateWindowRoot != null)
            gateWindowRoot.SetActive(!showingCustomTutorialUi);
    }

    private void SetGateVisible(bool visible)
    {
        bool showingCustomTutorialUi = visible && UpdateSceneTutorialUiVisibility();

        if (!visible)
            HideAllSceneTutorialUi();

        if (gateWindowRoot != null)
            gateWindowRoot.SetActive(visible && !showingCustomTutorialUi);
    }

    private string BuildUpPracticeMessage()
    {
        string baseMessage = string.IsNullOrWhiteSpace(upPracticeMessage)
            ? "Hit 3 successful Up notes in a row.\n\nPress confirm to start."
            : upPracticeMessage.TrimEnd();

        return $"{baseMessage}\n\nStreak: {consecutiveSuccessfulNotes}/{requiredSuccessfulUpNotes}";
    }

    private void RefreshProgress()
    {
        if (progressText != null)
        {
            if (flowState == FlowState.SequenceInfoGate || flowState == FlowState.SequencePracticeActive)
                progressText.text = $"Sequence (L-R-U): {sequenceProgress}/{FinalSequenceStep.Length}";
            else
                progressText.text = $"{DirectionLabel(CurrentTargetDirection())} Streak: {consecutiveSuccessfulNotes}/{requiredSuccessfulUpNotes}";
        }

        if (flowState == FlowState.DirectionInfoGate)
            SetGateMessage(BuildDirectionInfoMessage());
        else if (flowState == FlowState.SequenceInfoGate)
            SetGateMessage(BuildSequenceInfoMessage());
    }

    private void SetProgressVisible(bool visible)
    {
        if (progressText != null)
            progressText.enabled = visible;
    }

    private FlickDirection CurrentTargetDirection()
    {
        int clampedIndex = Mathf.Clamp(currentDirectionStepIndex, 0, DirectionSteps.Length - 1);
        return DirectionSteps[clampedIndex];
    }

    private string BuildDirectionInfoMessage()
    {
        string baseMessage;
        switch (CurrentTargetDirection())
        {
            case FlickDirection.Left:
                baseMessage = string.IsNullOrWhiteSpace(leftPracticeMessage)
                    ? "Hit 3 successful Left notes in a row.\n\nPress confirm to start."
                    : leftPracticeMessage.TrimEnd();
                break;

            case FlickDirection.Right:
                baseMessage = string.IsNullOrWhiteSpace(rightPracticeMessage)
                    ? "Hit 3 successful Right notes in a row.\n\nPress confirm to start."
                    : rightPracticeMessage.TrimEnd();
                break;

            default:
                baseMessage = BuildUpPracticeMessage();
                return baseMessage;
        }

        return $"{baseMessage}\n\nStreak: {consecutiveSuccessfulNotes}/{requiredSuccessfulUpNotes}";
    }

    private string BuildSequenceInfoMessage()
    {
        string baseMessage = string.IsNullOrWhiteSpace(sequencePracticeMessage)
            ? "Hit Left -> Right -> Up in order.\n\nPress confirm to start."
            : sequencePracticeMessage.TrimEnd();

        return $"{baseMessage}\n\nSequence: {sequenceProgress}/{FinalSequenceStep.Length}";
    }

    private static string DirectionLabel(FlickDirection direction)
    {
        switch (direction)
        {
            case FlickDirection.Left: return "Left";
            case FlickDirection.Right: return "Right";
            default: return "Up";
        }
    }

    private void RefreshGateVisuals()
    {
        bool showDirectionImage = flowState == FlowState.DirectionInfoGate &&
                                  gateWindowRoot != null &&
                                  gateWindowRoot.activeSelf;
        float layoutShiftX = showDirectionImage ? -Mathf.Abs(directionTutorialImageLeftShift) : 0f;

        if (gatePanelRect != null)
        {
            gatePanelRect.sizeDelta = showDirectionImage ? directionPanelSizeWithImage : panelSize;
            gatePanelRect.anchoredPosition = new Vector2(layoutShiftX, 0f);
        }

        if (directionTutorialImageRect != null)
            directionTutorialImageRect.anchoredPosition = directionTutorialImageOffset + new Vector2(layoutShiftX, 0f);

        if (directionTutorialImage == null)
            return;

        Sprite tutorialSprite = showDirectionImage ? GetDirectionTutorialSprite(CurrentTargetDirection()) : null;
        directionTutorialImage.sprite = tutorialSprite;
        if (directionTutorialImageRect != null)
            directionTutorialImageRect.sizeDelta = GetDirectionTutorialImageDisplaySize(tutorialSprite);
        directionTutorialImage.enabled = showDirectionImage && tutorialSprite != null;
    }

    private Vector2 GetDirectionTutorialImageDisplaySize(Sprite sprite)
    {
        if (sprite == null)
            return directionTutorialImageMaxSize;

        Rect spriteRect = sprite.rect;
        if (spriteRect.width <= 0f || spriteRect.height <= 0f)
            return directionTutorialImageMaxSize;

        float scaleByWidth = directionTutorialImageMaxSize.x / spriteRect.width;
        float scaleByHeight = directionTutorialImageMaxSize.y / spriteRect.height;
        float uniformScale = Mathf.Min(scaleByWidth, scaleByHeight);
        if (float.IsNaN(uniformScale) || float.IsInfinity(uniformScale) || uniformScale <= 0f)
            return directionTutorialImageMaxSize;

        return new Vector2(spriteRect.width * uniformScale, spriteRect.height * uniformScale);
    }

    private Sprite GetDirectionTutorialSprite(FlickDirection direction)
    {
        switch (direction)
        {
            case FlickDirection.Left:
                return LoadLeftTutorialSprite();
            case FlickDirection.Right:
                return LoadRightTutorialSprite();
            default:
                return LoadUpTutorialSprite();
        }
    }

    private Sprite LoadUpTutorialSprite()
    {
        if (upTutorialSprite != null)
            return upTutorialSprite;

        Texture2D texture = null;
        if (!string.IsNullOrWhiteSpace(upTutorialImageResourcePath))
            texture = Resources.Load<Texture2D>(upTutorialImageResourcePath);

#if UNITY_EDITOR
        if (texture == null && !string.IsNullOrWhiteSpace(upTutorialImageEditorAssetPath))
            texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(upTutorialImageEditorAssetPath);
#endif

        if (texture == null)
        {
            if (!loggedMissingUpTutorialImage)
            {
                Debug.LogWarning(
                    "RhythmTutorialCoach: Couldn't load Up tutorial image. " +
                    "Expected Resources path '" + upTutorialImageResourcePath + "' " +
                    "or editor asset '" + upTutorialImageEditorAssetPath + "'.");
                loggedMissingUpTutorialImage = true;
            }
            return null;
        }

        upTutorialSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        upTutorialSprite.name = "UpTutorialSprite";
        return upTutorialSprite;
    }

    private Sprite LoadLeftTutorialSprite()
    {
        if (leftTutorialSprite != null)
            return leftTutorialSprite;

        Texture2D texture = null;
        if (!string.IsNullOrWhiteSpace(leftTutorialImageResourcePath))
            texture = Resources.Load<Texture2D>(leftTutorialImageResourcePath);

#if UNITY_EDITOR
        if (texture == null && !string.IsNullOrWhiteSpace(leftTutorialImageEditorAssetPath))
            texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(leftTutorialImageEditorAssetPath);
#endif

        if (texture == null)
        {
            if (!loggedMissingLeftTutorialImage)
            {
                Debug.LogWarning(
                    "RhythmTutorialCoach: Couldn't load Left tutorial image. " +
                    "Expected Resources path '" + leftTutorialImageResourcePath + "' " +
                    "or editor asset '" + leftTutorialImageEditorAssetPath + "'.");
                loggedMissingLeftTutorialImage = true;
            }
            return null;
        }

        leftTutorialSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        leftTutorialSprite.name = "LeftTutorialSprite";
        return leftTutorialSprite;
    }

    private Sprite LoadRightTutorialSprite()
    {
        if (rightTutorialSprite != null)
            return rightTutorialSprite;

        Texture2D texture = null;
        if (!string.IsNullOrWhiteSpace(rightTutorialImageResourcePath))
            texture = Resources.Load<Texture2D>(rightTutorialImageResourcePath);

#if UNITY_EDITOR
        if (texture == null && !string.IsNullOrWhiteSpace(rightTutorialImageEditorAssetPath))
            texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(rightTutorialImageEditorAssetPath);
#endif

        if (texture == null)
        {
            if (!loggedMissingRightTutorialImage)
            {
                Debug.LogWarning(
                    "RhythmTutorialCoach: Couldn't load Right tutorial image. " +
                    "Expected Resources path '" + rightTutorialImageResourcePath + "' " +
                    "or editor asset '" + rightTutorialImageEditorAssetPath + "'.");
                loggedMissingRightTutorialImage = true;
            }
            return null;
        }

        rightTutorialSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        rightTutorialSprite.name = "RightTutorialSprite";
        return rightTutorialSprite;
    }

    private void DestroyDirectionTutorialSprites()
    {
        if (upTutorialSprite != null)
        {
            Destroy(upTutorialSprite);
            upTutorialSprite = null;
        }

        if (leftTutorialSprite != null)
        {
            Destroy(leftTutorialSprite);
            leftTutorialSprite = null;
        }

        if (rightTutorialSprite != null)
        {
            Destroy(rightTutorialSprite);
            rightTutorialSprite = null;
        }
    }

    private void ResolveSceneTutorialUi()
    {
        Scene tutorialScene = SceneManager.GetSceneByName(tutorialSceneName);
        if (!tutorialScene.IsValid() || !tutorialScene.isLoaded)
            return;

        GameObject canvasObject = FindSceneGameObject(tutorialScene, "Canvas");
        if (canvasObject == null)
            return;

        bool useXBoxTutorialUi = TutorialStartGate.IsXBoxControllerSelected();
        rhythmStartTutorialUi = FindTutorialUiWithFallback(
            canvasObject.transform,
            useXBoxTutorialUi ? FindTutorialUiAny(canvasObject.transform, "RhythmStartTutorialXbox", "RhythmStartTutorialXBox") : FindTutorialUi(canvasObject.transform, "RhythmStartTutorial"),
            "RhythmStartTutorial");
        upPracticeTutorialUi = FindTutorialUiWithFallback(
            canvasObject.transform,
            useXBoxTutorialUi ? FindTutorialUiAny(canvasObject.transform, "UpPracticeTutorialXbox", "UpPracticeTutorialXBox") : FindTutorialUi(canvasObject.transform, "UpPracticeTutorial"),
            "UpPracticeTutorial");
        leftPracticeTutorialUi = FindTutorialUiWithFallback(
            canvasObject.transform,
            useXBoxTutorialUi ? FindTutorialUiAny(canvasObject.transform, "LeftPracticeTutorialXbox", "LeftPracticeTutorialXBox") : FindTutorialUi(canvasObject.transform, "LeftPracticeTutorial"),
            "LeftPracticeTutorial");
        rightPracticeTutorialUi = FindTutorialUiWithFallback(
            canvasObject.transform,
            useXBoxTutorialUi ? FindTutorialUiAny(canvasObject.transform, "RightPracticeTutorialXbox", "RightPracticeTutorialXBox") : FindTutorialUi(canvasObject.transform, "RightPracticeTutorial"),
            "RightPracticeTutorial");
        sequenceTutorialUi = FindTutorialUiWithFallback(
            canvasObject.transform,
            useXBoxTutorialUi ? FindTutorialUiAny(canvasObject.transform, "SequenceTutorialXbox", "SequenceTutorialXBox") : FindTutorialUi(canvasObject.transform, "SequenceTutorial"),
            "SequenceTutorial");
        endTutorialUi = FindTutorialUi(canvasObject.transform, "EndTutorial");

        HideAllSceneTutorialUi();
    }

    private static GameObject FindTutorialUi(Transform canvasTransform, string childName)
    {
        Transform child = canvasTransform.Find(childName);
        return child != null ? child.gameObject : null;
    }

    private static GameObject FindTutorialUiWithFallback(
        Transform canvasTransform,
        GameObject preferred,
        string fallbackChildName)
    {
        if (preferred != null)
            return preferred;

        return FindTutorialUi(canvasTransform, fallbackChildName);
    }

    private static GameObject FindTutorialUiAny(Transform canvasTransform, params string[] childNames)
    {
        for (int i = 0; i < childNames.Length; i++)
        {
            GameObject match = FindTutorialUi(canvasTransform, childNames[i]);
            if (match != null)
                return match;
        }

        return null;
    }

    private static GameObject FindSceneGameObject(Scene scene, string objectName)
    {
        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            Transform match = FindChildRecursive(rootObjects[i].transform, objectName);
            if (match != null)
                return match.gameObject;
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string objectName)
    {
        if (parent.name == objectName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform match = FindChildRecursive(parent.GetChild(i), objectName);
            if (match != null)
                return match;
        }

        return null;
    }

    private void HideAllSceneTutorialUi()
    {
        SetTutorialUiActive(rhythmStartTutorialUi, false);
        SetTutorialUiActive(upPracticeTutorialUi, false);
        SetTutorialUiActive(leftPracticeTutorialUi, false);
        SetTutorialUiActive(rightPracticeTutorialUi, false);
        SetTutorialUiActive(sequenceTutorialUi, false);
        SetTutorialUiActive(endTutorialUi, false);
    }

    private bool UpdateSceneTutorialUiVisibility()
    {
        ResolveSceneTutorialUi();
        HideAllSceneTutorialUi();

        GameObject tutorialUiToShow = null;
        switch (flowState)
        {
            case FlowState.IntroHowToPlayGate:
                tutorialUiToShow = rhythmStartTutorialUi;
                break;

            case FlowState.DirectionInfoGate:
                switch (CurrentTargetDirection())
                {
                    case FlickDirection.Left:
                        tutorialUiToShow = leftPracticeTutorialUi;
                        break;

                    case FlickDirection.Right:
                        tutorialUiToShow = rightPracticeTutorialUi;
                        break;

                    default:
                        tutorialUiToShow = upPracticeTutorialUi;
                        break;
                }
                break;

            case FlowState.SequenceInfoGate:
                tutorialUiToShow = sequenceTutorialUi;
                break;

            case FlowState.SuccessGate:
                tutorialUiToShow = endTutorialUi;
                break;
        }

        bool hasCustomUi = tutorialUiToShow != null;
        SetTutorialUiActive(tutorialUiToShow, hasCustomUi);
        if (gateText != null)
            gateText.gameObject.SetActive(!hasCustomUi);

        return hasCustomUi;
    }

    private static void SetTutorialUiActive(GameObject tutorialUi, bool active)
    {
        if (tutorialUi != null)
            tutorialUi.SetActive(active);
    }

    private void HandleSequenceJudgement(
        RhythmJudge.JudgeRating rating,
        RhythmArcNote.NoteType noteType,
        FlickDirection direction)
    {
        if (flowState != FlowState.SequencePracticeActive)
            return;

        if (noteType != RhythmArcNote.NoteType.Flick)
            return;

        FlickDirection expected = FinalSequenceStep[Mathf.Clamp(sequenceProgress, 0, FinalSequenceStep.Length - 1)];
        bool hitSuccess = rating == RhythmJudge.JudgeRating.Perfect || rating == RhythmJudge.JudgeRating.Good;

        if (hitSuccess && direction == expected)
        {
            sequenceProgress++;
        }
        else
        {
            // Reset sequence progress on failed/missed step.
            sequenceProgress = 0;
            // Allow immediate restart if player hit the first expected direction successfully.
            if (hitSuccess && direction == FinalSequenceStep[0])
                sequenceProgress = 1;
        }

        RefreshProgress();
        if (sequenceProgress >= FinalSequenceStep.Length)
            QueuePendingAdvanceAction(PendingAdvanceAction.SuccessGate);
    }

    private void QueuePendingAdvanceAction(PendingAdvanceAction action)
    {
        if (pendingAdvanceAction == PendingAdvanceAction.SuccessGate)
            return;

        if (pendingAdvanceAction == PendingAdvanceAction.SequenceInfo &&
            action == PendingAdvanceAction.NextDirection)
            return;

        pendingAdvanceAction = action;
    }

    private void ProcessPendingAdvanceAction()
    {
        if (pendingAdvanceAction == PendingAdvanceAction.None || gateActive)
            return;

        PendingAdvanceAction action = pendingAdvanceAction;
        pendingAdvanceAction = PendingAdvanceAction.None;

        switch (action)
        {
            case PendingAdvanceAction.NextDirection:
                EnterNextDirectionInfoGate();
                break;
            case PendingAdvanceAction.SequenceInfo:
                EnterSequenceInfoGate();
                break;
            case PendingAdvanceAction.SuccessGate:
                EnterSuccessGate();
                break;
        }
    }

    private bool IsRhythmVisible()
    {
        if (SceneLoading.Instance == null)
            return false;

        return SceneLoading.Instance.IsRhythmVisible;
    }

    private bool IsTutorialSceneLoaded()
    {
        Scene tutorialScene = SceneManager.GetSceneByName(tutorialSceneName);
        return tutorialScene.IsValid() && tutorialScene.isLoaded;
    }

    private void ResolveRhythmReferences()
    {
        if (conductor == null)
            conductor = FindObjectOfType<RhythmConductor>();
        if (musicPlayer == null)
            musicPlayer = FindObjectOfType<RhythmMusicPlayer>();
        if (rhythmPerformanceHud == null)
            rhythmPerformanceHud = FindObjectOfType<RhythmPerformanceHud>();
        if (fishingSessionHud == null)
            fishingSessionHud = FindObjectOfType<FishingSessionHud>();
    }

    private void SuppressConflictingHud()
    {
        if (conflictingHudSuppressed)
            return;

        ResolveRhythmReferences();

        if (rhythmPerformanceHud != null)
        {
            rhythmPerformanceHud.SetJudgementFeedbackOnlyEnabled(true);
            rhythmPerformanceHud.SetHudEnabled(false);
        }
        if (fishingSessionHud != null)
            fishingSessionHud.SetHudEnabled(false);

        conflictingHudSuppressed = true;
    }

    private void RestoreConflictingHud()
    {
        if (!conflictingHudSuppressed)
            return;

        if (rhythmPerformanceHud != null)
        {
            rhythmPerformanceHud.SetHudEnabled(true);
            rhythmPerformanceHud.SetJudgementFeedbackOnlyEnabled(false);
        }
        if (fishingSessionHud != null)
            fishingSessionHud.SetHudEnabled(true);

        conflictingHudSuppressed = false;
    }

    private static bool WasConfirmPressedThisFrame()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            return true;

        Gamepad pad = Gamepad.current;
        if (pad != null && pad.buttonSouth.wasPressedThisFrame)
            return true;

        return JoyConMenuInput.SubmitPressedThisFrame;
    }

    public static bool IsOverlayGateActive()
    {
        return activeInstance != null && activeInstance.gateActive;
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
