using System;
using UnityEngine;

public class JoyConCrankProvider : MonoBehaviour, IRhythmInputT
{
    // Events required by IRhythmInputT (we'll leave flick empty since this is just a crank)
    public event Action<FlickDirection> OnFlick;
    public event Action<int> OnButtonDown;

    [Header("Crank Settings")]
    public float radiusThreshold = 0.2f;
    public float smoothing = 15f;
    
    private int _deviceId = -1;
    private Vector2 _smoothedAccel;
    private float _lastCrankAngle;
    private float _currentVelocity;
    private JSL.JOY_SHOCK_STATE _lastState;
    private Vector2 _virtualCrankStick;

    void Update()
        {
            int[] handles = JoyConConnectionService.GetConnectedHandles();
        if (handles == null || handles.Length < 2) 
        {
            _currentVelocity = 0;
            return;
        }
        
        _deviceId = handles[1];
        JSL.MOTION_STATE motion = JSL.JslGetMotionState(_deviceId);

        // 1. ISOLATE USER ACCELERATION
        // We subtract gravity so we only see the "swing" of the arm.
        // Assuming the crank is a vertical circle in front of you (X and Y axes).
        Vector2 userAccel = new Vector2(
            motion.accelX - motion.gravX, 
            motion.accelY - motion.gravY
        );

        // 2. SMOOTH THE NOISE
        // Accelerometer data is "spikier" than gravity, so we need a filter.
        _smoothedAccel = Vector2.Lerp(_smoothedAccel, userAccel, Time.deltaTime * smoothing);

        // 3. DISTANCE CHECK (The "Crank Radius")
        // If the player isn't moving their arm in a big enough circle, ignore it.
        if (_smoothedAccel.magnitude > radiusThreshold)
        {
            // Treat the smoothed acceleration vector like a virtual joystick
            _virtualCrankStick = _smoothedAccel.normalized; 

            float currentAngle = Mathf.Atan2(_smoothedAccel.y, _smoothedAccel.x) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(_lastCrankAngle, currentAngle);

            _currentVelocity = delta / Mathf.Max(0.0001f, Time.deltaTime);
            _lastCrankAngle = currentAngle;
        }
        else
        {
            _currentVelocity = 0;
            _virtualCrankStick = Vector2.zero;
        }
        // 2. Process Buttons (The "South" button)
        JSL.JOY_SHOCK_STATE state = JSL.JslGetSimpleState(_deviceId);
        bool isDown = (state.buttons & (1 << JSL.ButtonMaskDown)) != 0;
        bool wasDown = (_lastState.buttons & (1 << JSL.ButtonMaskDown)) != 0;

        if (isDown && !wasDown) OnButtonDown?.Invoke(0);
        _lastState = state;
    }

    // --- IRhythmInputT Implementation ---
    public float GetSpinVelocity() => _currentVelocity;
    public bool GetButton(int index) => (_lastState.buttons & (1 << JSL.ButtonMaskDown)) != 0;
    
    // Unused by the Crank Joy-Con, but required by the interface
    public bool IsHoldingDirection(FlickDirection dir) => false;
    public Vector2 GetReelStickDirection() => _virtualCrankStick.normalized; // Could be used for visual feedback if desired
    public float GetTotalAccumulatedSpin() => 0f; // Processor tracks this globally
    public void ResetAccumulatedSpin() { }
}