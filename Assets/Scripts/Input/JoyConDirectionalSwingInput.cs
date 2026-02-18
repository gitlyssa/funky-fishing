using System;
using UnityEngine;

// Drives BobberArcCaster directional swing (up/left/right) from Joy-Con gyro motion.
public class JoyConDirectionalSwingInput : MonoBehaviour
{
    public enum Axis { X, Y, Z }
    private enum MotionDirection { None, Up, Left, Right }

    [Header("Target")]
    public BobberArcCaster caster;
    public bool onlyWhenTensionState = true;

    [Header("Device")]
    public int deviceIndex = 0;

    [Header("Axis Mapping")]
    public Axis upGyroAxis = Axis.X;
    public float upGyroSign = 1f;
    public Axis sideGyroAxis = Axis.Y;
    public float sideGyroSign = 1f;

    [Header("Filtering")]
    public float gyroSmooth = 20f;

    [Header("Thresholds (dps)")]
    public float upEnterDps = 170f;
    public float upExitDps = 110f;
    public float sideEnterDps = 170f;
    public float sideExitDps = 110f;
    public float upPriorityBias = 1.05f;
    public float directionRearmDelay = 0.08f;
    public float sideNeutralRearmDps = 65f;
    public float sideNeutralHoldTime = 0.03f;

    [Header("Debug")]
    public bool logDirectionChanges = false;

    private int[] _handles = Array.Empty<int>();
    private int _deviceId = -1;
    private Vector3 _gyro;
    private MotionDirection _activeDirection = MotionDirection.None;
    private float _directionRearmUntil = -1f;
    private bool _sideRearmed = true;
    private float _sideNeutralSince = -1f;

    void Start()
    {
        ConnectDevice();
    }

    void OnDisable()
    {
        if (caster != null)
            caster.ClearDirectionalSwingHeld();
        _activeDirection = MotionDirection.None;
        _directionRearmUntil = -1f;
        _sideRearmed = true;
        _sideNeutralSince = -1f;
    }

    void Update()
    {
        if (caster == null || _deviceId < 0)
            return;

        if (onlyWhenTensionState && caster.CurrentState != BobberArcCaster.State.Tension)
        {
            PushDirection(MotionDirection.None);
            return;
        }

        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        JSL.IMU_STATE imu = JSL.JslGetIMUState(_deviceId);

        Vector3 gyroRaw = new Vector3(imu.gyroX, imu.gyroY, imu.gyroZ);
        float smoothK = 1f - Mathf.Exp(-Mathf.Max(0.01f, gyroSmooth) * dt);
        _gyro = Vector3.Lerp(_gyro, gyroRaw, smoothK);

        float upValue = upGyroSign * GetAxis(_gyro, upGyroAxis);
        float sideValue = sideGyroSign * GetAxis(_gyro, sideGyroAxis);

        MotionDirection nextDirection = ResolveDirection(upValue, sideValue);
        PushDirection(nextDirection);
    }

    private void ConnectDevice()
    {
        int count = JSL.JslConnectDevices();
        _handles = new int[Mathf.Max(0, count)];
        if (count > 0)
            JSL.JslGetConnectedDeviceHandles(_handles, _handles.Length);

        if (_handles.Length == 0)
        {
            Debug.LogWarning("JoyConDirectionalSwingInput: no JoyShockLibrary device found.");
            _deviceId = -1;
            enabled = false;
            return;
        }

        _deviceId = _handles[Mathf.Clamp(deviceIndex, 0, _handles.Length - 1)];
        Debug.Log($"JoyConDirectionalSwingInput using device handle: {_deviceId}");
    }

    private MotionDirection ResolveDirection(float upValue, float sideValue)
    {
        UpdateSideRearmState(sideValue);

        switch (_activeDirection)
        {
            case MotionDirection.Up:
                if (upValue >= upExitDps) return MotionDirection.Up;
                _directionRearmUntil = Time.time + Mathf.Max(0f, directionRearmDelay);
                return MotionDirection.None;
            case MotionDirection.Left:
                if (sideValue <= -sideExitDps) return MotionDirection.Left;
                _sideRearmed = false;
                _sideNeutralSince = -1f;
                _directionRearmUntil = Time.time + Mathf.Max(0f, directionRearmDelay);
                return MotionDirection.None;
            case MotionDirection.Right:
                if (sideValue >= sideExitDps) return MotionDirection.Right;
                _sideRearmed = false;
                _sideNeutralSince = -1f;
                _directionRearmUntil = Time.time + Mathf.Max(0f, directionRearmDelay);
                return MotionDirection.None;
        }

        if (Time.time < _directionRearmUntil)
            return MotionDirection.None;

        float upScore = upValue >= upEnterDps
            ? (upValue / Mathf.Max(0.01f, upEnterDps)) * Mathf.Max(0.01f, upPriorityBias)
            : 0f;
        float leftScore = sideValue <= -sideEnterDps
            ? (-sideValue / Mathf.Max(0.01f, sideEnterDps))
            : 0f;
        float rightScore = sideValue >= sideEnterDps
            ? (sideValue / Mathf.Max(0.01f, sideEnterDps))
            : 0f;

        // Prevent opposite-side triggers on swing rebound until we re-arm in neutral.
        if (!_sideRearmed)
        {
            leftScore = 0f;
            rightScore = 0f;
        }

        if (upScore <= 0f && leftScore <= 0f && rightScore <= 0f)
            return MotionDirection.None;

        if (upScore >= leftScore && upScore >= rightScore)
            return MotionDirection.Up;
        if (leftScore >= rightScore)
            return MotionDirection.Left;
        return MotionDirection.Right;
    }

    private void PushDirection(MotionDirection direction)
    {
        // Update held flags every frame (fishing uses this)
        if (_activeDirection == direction && caster != null)
        {
            caster.SetDirectionalSwingHeld(
                direction == MotionDirection.Up,
                direction == MotionDirection.Left,
                direction == MotionDirection.Right);
            return;
        }

        _activeDirection = direction;

        if (direction == MotionDirection.Left || direction == MotionDirection.Right)
        {
            _sideRearmed = false;
            _sideNeutralSince = -1f;
        }

        if (logDirectionChanges && _activeDirection != MotionDirection.None)
            Debug.Log($"JoyCon directional swing: {_activeDirection}");

        if (caster != null)
        {
            caster.SetDirectionalSwingHeld(
                direction == MotionDirection.Up,
                direction == MotionDirection.Left,
                direction == MotionDirection.Right);
        }
    }

    private static float GetAxis(Vector3 v, Axis axis)
    {
        return axis == Axis.X ? v.x : (axis == Axis.Y ? v.y : v.z);
    }

    private void UpdateSideRearmState(float sideValue)
    {
        if (_sideRearmed)
            return;

        if (Mathf.Abs(sideValue) <= Mathf.Max(0f, sideNeutralRearmDps))
        {
            if (_sideNeutralSince < 0f)
            {
                _sideNeutralSince = Time.time;
            }
            else if ((Time.time - _sideNeutralSince) >= Mathf.Max(0f, sideNeutralHoldTime))
            {
                _sideRearmed = true;
            }
        }
        else
        {
            _sideNeutralSince = -1f;
        }
    }
}
