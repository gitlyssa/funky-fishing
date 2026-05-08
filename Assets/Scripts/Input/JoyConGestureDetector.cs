using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;

// Reads Joy-Con IMU data via JoyShockLibrary and fires cast/yank events based on
// linear-accel + gyro thresholds.
public class JoyConGestureDetector : MonoBehaviour
{
    private const string DLL = "JoyShockLibrary";
    public enum Axis { X, Y, Z }

    [StructLayout(LayoutKind.Sequential)]
    public struct IMU_STATE
    {
        public float accelX, accelY, accelZ; // g
        public float gyroX, gyroY, gyroZ;    // dps
    }

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern IMU_STATE JslGetIMUState(int deviceId);

    [Header("Device")]
    public int deviceIndex = 0;
    public bool useAnyConnectedDevice = true;
    public float reconnectInterval = 2f;

    [Header("Axis mapping (make CAST positive on baseline hand)")]
    public Axis forwardAxis = Axis.Z;     // "thrust forward/back" axis
    public float forwardSign = 1f;        // flip to -1 if backwards
    public Axis swingGyroAxis = Axis.X;   // "swing" axis (rotation)
    public float gyroSign = 1f;           // flip to -1 if backwards
    public bool autoMirrorForOtherHand = true;

    [Header("Filtering")]
    public float gravityFollow = 12f;     // gravity estimate speed
    public float linAccelSmooth = 25f;    // smooth linear accel
    public float gyroSmooth = 20f;        // smooth gyro

    [Header("Thresholds (tune)")]
    public float castForwardLinG = 0.60f;
    public float castGyroDps = 220f;
    public float yankBackLinG = 0.60f;
    public float yankGyroDps = 220f;

    [Header("Timing")]
    public float minTimeBetweenCastAndYank = 0.25f;
    public float cooldownAfterTrigger = 0.25f;

    [Header("Cast Input Guard")]
    public bool requireBumperOrTriggerHold = false;
    public float triggerHoldThreshold = 0.35f;

    [Header("Yank Guard")]
    public BobberArcCaster caster;
    public bool blockYankDuringTension = true;

    [Header("Events")]
    public UnityEvent onCast;
    public UnityEvent onYank;

    [Header("Debug")]
    public bool logTriggers = true;
    public int LastTriggerHandle { get; private set; } = -1;

    private int[] _handles = Array.Empty<int>();
    private int _id = -1;
    private int _knownConnectionRevision = -1;
    private float _nextReconnectTime;

    private struct FilterState
    {
        public Vector3 gravity;
        public Vector3 linAccel;
        public Vector3 gyro;
    }
    private readonly Dictionary<int, FilterState> _filtersByHandle = new Dictionary<int, FilterState>();

    private enum State { Idle, Casted, Cooldown }
    private State _state = State.Idle;
    private float _castTime = -999f;
    private float _cooldownUntil = -999f;
    private int _castPolarity = 1;

    void Start()
    {
        if (caster == null)
            caster = FindObjectOfType<BobberArcCaster>();

        Connect();
        _knownConnectionRevision = JoyConConnectionService.GetRevision();
    }

    [ContextMenu("Reconnect")]
    public void Connect()
    {
        _handles = JoyConConnectionService.GetConnectedHandles();
        _knownConnectionRevision = JoyConConnectionService.GetRevision();

        if (_handles == null || _handles.Length == 0)
        {
            _handles = Array.Empty<int>();
            _id = -1;
            LastTriggerHandle = -1;
            _filtersByHandle.Clear();
            JoyConConnectionService.RequestScan();
            Debug.LogWarning("JoyConGestureDetector: No JoyShockLibrary devices found.");
            return;
        }

        _id = _handles[Mathf.Clamp(deviceIndex, 0, _handles.Length - 1)];
        if (!useAnyConnectedDevice)
            LastTriggerHandle = _id;
        _filtersByHandle.Clear();
        Debug.Log($"JoyConGestureDetector handles={_handles.Length}, selectedHandle={_id}, useAnyConnectedDevice={useAnyConnectedDevice}");
    }

