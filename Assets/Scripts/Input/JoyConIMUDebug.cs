using System;
using System.Runtime.InteropServices;
using UnityEngine;

// Minimal IMU logger for verifying Joy-Con sensor data and filtering values.
public class JoyConIMUDebug : MonoBehaviour
{
    private const string DLL = "JoyShockLibrary";

    [StructLayout(LayoutKind.Sequential)]
    public struct IMU_STATE
    {
        public float accelX, accelY, accelZ; // in g
        public float gyroX, gyroY, gyroZ;    // in dps
    }

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern IMU_STATE JslGetIMUState(int deviceId);

    [Header("Which connected device to use")]
    public int deviceIndex = 0;

    [Header("Filtering")]
    public float gravityFollow = 12f; // higher = gravity estimate reacts faster
    public float linAccelSmooth = 25f;

    [Header("Logging")]
    public bool logToConsole = true;
    public float logHz = 10f;

    private int _id;
    private int _knownConnectionRevision = -1;

    private Vector3 _gravity;      // estimated gravity (g)
    private Vector3 _linAccelFilt; // linear accel (g), filtered

    private float _nextLogTime;

    void Start()
    {
        ConnectDevice();
        _knownConnectionRevision = JoyConConnectionService.GetRevision();
    }

    void Update()
    {
        int revision = JoyConConnectionService.GetRevision();
        if (revision != _knownConnectionRevision)
        {
            _knownConnectionRevision = revision;
            ConnectDevice();
        }

        if (_id < 0 || !JoyConConnectionService.IsHandleConnected(_id))
        {
            _id = -1;
            JoyConConnectionService.RequestScan();
            return;
        }

        float dt = Time.deltaTime;

        IMU_STATE imu;
        try
        {
            imu = JslGetIMUState(_id);
        }
        catch
        {
            _id = -1;
            JoyConConnectionService.RequestScan();
            return;
        }
        var accelG = new Vector3(imu.accelX, imu.accelY, imu.accelZ);
        var gyroDps = new Vector3(imu.gyroX, imu.gyroY, imu.gyroZ);

        // Estimate gravity and remove it.
        _gravity = Vector3.Lerp(_gravity, accelG, 1f - Mathf.Exp(-gravityFollow * dt));
        var lin = accelG - _gravity;

        // Smooth linear accel a bit.
        _linAccelFilt = Vector3.Lerp(_linAccelFilt, lin, 1f - Mathf.Exp(-linAccelSmooth * dt));

        // Log at logHz so the console doesn't spam.
        if (logToConsole && Time.time >= _nextLogTime)
        {
            _nextLogTime = Time.time + (1f / Mathf.Max(1f, logHz));

            Debug.Log(
                $"RAW accel(g)=({accelG.x:F2},{accelG.y:F2},{accelG.z:F2})  " +
                $"LIN accel(g)=({_linAccelFilt.x:F2},{_linAccelFilt.y:F2},{_linAccelFilt.z:F2})  " +
                $"gyro(dps)=({gyroDps.x:F0},{gyroDps.y:F0},{gyroDps.z:F0})"
            );
        }
    }

    private void ConnectDevice()
    {
        int[] handles = JoyConConnectionService.GetConnectedHandles();
        if (handles == null || handles.Length == 0)
        {
            _id = -1;
            JoyConConnectionService.RequestScan();
            return;
        }

        _id = handles[Mathf.Clamp(deviceIndex, 0, handles.Length - 1)];
        Debug.Log($"JoyConIMUDebug using device handle {_id} (deviceIndex={deviceIndex}, total={handles.Length})");
    }
}
