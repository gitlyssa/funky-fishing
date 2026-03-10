using UnityEngine;
using System.Collections.Generic;
using System;

public class RhythmJudge : MonoBehaviour
{
    /*
    This is where all the note hitting logic happens. It listens to the processor for the player input
    and has a reference to the conductor to get the position of all the acitve notes
    I have some manually set timing windows for now to control the scoring of the notes.
    */
    public static RhythmJudge Instance { get; private set; }
    public enum JudgeRating { Perfect, Good, Bad, Miss }
    [Header("References")]
    public RhythmConductor conductor;
    public RhythmInputProcessorT processor;

    [Header("Timing Windows (Seconds)")]
    public float perfectWindow = 0.2f;
    public float goodWindow = 0.4f;
    public float badWindow = 0.5f; // Beyond this is an automatic Miss
    public float PerfectWindow => perfectWindow;
    public float GoodWindow => goodWindow;
    public float BadWindow => badWindow;

    [Header("Debug")]
    [SerializeField] private bool logNoteResolutions = false;
    [SerializeField] private bool logReelOutcome = false;

    public static event Action<JudgeRating> OnNoteJudged;
    public static event Action<JudgeRating, RhythmArcNote.NoteType, FlickDirection> OnDetailedNoteJudged;

    void Start()
    {
        if (processor != null)
            processor.OnValidFlick += HandleFlick;
    }
    private void Awake()
    {
        // Initialize Singleton
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (processor != null)
            processor.OnValidFlick -= HandleFlick;
    }

    void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        // Flick notes are handled through on flick events, separate to the update loop
        //Anything under the update loop is essentially a state check, for continuous notes

        // Slides are checked every frame to see if the player isholding down in the correct direction
        CheckSlideNotes();

        // Any notes past the window should automatically be missed, as they cant be hit anymore
        CheckAutoMiss();
        // Process reels, which are done over a time frame
        CheckReelNotes();
    }

    private void HandleFlick(FlickDirection dir)
{
    RhythmArcNote target = null;
    float oldestHitTime = float.MaxValue;

    foreach (var note in conductor.activeNotes)
    {
        // 1. flick or first slide note 
        if (note.Direction == dir)
        {
            float songTime = conductor.songTime;
            float diff = songTime - note.TargetHitTime; // Negative = Early, Positive = Late

            // 2. Is this note within the judging window (-0.5s to +0.5s)?
            if (Mathf.Abs(diff) <= badWindow)
            {
                // 3. PRIORITY: Is this the oldest note we've found so far?
                // By checking 'targetHitTime' instead of 'Abs(diff)', we ensure 
                // we always try to hit the note that's been on screen the longest.
                if (note.TargetHitTime < oldestHitTime)
                {
                    target = note;
                    oldestHitTime = note.TargetHitTime;
                }
            }
        }
    }

    if (target != null)
    {
        float finalDiff = Mathf.Abs(conductor.songTime - target.TargetHitTime);
        JudgeRating rating = GetRating(finalDiff);

        if (rating == JudgeRating.Bad || rating == JudgeRating.Miss)
            TryRecordMiss(true, finalDiff); // Input was provided, but late/early
        else
            TryRecordHit(rating, finalDiff);

        ResolveNote(target, rating);
    }
}

    private void CheckSlideNotes()
{
    for (int i = conductor.activeNotes.Count - 1; i >= 0; i--)
    {
        var note = conductor.activeNotes[i];
        if (note.Type == RhythmArcNote.NoteType.Slide)
        {
            float songTime = conductor.songTime;
            float rawDiff = songTime - note.TargetHitTime; // Negative = Early, Positive = Late
            // For slides, we want to allow the player to hit early and then hold through the perfect window.
            if (rawDiff >= 0 && rawDiff <= badWindow)
            {
                if (processor.IsHoldingDirection(note.Direction))
                {
                    JudgeRating rating = GetRating(Mathf.Abs(rawDiff));
                    ResolveNote(note, rating);
                }
            }
        }
    }
}

    private void CheckAutoMiss()
    {
        for (int i = conductor.activeNotes.Count - 1; i >= 0; i--)
        {
            var note = conductor.activeNotes[i];
            
            // If the current time is beyond the bad window on the LATE side
            if (conductor.songTime > note.TargetHitTime + badWindow)
            {
                TryRecordMiss(false);
                ResolveNote(note, JudgeRating.Miss);
            }
        }
    }

    private void TryRecordHit(JudgeRating rating, float timingDelta)
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.RecordHit(rating, timingDelta);
    }

    private void TryRecordMiss(bool wasInputProvided, float timingDelta = 0f)
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.RecordMiss(wasInputProvided, timingDelta);
    }

    private JudgeRating GetRating(float absDiff)
    {
        if (absDiff <= perfectWindow) return JudgeRating.Perfect;
        if (absDiff <= goodWindow)    return JudgeRating.Good;
        return JudgeRating.Bad; // If it's within 0.5 but past 0.3
    }

    private void ResolveNote(RhythmArcNote note, JudgeRating rating)
    {
        RhythmArcNote.NoteType noteType = note.Type;
        FlickDirection direction = note.Direction;
        conductor.activeNotes.Remove(note);
        if (logNoteResolutions)
        {
            Debug.Log($"Resolving Note: Type={note.Type}, Direction={note.Direction}, Rating={rating} (diff={conductor.songTime - note.TargetHitTime:F2})");
        }
        switch (rating)
        {
            case JudgeRating.Perfect:
                note.OnPerfectHit();
                break;
            case JudgeRating.Good:
                note.OnGoodHit();
                break;
            case JudgeRating.Bad:
                note.OnMiss();
                break;
            case JudgeRating.Miss:
                note.OnMiss();
                break;
        }

        OnNoteJudged?.Invoke(rating);
        OnDetailedNoteJudged?.Invoke(rating, noteType, direction);
    }

    private void CheckReelNotes()
    {
        RhythmReelNote reel = conductor.activeReel;
        if (reel == null) return;

        float songTime = conductor.songTime;
        float endTime = reel.Data.startTime + reel.Data.duration;


        if (reel.CurrentPhase == ReelPhase.Active)
        {
            float spinVelocity = processor.GetSmoothedSpinVelocity(); // Get the current smoothed spin velocity
            float delta = spinVelocity * Time.deltaTime; // Convert velocity to delta for this frame
            reel.AddSpin(delta);
        }

        if (songTime >= endTime)
        {
            
            float finalProgress = reel.Progress;

            if (finalProgress >= 1.0f)
            {
                if (logReelOutcome)
                    Debug.Log("<color=green>REEL CLEARED!</color>");
                if (finalProgress > 1.0f)
                {
                    float bonus = Mathf.Min(finalProgress - 1.0f, 1.0f); 

                    if (logReelOutcome)
                        Debug.Log($"<color=gold>BONUS REACHED: {bonus * 100:F0}%</color>");
                }

                reel.OnClear();
            }
            else
            {
                Debug.Log("<color=red>REEL FAILED!</color>");
                reel.OnFail();
            }

            // Clear the reference in the conductor so visuals stop
            conductor.activeReel = null;
        }
    }
}
