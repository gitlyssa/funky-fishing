using UnityEngine;

public class RhythmReelNote : MonoBehaviour
{
    [Header("Goal Data")]
    public float targetDegrees;  // e.g., 720 for 2 rotations
    public bool clockwise;       // Direction
    public float duration;       // How long the "Glow/Active" phase lasts
    public float startTime;      // When the "Glow" starts
    public float leadInTime = 1.0f; // How many seconds before startTime it starts spinning

    [Header("Visual Settings")]
    public float maxVisualSpinSpeed = 360f; // Degrees per second for the ring's visual spin

    private bool _isActive = false;
    private bool _isWarmingUp = false;
    private float _currentVisualSpeed = 0f;
    private IRhythmInputT _provider;

    public void Initialize(float start, float dur, float goalDegrees, bool isClockwise, IRhythmInputT input)
    {
        startTime = start;
        duration = dur;
        targetDegrees = goalDegrees;
        clockwise = isClockwise;
        _provider = input;
    }

    void Update()
    {
        float songTime = Time.time; // Sync to your music manager later
        
        // 1. Warm-up Phase (Visual Cue)
        if (songTime >= startTime - leadInTime && songTime < startTime)
        {
            _isWarmingUp = true;
            // Accelerate the ring visually
            float t = (songTime - (startTime - leadInTime)) / leadInTime;
            float targetSpeed = clockwise ? -maxVisualSpinSpeed : maxVisualSpinSpeed;
            _currentVisualSpeed = Mathf.Lerp(0, targetSpeed, t);
        }
        
        // 2. Active Phase (Detection)
        else if (songTime >= startTime && songTime < startTime + duration)
        {
            if (!_isActive) StartDetection();
            _isWarmingUp = false;
            _isActive = true;
            _currentVisualSpeed = clockwise ? -maxVisualSpinSpeed : maxVisualSpinSpeed;
            
            // Provide Glow/VFX feedback here
        }

        // 3. Completion
        else if (songTime >= startTime + duration)
        {
            if (_isActive) ResolveNote();
        }

        // 4. Apply Visual Rotation to the Wheel (The HUD)
        // Note: This assumes the Wheel logic is handled by the Manager, 
        // but for now, we can rotate the ring directly for debugging.
        ApplyVisualRotation();
    }

    private void StartDetection()
    {
        _provider.ResetAccumulatedSpin();
        Debug.Log("<color=yellow>Reel Started! START SPINNING!</color>");
    }

    private void ResolveNote()
    {
        _isActive = false;
        float total = Mathf.Abs(_provider.GetAccumulatedSpin());
        
        if (total >= targetDegrees) {
            Debug.Log("<color=green>REEL SUCCESS!</color>");
        } else {
            Debug.Log("<color=red>REEL FAIL!</color>");
        }
        
        Destroy(gameObject);
    }

    private void ApplyVisualRotation()
    {
        // This logic will eventually move to a dedicated WheelController
        // but for now, it lets you see the "Reel Note" working.
        if (_isActive || _isWarmingUp)
        {
             // Find the Outer Ring and rotate it
             // GameWheel.Instance.OuterRing.Rotate(0, 0, _currentVisualSpeed * Time.deltaTime);
        }
    }
}