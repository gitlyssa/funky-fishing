using UnityEngine;

[System.Serializable]
public class NoteData
{
    public float hitTime;               // time the note needs to be hit
    public RhythmArcNote.NoteType type; // flick or slide, reels separate
    public FlickDirection direction;    // left right up down
}

[System.Serializable]
public class ReelData 
{
    public float startTime;
    public float duration;
    public float goalDegrees; // total degrees to spin (positive for clockwise, negative for counterclockwise)
    public float leadInTime = 1.0f; // Visual warning
    public bool isClockwise => goalDegrees > 0;
}