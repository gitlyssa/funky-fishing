using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
public class SceneManager : MonoBehaviour
{
    public string rhythmSceneName = "RhythmHittable";
    public string fishingSceneName = "rodBobberMech";
    
    private bool isRhythmLoaded = false;
    void Update()
    {
        // RESET: Reloads the base fishing scene (wipes everything)
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(fishingSceneName);
        }

        // TOGGLE ADDITIVE: Press 3 to start, press 4 to end/unload
        if (Keyboard.current.digit3Key.wasPressedThisFrame && !isRhythmLoaded)
        {
            StartRhythmEncounter();
        }
        
        if (Keyboard.current.digit4Key.wasPressedThisFrame && isRhythmLoaded)
        {
            EndRhythmEncounter();
        }
    }

    public void StartRhythmEncounter()
    {
        // Start the additive load
        StartCoroutine(LoadRhythmAdditive());
    }

    public void EndRhythmEncounter()
    {
        if (isRhythmLoaded)
        {
            // This is the "Cleanup" phase
            StartCoroutine(UnloadRhythm());
        }
    }

    private IEnumerator LoadRhythmAdditive()
    {
        isRhythmLoaded = true;
        AsyncOperation op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(rhythmSceneName, LoadSceneMode.Additive);
        yield return op;
        
        Debug.Log("Rhythm Scene Overlay Active.");
    }

    private IEnumerator UnloadRhythm()
    {
        // 1. Remove the camera from the stack before unloading (Recommended)
        // You could also handle this inside the URPStacker's OnDestroy()
        
        AsyncOperation op = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(rhythmSceneName);
        yield return op;

        isRhythmLoaded = false;
        Debug.Log("Rhythm Scene Unloaded. Back to pure fishing.");
    }
}
