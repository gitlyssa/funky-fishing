using UnityEngine;
    /*
    This interface specifies what input data any control scheme needs to provide.
     */
[System.Flags]
public enum FlickDirection
{
    // Bit flags so we can technically inpnut any direction. When referring to flick directions, please use something like
    // FlickDirection.Right or FlickDirection.Up, or FlickDirection.None 
    // this allows multiple directions to be active at once, but not sure if we will need that
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
    // Discrete Events when buttons are pressed or flicks are started
    event System.Action<FlickDirection> OnFlick;
    event System.Action<int> OnButtonDown;

    // Continuous States 
    bool IsHoldingDirection(FlickDirection direction);
    float GetSpinVelocity(); // Returns degrees per second (positive/negative for direction)
    bool GetButton(int index);
    
    // i dont think i ended up using this, but i left it in
    float GetTotalAccumulatedSpin(); 
    void ResetAccumulatedSpin();
    Vector2 GetReelStickDirection();
}