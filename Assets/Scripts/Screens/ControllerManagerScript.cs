using UnityEngine;
using UnityEngine.SceneManagement;

public class ControllerManagerScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void BackToMenu()
    {
        Debug.Log("Back to Menu");
        SceneManager.LoadScene("MainMenu");
    }
}
