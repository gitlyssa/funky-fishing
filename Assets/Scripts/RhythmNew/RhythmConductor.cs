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
    private float _overtimeClock = 0f;
    private bool _isUsingOvertime = false;
    public bool isOvertime => _isUsingOvertime;

    [Header("Timing Ring Visuals")]
    public GameObject timingRingPrefab; 
    public float ringThickness = 0.05f;

    public Color perfectZoneColor = new Color(1f, 0.85f, 0f, 0.4f); // Shaded Gold (with alpha!)
    public Color goodGuidelineColor = new Color(0.2f, 1f, 1f, 1f);   // Bright Cyan

    void Start()
    {
        rhythmMusicPlayer = FindObjectOfType<RhythmMusicPlayer>();
        if (rhythmMusicPlayer == null)
        {
            Debug.LogError("RhythmMusicPlayer not found in the active scene.");
        }

        noteTravelTime = (hitRingRadius - spawnRadius) / noteSpeed;

        // Load the beatmap and parse it into NoteData objects
        Invoke(nameof(CreateTimingRings), 0.05f);
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
        else if (_isUsingOvertime) 
        {
            // Artificially increment the clock using DeltaTime
            _overtimeClock += Time.deltaTime;
            songTime = _overtimeClock;
        }
        else 
        {
            songTime = GetFmodSongTimeSeconds();
        }

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

        if (enableDebugInput)
        {
            // PRESS R: Spawn a Reel 2 seconds in the future
            if (Input.GetKeyDown(KeyCode.R) && activeReel == null)
            {
                ReelData debugReel = new ReelData
                {
                    startTime = songTime + 2.0f, // Becomes active in 2s
                    duration = 4.0f,             // Player has 4s to finish
                    goalDegrees = -720f,          // 2 full rotations
                    leadInTime = 1.0f            // Visuals start 1s before startTime
                };
                SpawnReel(debugReel);
                Debug.Log($"Debug Reel Spawned! Will be active at: {debugReel.startTime}");
            }

            // PRESS F: Spawn a Flick 2 seconds in the future
            if (Input.GetKeyDown(KeyCode.F))
            {
                NoteData debugNote = new NoteData
                {
                    hitTime = songTime + 2.0f,
                    type = RhythmArcNote.NoteType.Flick,
                    direction = FlickDirection.Up
                };
                SpawnNote(debugNote);
                Debug.Log("Debug Flick Spawned!");
            }
        }
    }

    
    public void SpawnReel(ReelData data)
    {
        GameObject go = new GameObject("ReelLogic");
        go.transform.SetParent(this.transform);
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

    public void SpawnFinalPlaytestReel()
    {
        // We spawn it at the current songTime (which is the end of the song)
        ReelData finalReel = new ReelData
        {
            startTime = songTime + 0.5f, // Half second delay after music stops
            duration = 5.0f,            // Give them 5 seconds to finish
            goalDegrees = -1080f,        // 3 full rotations for a "final" feel
            leadInTime = 0.4f            // Quick wind-up
        };
        
        SpawnReel(finalReel);
        Debug.Log("Final Playtest Reel Spawned!");
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

    public void StartOvertime()
    {
        _isUsingOvertime = true;
        _overtimeClock = songTime; // Start from wherever the music stopped
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
        _isUsingOvertime = false;
        _overtimeClock = 0f;    

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
    private void CreateTimingRings()
    {
        if (RhythmJudge.Instance == null) return;


        SpawnStaticRing(hitRingRadius, Color.white, ringThickness * 0.5f, "Ring_AbsoluteCenter");

        float pWindowSecs = RhythmJudge.Instance.PerfectWindow;
    
    // t=0.0 at spawn, t=1.0 at target.
    float tStartPerf = 1f - (pWindowSecs / noteTravelTime); // Early Perfect
    float tEndPerf = 1f;  

    // Calculate radii using Lerp and your AnimationCurve
    float rStartPerf = Mathf.Lerp(spawnRadius, hitRingRadius, noteScaleCurve.Evaluate(tStartPerf));
    // We allow Evaluate to go past 1.0 to find where the visual boundary would be
    float rEndPerf = Mathf.Lerp(spawnRadius, hitRingRadius, noteScaleCurve.Evaluate(tEndPerf)); 

    // The new ring's parameters
    float shadedCenterRadius = (rStartPerf + rEndPerf) / 2f;
    float shadedThickness = Mathf.Abs(rEndPerf - rStartPerf);

    // Spawn the wide, shaded golden ring
    SpawnStaticRing(shadedCenterRadius, perfectZoneColor, shadedThickness, "Zone_Perfect_Shaded");

    // Add a very thin border on the outside of the shaded zone for crispness
    SpawnStaticRing(rStartPerf, new Color(1f, 0.9f, 0.5f, 0.8f), 0.02f, "Ring_Perfect_Border");

        
        float goodTimeOffset = RhythmJudge.Instance.GoodWindow;
        float goodT = 1f - (goodTimeOffset / noteTravelTime);
        float goodRadius = Mathf.Lerp(spawnRadius, hitRingRadius, noteScaleCurve.Evaluate(goodT));
        SpawnStaticRing(goodRadius, goodGuidelineColor, ringThickness, "Ring_Good_Entry");

    }

    private void SpawnStaticRing(float radius, Color color, float thickness, string ringName)
    {
        GameObject ring = Instantiate(timingRingPrefab, transform);
        ring.name = ringName;
        ring.transform.localPosition = new Vector3(0, 0, 0.01f); 
        ring.layer = gameObject.layer;

        DynamicArc arc = ring.GetComponent<DynamicArc>();
        if (arc != null)
        {
            arc.Setup(64); // Higher segments for a smooth circle
            arc.Redraw(radius, thickness, 360f, 64);
            
            MeshRenderer ren = ring.GetComponent<MeshRenderer>();
            if (ren != null)
            {
                // Creating a new material instance so they don't all share the same color
                ren.material.color = color;
            }
        }
    }
}