    void Update()
    {
        int revision = JoyConConnectionService.GetRevision();
        if (revision != _knownConnectionRevision)
        {
            _knownConnectionRevision = revision;
            Connect();
        }

        if (FishingGameplayInputGate.IsBlocked())
        {
            ResetTransientGestureState();
            return;
        }

        SyncStateWithCaster();

        if (Time.time < _cooldownUntil) return;

        if (_state == State.Cooldown)
        {
            // One-frame reset so we can re-arm after the cooldown window.
            _state = State.Idle;
        }

        if (_handles.Length == 0 && Time.time >= _nextReconnectTime)
        {
            Connect();
            _nextReconnectTime = Time.time + Mathf.Max(0.1f, reconnectInterval);
        }

        if (_handles.Length == 0)
            return;

        float dt = Time.deltaTime;

        bool anyConnected = false;

        if (useAnyConnectedDevice)
        {
            foreach (int handle in _handles)
            {
                if (!JoyConConnectionService.IsHandleConnected(handle))
                    continue;

                anyConnected = true;
                if (ProcessHandle(handle, dt))
                    break;
            }
        }
        else
        {
            if (_id >= 0 && JoyConConnectionService.IsHandleConnected(_id))
            {
                anyConnected = true;
                ProcessHandle(_id, dt);
            }
        }

        if (!anyConnected && Time.time >= _nextReconnectTime)
        {
            JoyConConnectionService.RequestScan();
            Connect();
            _nextReconnectTime = Time.time + Mathf.Max(0.1f, reconnectInterval);
        }
    }

    private void ResetTransientGestureState()
    {
        _state = State.Idle;
        _castTime = -999f;
        _cooldownUntil = -999f;
        _castPolarity = 1;
        _filtersByHandle.Clear();
    }

    private bool ProcessHandle(int handle, float dt)
    {
        IMU_STATE imu;
        try
        {
            imu = JslGetIMUState(handle);
        }
        catch
        {
            JoyConConnectionService.RequestScan();
            return false;
        }
        var accelG = new Vector3(imu.accelX, imu.accelY, imu.accelZ);
        var gyroDps = new Vector3(imu.gyroX, imu.gyroY, imu.gyroZ);
        JSL.JOY_SHOCK_STATE simpleState;
        try
        {
            simpleState = JSL.JslGetSimpleState(handle);
        }
        catch
        {
            simpleState = default;
        }

        if (!_filtersByHandle.TryGetValue(handle, out FilterState state))
            state = default;

        // Estimate gravity and remove it to get linear acceleration.
        state.gravity = Vector3.Lerp(state.gravity, accelG, 1f - Mathf.Exp(-gravityFollow * dt));
        var lin = accelG - state.gravity;

        // Smooth noisy signals before thresholding.
        state.linAccel = Vector3.Lerp(state.linAccel, lin, 1f - Mathf.Exp(-linAccelSmooth * dt));
        state.gyro = Vector3.Lerp(state.gyro, gyroDps, 1f - Mathf.Exp(-gyroSmooth * dt));
        _filtersByHandle[handle] = state;

        // Project onto the configured axes and normalize sign.
        float forwardLin = forwardSign * GetAxis(state.linAccel, forwardAxis);
        float swingGyro = gyroSign * GetAxis(state.gyro, swingGyroAxis);

        bool canAttemptYank = CanAttemptYank();
        if (canAttemptYank && Time.time - _castTime >= minTimeBetweenCastAndYank)
        {
            int yankPolarity = GetMatchingYankPolarity(forwardLin, swingGyro);
            if (yankPolarity != 0)
            {
                if (logTriggers) Debug.Log($"YANK! handle={handle} lin={forwardLin:F2}g gyro={swingGyro:F0}dps polarity={yankPolarity}");
                LastTriggerHandle = handle;
                onYank?.Invoke();
                _castPolarity = yankPolarity;
                _castTime = -999f;
                _state = State.Cooldown;
                _cooldownUntil = Time.time + cooldownAfterTrigger;
                return true;
            }
        }

        if (_state != State.Idle)
            return false;

        if (requireBumperOrTriggerHold && !IsGestureModifierHeld(simpleState))
            return false;

        if (TryDetectCast(forwardLin, swingGyro, out int castPolarity))
        {
            if (logTriggers) Debug.Log($"CAST! handle={handle} lin={forwardLin:F2}g gyro={swingGyro:F0}dps polarity={castPolarity}");
            LastTriggerHandle = handle;
            onCast?.Invoke();
            _castPolarity = castPolarity;
            _castTime = Time.time;
            _state = State.Casted;
            _cooldownUntil = Time.time + cooldownAfterTrigger;
            return true;
        }

        return false;
    }

