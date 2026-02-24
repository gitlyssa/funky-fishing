using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    public GameObject PausePanel;
    public GameObject ControlsPanel;

    private bool isPaused = false;

    void Awake()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (PausePanel != null)
            PausePanel.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("ESC key pressed");
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    public void PauseGame()
    {
        PausePanel.SetActive(true);
        if (ControlsPanel != null)
            ControlsPanel.SetActive(false);
        Time.timeScale = 0f;
        isPaused = true;
    }

    public void ResumeGame()
    {
        PausePanel.SetActive(false);
        if (ControlsPanel != null)
            ControlsPanel.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void OpenControls()
    {
        PausePanel.SetActive(false);
        if (ControlsPanel != null)
            ControlsPanel.SetActive(true);
    }

    public void BackToPauseMenu()
    {
        if (ControlsPanel != null)
            ControlsPanel.SetActive(false);
        PausePanel.SetActive(true);
    }
}
