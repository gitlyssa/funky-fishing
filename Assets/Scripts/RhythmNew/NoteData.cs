using UnityEngine;


[System.Serializable]
public class NoteData
{
    public float hitTime;               // time the note needs to be hit
    public RhythmArcNote.NoteType type; // flick or slide
    public FlickDirection direction;    // left right up down ( i guess diagonals are okay too technically)
}

[System.Serializable]
public class ReelData 
{
    public float startTime; // time the player needs to begin reeling
    public float duration; // duration the reel note lasts
    public float goalDegrees; // total degrees the player needs to spin
    public float leadInTime = 1.0f; // how long the reel starts spinning before the player needs to start inputting. windup time
    public bool isClockwise => goalDegrees > 0;
}