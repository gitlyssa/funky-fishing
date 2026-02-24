using System;
using UnityEngine;

// Alternate Joy-Con rhythm input path based on the directional swing gesture style
// used in fishing. This plugs into RhythmInputProcessorT via IRhythmInputT.
public class JoyConDirectionalRhythmProvider : MonoBehaviour, IRhythmInputT
{
    public event Action<FlickDirection> OnFlick;
    public event Action<int> OnButtonDown;

    private enum MotionDirection { None, Up, Left, Right }

    [Header("Optional Shared Source")]
    [SerializeField] private JoyConDirectionalSwingInput directionalSwingInput;
    [SerializeField] private JoyConGestureDetector gestureDetector;
    [SerializeField] private bool syncSettingsFromDirectionalSwing = true;
    [SerializeField] private bool followGestureDetectorLastTrigger = true;

    [Header("Device")]
    [SerializeField] private int deviceIndex = 0;
    [SerializeField] private bool useAnyConnectedDevice = true;
    [SerializeField] private float reconnectInterval = 2f;

    [Header("Axis Mapping")]
    [SerializeField] private JoyConDirectionalSwingInput.Axis upGyroAxis = JoyConDirectionalSwingInput.Axis.X;
    [SerializeField] private float upGyroSign = 1f;
    [SerializeField] private JoyConDirectionalSwingInput.Axis sideGyroAxis = JoyConDirectionalSwingInput.Axis.Y;
    [SerializeField] private float sideGyroSign = 1f;

    [Header("Filtering")]
    [SerializeField] private float gyroSmooth = 20f;

    [Header("Flick Thresholds (dps)")]
    [SerializeField] private float upEnterDps = 170f;
    [SerializeField] private float upExitDps = 110f;
    [SerializeField] private float sideEnterDps = 170f;
    [SerializeField] private float sideExitDps = 110f;
    [SerializeField] private float upPriorityBias = 1.05f;
    [SerializeField] private float directionRearmDelay = 0.08f;
    [SerializeField] private float sideNeutralRearmDps = 65f;
    [SerializeField] private float sideNeutralHoldTime = 0.03f;

    [Header("Reel")]
    [SerializeField] private bool enableThumbstickReel = true;
    [SerializeField] private float reelDeadzone = 0.15f;

    [Header("Buttons")]
    [SerializeField] private bool emitSouthButtonEvents = true;

    [Header("Debug")]
    [SerializeField] private bool logFlicks = false;

    private int[] _handles = Array.Empty<int>();
    private int _deviceId = -1;
    private float _nextReconnectTime = -1f;
    private bool _warnedMissingDevice;

    private Vector3 _gyro;
    private MotionDirection _activeDirection = MotionDirection.None;
    private float _directionRearmUntil = -1f;
    private bool _sideRearmed = true;
    private float _sideNeutralSince = -1f;

    private float _currentSpinVelocity;
    private float _lastReelAngle;
    private float _accumulatedSpin;
    private Vector2 _reelStick;
    private JSL.JOY_SHOCK_STATE _lastSimpleState;

    private void Start()
    {
        if (gestureDetector == null)
            gestureDetector = FindObjectOfType<JoyConGestureDetector>();

        if (directionalSwingInput == null)
            directionalSwingInput = FindObjectOfType<JoyConDirectionalSwingInput>();

        SyncFromDirectionalSwingIfNeeded();
        ConnectDevice();
    }

    private void OnDisable()
    {
        _activeDirection = MotionDirection.None;
        _directionRearmUntil = -1f;
        _sideRearmed = true;
        _sideNeutralSince = -1f;
        _currentSpinVelocity = 0f;
        _reelStick = Vector2.zero;
    }

