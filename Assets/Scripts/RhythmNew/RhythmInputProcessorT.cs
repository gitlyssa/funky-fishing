using UnityEngine;
using System.Collections.Generic;
using System.Linq; 

public class RhythmInputProcessorT : MonoBehaviour
{
    private IRhythmInputT _currentHardware;

    [Header("Flick Calibration")]
    public float globalFlickThreshold = 2.0f; //minimum velocity for a flick
    public float directionWindow = 45f; // Half-window for 8-direction detection

    [Header("Reeling Smoothing")]
    public int smoothingWindowSize = 10; 
    private Queue<float> _spinVelocityBuffer = new Queue<float>();

    public event System.Action<FlickDirection> OnValidFlick;
    public event System.Action<float> OnSpinAccumulated;

    public void Initialize(IRhythmInputT hardware)
    {
        _currentHardware = hardware;
        _currentHardware.OnFlick += HandleHardwareFlick;
    }

    public float GetSmoothedSpinVelocity()
    {
        if (_spinVelocityBuffer.Count == 0) return 0f;
        return _spinVelocityBuffer.Average();
    }

    private void HandleHardwareFlick(FlickDirection dir)
    {
        Debug.Log($"[Processor] Received Flick from Hardware: {dir}"); // STEP 1
        if (CanPlayerAct())
        {
            Debug.Log($"[Processor] Relaying Valid Flick: {dir}"); // STEP 2
            OnValidFlick?.Invoke(dir);
        }
    }

    public bool IsHoldingDirection(FlickDirection dir)
    {
        if (_currentHardware == null) return false;
        return _currentHardware.IsHoldingDirection(dir);
    }

    private void Update()
    {
        if (_currentHardware == null) return;

        // Process Continuous Logic (Reeling)
        float rawSpin = _currentHardware.GetSpinVelocity();

        // 2. Manage the Sliding Window
        _spinVelocityBuffer.Enqueue(rawSpin);
        if (_spinVelocityBuffer.Count > smoothingWindowSize)
        {
            _spinVelocityBuffer.Dequeue();
        }
        float smoothed = GetSmoothedSpinVelocity();
    }

    private bool CanPlayerAct() => true; // Add your game state logic here
}