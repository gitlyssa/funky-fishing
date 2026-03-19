using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

public class PauseManager : MonoBehaviour
{
    private const string PauseOverlayCanvasName = "PauseOverlayCanvas";
    private const int PauseMenuSortingOrder = 30000;

    public GameObject PausePanel;
    public GameObject ControlsPanel;

    [Header("Controller")]
    [SerializeField] private bool allowControllerPauseToggle = true;
    [SerializeField] private bool allowControllerCancelBack = true;
    [SerializeField, Min(0f)] private float resumeInputBlockSeconds = 0.25f;
    [SerializeField, Min(0f)] private float menuExitInputBlockSeconds = 0.35f;

    [Header("UI Selection")]
    [SerializeField] private GameObject pauseDefaultSelected;
    [SerializeField] private GameObject controlsDefaultSelected;
    [SerializeField] private string pauseDefaultSelectedName = "ResumeButton";
    [SerializeField] private string controlsDefaultSelectedName = "BackButtton";

    private bool isPaused = false;
    private bool isStandaloneMenuMode = false;
    private Coroutine selectRoutine;
    private RhythmMusicPlayer rhythmMusicPlayer;
    private float timeScaleBeforePause = 1f;
    private bool resumeRhythmAfterPause;
    private RectTransform pauseOverlayRoot;
    private Canvas pauseOverlayCanvas;
    private CanvasScaler pauseOverlayScaler;
    private GraphicRaycaster pauseOverlayRaycaster;
    private Canvas pauseSourceCanvas;
    private CanvasScaler pauseSourceScaler;

    void Awake()
    {
        isPaused = false;
        Time.timeScale = 1f;

        EnsurePauseUiForeground();

        if (PausePanel != null)
            PausePanel.SetActive(false);

        if (ControlsPanel != null)
            ControlsPanel.SetActive(false);

        DisableDecorativePauseOverlayRaycasts();
        ResolveSelectionReferences();
        ClearSelectedObject();
    }

    void Update()
    {
        if (isStandaloneMenuMode)
        {
            if (WasCancelBackPressed())
            {
                if (TryCloseTuningPanel())
                    return;

                GoToMainMenu();
                return;
            }

            if (!TryEnsureTuningSelection())
                EnsurePausedSelection();

            return;
        }

        if (WasPauseTogglePressed())
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
            return;
        }

        if (isPaused && WasCancelBackPressed())
        {
            if (TryCloseTuningPanel())
                return;

            if (IsControlsOpen())
                BackToPauseMenu();
            else
                ResumeGame();
            return;
        }