    private void Update()
    {
        SyncFromDirectionalSwingIfNeeded();

        if (_deviceId < 0)
        {
            TryReconnect();
            if (_deviceId < 0)
                return;
        }
        else if (!JSL.JslStillConnected(_deviceId))
        {
            _deviceId = -1;
            return;
        }

        int preferredHandle = GetPreferredHandle();
        if (preferredHandle >= 0 && preferredHandle != _deviceId)
        {
            _deviceId = preferredHandle;
            _gyro = Vector3.zero;
            _activeDirection = MotionDirection.None;
            _directionRearmUntil = -1f;
            _sideRearmed = true;
            _sideNeutralSince = -1f;
        }

        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        JSL.IMU_STATE imu = JSL.JslGetIMUState(_deviceId);
        JSL.JOY_SHOCK_STATE state = JSL.JslGetSimpleState(_deviceId);

        Vector3 gyroRaw = new Vector3(imu.gyroX, imu.gyroY, imu.gyroZ);
        float smoothK = 1f - Mathf.Exp(-Mathf.Max(0.01f, gyroSmooth) * dt);
        _gyro = Vector3.Lerp(_gyro, gyroRaw, smoothK);

        float upValue = upGyroSign * GetAxis(_gyro, upGyroAxis);
        float sideValue = sideGyroSign * GetAxis(_gyro, sideGyroAxis);

        MotionDirection nextDirection = ResolveDirection(upValue, sideValue);
        EmitFlickOnDirectionEnter(nextDirection);
        _activeDirection = nextDirection;

        if (enableThumbstickReel)
            UpdateReelFromStick(state);
        else
            _currentSpinVelocity = 0f;

        if (emitSouthButtonEvents)
            HandleSouthButton(state);

        _lastSimpleState = state;
    }

    private void TryReconnect()
    {
        if (Time.unscaledTime < _nextReconnectTime)
            return;

        ConnectDevice();
        _nextReconnectTime = Time.unscaledTime + Mathf.Max(0.1f, reconnectInterval);
    }

    private void ConnectDevice()
    {
        int count = JSL.JslConnectDevices();
        _handles = new int[Mathf.Max(0, count)];
        if (count > 0)
            JSL.JslGetConnectedDeviceHandles(_handles, _handles.Length);

        if (_handles.Length == 0)
        {
            if (!_warnedMissingDevice)
            {
                Debug.LogWarning("JoyConDirectionalRhythmProvider: no JoyShockLibrary device found. Retrying...");
                _warnedMissingDevice = true;
            }

            _deviceId = -1;
            return;
        }

        _warnedMissingDevice = false;
        _deviceId = useAnyConnectedDevice
            ? _handles[0]
            : _handles[Mathf.Clamp(deviceIndex, 0, _handles.Length - 1)];
    }

    private int GetPreferredHandle()
    {
        if (followGestureDetectorLastTrigger && gestureDetector != null && gestureDetector.LastTriggerHandle >= 0)
        {
            int lastHandle = gestureDetector.LastTriggerHandle;
            if (JSL.JslStillConnected(lastHandle))
                return lastHandle;
        }

        if (_deviceId >= 0 && JSL.JslStillConnected(_deviceId))
            return _deviceId;

        if (_handles == null || _handles.Length == 0)
            return -1;

        if (useAnyConnectedDevice)
            return _handles[0];

        return _handles[Mathf.Clamp(deviceIndex, 0, _handles.Length - 1)];
    }

