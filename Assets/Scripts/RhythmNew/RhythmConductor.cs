using UnityEngine;
using System.Collections.Generic;

public class RhythmConductor : MonoBehaviour
{   
    public static RhythmConductor Instance; 
    public List<RhythmArcNote> activeNotes = new List<RhythmArcNote>();

    [Header("Current Reel State")]
    public RhythmReelNote activeReel; 

    [Header("Global Wheel Config")]
    public float spawnRadius = 1f; 
    public float hitRingRadius = 5.0f;
    public AnimationCurve noteScaleCurve;
    
    [Header("Prefabs")]
    public GameObject notePrefab;

    [Header("Timing")]
    [Header("Note Styles")]
    public Material flickMaterial;
    public Material slideMaterial;

    

    public float songTime => Time.time; // To be replaced by AudioSource.timeSamples
    public float noteTravelTime = 2.0f; //global speed setting for notes

    // This is where you'd load your JSON or MIDI file later
    public List<NoteData> _chart = new List<NoteData>();

    public List<ReelData> _reelQueue = new List<ReelData>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Update()
    {
        // Spawning Logic
        if (_chart.Count > 0 && songTime >= _chart[0].hitTime - noteTravelTime)
        {
            SpawnNote(_chart[0]);
            _chart.RemoveAt(0);
        }

        if (_reelQueue.Count > 0 && songTime >= _reelQueue[0].startTime - _reelQueue[0].leadInTime)
        {
            SpawnReel(_reelQueue[0]);
        }

        // on pressing space, spawn a random direction note for testing
        if (Input.GetKeyDown(KeyCode.Space))
        {
            int randomDir = Random.Range(0, 4);
            FlickDirection dir = FlickDirection.Right;
            if (randomDir == 0)
            {
                dir = FlickDirection.Right;
            }
            else if (randomDir == 1)
            {
                dir = FlickDirection.Up;
            }
            else if (randomDir == 2)
            {
                dir = FlickDirection.Left;
            }
            else if (randomDir == 3)
            {
                dir = FlickDirection.Down;
            }

            NoteData testData = new NoteData
            {
                hitTime = songTime + noteTravelTime,
                type = (Random.value > 0.5f) ? RhythmArcNote.NoteType.Flick : RhythmArcNote.NoteType.Slide,
                direction = dir

            };
            SpawnNote(testData);
        }

        // on pressing r, spawn a reel
        // start time is when the reel becomes active
        // lead in is how long before it starts winding up
        if (Input.GetKeyDown(KeyCode.R) && activeReel == null)
        {
            ReelData testReel = new ReelData
            {
                startTime = songTime + 2.0f,
                duration = 3.0f,
                goalDegrees = (Random.value > 0.5f) ? 720f : -720f, // 2 full spins in either direction
                leadInTime = 1.0f
            };
            SpawnReel(testReel);
        }

        
        
    }

    void SpawnReel(ReelData data)
    {
        GameObject go = new GameObject("ReelLogic");
        activeReel = go.AddComponent<RhythmReelNote>();
        activeReel.Initialize(data);
    }

    void SpawnNote(NoteData data)
    {
        GameObject go = Instantiate(notePrefab, transform);
        RhythmArcNote note = go.GetComponent<RhythmArcNote>();
        
        note.Initialize(data, noteTravelTime, spawnRadius, hitRingRadius, noteScaleCurve);
        
        activeNotes.Add(note);
    }
}