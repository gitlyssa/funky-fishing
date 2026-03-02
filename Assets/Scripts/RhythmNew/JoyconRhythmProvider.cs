using System;
using UnityEngine;

public class JoyconRhythmProvider : MonoBehaviour, IRhythmInputT
{
    public event Action<FlickDirection> OnFlick;
    public event Action<int> OnButtonDown;

    [Header("Connection")]
    public int rodDeviceId = -1;   // Joy-Con 1: Flicks & Buttons
    public int crankDeviceId = -1; // Joy-Con 2: Accelerometer Reeling
    [SerializeField] private bool autoAssignDualMode = true;
    [SerializeField] private bool useAnyConnectedDevice = true;
    [SerializeField] private int deviceIndex = 0;
    [SerializeField, Min(0.1f)] private float reconnectInterval = 1.5f;

    [Header("Joycon Flick Settings")]

    public float flickThreshold = 2.5f; // Minimum acceleration magnitude to register a flick
    public float resetThreshold = 1.5f; // Acceleration magnitude below which we
    public float holdingDeadzone = 0.4f;
    public float enterThreshold = 3.0f; 
    public float exitThreshold = 1.5f;  

    public float gyroSnapThreshold = 150f; // Degrees per second to trigger snap
    public float gyroSnapResetThreshold = 80f; // Degrees per second to reset snap state
    private Vector3 _peakDirectionVector = Vector3.zero;
    private bool _isAwaitingSnap = false;

    [Header("Simplified Gyro Gate")]
    public float gyroEntryDps = 170f; 
    public float gyroNeutralDps = 60f; 


    [Header("Reel Settings")]
    public float deadzone = 0.15f;
    [Header("Accelerometer Crank Settings")]
    public float crankRadiusThreshold = 0.2f; // Minimum "size" of the circle to count
    public float accelFilterSmoothing = 15f; // To smooth out shaky hands

    private Vector2 _smoothedAccel;
    private float _lastCrankAngle;

    private readonly int[] _handlesBuffer = new int[16];
    private int[] _connectedHandles = Array.Empty<int>();
    private float _nextReconnectTime;
    private bool _warnedMissingDevices;

    [Header("Flick Variables")]
    private Vector2 _virtualStick; // Derived from Gravity Tilt
    private bool _isFlicking;
    private Vector3 _lastFlickVector; // For direction consistency checks

    private Vector3 _lastGyroVector; // For snap direction consistency

    [Header("Rebound Protection")]
    public float reboundLockoutTime = 0.08f; // 80ms window to ignore rebounds
    public float requiredIntensityRatio = 0.6f; // New flick must be 60% as strong as previous peak

    private float _lockoutTimer = 0f;
    private float _lastPeakSpeed = 0f;
    public float directionChangeThreshold = -0.2f; // -1 is perfectly opposite

    [Header("Reel Variables")]
    private Vector2 _reelStick;    // Derived from Physical Thumbstick
    private float _currentSpinVelocity;
    private float _lastReelAngle;
    private float _accumulatedSpin;
    
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

        if (rodDeviceId >= 0)
        {
            JSL.JOY_SHOCK_STATE rodState = JSL.JslGetSimpleState(rodDeviceId);
            JSL.MOTION_STATE rodMotion = JSL.JslGetMotionState(rodDeviceId);
            JSL.IMU_STATE rodImu = JSL.JslGetIMUState(rodDeviceId);

            HandleMotionFlick2(rodMotion, rodImu);
            HandleButtons(rodState);
            
            HandleThumbstickReel(rodState);
            _lastSimpleState = rodState;
            
        }

