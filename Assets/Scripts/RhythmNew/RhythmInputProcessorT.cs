using UnityEngine;
using System.Collections.Generic;
using System.Linq; 

public class RhythmInputProcessorT : MonoBehaviour
{
    /*
    This class bridges the providers, which collect the raw data from a control scheme. The processor reads this and turns it inot
    slightly processed input that any of the other classes can read from. I think currently the judge, and the visualizers both read from this
    Its just a bunch of state checks mostly, and a handle flick which happens whenever a flick goes off.
    */
    private List<IRhythmInputT> _connectedHardware = new List<IRhythmInputT>();

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
        if (!_connectedHardware.Contains(hardware))
        {
            _connectedHardware.Add(hardware);
            hardware.OnFlick += HandleHardwareFlick;
            Debug.Log($"[Processor] Successfully added {hardware.GetType().Name}");
        }
    }

    public float GetSmoothedSpinVelocity()
    {
        if (_spinVelocityBuffer.Count == 0) return 0f;
        return _spinVelocityBuffer.Average();
    }

    private void HandleHardwareFlick(FlickDirection dir)
    {
        if (CanPlayerAct())
        {
            OnValidFlick?.Invoke(dir);
        }
    }

    public bool IsHoldingDirection(FlickDirection dir)
    {
        foreach (var hardware in _connectedHardware)
        {
            if (hardware.IsHoldingDirection(dir)) return true;
        }
        return false;
    }

    public Vector2 GetCombinedReelStick()
    {
        Vector2 combined = Vector2.zero;
        foreach (var hardware in _connectedHardware)
        {
            Vector2 stick = hardware.GetReelStickDirection();
            if (stick.magnitude > 0.1f) return stick; // Prioritize the active one
        }
        return Vector2.zero;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        if (_connectedHardware.Count == 0) return;

        float totalRawSpin = 0f;
        foreach (var hardware in _connectedHardware)
        {
            // We sum them up. If only one is used, the others add 0.
            totalRawSpin += hardware.GetSpinVelocity();
            Debug.Log($"[Processor] Raw spin from {hardware.GetType().Name}: {hardware.GetSpinVelocity()}");
        }

        // 2. Manage the Sliding Window
        _spinVelocityBuffer.Enqueue(totalRawSpin);
        if (_spinVelocityBuffer.Count > smoothingWindowSize)
        {
            _spinVelocityBuffer.Dequeue();
        }
        float smoothed = GetSmoothedSpinVelocity();
    }

    private bool CanPlayerAct() => true; // Add your game state logic here
}
