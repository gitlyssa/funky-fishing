using UnityEngine;
using System.Collections.Generic;

public class RhythmJudge : MonoBehaviour
{
    public RhythmConductor conductor;
    public RhythmInputProcessorT processor;
    public float hitWindow = 0.12f; // +/- seconds

    void Start()
    {
        // The Judge listens to the Processor
        processor.OnValidFlick += HandleFlick;
    }

    void Update()
    {
        // SLIDE NOTES: Checked every frame because they are "State" based
        CheckSlideNotes();
    }

    private void HandleFlick(FlickDirection dir)
    {
        // 1. Find the oldest Flick note in that direction
        RhythmArcNote target = null;
        float bestDiff = float.MaxValue;

        foreach (var note in conductor.activeNotes)
        {
            if (note.Type == RhythmArcNote.NoteType.Flick && note.Direction == dir)
            {
                float diff = Mathf.Abs(conductor.songTime - note.TargetHitTime);
                if (diff < hitWindow && diff < bestDiff)
                {
                    target = note;
                    bestDiff = diff;
                }
            }
        }

        // 2. Score it
        if (target != null)
        {
            conductor.activeNotes.Remove(target);
            target.OnHit();
            Debug.Log($"<color=cyan>Flick Hit! Accuracy: {bestDiff}</color>");
        }
    }

    private void CheckSlideNotes()
    {
        for (int i = conductor.activeNotes.Count - 1; i >= 0; i--)
        {
            var note = conductor.activeNotes[i];
            if (note.Type == RhythmArcNote.NoteType.Slide)
            {
                float diff = conductor.songTime - note.TargetHitTime;
                 
                // If the player is holding the right direction exactly as it passes
                if (Mathf.Abs(diff) < hitWindow && processor.IsHoldingDirection(note.Direction))
                {
                    conductor.activeNotes.RemoveAt(i);
                    note.OnHit();
                    Debug.Log("<color=white>Slide Hit!</color>");
                }
            }
        }
    }

    private void CheckReelNotes() 
    {
        // If activeNotes contains a ReelNote, 
        // ask the processor for accumulated spin.
        // If spin > goal, call note.OnHit().
    }
}