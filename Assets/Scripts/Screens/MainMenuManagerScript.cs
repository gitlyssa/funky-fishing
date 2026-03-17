using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManagerScript : MonoBehaviour
{
    public void StartGame()
    {
        FunkyAudioSettings.PlayUiConfirm();
        Debug.Log("Start Game");
        SceneManager.LoadScene("PondSelect");
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
