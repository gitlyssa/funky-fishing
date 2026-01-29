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

    [Header("Materials")]
    public Material holdMaterial;
    public Material flickMaterial; 

    public List<RhythmNote> activeNotes = new List<RhythmNote>();

    private float spawnTimer = 0f;
    private float spawnInterval = 1f; // spawn a note every second
    
    // hit time is time when note should be hit
    // duration is how long the note lasts (0 for flick)
    // starting angle and ending angle
    public void SpawnNote(float hitTime, float duration, float sAngle, float eAngle)
    {
        // float travelTime = spawnZ / globalScrollSpeed;
        float travelTime = 2f * (60f / metronome.bpm);
        
        GameObject go = Instantiate(notePrefab);
        RhythmNote note = go.GetComponent<RhythmNote>();
        
        note.Initialize(hitTime, duration, travelTime, sAngle, eAngle, holdMaterial, flickMaterial);
        activeNotes.Add(note);
    }

    public void RemoveNote(int index)
    {
        RhythmNote note = activeNotes[index];
        activeNotes.RemoveAt(index);
        Destroy(note.gameObject);
    }

    // Update is called once per frame
    void Update()
    {   
        float secondsPerBeat = 60f / metronome.bpm;
        float beatsAhead = 2f;
        float travelTime = beatsAhead * secondsPerBeat;

        // spawnTimer += Time.deltaTime;
        // if (spawnTimer >= spawnInterval)
        // {
        //     spawnTimer -= spawnInterval;

        //     float angle = Random.Range(0, 8) * 45f; // 0, 45, 90, ..., 315

        //     SpawnNote(Time.time + (spawnZ / globalScrollSpeed), 0f, angle, angle);
        // }

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
