using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManagerScript : MonoBehaviour
{
    public void StartGame()
    {
        FunkyAudioSettings.PlayUiConfirm();
        Debug.Log("Start Game");
        SceneManager.LoadScene("Pond_Level_1");
    }

    public void TutorialSelected()
    {
        FunkyAudioSettings.PlayUiConfirm();
        Debug.Log("Tutorial Selected");
        SceneManager.LoadScene("Tutorial_Level");
    }

    public void ControllerMenu()
    {
        FunkyAudioSettings.PlayUiConfirm();
        Debug.Log("Controller Menu");
        SceneManager.LoadScene("ControllerMenu");
    }

    public void QuitGame()
    {
        FunkyAudioSettings.PlayUiConfirm();
        Debug.Log("Quit Game");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
