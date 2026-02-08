using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
public class SceneManager : MonoBehaviour
{
    void Update()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            LoadSceneByName("rodBobberMech");
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            LoadSceneByName("RhythmHittable");
        }
    }
    public void LoadSceneByName(string sceneName)
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }
}
