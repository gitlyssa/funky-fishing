using UnityEngine;
using UnityEngine.InputSystem;
public class JoyconRhythmProvider : MonoBehaviour, IRhythmInputT
{
    public event System.Action<FlickDirection> OnFlick;
    public event System.Action<int> OnButtonDown;

    [Header("Connection")]
    public int deviceId = 0;    

    [Header("Joycon Flick Settings")]
    public float flickThreshold = 2.5f; // G-force threshold (1.0 is gravity)
    public float resetThreshold = 1.2f; // Must drop below this to flick again
    public float holdingDeadzone = 0.4f;
    private bool _hasTriggeredFlick;

    [Header("Reel Settings")]
    public float deadzone = 0.15f; 

    private Vector2 _virtualStick; // Derived from Gravity Tilt
    private Vector2 _reelStick;    // Derived from Physical Thumbstick
    private float _currentSpinVelocity;
    private float _lastReelAngle;
    private float _accumulatedSpin;
    private bool _isFlicking;
    private JSL.JOY_SHOCK_STATE _lastSimpleState;

    void Start()
    {
        JSL.JslConnectDevices();
    }

    void OnApplicationQuit()
    {
        JSL.JslDisconnectAndDisposeAll();
    }
    void Update()
    {
        int[] handles = new int[16];
        int count = JSL.JslGetConnectedDeviceHandles(handles, handles.Length);
        if (count <= 0)
            return;

        int selectedHandle = ResolveConnectedHandle(handles, count);
        if (selectedHandle < 0)
            return;

        deviceId = selectedHandle;
        JSL.JOY_SHOCK_STATE state = JSL.JslGetSimpleState(deviceId);
        JSL.MOTION_STATE motion = JSL.JslGetMotionState(deviceId);

        HandleMotionFlick(motion);    // Accelerometer Logic
        HandleThumbstickReel(state); // Physical Stick Logic
        HandleButtons(state);

        _lastSimpleState = state;
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
        {
            _isFlicking = false;
        }
    }
    private void HandleThumbstickReel(JSL.JOY_SHOCK_STATE state)
    {
        _reelStick = new Vector2(state.stickLX + state.stickRX, state.stickLY + state.stickRY);

        if (_reelStick.magnitude > deadzone)
        {
            float currentAngle = Mathf.Atan2(_reelStick.y, _reelStick.x) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(_lastReelAngle, currentAngle);

            _currentSpinVelocity = delta / Time.deltaTime;
            _lastReelAngle = currentAngle;
            _accumulatedSpin += delta;
        }
        else
        {
            _currentSpinVelocity = 0;
        }
    }
    private void HandleButtons(JSL.JOY_SHOCK_STATE state)
    {
        // South Button check (Masking bit 15 - N/Down depending on Joycon)
        bool isDown = (state.buttons & (1 << JSL.ButtonMaskDown)) != 0;
        bool wasDown = (_lastSimpleState.buttons & (1 << JSL.ButtonMaskDown)) != 0;

        if (isDown && !wasDown) OnButtonDown?.Invoke(0);
    }
    private void StopRumble() => JSL.JslSetRumble(deviceId, 0, 0);
    public bool IsHoldingDirection(FlickDirection direction) => GetDirectionFromVector(_virtualStick) == direction;
    public float GetSpinVelocity() => _currentSpinVelocity;
    public float GetTotalAccumulatedSpin() => _accumulatedSpin;
    public void ResetAccumulatedSpin() => _accumulatedSpin = 0f;
    public Vector2 GetReelStickDirection() => _reelStick;
    public bool GetButton(int index)
    {
        if (!JSL.JslStillConnected(deviceId))
            return false;

        return (JSL.JslGetSimpleState(deviceId).buttons & (1 << JSL.ButtonMaskDown)) != 0;
    }

    private int ResolveConnectedHandle(int[] handles, int count)
    {
        if (JSL.JslStillConnected(deviceId))
            return deviceId;

        for (int i = 0; i < count; i++)
        {
            int handle = handles[i];
            if (JSL.JslStillConnected(handle))
                return handle;
        }

        return -1;
    }

    // --- Helper Math ---

    private FlickDirection GetDirectionFromAccel(Vector3 acc)
    {
        float x = acc.x;
        float y = acc.y;
        float absX = Mathf.Abs(x);
        float absY = Mathf.Abs(y);

        // bool isDiagonal = (Mathf.Min(absX, absY) / Mathf.Max(absX, absY)) > 0.4f;

        // if (isDiagonal)
        // {
        //     if (x > 0 && y > 0) return FlickDirection.UpRight;
        //     if (x < 0 && y > 0) return FlickDirection.UpLeft;
        //     if (x < 0 && y < 0) return FlickDirection.DownLeft;
        //     if (x > 0 && y < 0) return FlickDirection.DownRight;
        // }

        if (absX > absY)
            return x > 0 ? FlickDirection.Right : FlickDirection.Left;
        else
            return y > 0 ? FlickDirection.Up : FlickDirection.Down;
    }

    
    private FlickDirection GetDirectionFromVector(Vector2 v)
    {
        if (v.magnitude < 0.4f) return FlickDirection.None;
        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;

        if (angle <= 22.5f || angle > 337.5f) return FlickDirection.Right;
        if (angle > 67.5f && angle <= 112.5f) return FlickDirection.Up;
        if (angle > 157.5f && angle <= 202.5f) return FlickDirection.Left;
        if (angle > 247.5f && angle <= 292.5f) return FlickDirection.Down;
        return FlickDirection.None;
    }
}