        if (isPaused)
        {
            if (!TryEnsureTuningSelection())
                EnsurePausedSelection();
        }
    }

    public void EnterStandaloneMenuMode()
    {
        isStandaloneMenuMode = true;
        allowControllerPauseToggle = false;
        allowControllerCancelBack = true;
        isPaused = true;

        if (PausePanel != null)
            PausePanel.SetActive(true);

        if (ControlsPanel != null)
            ControlsPanel.SetActive(false);

        EnsurePauseUiForeground();

        Time.timeScale = 1f;
        ResolveSelectionReferences();
        ClearSelectedObject();
    }

    public void PauseGame()
    {
        timeScaleBeforePause = Time.timeScale;
        EnsurePauseUiForeground();
        PausePanel.SetActive(true);
        if (ControlsPanel != null)
            ControlsPanel.SetActive(false);
        Time.timeScale = 0f;
        isPaused = true;
        ResolveRhythmMusicPlayer();
        resumeRhythmAfterPause = rhythmMusicPlayer != null && !rhythmMusicPlayer.IsPausedForGamePause;
        if (rhythmMusicPlayer != null)
            rhythmMusicPlayer.PauseRhythmForGamePause();
        ResolveSelectionReferences();
        QueueSelect(pauseDefaultSelected);
    }

    public void ResumeGame()
    {
        FunkyAudioSettings.PlayUiConfirm();
        PausePanel.SetActive(false);
        if (ControlsPanel != null)
            ControlsPanel.SetActive(false);
        XboxFishingInput.BlockGameplayInputForRealtimeSeconds(resumeInputBlockSeconds);
        Time.timeScale = timeScaleBeforePause;
        isPaused = false;
        ResolveRhythmMusicPlayer();
        if (resumeRhythmAfterPause && rhythmMusicPlayer != null)
            rhythmMusicPlayer.ResumeRhythmFromGamePause();
        resumeRhythmAfterPause = false;
        StopSelectionRoutine();
        ClearSelectedObject();
    }

    public void GoToMainMenu()
    {
        FunkyAudioSettings.PlayUiConfirm();
        // Prevent the same confirm press from reaching gameplay input on this frame.
        XboxFishingInput.BlockGameplayInputForRealtimeSeconds(Mathf.Max(menuExitInputBlockSeconds, resumeInputBlockSeconds));
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void OpenControls()
    {
        FunkyAudioSettings.PlayUiConfirm();
        EnsurePauseUiForeground();
        PausePanel.SetActive(false);
        if (ControlsPanel != null)
            ControlsPanel.SetActive(true);
        ResolveSelectionReferences();
        QueueSelect(controlsDefaultSelected);
    }

    public void BackToPauseMenu()
    {
        FunkyAudioSettings.PlayUiConfirm();
        EnsurePauseUiForeground();
        if (ControlsPanel != null)
            ControlsPanel.SetActive(false);
        PausePanel.SetActive(true);
        ResolveSelectionReferences();
        QueueSelect(pauseDefaultSelected);
    }

    private bool WasPauseTogglePressed()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            return true;

        if (allowControllerPauseToggle && JoyConMenuInput.PausePressedThisFrame)
            return true;

        if (!allowControllerPauseToggle)
            return false;

        Gamepad pad = Gamepad.current;
        return pad != null && pad.startButton.wasPressedThisFrame;
    }

    private bool WasCancelBackPressed()
    {
        if (!allowControllerCancelBack)
            return false;

        Gamepad pad = Gamepad.current;
        return pad != null && pad.buttonEast.wasPressedThisFrame;
    }

    private bool IsControlsOpen()
    {
        return ControlsPanel != null && ControlsPanel.activeInHierarchy;
    }

    private bool TryCloseTuningPanel()
    {
        PauseOptionsPanel optionsPanel = GetComponent<PauseOptionsPanel>();
        if (optionsPanel != null && optionsPanel.IsOptionsPanelOpen())
        {
            optionsPanel.CloseOptionsPanel();
            return true;
        }

        FishingPauseTuningPanel fishing = GetComponent<FishingPauseTuningPanel>();
        if (fishing != null && fishing.IsTuningPanelOpen())
        {
            fishing.CloseTuningPanel();
            return true;
        }

        RhythmPauseTuningPanel tuning = GetComponent<RhythmPauseTuningPanel>();
        if (tuning == null || !tuning.IsTuningPanelOpen())
            return false;

        tuning.CloseTuningPanel();
        return true;
    }

    private bool TryEnsureTuningSelection()
    {
        PauseOptionsPanel optionsPanel = GetComponent<PauseOptionsPanel>();
        if (optionsPanel != null && optionsPanel.IsOptionsPanelOpen())
        {
            optionsPanel.EnsureSelection();
            return true;
        }

        FishingPauseTuningPanel fishing = GetComponent<FishingPauseTuningPanel>();
        if (fishing != null && fishing.IsTuningPanelOpen())
        {
            fishing.EnsureSelection();
            return true;
        }

        RhythmPauseTuningPanel tuning = GetComponent<RhythmPauseTuningPanel>();
        if (tuning == null || !tuning.IsTuningPanelOpen())
            return false;

        tuning.EnsureSelection();
        return true;
    }

    private void EnsurePausedSelection()
    {
        EventSystem evt = EventSystem.current;
        if (evt == null || evt.currentSelectedGameObject != null)
            return;

        if (IsControlsOpen())
            QueueSelect(controlsDefaultSelected);
        else if (PausePanel != null && PausePanel.activeInHierarchy)
            QueueSelect(pauseDefaultSelected);
    }

    private void ResolveSelectionReferences()
    {
        if (PausePanel != null && pauseDefaultSelected == null)
            pauseDefaultSelected = FindByNameRecursive(PausePanel.transform, pauseDefaultSelectedName);

        if (ControlsPanel != null && controlsDefaultSelected == null)
            controlsDefaultSelected = FindByNameRecursive(ControlsPanel.transform, controlsDefaultSelectedName);
    }

    private void ResolveRhythmMusicPlayer()
    {
        if (rhythmMusicPlayer == null)
            rhythmMusicPlayer = FindObjectOfType<RhythmMusicPlayer>();
    }

    public bool IsPauseUiOpen()
    {
        if (!isPaused)
            return false;

        if (PausePanel != null && PausePanel.activeInHierarchy)
            return true;

        if (ControlsPanel != null && ControlsPanel.activeInHierarchy)
            return true;

        return isStandaloneMenuMode;
    }

    public static bool IsAnyPauseUiOpen()
    {
        PauseManager[] pauseManagers = FindObjectsByType<PauseManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < pauseManagers.Length; i++)
        {
            PauseManager manager = pauseManagers[i];
            if (manager != null && manager.IsPauseUiOpen())
                return true;
        }

        return false;
    }

    private static GameObject FindByNameRecursive(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
            return null;

        if (root.name == targetName)
            return root.gameObject;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            GameObject match = FindByNameRecursive(child, targetName);
            if (match != null)
                return match;
        }

        return null;
    }

    private void DisableDecorativePauseOverlayRaycasts()
    {
        if (PausePanel == null)
            return;

        DisableGraphicRaycastTargetByName(PausePanel.transform, "PausedTextImg");
        DisableGraphicRaycastTargetByName(PausePanel.transform, "PausedText");
    }

    private static void DisableGraphicRaycastTargetByName(Transform root, string targetName)
    {
        GameObject target = FindByNameRecursive(root, targetName);
        if (target == null)
            return;

        Graphic graphic = target.GetComponent<Graphic>();
        if (graphic != null)
            graphic.raycastTarget = false;
    }

    private void EnsurePauseUiForeground()
    {
        CachePauseCanvasSource();

        RectTransform overlayRoot = EnsurePauseOverlayRoot();
        if (overlayRoot == null)
            return;

        AttachPanelToOverlay(PausePanel, overlayRoot);
        AttachPanelToOverlay(ControlsPanel, overlayRoot);

        if (pauseOverlayCanvas != null)
            pauseOverlayCanvas.sortingOrder = PauseMenuSortingOrder;

        overlayRoot.SetAsLastSibling();
    }

    private void CachePauseCanvasSource()
    {
        if (pauseSourceCanvas != null)
            return;

        pauseSourceCanvas = FindSourceCanvas(PausePanel);
        if (pauseSourceCanvas == null)
            pauseSourceCanvas = FindSourceCanvas(ControlsPanel);

        if (pauseSourceCanvas != null)
            pauseSourceScaler = pauseSourceCanvas.GetComponent<CanvasScaler>();
    }

    private RectTransform EnsurePauseOverlayRoot()
    {
        if (pauseOverlayRoot != null && pauseOverlayCanvas != null)
            return pauseOverlayRoot;

        Transform existing = transform.root != null
            ? transform.root.Find(PauseOverlayCanvasName)
            : null;
        if (existing != null)
        {
            pauseOverlayRoot = existing as RectTransform;
            pauseOverlayCanvas = existing.GetComponent<Canvas>();
            pauseOverlayScaler = existing.GetComponent<CanvasScaler>();
            pauseOverlayRaycaster = existing.GetComponent<GraphicRaycaster>();
        }

        if (pauseOverlayRoot == null || pauseOverlayCanvas == null)
        {
            GameObject overlayObject = new GameObject(
                PauseOverlayCanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            SceneManager.MoveGameObjectToScene(overlayObject, gameObject.scene);

            pauseOverlayRoot = overlayObject.GetComponent<RectTransform>();
            pauseOverlayCanvas = overlayObject.GetComponent<Canvas>();
            pauseOverlayScaler = overlayObject.GetComponent<CanvasScaler>();
            pauseOverlayRaycaster = overlayObject.GetComponent<GraphicRaycaster>();
            overlayObject.layer = PausePanel != null ? PausePanel.layer : gameObject.layer;
        }

        ConfigurePauseOverlayCanvas();
        return pauseOverlayRoot;
    }

    private void ConfigurePauseOverlayCanvas()
    {
        if (pauseOverlayRoot == null || pauseOverlayCanvas == null)
            return;

        pauseOverlayRoot.SetParent(null, false);
        pauseOverlayRoot.anchorMin = Vector2.zero;
        pauseOverlayRoot.anchorMax = Vector2.one;
        pauseOverlayRoot.offsetMin = Vector2.zero;
        pauseOverlayRoot.offsetMax = Vector2.zero;
        pauseOverlayRoot.localScale = Vector3.one;
        pauseOverlayRoot.localRotation = Quaternion.identity;

        pauseOverlayCanvas.renderMode = pauseSourceCanvas != null
            ? pauseSourceCanvas.renderMode
            : RenderMode.ScreenSpaceOverlay;
        pauseOverlayCanvas.worldCamera = pauseSourceCanvas != null
            ? pauseSourceCanvas.worldCamera
            : null;
        pauseOverlayCanvas.planeDistance = pauseSourceCanvas != null
            ? pauseSourceCanvas.planeDistance
            : 100f;
        pauseOverlayCanvas.sortingLayerID = pauseSourceCanvas != null
            ? pauseSourceCanvas.sortingLayerID
            : pauseOverlayCanvas.sortingLayerID;
        pauseOverlayCanvas.overrideSorting = true;
        pauseOverlayCanvas.sortingOrder = PauseMenuSortingOrder;

        if (pauseOverlayScaler == null)
            pauseOverlayScaler = pauseOverlayRoot.gameObject.AddComponent<CanvasScaler>();

        if (pauseSourceScaler != null)
        {
            pauseOverlayScaler.uiScaleMode = pauseSourceScaler.uiScaleMode;
            pauseOverlayScaler.referencePixelsPerUnit = pauseSourceScaler.referencePixelsPerUnit;
            pauseOverlayScaler.scaleFactor = pauseSourceScaler.scaleFactor;
            pauseOverlayScaler.referenceResolution = pauseSourceScaler.referenceResolution;
            pauseOverlayScaler.screenMatchMode = pauseSourceScaler.screenMatchMode;
            pauseOverlayScaler.matchWidthOrHeight = pauseSourceScaler.matchWidthOrHeight;
            pauseOverlayScaler.physicalUnit = pauseSourceScaler.physicalUnit;
            pauseOverlayScaler.fallbackScreenDPI = pauseSourceScaler.fallbackScreenDPI;
            pauseOverlayScaler.defaultSpriteDPI = pauseSourceScaler.defaultSpriteDPI;
            pauseOverlayScaler.dynamicPixelsPerUnit = pauseSourceScaler.dynamicPixelsPerUnit;
        }

        if (pauseOverlayRaycaster == null)
            pauseOverlayRaycaster = pauseOverlayRoot.gameObject.AddComponent<GraphicRaycaster>();

        pauseOverlayRaycaster.enabled = true;
    }

    private static Canvas FindSourceCanvas(GameObject panel)
    {
        if (panel == null)
            return null;

        Transform parent = panel.transform.parent;
        while (parent != null)
        {
            Canvas canvas = parent.GetComponent<Canvas>();
            if (canvas != null)
                return canvas;

            parent = parent.parent;
        }

        return panel.GetComponentInParent<Canvas>();
    }

    private static void AttachPanelToOverlay(GameObject panel, RectTransform overlayRoot)
    {
        if (panel == null || overlayRoot == null)
            return;

        RectTransform rect = panel.transform as RectTransform;
        if (rect == null)
            return;

        if (rect.parent != overlayRoot)
            rect.SetParent(overlayRoot, false);

        rect.SetAsLastSibling();
    }

    private void QueueSelect(GameObject target)
    {
        StopSelectionRoutine();
        if (target == null)
            return;

        selectRoutine = StartCoroutine(SelectNextFrame(target));
    }

    private IEnumerator SelectNextFrame(GameObject target)
    {
        yield return null;

        EventSystem evt = EventSystem.current;
        if (evt == null || target == null || !target.activeInHierarchy)
        {
            selectRoutine = null;
            yield break;
        }

        evt.SetSelectedGameObject(null);
        evt.SetSelectedGameObject(target);
        selectRoutine = null;
    }

    private void StopSelectionRoutine()
    {
        if (selectRoutine == null)
            return;

        StopCoroutine(selectRoutine);
        selectRoutine = null;
    }

    private static void ClearSelectedObject()
    {
        EventSystem evt = EventSystem.current;
        if (evt != null)
            evt.SetSelectedGameObject(null);
    }
}
