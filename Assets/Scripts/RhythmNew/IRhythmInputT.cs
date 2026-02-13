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
    // Returns true the moment a flick is detected
    bool GetFlick(FlickDirection direction);
    
    // Returns true as long as the direction is being held
    bool IsHolding(FlickDirection direction);
    
    // Returns the current rotation speed
    float GetSpinVelocity();
}

public interface IRhythmInputT
{
    // Events allow other classes to "Subscribe"
    event System.Action<FlickDirection> OnFlick;
    event System.Action<float> OnReelUpdate; 
    
    // Still useful for continuous checks (like reeling)
    bool IsHoldingDirection(FlickDirection dir);
    bool IsButtonDown(int buttonIndex); // Support for those 10 extra buttons
}