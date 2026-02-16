using UnityEngine;

[System.Serializable]
public class NoteData
{
    public float hitTime;               // time the note needs to be hit
    public RhythmArcNote.NoteType type; // flick or slide, reels separate
    public FlickDirection direction;    // left right up down
}