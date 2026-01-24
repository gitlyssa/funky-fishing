using UnityEngine;
using UnityEngine.Audio;

public class GameManager : MonoBehaviour
{
    bool inRhythmMode = false;
    bool isPaused = false;
    int fishCaught = 0;

    public PondManager pondManager;
    public GameObject playerBobber;

    public GameObject beatSpawner;

    public Camera mainCamera;

    public AudioSource ambientMusic;
    public AudioResource ambientMusicClip;
    [SerializeField, Range(0f, 2f)] private float ambientVolume = 1.5f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        pondManager = FindObjectOfType<PondManager>();

        beatSpawner = FindObjectOfType<BeatSpawner>().gameObject;
        beatSpawner.SetActive(false);
        inRhythmMode = false;

        ambientMusic.resource = ambientMusicClip;
        ambientMusic.spatialBlend = 0f;
        ambientMusic.volume = ambientVolume;
        ambientMusic.Play();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void HookFish()
    {
        inRhythmMode = true;
        beatSpawner.SetActive(true);
        fishCaught += 1;
        Debug.Log("Fish Caught! Total: " + fishCaught);
    }

    public void FinishRhythmGame(bool success)
    {
        inRhythmMode = false;
        beatSpawner.SetActive(false);
        Debug.Log("Rhythm Game Finished! Success: " + success);
    }
}
