using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
public class NoteSpawner : MonoBehaviour
{

    public GameObject notePrefab;
    public Beatmap beatmap;
    public Metronome metronome;

    private int currentEventIndex = 0;

    public float globalScrollSpeed = 10f;
    public float spawnZ = 30f;
    public float beatsAheadToSpawn = 4f;

    public List<RhythmNote> activeNotes = new List<RhythmNote>();
    
    // hit time is time when note should be hit
    // duration is how long the note lasts (0 for flick)
    // starting angle and ending angle
    public void SpawnNote(float hitTime, float duration, float sAngle, float eAngle)
    {
        float travelTime = spawnZ / globalScrollSpeed;
        
        GameObject go = Instantiate(notePrefab);
        RhythmNote note = go.GetComponent<RhythmNote>();
        
        note.Initialize(hitTime, duration, travelTime, sAngle, eAngle);
        activeNotes.Add(note);
    }

    // Update is called once per frame
    void Update()
    {   
        // // press z, x, c, v for a flick note in left, up, right, down
        // if (Keyboard.current.zKey.wasPressedThisFrame)
        // {
        //     SpawnNote(Time.time + 2f, 0f, 180f, 180f); // Left
        // }
        // if (Keyboard.current.xKey.wasPressedThisFrame)
        // {
        //     SpawnNote(Time.time + 2f, 0f, 90f, 90f); // Up
        // }
        // if (Keyboard.current.cKey.wasPressedThisFrame)
        // {
        //     SpawnNote(Time.time + 2f, 0f, 0f, 0f); // Right
        // }
        // if (Keyboard.current.vKey.wasPressedThisFrame)
        // {
        //     SpawnNote(Time.time + 2f, 0f, 270f, 270f); // Down
        // }

        while (currentEventIndex < beatmap.events.Count) 
        {
            var currentEvent = beatmap.events[currentEventIndex];

            // Check if the current song beat matches the event's beat
            if (metronome.lastBeat >= currentEvent.beat)
            {
                SpawnNote(Time.time + 2f, 0f, currentEvent.sAngle, currentEvent.eAngle);
                currentEventIndex++; // Move to the next event
            }
            else
            {
                // If the current beat is less than the event's beat, stop checking
                break;
            }
        }
    }
}
