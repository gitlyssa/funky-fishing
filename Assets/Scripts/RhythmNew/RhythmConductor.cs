using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

public class RhythmConductor : MonoBehaviour
{   
    /*
    The Rhythm Conductor is pretty much the note spawner. It holds a list of all the notes and the reel note state
    It is responsible for spawning all the ntoes at the right time and storing their data when they are active
    I currently just have two lists, but they should probably be adjusted to be text files or something for easier
    charting.

    Currently, I have pressing space bar spawns a random flick or slide note and pressing r spawns a reel in a random direction
    If you scroll down, there are spawn note and spawn reel functions that can be called from elsewhere
    */
    public static RhythmConductor Instance; 
    public static RhythmMusicPlayer rhythmMusicPlayer;

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
    
    [Header("Debug")]
    public bool enableDebugInput = true;

    public float songTime;
    public float noteSpeed = 2.0f; //global speed setting for notes
    public float noteTravelTime;

    public List<NoteData> _chart = new List<NoteData>();
    public List<ReelData> _reelQueue = new List<ReelData>();
    public TextAsset beatmapFile; // Reference to the CSV file


    void Start()
    {
        noteTravelTime = (hitRingRadius - spawnRadius) / noteSpeed;

        // Load the beatmap and parse it into NoteData objects
        LoadBeatmapFromCSV();
        _chart.Sort((a, b) => a.hitTime.CompareTo(b.hitTime));
    }

    
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }


    void Update()
    {
        songTime = GetFmodSongTimeSeconds();

        while (_chart.Count > 0 && songTime >= _chart[0].hitTime - noteTravelTime)
        {
            SpawnNote(_chart[0]);
            _chart.RemoveAt(0);
        }

        if (_reelQueue.Count > 0 && songTime >= _reelQueue[0].startTime - _reelQueue[0].leadInTime)
        {
            SpawnReel(_reelQueue[0]);
        }

            // on pressing r, spawn a reel
            // start time is when the reel becomes active
            // lead in is how long before it starts winding up
            // if (Input.GetKeyDown(KeyCode.R) && activeReel == null)
            // {
            //     ReelData testReel = new ReelData
            //     {
            //         startTime = songTime + 2.0f,
            //         duration = 3.0f,
            //         goalDegrees = (Random.value > 0.5f) ? 720f : -720f, // 2 full spins in either direction
            //         leadInTime = 1.0f
            //     };
            //     SpawnReel(testReel);
            // }
        // }
    }


    public void SpawnReel(ReelData data)
    {
        GameObject go = new GameObject("ReelLogic");
        go.layer = gameObject.layer;
        activeReel = go.AddComponent<RhythmReelNote>();
        activeReel.Initialize(data);
        
    }


    public void SpawnNote(NoteData data)
    {
        GameObject go = Instantiate(notePrefab, transform);
        go.layer = gameObject.layer;
        RhythmArcNote note = go.GetComponent<RhythmArcNote>();
        
        note.Initialize(data, noteTravelTime, spawnRadius, hitRingRadius, noteScaleCurve);
        
        activeNotes.Add(note);
    }


    private void LoadBeatmapFromCSV()
    {
        if (beatmapFile == null)
        {
            Debug.LogError("Beatmap file not assigned!");
            return;
        }

        string[] lines = beatmapFile.text.Split('\n'); // Split the CSV into lines
        for (int i = 1; i < lines.Length; i++) // Skip the header row
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue; // Skip empty lines

            string[] values = line.Split(',');
            if (values.Length < 3) continue; // Ensure the row has enough columns

            // Parse the values
            if (float.TryParse(values[0], out float hitTime))
            {
                RhythmArcNote.NoteType type = RhythmArcNote.NoteType.Flick; // Default value
                FlickDirection direction = FlickDirection.None; // Default value

                Enum.TryParse(values[1], true, out type);
                Enum.TryParse(values[2], true, out direction);

                // Create a new NoteData object and add it to the chart
                NoteData note = new NoteData
                {
                    hitTime = hitTime,
                    type = type,
                    direction = direction
                };
                _chart.Add(note);
            }
            else
            {
                Debug.LogWarning($"Invalid data in beatmap line: {line}");
            }
        }

        Debug.Log($"Loaded {_chart.Count} notes from beatmap.");
    }


    private float GetFmodSongTimeSeconds()
    {
        if (rhythmMusicPlayer == null) return Time.time; // fallback

        int ms;
        var result = rhythmMusicPlayer.musicInstance.getTimelinePosition(out ms);
        // If you want: handle result != FMOD.RESULT.OK
        return ms / 1000f;
    }
}