    private bool TryDetectCast(float forwardLin, float swingGyro, out int castPolarity)
    {
        if (forwardLin > castForwardLinG && swingGyro > castGyroDps)
        {
            castPolarity = 1;
            return true;
        }

        if (autoMirrorForOtherHand && forwardLin < -castForwardLinG && swingGyro < -castGyroDps)
        {
            castPolarity = -1;
            return true;
        }

        castPolarity = 1;
        return false;
    }

    private bool IsYankForPolarity(float forwardLin, float swingGyro, int polarity)
    {
        float signedForward = forwardLin * polarity;
        float signedGyro = swingGyro * polarity;
        return signedForward < -yankBackLinG && signedGyro < -yankGyroDps;
    }

    private int GetMatchingYankPolarity(float forwardLin, float swingGyro)
    {
        if (_state == State.Casted && IsYankForPolarity(forwardLin, swingGyro, _castPolarity))
            return _castPolarity;

        if (IsYankForPolarity(forwardLin, swingGyro, 1))
            return 1;

        if (autoMirrorForOtherHand && IsYankForPolarity(forwardLin, swingGyro, -1))
            return -1;

        return 0;
    }

    private bool CanAttemptYank()
    {
        if (ShouldBlockYank())
            return false;

        if (caster == null)
            return _state == State.Casted;

        return caster.CurrentState == BobberArcCaster.State.Landed ||
               caster.CurrentState == BobberArcCaster.State.Tension;
    }

    private void SyncStateWithCaster()
    {
        if (caster == null)
            return;

        if (caster.CurrentState == BobberArcCaster.State.Idle &&
            _state == State.Casted &&
            Time.time >= _cooldownUntil)
        {
            _state = State.Idle;
            _castTime = -999f;
        }
    }

    private bool ShouldBlockYank()
    {
        return blockYankDuringTension &&
               caster != null &&
               caster.CurrentState == BobberArcCaster.State.Tension;
    }

    private bool IsGestureModifierHeld(JSL.JOY_SHOCK_STATE state)
    {
        bool shoulderHeld =
            IsButtonPressed(state.buttons, JSL.ButtonMaskL) ||
            IsButtonPressed(state.buttons, JSL.ButtonMaskR) ||
            IsButtonPressed(state.buttons, JSL.ButtonMaskZL) ||
            IsButtonPressed(state.buttons, JSL.ButtonMaskZR) ||
            IsButtonPressed(state.buttons, JSL.ButtonMaskSL) ||
            IsButtonPressed(state.buttons, JSL.ButtonMaskSR);

        float triggerThreshold = Mathf.Clamp01(triggerHoldThreshold);
        bool analogTriggerHeld =
            state.lTrigger >= triggerThreshold ||
            state.rTrigger >= triggerThreshold;

        return shoulderHeld || analogTriggerHeld;
    }

    private static bool IsButtonPressed(int buttons, int bit)
    {
        return (buttons & (1 << bit)) != 0;
    }

    private static float GetAxis(Vector3 v, Axis a) => a == Axis.X ? v.x : (a == Axis.Y ? v.y : v.z);
}
