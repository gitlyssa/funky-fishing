using UnityEngine;
using System.Collections.Generic;


[System.Serializable]
public class NoteData
{
    public float hitTime;               // time the note needs to be hit
    public RhythmArcNote.NoteType type; // flick or slide
    public FlickDirection direction;    // left right up down ( i guess diagonals are okay too technically)
    public bool isGolden = false;                // whether the note is a golden note, which gives bonus points and has a stricter timing window
    public bool isGhost = false;                 // whether the note is a ghost note which has trailing afterimage notes that are auto hit
    public int ghostRepeats = 0;              // how many times the ghost note repeats, if it's a ghost note. 
    public float ghostRepeatInterval = 1f;    // the interval between ghost note repeats, if it's a ghost note
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

public enum DisplayMode { Player, Dev, InDepth }

[System.Serializable]
public class DetailedStats
{
    // Miss Categories
    public int earlyMisses;    // Swung too early (before bad window)
    public int lateMisses;     // Swung too late
    public int completeMisses; // Didn't swing at all (AutoMiss)

    // Hit Categories
    public int perfects;
    public int earlyGoods;
    public int lateGoods;

    // Timing Data
    public List<float> timingOffsets = new List<float>(); // Raw deltas
    public float averageOffset;

    // Reel Data
    public int reelsCleared;
    public int reelsFailed;
    public float totalBonusDegrees;

    // Helper to get total counts for the player UI
    public int TotalMisses => earlyMisses + lateMisses + completeMisses;
    public int TotalGoods => earlyGoods + lateGoods;
}