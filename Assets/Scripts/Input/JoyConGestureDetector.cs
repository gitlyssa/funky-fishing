using System;
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
    private static extern int JslConnectDevices();

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern int JslGetConnectedDeviceHandles([Out] int[] deviceHandleArray, int size);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern IMU_STATE JslGetIMUState(int deviceId);

    [Header("Device")]
    public int deviceIndex = 0;

    [Header("Axis mapping (make CAST positive on both)")]
    public Axis forwardAxis = Axis.Z;     // “thrust forward/back” axis
    public float forwardSign = 1f;        // flip to -1 if backwards
    public Axis swingGyroAxis = Axis.X;   // “swing” axis (rotation)
    public float gyroSign = 1f;           // flip to -1 if backwards

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

    [Header("Events")]
    public UnityEvent onCast;
    public UnityEvent onYank;

    [Header("Debug")]
    public bool logTriggers = true;

    private int[] _handles = Array.Empty<int>();
    private int _id;

    private Vector3 _gravity;      // g
    private Vector3 _linAccel;     // g (filtered)
    private Vector3 _gyro;         // dps (filtered)

    private enum State { Idle, Casted, Cooldown }
    private State _state = State.Idle;
    private float _castTime = -999f;
    private float _cooldownUntil = -999f;

    void Start()
    {
        // Discover and select a Joy-Con device to read IMU data from.
        int count = JslConnectDevices();
        _handles = new int[Mathf.Max(0, count)];
        if (count > 0) JslGetConnectedDeviceHandles(_handles, _handles.Length);

        if (_handles.Length == 0)
        {
            Debug.LogError("No JoyShockLibrary devices found.");
            enabled = false;
            return;
        }

        _id = _handles[Mathf.Clamp(deviceIndex, 0, _handles.Length - 1)];
        Debug.Log($"JoyConGestureDetector using device handle: {_id}");
    }

    void Update()
    {
        if (Time.time < _cooldownUntil) return;

        float dt = Time.deltaTime;

        var imu = JslGetIMUState(_id);
        var accelG = new Vector3(imu.accelX, imu.accelY, imu.accelZ);
        var gyroDps = new Vector3(imu.gyroX, imu.gyroY, imu.gyroZ);

        // Estimate gravity and remove it to get linear acceleration.
        _gravity = Vector3.Lerp(_gravity, accelG, 1f - Mathf.Exp(-gravityFollow * dt));
        var lin = accelG - _gravity;

        // Smooth noisy signals before thresholding.
        _linAccel = Vector3.Lerp(_linAccel, lin, 1f - Mathf.Exp(-linAccelSmooth * dt));
        _gyro = Vector3.Lerp(_gyro, gyroDps, 1f - Mathf.Exp(-gyroSmooth * dt));

        // Project onto the configured axes and normalize sign.
        float forwardLin = forwardSign * GetAxis(_linAccel, forwardAxis);
        float swingGyro  = gyroSign * GetAxis(_gyro, swingGyroAxis);

        switch (_state)
        {
            case State.Idle:
                // CAST gesture = forward linear accel + swing gyro in the positive direction.
                if (forwardLin > castForwardLinG && swingGyro > castGyroDps)
                {
                    if (logTriggers) Debug.Log($"CAST! lin={forwardLin:F2}g gyro={swingGyro:F0}dps");
                    onCast?.Invoke();
                    _castTime = Time.time;
                    _state = State.Casted;
                    _cooldownUntil = Time.time + cooldownAfterTrigger;
                }
                break;

            case State.Casted:
                if (Time.time - _castTime < minTimeBetweenCastAndYank) break;

                // YANK gesture = pull back linear accel + opposite swing gyro.
                if (forwardLin < -yankBackLinG && swingGyro < -yankGyroDps)
                {
                    if (logTriggers) Debug.Log($"YANK! lin={forwardLin:F2}g gyro={swingGyro:F0}dps");
                    onYank?.Invoke();
                    _state = State.Cooldown;
                    _cooldownUntil = Time.time + cooldownAfterTrigger;
                }
                break;

            case State.Cooldown:
                // One-frame reset so we can re-arm after the cooldown window.
                _state = State.Idle;
                break;
        }
    }

    private static float GetAxis(Vector3 v, Axis a) => a == Axis.X ? v.x : (a == Axis.Y ? v.y : v.z);
}