    private void SyncFromDirectionalSwingIfNeeded()
    {
        if (!syncSettingsFromDirectionalSwing || directionalSwingInput == null)
            return;

        deviceIndex = directionalSwingInput.deviceIndex;
        useAnyConnectedDevice = directionalSwingInput.useAnyConnectedDevice;
        reconnectInterval = directionalSwingInput.reconnectInterval;

        upGyroAxis = directionalSwingInput.upGyroAxis;
        upGyroSign = directionalSwingInput.upGyroSign;
        sideGyroAxis = directionalSwingInput.sideGyroAxis;
        sideGyroSign = directionalSwingInput.sideGyroSign;

        gyroSmooth = directionalSwingInput.gyroSmooth;

        upEnterDps = directionalSwingInput.upEnterDps;
        upExitDps = directionalSwingInput.upExitDps;
        sideEnterDps = directionalSwingInput.sideEnterDps;
        sideExitDps = directionalSwingInput.sideExitDps;
        upPriorityBias = directionalSwingInput.upPriorityBias;
        directionRearmDelay = directionalSwingInput.directionRearmDelay;
        sideNeutralRearmDps = directionalSwingInput.sideNeutralRearmDps;
        sideNeutralHoldTime = directionalSwingInput.sideNeutralHoldTime;
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

    private void EmitFlickOnDirectionEnter(MotionDirection nextDirection)
    {
        if (_activeDirection == nextDirection)
            return;

        if (nextDirection == MotionDirection.None)
            return;

        FlickDirection flick = ConvertDirection(nextDirection);
        if (flick == FlickDirection.None)
            return;

        if (logFlicks)
            Debug.Log($"JoyConDirectionalRhythmProvider flick: {flick}");

        OnFlick?.Invoke(flick);
    }

    private void UpdateReelFromStick(JSL.JOY_SHOCK_STATE state)
    {
        _reelStick = new Vector2(state.stickLX + state.stickRX, state.stickLY + state.stickRY);

        if (_reelStick.magnitude > reelDeadzone)
        {
            float currentAngle = Mathf.Atan2(_reelStick.y, _reelStick.x) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(_lastReelAngle, currentAngle);
            _currentSpinVelocity = delta / Mathf.Max(0.0001f, Time.deltaTime);
            _lastReelAngle = currentAngle;
            _accumulatedSpin += delta;
        }
        else
        {
            _currentSpinVelocity = 0f;
        }
    }

    private void HandleSouthButton(JSL.JOY_SHOCK_STATE state)
    {
        bool isDown = (state.buttons & (1 << JSL.ButtonMaskDown)) != 0;
        bool wasDown = (_lastSimpleState.buttons & (1 << JSL.ButtonMaskDown)) != 0;
        if (isDown && !wasDown)
            OnButtonDown?.Invoke(0);
    }

    private static FlickDirection ConvertDirection(MotionDirection direction)
    {
        return direction switch
        {
            MotionDirection.Up => FlickDirection.Up,
            MotionDirection.Left => FlickDirection.Left,
            MotionDirection.Right => FlickDirection.Right,
            _ => FlickDirection.None
        };
    }

    private bool IsHoldingMotion(MotionDirection direction) => _activeDirection == direction;

    public bool IsHoldingDirection(FlickDirection direction)
    {
        if (direction == FlickDirection.Left)
            return IsHoldingMotion(MotionDirection.Left);
        if (direction == FlickDirection.Right)
            return IsHoldingMotion(MotionDirection.Right);
        if (direction == FlickDirection.Up)
            return IsHoldingMotion(MotionDirection.Up);
        return false;
    }

    public float GetSpinVelocity() => _currentSpinVelocity;
    public bool GetButton(int index)
    {
        if (index != 0 || _deviceId < 0 || !JSL.JslStillConnected(_deviceId))
            return false;

        JSL.JOY_SHOCK_STATE state = JSL.JslGetSimpleState(_deviceId);
        return (state.buttons & (1 << JSL.ButtonMaskDown)) != 0;
    }

    public float GetTotalAccumulatedSpin() => _accumulatedSpin;
    public void ResetAccumulatedSpin() => _accumulatedSpin = 0f;
    public Vector2 GetReelStickDirection() => _reelStick;

    private static float GetAxis(Vector3 v, JoyConDirectionalSwingInput.Axis axis)
    {
        return axis == JoyConDirectionalSwingInput.Axis.X
            ? v.x
            : (axis == JoyConDirectionalSwingInput.Axis.Y ? v.y : v.z);
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
