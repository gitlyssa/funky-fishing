using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections;

public class PauseManager : MonoBehaviour
{
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
    private Coroutine selectRoutine;
    private RhythmMusicPlayer rhythmMusicPlayer;

    void Awake()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (PausePanel != null)
            PausePanel.SetActive(false);

        if (ControlsPanel != null)
            ControlsPanel.SetActive(false);

        ResolveSelectionReferences();
        ClearSelectedObject();
    }

    void Update()
    {
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
            if (IsControlsOpen())
                BackToPauseMenu();
            else
                ResumeGame();
            return;
        }

        if (isPaused)
            EnsurePausedSelection();
    }

    public void PauseGame()
    {
        PausePanel.SetActive(true);
        if (ControlsPanel != null)
            ControlsPanel.SetActive(false);
        Time.timeScale = 0f;
        isPaused = true;
        ResolveRhythmMusicPlayer();
        if (rhythmMusicPlayer != null)
            rhythmMusicPlayer.PauseRhythmForGamePause();
        ResolveSelectionReferences();
        QueueSelect(pauseDefaultSelected);
    }

    public void ResumeGame()
    {
        PausePanel.SetActive(false);
        if (ControlsPanel != null)
            ControlsPanel.SetActive(false);
        XboxFishingInput.BlockGameplayInputForRealtimeSeconds(resumeInputBlockSeconds);
        Time.timeScale = 1f;
        isPaused = false;
        ResolveRhythmMusicPlayer();
        if (rhythmMusicPlayer != null)
            rhythmMusicPlayer.ResumeRhythmFromGamePause();
        StopSelectionRoutine();
        ClearSelectedObject();
    }

    public void GoToMainMenu()
    {
        // Prevent the same confirm press from reaching gameplay input on this frame.
        XboxFishingInput.BlockGameplayInputForRealtimeSeconds(Mathf.Max(menuExitInputBlockSeconds, resumeInputBlockSeconds));
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void OpenControls()
    {
        PausePanel.SetActive(false);
        if (ControlsPanel != null)
            ControlsPanel.SetActive(true);
        ResolveSelectionReferences();
        QueueSelect(controlsDefaultSelected);
    }

    public void BackToPauseMenu()
    {
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
