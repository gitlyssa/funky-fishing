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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        pondManager = FindObjectOfType<PondManager>();

        beatSpawner = FindObjectOfType<BeatSpawner>().gameObject;
        beatSpawner.SetActive(false);
        inRhythmMode = false;

        ambientMusic.resource = ambientMusicClip;
        ambientMusic.volume = 2.0f;
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
