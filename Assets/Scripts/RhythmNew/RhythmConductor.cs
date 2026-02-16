using UnityEngine;
using System.Collections.Generic;

public class RhythmConductor : MonoBehaviour
{
    public static RhythmConductor Instance; 
    public List<RhythmArcNote> activeNotes = new List<RhythmArcNote>();

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

        // on pressing space, spawn a random direction note for testing
        if (Input.GetKeyDown(KeyCode.Space))
        {
            NoteData testData = new NoteData
            {
                hitTime = songTime + noteTravelTime,
                type = (Random.value > 0.5f) ? RhythmArcNote.NoteType.Flick : RhythmArcNote.NoteType.Slide,
                direction = (FlickDirection)(Random.Range(0, 4)) // Random direction
            };
            SpawnNote(testData);
        }
        
        // Cleanup: If a note is way past the hit window, remove it from our list
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            if (songTime > activeNotes[i].TargetHitTime + 0.2f) 
            {
                var note = activeNotes[i];
                activeNotes.RemoveAt(i);
                note.OnMiss(); // The note handles its own destruction
            }
        }
    }

    void SpawnNote(NoteData data)
    {
        GameObject go = Instantiate(notePrefab, transform);
        RhythmArcNote note = go.GetComponent<RhythmArcNote>();
        
        note.Initialize(data, noteTravelTime, spawnRadius, hitRingRadius, noteScaleCurve);
        
        activeNotes.Add(note);
    }
}