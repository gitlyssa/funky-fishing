using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
public class SceneLoading : MonoBehaviour
{
    public static SceneLoading Instance { get; private set; }
    
    public string rhythmSceneName = "RhythmHittable";
    public string fishingSceneName = "rodBobberMech";

    public static GameObject MigratedFish;
    
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

     void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    public void StartRhythmEncounter(GameObject fishToMigrate = null)
    {
        if (fishToMigrate != null)
        {
            MigratedFish = fishToMigrate;
            StartCoroutine(LoadRhythmFishAdditive(fishToMigrate));
        }
        else
        {
            StartCoroutine(LoadRhythmAdditive());
        }
        
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

    private IEnumerator LoadRhythmFishAdditive(GameObject fish)
    {
        isRhythmLoaded = true;
        AsyncOperation op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(rhythmSceneName, LoadSceneMode.Additive);
        yield return op;

        Scene rhythmScene = SceneManager.GetSceneByName(rhythmSceneName);
        fish.transform.SetParent(null); 
        SceneManager.MoveGameObjectToScene(fish, rhythmScene);


        if (fish.TryGetComponent<MonoBehaviour>(out var fishAI)) {
            fishAI.enabled = false; 
        }

        // also disable any colliders or rigidbodies on the fish to prevent physics interactions in the rhythm scene
        foreach (Collider col in fish.GetComponentsInChildren<Collider>()) {
            col.enabled = false;
        }
        foreach (Rigidbody rb in fish.GetComponentsInChildren<Rigidbody>()) {
            rb.isKinematic = true;
        }

        SetLayerRecursively(fish, LayerMask.NameToLayer("RhythmLayer"));
        
        Debug.Log($"Migrated {fish.name} to Rhythm Scene.");
    }

    private IEnumerator UnloadRhythm()
    {
        if (MigratedFish != null) 
        {
            Destroy(MigratedFish);
            MigratedFish = null;
        }
        
        AsyncOperation op = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(rhythmSceneName);
        yield return op;

        isRhythmLoaded = false;
        Debug.Log("Rhythm Unloaded");
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}
