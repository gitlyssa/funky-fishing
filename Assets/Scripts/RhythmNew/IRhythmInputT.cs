using UnityEngine;

[System.Flags]
public enum FlickDirection
{
    None = 0,
    Right     = 1 << 0,
    UpRight   = 1 << 1,
    Up        = 1 << 2,
    UpLeft    = 1 << 3,
    Left      = 1 << 4,
    DownLeft  = 1 << 5,
    Down      = 1 << 6,
    DownRight = 1 << 7
}
// raw data for each flick
public struct FlickEventArgs
{
    public FlickDirection Direction;
    public float Velocity; 
    public float Timestamp;
}

public interface IRhythmInputT
{
    // Discrete Events
    event System.Action<FlickDirection> OnFlick;
    event System.Action<int> OnButtonDown;

    // Continuous States 
    bool IsHoldingDirection(FlickDirection direction);
    float GetSpinVelocity(); // Returns degrees per second (positive/negative for direction)
    bool GetButton(int index);
    
    // float GetTotalAccumulatedSpin(); 
    // void ResetAccumulatedSpin();
    // Vector2 GetReelStickDirection();
}