using UnityEngine;
using UnityEngine.InputSystem;

public class NoteSpawner : MonoBehaviour
{

    public GameObject notePrefab;
    public float globalScrollSpeed = 10f;
    public float spawnZ = 30f;
    
    // hit time is time when note should be hit
    // duration is how long the note lasts (0 for flick)
    // starting angle and ending angle
    public void SpawnNote(float hitTime, float duration, float sAngle, float eAngle)
    {
        float travelTime = spawnZ / globalScrollSpeed;
        
        GameObject go = Instantiate(notePrefab);
        RhythmNote note = go.GetComponent<RhythmNote>();
        
        note.Initialize(hitTime, duration, travelTime, sAngle, eAngle);
    }

    // Update is called once per frame
    void Update()
    {   
        //randomize between note durations
        float dur = Random.value < 0.5f ? 0f : 1f;
        //randomize curve left or right for hold notes
        float dir = 0f;
        if (dur > 0f)
        {
            dir = Random.value < 0.5f ? -1f : 1f;
        }
        // press z, x, c, v for a flick note in left, up, right, down
        if (Keyboard.current.zKey.wasPressedThisFrame)
        {
            SpawnNote(Time.time + 2f, dur, 180f, 180f + (90f * dir)); // Left
        }
        if (Keyboard.current.xKey.wasPressedThisFrame)
        {
            SpawnNote(Time.time + 2f, dur, 90f, 90f + (90f * dir)); // Up
        }
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            SpawnNote(Time.time + 2f, dur, 0f, 0f + (90f * dir)); // Right
        }
        if (Keyboard.current.vKey.wasPressedThisFrame)
        {
            SpawnNote(Time.time + 2f, dur, 270f, 270f + (90f * dir)); // Down
        }
    }
}