        // --- POLL CRANK DEVICE (Circular Motion) ---
        if (crankDeviceId >= 0)
        {
            JSL.MOTION_STATE crankMotion = JSL.JslGetMotionState(crankDeviceId);
            HandleAccelerometerCrank(crankMotion);
            
        }

        
    }


    private void HandleMotionFlick2(JSL.MOTION_STATE motion, JSL.IMU_STATE imu)
    {
        Vector2 userAcc2D = new Vector2(
            motion.accelX - motion.gravX,
            motion.accelY - motion.gravY
        );

        // get gyro for flick logic
        Vector3 gyroVel = new Vector3(
            -imu.gyroY,
            imu.gyroX
        );

        float gyroSpeed = gyroVel.magnitude;
        float force = userAcc2D.magnitude;

        _lockoutTimer -= Time.deltaTime;

        if (_isFlicking)
        {


            if (gyroSpeed > _lastPeakSpeed) _lastPeakSpeed = gyroSpeed;


            if (_lockoutTimer <= 0)
            {
                float dot = Vector2.Dot(gyroVel.normalized, _lastFlickVector.normalized);
                
                
                if (dot < directionChangeThreshold && gyroSpeed > (_lastPeakSpeed * requiredIntensityRatio))
                {
                    _isFlicking = false; 
                }
            }


            if (gyroSpeed < gyroNeutralDps)
            {
                _isFlicking = false;
                _lastPeakSpeed = 0f;
            }
            return; 
        }
        if (gyroSpeed > gyroEntryDps && _lockoutTimer <= 0  )
        {
            FlickDirection dir = GetDirectionFromGyro(gyroVel);
            if (dir != FlickDirection.None)
            {
                OnFlick?.Invoke(dir);
                _isFlicking = true; 
                _lastFlickVector = gyroVel;
                _lastPeakSpeed = gyroSpeed;
                _lockoutTimer = reboundLockoutTime; // START THE GUARD
            }
        }

        // if (_isFlicking)
        // {
            
        //     if (force < exitThreshold)
        //     {
        //         _isFlicking = false;
        //     }
        //     return; 
        // }


        // if (force > enterThreshold)
        // {
        //     FlickDirection dir = GetDirectionFromAccel(userAcc2D);
        //     if (dir != FlickDirection.None)
        //     {
        //         OnFlick?.Invoke(dir);
        //         _isFlicking = true; 

        //     }
        // }
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

    private void HandleAccelerometerCrank(JSL.MOTION_STATE motion)
    {

        Vector2 rawInput = new Vector2(motion.accelX - motion.gravX, motion.accelY - motion.gravY);

        _smoothedAccel = Vector2.Lerp(_smoothedAccel, rawInput, Time.deltaTime * accelFilterSmoothing);

        if (_smoothedAccel.magnitude > crankRadiusThreshold)
        {
            float currentAngle = Mathf.Atan2(_smoothedAccel.y, _smoothedAccel.x) * Mathf.Rad2Deg;
            
            float delta = Mathf.DeltaAngle(_lastCrankAngle, currentAngle);

            _currentSpinVelocity = delta / Mathf.Max(0.0001f, Time.deltaTime);
            _accumulatedSpin += delta;
            _lastCrankAngle = currentAngle;

            float rad = currentAngle * Mathf.Deg2Rad;
            _reelStick = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
        else
        {
            _currentSpinVelocity = Mathf.MoveTowards(_currentSpinVelocity, 0, Time.deltaTime * 100f);
        }
    }

    private void HandleButtons(JSL.JOY_SHOCK_STATE state)
    {
        bool isDown = (state.buttons & (1 << JSL.ButtonMaskDown)) != 0;
        bool wasDown = (_lastSimpleState.buttons & (1 << JSL.ButtonMaskDown)) != 0;

        if (isDown && !wasDown)
            OnButtonDown?.Invoke(0);
    }

    private void StopRumble() => JSL.JslSetRumble(rodDeviceId, 0, 0);
    public bool IsHoldingDirection(FlickDirection direction) => GetDirectionFromVector(_virtualStick) == direction;
    public float GetSpinVelocity() => _currentSpinVelocity;
    public float GetTotalAccumulatedSpin() => _accumulatedSpin;
    public void ResetAccumulatedSpin() => _accumulatedSpin = 0f;
    public Vector2 GetReelStickDirection() => _reelStick;
    private bool TryEnsureActiveDevice()
{

    if (rodDeviceId >= 0 && JSL.JslStillConnected(rodDeviceId))
        return true;

    if (Time.unscaledTime >= _nextReconnectTime)
        ReconnectDevices();


    if (rodDeviceId >= 0 && JSL.JslStillConnected(rodDeviceId))
        return true;

    _currentSpinVelocity = 0f;
    return false;
}

    private void ReconnectDevices()
    {
        int count = JSL.JslConnectDevices();
        
        rodDeviceId = -1;
        crankDeviceId = -1;

        if (count <= 0)
        {
            _connectedHandles = Array.Empty<int>();
            _nextReconnectTime = Time.unscaledTime + Mathf.Max(0.1f, reconnectInterval);
            if (!_warnedMissingDevices)
            {
                Debug.LogWarning("JoyconRhythmProvider: No devices found.");
                _warnedMissingDevices = true;
            }
            return;
        }

        int copiedCount = JSL.JslGetConnectedDeviceHandles(_handlesBuffer, _handlesBuffer.Length);
        _connectedHandles = new int[copiedCount];
        Array.Copy(_handlesBuffer, _connectedHandles, copiedCount);
        
        _warnedMissingDevices = false;
        _nextReconnectTime = Time.unscaledTime + Mathf.Max(0.1f, reconnectInterval);

        if (_connectedHandles.Length >= 2)
        {
            rodDeviceId = _connectedHandles[0];
            crankDeviceId = _connectedHandles[1];
            Debug.Log($"[Dual Mode] Rod ID: {rodDeviceId}, Crank ID: {crankDeviceId}");
        }
        else if (_connectedHandles.Length == 1)
        {
            rodDeviceId = _connectedHandles[0];
            Debug.Log($"[Single Mode] Rod ID: {rodDeviceId}");
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

    public bool GetButton(int index)
    {

        if (index != 0 || rodDeviceId < 0 || !JSL.JslStillConnected(rodDeviceId))
            return false;

        return (JSL.JslGetSimpleState(rodDeviceId).buttons & (1 << JSL.ButtonMaskDown)) != 0;
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

    private FlickDirection GetDirectionFromGyro(Vector3 gyroVelocity)
    {
        float x = gyroVelocity.x;
        float y = gyroVelocity.y;
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
