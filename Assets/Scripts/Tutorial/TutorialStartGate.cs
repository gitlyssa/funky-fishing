using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TutorialStartGate : MonoBehaviour
{
    private static bool sceneHookRegistered;
    private static TutorialStartGate activeInstance;

    private enum TutorialFlowState
    {
        StartupGate,
        AwaitCastingTargetMove,
        AwaitCastHintDelay,
        CastHintGate,
        AwaitSuccessfulCast,
        AwaitYankHintDelay,
        YankHintGate,
        AwaitSuccessfulYank,
        AwaitCatchHintDelay,
        CatchHintGate,
        Complete
    }

    [Header("Scene")]
    [SerializeField] private string tutorialSceneName = "Tutorial_Level";

    [Header("Resume")]
    [SerializeField, Min(0f)] private float resumeInputBlockSeconds = 0.3f;
    [SerializeField, Range(2f, 3f)] private float castHintDelaySeconds = 2.5f;
    [SerializeField, Min(0f)] private float yankHintDelayAfterLandSeconds = 1.25f;
    [SerializeField, Range(1f, 2f)] private float catchHintDelayAfterSpawnSeconds = 1.5f;
    [SerializeField, Min(0.01f)] private float targetMoveDistanceThreshold = 0.3f;
    [SerializeField, Min(1)] private int tutorialCatchFishSpawnCount = 3;

    [Header("Copy")]
    [SerializeField, TextArea(3, 8)] private string welcomeMessage =
        "Welcome to Funky Fishing!\n\n" +
        "This tutorial will teach the basics.\n\n" +
        "Click, press Xbox A, or press Joy-Con confirm to continue.";
    [SerializeField, TextArea(3, 8)] private string castTargetMessage =
        "Move the joystick to position your bobber\n\n" +
        "Press confirm.";
    [SerializeField, TextArea(3, 8)] private string castMessage =
        "Nice! Now let's learn how to cast:\n\n" +
        "- Xbox: press A to cast the line\n" +
        "- Joy-Con: do a forward casting motion (like a real fishing rod) to cast the line\n\n" +
        "Press confirm to continue.";
    [SerializeField, TextArea(3, 8)] private string yankMessage =
        "Great cast! Now to bring the bobber back in:\n\n" +
        "- Xbox: press A again to yank\n" +
        "- Joy-Con: do a quick backward yank motion (as if you were really pulling the bobber back) to bring the line in\n\n" +
        "Press confirm to continue.";
    [SerializeField, TextArea(3, 8)] private string catchMessage =
        "Cool! A fish has appeared.\n\n" +
        "How to catch it:\n" +
        "1. Move your cast target close to the fish.\n" +
        "2. Cast near the fish.\n" +
        "3. When the fish bites and pulls the bobber down, yank to hook/catch it.\n\n" +
        "Press confirm to continue.";

    [Header("Style")]
    [SerializeField] private Vector2 welcomePanelSize = new Vector2(860f, 360f);
    [SerializeField] private Vector2 castTargetPanelSize = new Vector2(980f, 520f);
    [SerializeField] private Vector2 castTargetPanelSizeWithImage = new Vector2(760f, 520f);
    [SerializeField] private Color backdropColor = new Color(0f, 0f, 0f, 0f);
    [SerializeField] private Color panelColor = new Color(0.05f, 0.05f, 0.05f, 0.92f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private int fontSize = 40;
    [SerializeField] private int minFontSize = 24;

    [Header("Tutorial Images")]
    [SerializeField] private Vector2 castTargetTutorialImageSize = new Vector2(560f, 460f);
    [SerializeField] private Vector2 castTargetTutorialImageOffset = new Vector2(680f, 0f);
    [SerializeField] private float tutorialImageStepsLeftShift = 140f;
    [SerializeField] private string castTargetTutorialImageResourcePath = "Tutorial/tut1";
    [SerializeField] private string castTargetTutorialImageEditorAssetPath = "Assets/Images/Tutorial/tut1.jpg";
    [SerializeField] private string castTutorialImageResourcePath = "Tutorial/tut2";
    [SerializeField] private string castTutorialImageEditorAssetPath = "Assets/Images/Tutorial/tut2.jpg";
    [SerializeField] private string yankTutorialImageResourcePath = "Tutorial/tut3";
    [SerializeField] private string yankTutorialImageEditorAssetPath = "Assets/Images/Tutorial/tut3.jpg";

    private Canvas gateCanvas;
    private GameObject gateRoot;
    private TextMeshProUGUI gateText;
    private RectTransform gatePanelRect;
    private RectTransform castTargetTutorialImageRect;
    private Image castTargetTutorialImage;
    private Image gateImage;
    private GameObject welcomeTutorialUi;
    private GameObject bobberTutorialUi;
    private GameObject castTutorialUi;
    private GameObject yankTutorialUi;
    private GameObject catchTutorialUi;
    private bool gateActive;
    private int gateStepIndex;
    private TutorialFlowState flowState;
    private PondManager pondManager;
    private CursorCastTargeting castTargeting;
    private BobberArcCaster bobberArcCaster;
    private bool bobberStateSampleReady;
    private bool hasObservedCastSinceAwaitingYank;
    private bool yankStateSampleReady;
    private bool hasObservedRetractingSinceAwaitingCatch;
    private BobberArcCaster.State lastBobberState = BobberArcCaster.State.Idle;
    private bool castTargetBaselineReady;
    private Vector3 castTargetBaselinePoint;
    private float showCastHintAtUnscaledTime = -1f;
    private float showYankHintAtUnscaledTime = -1f;
    private float showCatchHintAtUnscaledTime = -1f;
    private bool tutorialFishSpawned;
    private bool loggedMissingCastTargetTutorialImage;
    private bool loggedMissingCastTutorialImage;
    private bool loggedMissingYankTutorialImage;
    private bool cachedCursorVisible;
    private CursorLockMode cachedCursorLockMode;
    private bool cursorStateCached;
    private readonly List<PauseManager> disabledPauseManagers = new List<PauseManager>();
    private Sprite stepOneTutorialSprite;
    private Sprite castTargetTutorialSprite;
    private Sprite castTutorialSprite;
    private Sprite yankTutorialSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        if (sceneHookRegistered)
            return;

        sceneHookRegistered = true;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TrySpawnForTutorialScene();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void TrySpawnAfterInitialSceneLoad()
    {
        TrySpawnForTutorialScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Tutorial_Level")
            return;

        TrySpawnForTutorialScene();
    }

    private static void TrySpawnForTutorialScene()
    {
        Scene tutorialScene = SceneManager.GetSceneByName("Tutorial_Level");
        if (!tutorialScene.IsValid() || !tutorialScene.isLoaded)
            return;

        if (FindObjectOfType<TutorialStartGate>() != null)
            return;

        GameObject gateObject = new GameObject("TutorialStartGate");
        SceneManager.MoveGameObjectToScene(gateObject, tutorialScene);
        gateObject.AddComponent<TutorialStartGate>();
    }

    private void Awake()
    {
        if (!IsTutorialScene())
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

        ResolveBobberTutorialUi();
        BuildGateUi();
        flowState = TutorialFlowState.StartupGate;
        ActivateGate(0);
    }

    private void Update()
    {
        if (!IsTutorialScene())
        {
            if (gateActive)
                DismissGate();
            return;
        }

        EnsureNoFishBeforeCatchPhase();

        if (!gateActive)
        {
            UpdatePostGateFlow();
            return;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (WasConfirmPressedThisFrame())
            AdvanceGateOrDismiss();
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
            activeInstance = null;

        if (welcomeTutorialUi != null)
            welcomeTutorialUi.SetActive(false);
        if (bobberTutorialUi != null)
            bobberTutorialUi.SetActive(false);
        if (castTutorialUi != null)
            castTutorialUi.SetActive(false);
        if (yankTutorialUi != null)
            yankTutorialUi.SetActive(false);
        if (catchTutorialUi != null)
            catchTutorialUi.SetActive(false);

        if (gateActive)
            Time.timeScale = 1f;

        RestorePauseManagers();

        if (cursorStateCached)
        {
            Cursor.visible = cachedCursorVisible;
            Cursor.lockState = cachedCursorLockMode;
            cursorStateCached = false;
        }

        if (castTargetTutorialSprite != null)
        {
            Destroy(castTargetTutorialSprite);
            castTargetTutorialSprite = null;
        }

        if (castTutorialSprite != null)
        {
            Destroy(castTutorialSprite);
            castTutorialSprite = null;
        }

        if (yankTutorialSprite != null)
        {
            Destroy(yankTutorialSprite);
            yankTutorialSprite = null;
        }
    }

    public static bool IsOverlayGateActive()
    {
        return activeInstance != null && activeInstance.gateActive;
    }

    private bool IsTutorialScene()
    {
        Scene tutorialScene = SceneManager.GetSceneByName(tutorialSceneName);
        return tutorialScene.IsValid() &&
               tutorialScene.isLoaded &&
               gameObject.scene == tutorialScene;
    }

    private void ActivateGate(int stepIndex)
    {
        CacheCursorState();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Time.timeScale = 0f;
        gateActive = true;
        gateStepIndex = stepIndex;
        DisablePauseManagers();
        RefreshGateText();

        if (gateCanvas != null)
            gateCanvas.enabled = true;
    }

    private void DismissGate()
    {
        if (!gateActive)
            return;

        gateActive = false;
        if (welcomeTutorialUi != null)
            welcomeTutorialUi.SetActive(false);
        if (bobberTutorialUi != null)
            bobberTutorialUi.SetActive(false);
        if (castTutorialUi != null)
            castTutorialUi.SetActive(false);
        if (yankTutorialUi != null)
            yankTutorialUi.SetActive(false);
        if (catchTutorialUi != null)
            catchTutorialUi.SetActive(false);
        Time.timeScale = 1f;
        XboxFishingInput.BlockGameplayInputForRealtimeSeconds(resumeInputBlockSeconds);
        RestorePauseManagers();

        if (gateCanvas != null)
            gateCanvas.enabled = false;

        if (cursorStateCached)
        {
            Cursor.visible = cachedCursorVisible;
            Cursor.lockState = cachedCursorLockMode;
            cursorStateCached = false;
        }
    }

    private bool WasConfirmPressedThisFrame()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            return true;

        Gamepad pad = Gamepad.current;
        if (pad != null && pad.buttonSouth.wasPressedThisFrame)
            return true;

        if (JoyConMenuInput.SubmitPressedThisFrame)
            return true;

        return false;
    }

    private void CacheCursorState()
    {
        cachedCursorVisible = Cursor.visible;
        cachedCursorLockMode = Cursor.lockState;
        cursorStateCached = true;
    }

    private void AdvanceGateOrDismiss()
    {
        if (gateStepIndex == 0)
        {
            gateStepIndex = 1;
            RefreshGateText();
            return;
        }

        if (gateStepIndex == 1)
        {
            DismissGate();
            BeginAwaitCastingTargetMove();
            return;
        }

        if (gateStepIndex == 2)
        {
            DismissGate();
            BeginAwaitSuccessfulCast();
            return;
        }

        if (gateStepIndex == 3)
        {
            DismissGate();
            BeginAwaitSuccessfulYank();
            return;
        }

        DismissGate();
        flowState = TutorialFlowState.Complete;
    }

    private void RefreshGateText()
    {
        if (gateText == null)
            return;

        bool showWelcomeTutorialObject = gateStepIndex == 0 && welcomeTutorialUi != null;
        bool showBobberTutorialObject = gateStepIndex == 1 && bobberTutorialUi != null;
        bool showCastTutorialObject = gateStepIndex == 2 && castTutorialUi != null;
        bool showYankTutorialObject = gateStepIndex == 3 && yankTutorialUi != null;
        bool showCatchTutorialObject = gateStepIndex == 4 && catchTutorialUi != null;
        if (welcomeTutorialUi != null)
            welcomeTutorialUi.SetActive(showWelcomeTutorialObject);
        if (bobberTutorialUi != null)
            bobberTutorialUi.SetActive(showBobberTutorialObject);
        if (castTutorialUi != null)
            castTutorialUi.SetActive(showCastTutorialObject);
        if (yankTutorialUi != null)
            yankTutorialUi.SetActive(showYankTutorialObject);
        if (catchTutorialUi != null)
            catchTutorialUi.SetActive(showCatchTutorialObject);

        if (gatePanelRect != null)
            gatePanelRect.gameObject.SetActive(!showWelcomeTutorialObject && !showBobberTutorialObject && !showCastTutorialObject && !showYankTutorialObject && !showCatchTutorialObject);

        if (gateStepIndex == 0)
        {
            gateText.text = welcomeMessage;
            gateText.gameObject.SetActive(!showWelcomeTutorialObject);
        }
        else if (gateStepIndex == 1)
        {
            gateText.gameObject.SetActive(!showBobberTutorialObject);
        }
        else if (gateStepIndex == 2)
        {
            gateText.text = castMessage;
            gateText.gameObject.SetActive(!showCastTutorialObject);
        }
        else if (gateStepIndex == 3)
        {
            gateText.text = yankMessage;
            gateText.gameObject.SetActive(!showYankTutorialObject);
        }
        else
        {
            gateText.text = catchMessage;
            gateText.gameObject.SetActive(!showCatchTutorialObject);
        }

        if (gatePanelRect != null)
            gatePanelRect.sizeDelta = gateStepIndex == 0 ? welcomePanelSize : castTargetPanelSize;

        RefreshGateImage();
    }

    private void ResolveBobberTutorialUi()
    {
        Scene tutorialScene = SceneManager.GetSceneByName(tutorialSceneName);
        if (!tutorialScene.IsValid() || !tutorialScene.isLoaded)
            return;

        GameObject canvasObject = FindSceneGameObject(tutorialScene, "Canvas");
        if (canvasObject == null)
            return;

        Transform welcomeTutorialTransform = canvasObject.transform.Find("WelcomeTutorial");
        if (welcomeTutorialTransform != null)
        {
            welcomeTutorialUi = welcomeTutorialTransform.gameObject;
            welcomeTutorialUi.SetActive(false);
        }

        Transform bobberTutorialTransform = canvasObject.transform.Find("BobberTutorial");
        if (bobberTutorialTransform != null)
        {
            bobberTutorialUi = bobberTutorialTransform.gameObject;
            bobberTutorialUi.SetActive(false);
        }

        Transform castTutorialTransform = canvasObject.transform.Find("CastTutorial");
        if (castTutorialTransform != null)
        {
            castTutorialUi = castTutorialTransform.gameObject;
            castTutorialUi.SetActive(false);
        }

        Transform yankTutorialTransform = canvasObject.transform.Find("YankTutorial");
        if (yankTutorialTransform != null)
        {
            yankTutorialUi = yankTutorialTransform.gameObject;
            yankTutorialUi.SetActive(false);
        }

        Transform catchTutorialTransform = canvasObject.transform.Find("CatchTutorial");
        if (catchTutorialTransform != null)
        {
            catchTutorialUi = catchTutorialTransform.gameObject;
            catchTutorialUi.SetActive(false);
        }
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

    private void RefreshGateImage()
    {
        if (gateImage == null)
            return;

        gateImage.sprite = gateStepIndex == 1 ? GetStepOneTutorialSprite() : null;
        gateImage.enabled = gateImage.sprite != null;
    }

    private void UpdatePostGateFlow()
    {
        if (flowState == TutorialFlowState.AwaitCastingTargetMove)
        {
            if (HasMovedCastingTarget())
            {
                flowState = TutorialFlowState.AwaitCastHintDelay;
                showCastHintAtUnscaledTime = Time.unscaledTime + castHintDelaySeconds;
            }
            return;
        }

        if (flowState == TutorialFlowState.AwaitCastHintDelay)
        {
            if (Time.unscaledTime >= showCastHintAtUnscaledTime)
            {
                flowState = TutorialFlowState.CastHintGate;
                ActivateGate(2);
            }
            return;
        }

        if (flowState == TutorialFlowState.AwaitSuccessfulCast)
        {
            if (HasSuccessfullyCast())
            {
                flowState = TutorialFlowState.AwaitYankHintDelay;
                showYankHintAtUnscaledTime = Time.unscaledTime + yankHintDelayAfterLandSeconds;
            }
            return;
        }

        if (flowState == TutorialFlowState.AwaitYankHintDelay)
        {
            if (Time.unscaledTime >= showYankHintAtUnscaledTime)
            {
                flowState = TutorialFlowState.YankHintGate;
                ActivateGate(3);
            }
            return;
        }

        if (flowState == TutorialFlowState.AwaitSuccessfulYank)
        {
            if (HasSuccessfullyYanked())
            {
                if (SpawnSingleTutorialFish())
                {
                    flowState = TutorialFlowState.AwaitCatchHintDelay;
                    showCatchHintAtUnscaledTime = Time.unscaledTime + catchHintDelayAfterSpawnSeconds;
                }
            }
            return;
        }

        if (flowState == TutorialFlowState.AwaitCatchHintDelay)
        {
            if (Time.unscaledTime >= showCatchHintAtUnscaledTime)
            {
                flowState = TutorialFlowState.CatchHintGate;
                ActivateGate(4);
            }
        }
    }

    private void BeginAwaitCastingTargetMove()
    {
        flowState = TutorialFlowState.AwaitCastingTargetMove;
        castTargetBaselineReady = false;
        showCastHintAtUnscaledTime = -1f;
        castTargeting = FindObjectOfType<CursorCastTargeting>();
    }

    private void BeginAwaitSuccessfulCast()
    {
        flowState = TutorialFlowState.AwaitSuccessfulCast;
        bobberArcCaster = FindObjectOfType<BobberArcCaster>();
        bobberStateSampleReady = false;
        hasObservedCastSinceAwaitingYank = false;
        showYankHintAtUnscaledTime = -1f;
        lastBobberState = BobberArcCaster.State.Idle;
    }

    private void BeginAwaitSuccessfulYank()
    {
        flowState = TutorialFlowState.AwaitSuccessfulYank;
        bobberArcCaster = FindObjectOfType<BobberArcCaster>();
        yankStateSampleReady = false;
        hasObservedRetractingSinceAwaitingCatch = false;
        lastBobberState = BobberArcCaster.State.Idle;
    }

    private bool HasMovedCastingTarget()
    {
        if (castTargeting == null)
            castTargeting = FindObjectOfType<CursorCastTargeting>();

        if (castTargeting == null || !castTargeting.HasTarget)
            return false;

        if (!castTargetBaselineReady)
        {
            castTargetBaselinePoint = castTargeting.CurrentTargetPoint;
            castTargetBaselineReady = true;
            return false;
        }

        float movedDistance = Vector3.Distance(castTargeting.CurrentTargetPoint, castTargetBaselinePoint);
        return movedDistance >= targetMoveDistanceThreshold;
    }

    private bool HasSuccessfullyCast()
    {
        if (bobberArcCaster == null)
            bobberArcCaster = FindObjectOfType<BobberArcCaster>();

        if (bobberArcCaster == null)
            return false;

        BobberArcCaster.State currentState = bobberArcCaster.CurrentState;
        if (!bobberStateSampleReady)
        {
            lastBobberState = currentState;
            bobberStateSampleReady = true;
            hasObservedCastSinceAwaitingYank = currentState != BobberArcCaster.State.Idle;
            return hasObservedCastSinceAwaitingYank && currentState == BobberArcCaster.State.Landed;
        }

        bool transitionedOutOfIdle =
            lastBobberState == BobberArcCaster.State.Idle &&
            currentState != BobberArcCaster.State.Idle;
        if (transitionedOutOfIdle)
            hasObservedCastSinceAwaitingYank = true;

        bool transitionedToLanded =
            lastBobberState != BobberArcCaster.State.Landed &&
            currentState == BobberArcCaster.State.Landed;

        // If the player aborted back to idle before landing, require another cast.
        if (hasObservedCastSinceAwaitingYank &&
            lastBobberState != BobberArcCaster.State.Idle &&
            currentState == BobberArcCaster.State.Idle)
        {
            hasObservedCastSinceAwaitingYank = false;
        }

        lastBobberState = currentState;
        return hasObservedCastSinceAwaitingYank && transitionedToLanded;
    }

    private bool HasSuccessfullyYanked()
    {
        if (bobberArcCaster == null)
            bobberArcCaster = FindObjectOfType<BobberArcCaster>();

        if (bobberArcCaster == null)
            return false;

        BobberArcCaster.State currentState = bobberArcCaster.CurrentState;
        if (!yankStateSampleReady)
        {
            lastBobberState = currentState;
            yankStateSampleReady = true;
            hasObservedRetractingSinceAwaitingCatch = currentState == BobberArcCaster.State.Retracting;
            return hasObservedRetractingSinceAwaitingCatch && currentState == BobberArcCaster.State.Idle;
        }

        if (currentState == BobberArcCaster.State.Retracting)
            hasObservedRetractingSinceAwaitingCatch = true;

        bool returnedToIdle =
            lastBobberState != BobberArcCaster.State.Idle &&
            currentState == BobberArcCaster.State.Idle;

        lastBobberState = currentState;
        return hasObservedRetractingSinceAwaitingCatch && returnedToIdle;
    }

    private void EnsureNoFishBeforeCatchPhase()
    {
        if (tutorialFishSpawned)
            return;

        if (pondManager == null)
            pondManager = FindObjectOfType<PondManager>();

        if (pondManager == null || pondManager.fishList == null)
            return;

        for (int i = pondManager.fishList.Count - 1; i >= 0; i--)
        {
            GameObject fish = pondManager.fishList[i];
            if (fish == null)
            {
                pondManager.fishList.RemoveAt(i);
                continue;
            }

            pondManager.RemoveFish(fish);
        }
    }

    private bool SpawnSingleTutorialFish()
    {
        if (tutorialFishSpawned)
            return true;

        if (pondManager == null)
            pondManager = FindObjectOfType<PondManager>();

        if (pondManager == null || pondManager.fishPrefabs == null || pondManager.fishPrefabs.Length == 0)
        {
            Debug.LogWarning("TutorialStartGate: Cannot spawn tutorial fish, PondManager or fish prefabs missing.");
            return false;
        }

        if (pondManager.fishList == null)
            pondManager.fishList = new List<GameObject>();

        int spawnCount = Mathf.Max(1, tutorialCatchFishSpawnCount);
        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * pondManager.radius;
            Vector3 spawnPosition = new Vector3(
                pondManager.transform.position.x + randomCircle.x,
                pondManager.waterlevel,
                pondManager.transform.position.z + randomCircle.y);

            int randomIndex = Random.Range(0, pondManager.fishPrefabs.Length);
            GameObject fish = Instantiate(pondManager.fishPrefabs[randomIndex], spawnPosition, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(fish, pondManager.gameObject.scene);

            FishMovement movement = fish.GetComponent<FishMovement>();
            if (movement != null)
                movement.pondManager = pondManager;

            pondManager.fishList.Add(fish);
        }

        tutorialFishSpawned = true;
        return true;
    }

    private void BuildGateUi()
    {
        if (gateCanvas != null)
            return;

        gateRoot = new GameObject(
            "TutorialStartGateCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        gateRoot.transform.SetParent(transform, false);

        gateCanvas = gateRoot.GetComponent<Canvas>();
        gateCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        gateCanvas.sortingOrder = 1000;

        CanvasScaler scaler = gateRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backdrop.transform.SetParent(gateRoot.transform, false);
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
        panelRect.sizeDelta = welcomePanelSize;
        panelRect.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = panelColor;

        GameObject tutorialImageObject = new GameObject("CastTargetTutorialImage", typeof(RectTransform), typeof(Image));
        tutorialImageObject.transform.SetParent(backdrop.transform, false);
        castTargetTutorialImageRect = tutorialImageObject.GetComponent<RectTransform>();
        castTargetTutorialImageRect.anchorMin = new Vector2(0.5f, 0.5f);
        castTargetTutorialImageRect.anchorMax = new Vector2(0.5f, 0.5f);
        castTargetTutorialImageRect.pivot = new Vector2(0.5f, 0.5f);
        castTargetTutorialImageRect.sizeDelta = castTargetTutorialImageSize;
        castTargetTutorialImageRect.anchoredPosition = castTargetTutorialImageOffset;

        castTargetTutorialImage = tutorialImageObject.GetComponent<Image>();
        castTargetTutorialImage.color = Color.white;
        castTargetTutorialImage.preserveAspect = true;
        castTargetTutorialImage.raycastTarget = false;
        castTargetTutorialImage.enabled = false;

        GameObject textObject = new GameObject("WelcomeText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(40f, 220f);
        textRect.offsetMax = new Vector2(-40f, -28f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        gateText = text;
        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
        text.text = welcomeMessage;
        text.enableAutoSizing = true;
        text.fontSizeMax = fontSize;
        text.fontSizeMin = minFontSize;
        text.fontSize = fontSize;
        text.color = textColor;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;

        GameObject imageObject = new GameObject("GateImage", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(panel.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 0f);
        imageRect.anchorMax = new Vector2(0.5f, 0f);
        imageRect.pivot = new Vector2(0.5f, 0f);
        imageRect.anchoredPosition = new Vector2(0f, 28f);
        imageRect.sizeDelta = new Vector2(320f, 160f);

        gateImage = imageObject.GetComponent<Image>();
        gateImage.preserveAspect = true;
        gateImage.enabled = false;
    }

    private Sprite GetStepOneTutorialSprite()
    {
        if (stepOneTutorialSprite != null)
            return stepOneTutorialSprite;

        stepOneTutorialSprite = Resources.Load<Sprite>("Images/tut_img_1");
#if UNITY_EDITOR
        if (stepOneTutorialSprite == null)
            stepOneTutorialSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/tut_img_1.png");
#endif
        return stepOneTutorialSprite;
    }

    private void RefreshGateVisuals()
    {
        bool showTutorialImage = gateStepIndex == 1 || gateStepIndex == 2 || gateStepIndex == 3;
        float layoutShiftX = showTutorialImage ? -Mathf.Abs(tutorialImageStepsLeftShift) : 0f;

        if (gatePanelRect != null)
        {
            if (gateStepIndex == 0)
                gatePanelRect.sizeDelta = welcomePanelSize;
            else if (gateStepIndex == 1 || gateStepIndex == 2 || gateStepIndex == 3)
                gatePanelRect.sizeDelta = castTargetPanelSizeWithImage;
            else
                gatePanelRect.sizeDelta = castTargetPanelSize;

            gatePanelRect.anchoredPosition = new Vector2(layoutShiftX, 0f);
        }

        if (castTargetTutorialImageRect != null)
        {
            castTargetTutorialImageRect.anchoredPosition = castTargetTutorialImageOffset + new Vector2(layoutShiftX, 0f);
        }

        if (castTargetTutorialImage == null)
            return;

        Sprite tutorialSprite = null;
        if (gateStepIndex == 1)
            tutorialSprite = LoadCastTargetTutorialSprite();
        else if (gateStepIndex == 2)
            tutorialSprite = LoadCastTutorialSprite();
        else if (gateStepIndex == 3)
            tutorialSprite = LoadYankTutorialSprite();

        castTargetTutorialImage.sprite = tutorialSprite;
        if (castTargetTutorialImageRect != null)
            castTargetTutorialImageRect.sizeDelta = GetTutorialImageDisplaySize(tutorialSprite);
        castTargetTutorialImage.enabled = showTutorialImage && tutorialSprite != null;
    }

    private Vector2 GetTutorialImageDisplaySize(Sprite sprite)
    {
        if (sprite == null)
            return castTargetTutorialImageSize;

        Rect spriteRect = sprite.rect;
        if (spriteRect.width <= 0f || spriteRect.height <= 0f)
            return castTargetTutorialImageSize;

        float scaleByWidth = castTargetTutorialImageSize.x / spriteRect.width;
        float scaleByHeight = castTargetTutorialImageSize.y / spriteRect.height;
        float uniformScale = Mathf.Min(scaleByWidth, scaleByHeight);
        if (float.IsNaN(uniformScale) || float.IsInfinity(uniformScale) || uniformScale <= 0f)
            return castTargetTutorialImageSize;

        return new Vector2(spriteRect.width * uniformScale, spriteRect.height * uniformScale);
    }

    private Sprite LoadCastTargetTutorialSprite()
    {
        if (castTargetTutorialSprite != null)
            return castTargetTutorialSprite;

        Texture2D texture = null;
        if (!string.IsNullOrWhiteSpace(castTargetTutorialImageResourcePath))
            texture = Resources.Load<Texture2D>(castTargetTutorialImageResourcePath);

#if UNITY_EDITOR
        if (texture == null && !string.IsNullOrWhiteSpace(castTargetTutorialImageEditorAssetPath))
            texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(castTargetTutorialImageEditorAssetPath);
#endif

        if (texture == null)
        {
            if (!loggedMissingCastTargetTutorialImage)
            {
                Debug.LogWarning(
                    "TutorialStartGate: Couldn't load cast-target tutorial image. " +
                    "Expected Resources path '" + castTargetTutorialImageResourcePath + "' " +
                    "or editor asset '" + castTargetTutorialImageEditorAssetPath + "'.");
                loggedMissingCastTargetTutorialImage = true;
            }
            return null;
        }

        castTargetTutorialSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        castTargetTutorialSprite.name = "CastTargetTutorialSprite";
        return castTargetTutorialSprite;
    }

    private Sprite LoadCastTutorialSprite()
    {
        if (castTutorialSprite != null)
            return castTutorialSprite;

        Texture2D texture = null;
        if (!string.IsNullOrWhiteSpace(castTutorialImageResourcePath))
            texture = Resources.Load<Texture2D>(castTutorialImageResourcePath);

#if UNITY_EDITOR
        if (texture == null && !string.IsNullOrWhiteSpace(castTutorialImageEditorAssetPath))
            texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(castTutorialImageEditorAssetPath);
#endif

        if (texture == null)
        {
            if (!loggedMissingCastTutorialImage)
            {
                Debug.LogWarning(
                    "TutorialStartGate: Couldn't load cast tutorial image. " +
                    "Expected Resources path '" + castTutorialImageResourcePath + "' " +
                    "or editor asset '" + castTutorialImageEditorAssetPath + "'.");
                loggedMissingCastTutorialImage = true;
            }
            return null;
        }

        castTutorialSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        castTutorialSprite.name = "CastTutorialSprite";
        return castTutorialSprite;
    }

    private Sprite LoadYankTutorialSprite()
    {
        if (yankTutorialSprite != null)
            return yankTutorialSprite;

        Texture2D texture = null;
        if (!string.IsNullOrWhiteSpace(yankTutorialImageResourcePath))
            texture = Resources.Load<Texture2D>(yankTutorialImageResourcePath);

#if UNITY_EDITOR
        if (texture == null && !string.IsNullOrWhiteSpace(yankTutorialImageEditorAssetPath))
            texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(yankTutorialImageEditorAssetPath);
#endif

        if (texture == null)
        {
            if (!loggedMissingYankTutorialImage)
            {
                Debug.LogWarning(
                    "TutorialStartGate: Couldn't load yank tutorial image. " +
                    "Expected Resources path '" + yankTutorialImageResourcePath + "' " +
                    "or editor asset '" + yankTutorialImageEditorAssetPath + "'.");
                loggedMissingYankTutorialImage = true;
            }
            return null;
        }

        yankTutorialSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        yankTutorialSprite.name = "YankTutorialSprite";
        return yankTutorialSprite;
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

    public static bool IsCastAllowedByTutorial()
    {
        if (activeInstance == null)
            return true;

        return activeInstance.IsCastAllowedForCurrentFlow();
    }

    public static bool IsYankAllowedByTutorial()
    {
        if (activeInstance == null)
            return true;

        return activeInstance.IsYankAllowedForCurrentFlow();
    }

    private bool IsCastAllowedForCurrentFlow()
    {
        if (!IsTutorialScene())
            return true;

        switch (flowState)
        {
            case TutorialFlowState.StartupGate:
            case TutorialFlowState.AwaitCastingTargetMove:
            case TutorialFlowState.AwaitCastHintDelay:
            case TutorialFlowState.CastHintGate:
                return false;
            default:
                return true;
        }
    }

    private bool IsYankAllowedForCurrentFlow()
    {
        if (!IsTutorialScene())
            return true;

        switch (flowState)
        {
            case TutorialFlowState.StartupGate:
            case TutorialFlowState.AwaitCastingTargetMove:
            case TutorialFlowState.AwaitCastHintDelay:
            case TutorialFlowState.CastHintGate:
            case TutorialFlowState.AwaitSuccessfulCast:
            case TutorialFlowState.AwaitYankHintDelay:
            case TutorialFlowState.YankHintGate:
                return false;
            default:
                return true;
        }
    }
}
