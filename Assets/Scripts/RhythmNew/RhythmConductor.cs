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
    private readonly List<NoteData> _chartTemplate = new List<NoteData>();
    private readonly List<ReelData> _reelTemplate = new List<ReelData>();
    public TextAsset beatmapFile; // Reference to the CSV file


    void Start()
    {
        rhythmMusicPlayer = FindObjectOfType<RhythmMusicPlayer>();
        if (rhythmMusicPlayer == null)
        {
            Debug.LogError("RhythmMusicPlayer not found in the active scene.");
        }

        noteTravelTime = (hitRingRadius - spawnRadius) / noteSpeed;

        // Load the beatmap and parse it into NoteData objects
        LoadBeatmapFromCSV();
        _chart.Sort((a, b) => a.hitTime.CompareTo(b.hitTime));
        CacheBeatmapTemplates();
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
        if (rhythmMusicPlayer == null)
        {
            return;
        }

        songTime = GetFmodSongTimeSeconds();

        while (_chart.Count > 0 && songTime >= _chart[0].hitTime - noteTravelTime)
        {
            SpawnNote(_chart[0]);
            _chart.RemoveAt(0);
        }

        if (activeReel == null && _reelQueue.Count > 0 && songTime >= _reelQueue[0].startTime - _reelQueue[0].leadInTime)
        {
            SpawnReel(_reelQueue[0]);
            _reelQueue.RemoveAt(0);
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

    public void ResetBeatmapForReplay()
    {
        ClearActiveRhythmObjects();
        _chart.Clear();
        _reelQueue.Clear();

        foreach (NoteData note in _chartTemplate)
        {
            _chart.Add(new NoteData
            {
                hitTime = note.hitTime,
                type = note.type,
                direction = note.direction
            });
        }

        foreach (ReelData reel in _reelTemplate)
        {
            _reelQueue.Add(new ReelData
            {
                startTime = reel.startTime,
                duration = reel.duration,
                goalDegrees = reel.goalDegrees,
                leadInTime = reel.leadInTime
            });
        }

        songTime = 0f;
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

    private void CacheBeatmapTemplates()
    {
        _chartTemplate.Clear();
        _reelTemplate.Clear();

        foreach (NoteData note in _chart)
        {
            _chartTemplate.Add(new NoteData
            {
                hitTime = note.hitTime,
                type = note.type,
                direction = note.direction
            });
        }

        foreach (ReelData reel in _reelQueue)
        {
            _reelTemplate.Add(new ReelData
            {
                startTime = reel.startTime,
                duration = reel.duration,
                goalDegrees = reel.goalDegrees,
                leadInTime = reel.leadInTime
            });
        }
    }

    private void ClearActiveRhythmObjects()
    {
        for (int i = 0; i < activeNotes.Count; i++)
        {
            RhythmArcNote note = activeNotes[i];
            if (note != null)
            {
                Destroy(note.gameObject);
            }
        }
        activeNotes.Clear();

        if (activeReel != null)
        {
            Destroy(activeReel.gameObject);
            activeReel = null;
        }
    }


    private float GetFmodSongTimeSeconds()
    {
        int ms;
        var result = rhythmMusicPlayer.musicInstance.getTimelinePosition(out ms);
        return ms / 1000f;
    }
}
