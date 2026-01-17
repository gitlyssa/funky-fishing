using UnityEngine;

public class GameManager : MonoBehaviour
{
    bool inRhythmMode = false;
    bool isPaused = false;
    int fishCaught = 0;

    public PondManager pondManager;
    public GameObject playerBobber;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void HookFish()
    {
        bool

        fishCaught += 1;
        Debug.Log("Fish Caught! Total: " + fishCaught);
    }

    void FinishRhythmGame(bool success)
    {
        inRhythmMode = false;
        if (success)
        {
            CaughtFish();
        }
        else
        {
            Debug.Log("Missed the fish!");
        }
    }
}
