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

    [Header("Tutorial Practice (Runtime)")]
    [SerializeField] private bool tutorialUpPracticeActive;
    [SerializeField] private FlickDirection tutorialPracticeDirection = FlickDirection.Up;
    [SerializeField] private bool tutorialPracticeUseSequence;
    [SerializeField] private float tutorialPracticeBpm = 99f;
    [SerializeField] private int tutorialPracticeGroupSize = 3;
    [SerializeField] private int tutorialPracticeGroupRestBeats = 2;
    [SerializeField] private int tutorialPracticeNoteBeatSpacing = 1;
    [SerializeField] private bool tutorialPracticeSpawnPaused;

    private float tutorialBeatInterval = 60f / 99f;
    private float tutorialNextHitTime = -1f;
    private int tutorialSpawnedInCurrentGroup;
    private FlickDirection[] tutorialPracticeSequence = new FlickDirection[0];
    private int tutorialPracticeSequenceIndex;
    private float tutorialPracticeClock = 0f;


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
        if (Time.timeScale <= 0f)
            return;

        if (rhythmMusicPlayer == null)
        {
            return;
        }

        if (tutorialUpPracticeActive)
        {
            if (tutorialPracticeSpawnPaused)
                return;

            tutorialPracticeClock += Time.deltaTime;
            songTime = tutorialPracticeClock;
            UpdateTutorialPracticeSpawning();
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
            //         goalDegrees = (UnityEngine.Random.value > 0.5f) ? 720f : -720f, // 2 full spins in either direction
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

    public void StartTutorialUpPracticeMode(
        float bpm = 99f,
        int groupSize = 3,
        int groupRestBeats = 1,
        int noteBeatSpacingBeats = 1)
    {
        StartTutorialDirectionalPracticeMode(
            FlickDirection.Up,
            bpm,
            groupSize,
            groupRestBeats,
            noteBeatSpacingBeats);
    }

    public void StartTutorialDirectionalPracticeMode(
        FlickDirection direction,
        float bpm = 99f,
        int groupSize = 3,
        int groupRestBeats = 1,
        int noteBeatSpacingBeats = 1)
    {
        tutorialPracticeDirection = direction;
        tutorialPracticeUseSequence = false;
        tutorialPracticeSequence = new FlickDirection[0];
        tutorialPracticeSequenceIndex = 0;
        tutorialPracticeBpm = Mathf.Max(1f, bpm);
        tutorialPracticeGroupSize = Mathf.Max(1, groupSize);
        tutorialPracticeGroupRestBeats = Mathf.Max(0, groupRestBeats);
        tutorialPracticeNoteBeatSpacing = Mathf.Max(1, noteBeatSpacingBeats);
        tutorialBeatInterval = 60f / tutorialPracticeBpm;
        tutorialSpawnedInCurrentGroup = 0;
        tutorialPracticeClock = 0f;
        tutorialUpPracticeActive = true;
        tutorialPracticeSpawnPaused = false;

        ClearActiveRhythmObjects();
        _chart.Clear();
        _reelQueue.Clear();

        float leadIn = Mathf.Max(noteTravelTime + tutorialBeatInterval, noteTravelTime + 0.25f);
        tutorialNextHitTime = tutorialPracticeClock + leadIn;
        songTime = tutorialPracticeClock;
    }

    public void StartTutorialSequencePracticeMode(
        FlickDirection[] sequence,
        float bpm = 99f,
        int groupSize = 3,
        int groupRestBeats = 1,
        int noteBeatSpacingBeats = 1)
    {
        if (sequence == null || sequence.Length == 0)
        {
            StartTutorialDirectionalPracticeMode(
                FlickDirection.Up,
                bpm,
                groupSize,
                groupRestBeats,
                noteBeatSpacingBeats);
            return;
        }

        tutorialPracticeDirection = sequence[0];
        tutorialPracticeUseSequence = true;
        tutorialPracticeSequence = new FlickDirection[sequence.Length];
        for (int i = 0; i < sequence.Length; i++)
            tutorialPracticeSequence[i] = sequence[i];
        tutorialPracticeSequenceIndex = 0;

        tutorialPracticeBpm = Mathf.Max(1f, bpm);
        tutorialPracticeGroupSize = Mathf.Max(1, groupSize);
        tutorialPracticeGroupRestBeats = Mathf.Max(0, groupRestBeats);
        tutorialPracticeNoteBeatSpacing = Mathf.Max(1, noteBeatSpacingBeats);
        tutorialBeatInterval = 60f / tutorialPracticeBpm;
        tutorialSpawnedInCurrentGroup = 0;
        tutorialPracticeClock = 0f;
        tutorialUpPracticeActive = true;
        tutorialPracticeSpawnPaused = false;

        ClearActiveRhythmObjects();
        _chart.Clear();
        _reelQueue.Clear();

        float leadIn = Mathf.Max(noteTravelTime + tutorialBeatInterval, noteTravelTime + 0.25f);
        tutorialNextHitTime = tutorialPracticeClock + leadIn;
        songTime = tutorialPracticeClock;
    }

    public void StopTutorialUpPracticeMode(bool restoreBeatmapForReplay)
    {
        tutorialUpPracticeActive = false;
        tutorialPracticeUseSequence = false;
        tutorialPracticeSequence = new FlickDirection[0];
        tutorialPracticeSequenceIndex = 0;
        tutorialPracticeSpawnPaused = false;
        tutorialSpawnedInCurrentGroup = 0;
        tutorialNextHitTime = -1f;
        tutorialPracticeClock = 0f;
        ClearActiveRhythmObjects();

        if (restoreBeatmapForReplay)
            ResetBeatmapForReplay();
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
        tutorialPracticeClock = 0f;
    }

    public void SetBeatmapFile(TextAsset newBeatmapFile)
    {
        beatmapFile = newBeatmapFile;
        LoadBeatmapFromCSV();
        ResetBeatmapForReplay();
    }


    private void LoadBeatmapFromCSV()
    {
        _chart.Clear();
        _reelQueue.Clear();
        _chartTemplate.Clear();
        _reelTemplate.Clear();

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

        _chart.Sort((a, b) => a.hitTime.CompareTo(b.hitTime));
        CacheBeatmapTemplates();
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

    private void UpdateTutorialPracticeSpawning()
    {
        if (tutorialNextHitTime < 0f)
        {
            float leadIn = Mathf.Max(noteTravelTime + tutorialBeatInterval, noteTravelTime + 0.25f);
            tutorialNextHitTime = songTime + leadIn;
        }

        while (songTime >= tutorialNextHitTime - noteTravelTime)
        {
            float noteSpacingSeconds = tutorialBeatInterval * tutorialPracticeNoteBeatSpacing;
            NoteData upNote = new NoteData
            {
                hitTime = tutorialNextHitTime,
                type = RhythmArcNote.NoteType.Flick,
                direction = GetNextTutorialPracticeDirection()
            };
            SpawnNote(upNote);

            tutorialSpawnedInCurrentGroup++;
            tutorialNextHitTime += noteSpacingSeconds;

            if (tutorialSpawnedInCurrentGroup >= tutorialPracticeGroupSize)
            {
                tutorialSpawnedInCurrentGroup = 0;
                tutorialNextHitTime += tutorialBeatInterval * tutorialPracticeGroupRestBeats;
            }
        }
    }

    public void SetTutorialUpPracticeSpawnPaused(bool paused)
    {
        tutorialPracticeSpawnPaused = paused;
        if (paused)
            ClearActiveRhythmObjects();
    }

    private FlickDirection GetNextTutorialPracticeDirection()
    {
        if (!tutorialPracticeUseSequence || tutorialPracticeSequence == null || tutorialPracticeSequence.Length == 0)
            return tutorialPracticeDirection;

        FlickDirection direction = tutorialPracticeSequence[tutorialPracticeSequenceIndex];
        tutorialPracticeSequenceIndex = (tutorialPracticeSequenceIndex + 1) % tutorialPracticeSequence.Length;
        return direction;
    }


    private float GetFmodSongTimeSeconds()
    {
        int ms;
        var result = rhythmMusicPlayer.musicInstance.getTimelinePosition(out ms);
        return ms / 1000f;
    }
}
