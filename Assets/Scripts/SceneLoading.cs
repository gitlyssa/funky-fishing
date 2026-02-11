using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
public class SceneLoading : MonoBehaviour
{
    public string rhythmSceneName = "RhythmHittable";
    public string fishingSceneName = "rodBobberMech";
    
    private bool isRhythmLoaded = false;
    void Update()
    {
        // press 1 to reload the scene, might break everything not sure
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(fishingSceneName);
        }

        // 3 to load overlay, 4 to unload
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
        StartCoroutine(LoadRhythmAdditive());
    }

    public void EndRhythmEncounter()
    {
        if (isRhythmLoaded)
        {
            StartCoroutine(UnloadRhythm());
        }
    }

    private IEnumerator LoadRhythmAdditive()
    {
        isRhythmLoaded = true;
        AsyncOperation op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(rhythmSceneName, LoadSceneMode.Additive);
        yield return op;
        
        Debug.Log("Rhythm Overlay ON.");
    }

    private IEnumerator UnloadRhythm()
    {
        
        AsyncOperation op = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(rhythmSceneName);
        yield return op;

        isRhythmLoaded = false;
        Debug.Log("Rhythm Unloaded");
    }
}
