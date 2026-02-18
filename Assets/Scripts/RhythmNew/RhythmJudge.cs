using UnityEngine;
using System.Collections.Generic;

public class RhythmJudge : MonoBehaviour
{

    public enum JudgeRating { Perfect, Good, Bad, Miss }
    [Header("References")]
    public RhythmConductor conductor;
    public RhythmInputProcessorT processor;

    [Header("Timing Windows (Seconds)")]
    public float perfectWindow = 0.1f;
    public float goodWindow = 0.3f;
    public float badWindow = 0.5f; // Beyond this is an automatic Miss

    void Start()
    {
        processor.OnValidFlick += HandleFlick;
    }

    void Update()
    {
        // 1. Process State-based notes (Slides)
        CheckSlideNotes();

        // 2. Process Auto-Miss for notes that fly past the bad window
        CheckAutoMiss();
        // 3. Process Reel Notes (Continuous Evaluation)
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
                ResolveNote(note, JudgeRating.Miss);
            }
        }
    }

    private JudgeRating GetRating(float absDiff)
    {
        if (absDiff <= perfectWindow) return JudgeRating.Perfect;
        if (absDiff <= goodWindow)    return JudgeRating.Good;
        return JudgeRating.Bad; // If it's within 0.5 but past 0.3
    }

    private void ResolveNote(RhythmArcNote note, JudgeRating rating)
    {
        conductor.activeNotes.Remove(note);
        Debug.Log($"Resolving Note: Type={note.Type}, Direction={note.Direction}, Rating={rating} (diff={conductor.songTime - note.TargetHitTime:F2})");
        switch (rating)
        {
            case JudgeRating.Perfect:
                note.OnHit();
                break;
            case JudgeRating.Good:
                note.OnHit();
                break;
            case JudgeRating.Bad:
                note.OnMiss();
                break;
            case JudgeRating.Miss:
                note.OnMiss();
                break;
        }
    }

    private void CheckReelNotes()
    {
        RhythmReelNote reel = conductor.activeReel;
        if (reel == null) return;

        float songTime = conductor.songTime;


        if (reel.CurrentPhase == ReelPhase.Active)
        {
            float spinVelocity = processor.GetSmoothedSpinVelocity(); // Get the current smoothed spin velocity
            float delta = spinVelocity * Time.deltaTime; // Convert velocity to delta for this frame
            reel.AddSpin(delta);
        }
        

        if (songTime >= reel.Data.startTime + reel.Data.duration && reel.CurrentPhase != ReelPhase.Resolved)
        {
            
            float finalProgress = reel.Progress;

            if (finalProgress >= 1.0f)
            {

                Debug.Log("<color=green>REEL CLEARED!</color>");
                if (finalProgress > 1.0f)
                {
                    float bonus = Mathf.Min(finalProgress - 1.0f, 1.0f); 
   
                    Debug.Log($"<color=gold>BONUS REACHED: {bonus * 100:F0}%</color>");
                }

                reel.OnClear();
            }
            else
            {
                reel.OnFail();
            }

            // Clear the reference in the conductor so visuals stop
            conductor.activeReel = null;
        }
    }
}