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

    public List<RhythmNote> activeNotes = new List<RhythmNote>();
    
    // hit time is time when note should be hit
    // duration is how long the note lasts (0 for flick)
    // starting angle and ending angle
    public void SpawnNote(float hitTime, float duration, float sAngle, float eAngle)
    {
        // float travelTime = spawnZ / globalScrollSpeed;
        float travelTime = 2f * (60f / metronome.bpm);
        
        GameObject go = Instantiate(notePrefab);
        RhythmNote note = go.GetComponent<RhythmNote>();
        
        note.Initialize(hitTime, duration, travelTime, sAngle, eAngle);
        activeNotes.Add(note);
    }

    // Update is called once per frame
    void Update()
    {   
        float secondsPerBeat = 60f / metronome.bpm;
        float beatsAhead = 2f;
        float travelTime = beatsAhead * secondsPerBeat;

        while (currentEventIndex < beatmap.events.Count)
        {
            var e = beatmap.events[currentEventIndex];
            float hitTime = metronome.GetTimeForBeat(e.beat);

            if (Time.time >= hitTime - travelTime)
            {
                SpawnNote(hitTime, 0f, e.sAngle, e.eAngle);
                currentEventIndex++;
            }
            else
            {
                break;
            }
        }

    }
}
