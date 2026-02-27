using System;
using UnityEngine;

public class JoyconRhythmProvider : MonoBehaviour, IRhythmInputT
{
    public event Action<FlickDirection> OnFlick;
    public event Action<int> OnButtonDown;

    [Header("Connection")]
    public int deviceId = -1;
    [SerializeField] private bool useAnyConnectedDevice = true;
    [SerializeField] private int deviceIndex = 0;
    [SerializeField, Min(0.1f)] private float reconnectInterval = 1.5f;

    [Header("Joycon Flick Settings")]
    public float flickThreshold = 2.5f; // G-force threshold (1.0 is gravity)
    public float resetThreshold = 1.2f; // Must drop below this to flick again
    public float holdingDeadzone = 0.4f;

    [Header("Reel Settings")]
    public float deadzone = 0.15f;

    private readonly int[] _handlesBuffer = new int[16];
    private int[] _connectedHandles = Array.Empty<int>();
    private float _nextReconnectTime;
    private bool _warnedMissingDevices;

    private Vector2 _virtualStick; // Derived from Gravity Tilt
    private Vector2 _reelStick;    // Derived from Physical Thumbstick
    private float _currentSpinVelocity;
    private float _lastReelAngle;
    private float _accumulatedSpin;
    private bool _isFlicking;
    private JSL.JOY_SHOCK_STATE _lastSimpleState;

    private void Start()
    {
        ReconnectDevices();
    }

    private void OnApplicationQuit()
    {
        JSL.JslDisconnectAndDisposeAll();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
        {
            _currentSpinVelocity = 0f;
            return;
        }

        if (!TryEnsureActiveDevice())
            return;

        JSL.JOY_SHOCK_STATE state = JSL.JslGetSimpleState(deviceId);
        JSL.MOTION_STATE motion = JSL.JslGetMotionState(deviceId);

        HandleMotionFlick(motion);    // Accelerometer Logic
        HandleThumbstickReel(state);  // Physical Stick Logic
        HandleButtons(state);

        _lastSimpleState = state;
    }

    private bool TryEnsureActiveDevice()
    {
        if (deviceId >= 0 && JSL.JslStillConnected(deviceId))
            return true;

        if (Time.unscaledTime >= _nextReconnectTime)
            ReconnectDevices();

        if (deviceId >= 0 && JSL.JslStillConnected(deviceId))
            return true;

        _currentSpinVelocity = 0f;
        return false;
    }

    private void ReconnectDevices()
    {
        int count = JSL.JslConnectDevices();
        if (count <= 0)
        {
            _connectedHandles = Array.Empty<int>();
            deviceId = -1;
            _nextReconnectTime = Time.unscaledTime + Mathf.Max(0.1f, reconnectInterval);

            if (!_warnedMissingDevices)
            {
                Debug.LogWarning("JoyconRhythmProvider: no JoyShockLibrary devices found.");
                _warnedMissingDevices = true;
            }
            return;
        }

        int copiedCount = JSL.JslGetConnectedDeviceHandles(_handlesBuffer, _handlesBuffer.Length);
        copiedCount = Mathf.Clamp(copiedCount, 0, _handlesBuffer.Length);

        _connectedHandles = new int[copiedCount];
        Array.Copy(_handlesBuffer, _connectedHandles, copiedCount);
        _warnedMissingDevices = false;
        _nextReconnectTime = Time.unscaledTime + Mathf.Max(0.1f, reconnectInterval);

        if (useAnyConnectedDevice)
        {
            deviceId = FindFirstConnectedHandle(_connectedHandles);
        }
        else if (_connectedHandles.Length > 0)
        {
            int idx = Mathf.Clamp(deviceIndex, 0, _connectedHandles.Length - 1);
            deviceId = _connectedHandles[idx];
        }
        else
        {
            deviceId = -1;
        }
    }

    private static int FindFirstConnectedHandle(int[] handles)
    {
        for (int i = 0; i < handles.Length; i++)
        {
            int handle = handles[i];
            if (handle >= 0 && JSL.JslStillConnected(handle))
                return handle;
        }

        return -1;
    }

    private void HandleMotionFlick(JSL.MOTION_STATE motion)
    {
        Vector3 userAcc = new Vector3(
            motion.accelX - motion.gravX,
            motion.accelY - motion.gravY,
            motion.accelZ - motion.gravZ
        );

        float force = userAcc.magnitude;

        if (force > flickThreshold && !_isFlicking)
        {
            FlickDirection dir = GetDirectionFromAccel(userAcc);
            if (dir != FlickDirection.None)
            {
                OnFlick?.Invoke(dir);
                _isFlicking = true;
            }
        }

        if (force < resetThreshold)
            _isFlicking = false;
    }

    private void HandleThumbstickReel(JSL.JOY_SHOCK_STATE state)
    {
        _reelStick = new Vector2(state.stickLX + state.stickRX, state.stickLY + state.stickRY);

        if (_reelStick.magnitude > deadzone)
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

    private void HandleButtons(JSL.JOY_SHOCK_STATE state)
    {
        bool isDown = (state.buttons & (1 << JSL.ButtonMaskDown)) != 0;
        bool wasDown = (_lastSimpleState.buttons & (1 << JSL.ButtonMaskDown)) != 0;

        if (isDown && !wasDown)
            OnButtonDown?.Invoke(0);
    }

    private void StopRumble() => JSL.JslSetRumble(deviceId, 0, 0);
    public bool IsHoldingDirection(FlickDirection direction) => GetDirectionFromVector(_virtualStick) == direction;
    public float GetSpinVelocity() => _currentSpinVelocity;
    public float GetTotalAccumulatedSpin() => _accumulatedSpin;
    public void ResetAccumulatedSpin() => _accumulatedSpin = 0f;
    public Vector2 GetReelStickDirection() => _reelStick;

    public bool GetButton(int index)
    {
        if (index != 0)
            return false;

        if (deviceId < 0 || !JSL.JslStillConnected(deviceId))
            return false;

        return (JSL.JslGetSimpleState(deviceId).buttons & (1 << JSL.ButtonMaskDown)) != 0;
    }

    private FlickDirection GetDirectionFromAccel(Vector3 acc)
    {
        float x = acc.x;
        float y = acc.y;
        float absX = Mathf.Abs(x);
        float absY = Mathf.Abs(y);

        if (absX > absY)
            return x > 0 ? FlickDirection.Right : FlickDirection.Left;

        return y > 0 ? FlickDirection.Up : FlickDirection.Down;
    }

    private FlickDirection GetDirectionFromVector(Vector2 v)
    {
        if (v.magnitude < holdingDeadzone)
            return FlickDirection.None;

        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        if (angle < 0f)
            angle += 360f;

        if (angle <= 22.5f || angle > 337.5f) return FlickDirection.Right;
        if (angle > 67.5f && angle <= 112.5f) return FlickDirection.Up;
        if (angle > 157.5f && angle <= 202.5f) return FlickDirection.Left;
        if (angle > 247.5f && angle <= 292.5f) return FlickDirection.Down;
        return FlickDirection.None;
    }
}
