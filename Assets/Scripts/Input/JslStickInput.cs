using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class JslStickInput : MonoBehaviour
{
    private const string DLL = "JoyShockLibrary";

    [StructLayout(LayoutKind.Sequential)]
    public struct JOY_SHOCK_STATE
    {
        public int buttons;
        public float lTrigger, rTrigger;
        public float stickLX, stickLY;
        public float stickRX, stickRY;
    }

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern int JslConnectDevices();

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern int JslGetConnectedDeviceHandles([Out] int[] deviceHandleArray, int size);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool JslStillConnected(int deviceId);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern JOY_SHOCK_STATE JslGetSimpleState(int deviceId);

    [Header("Device")]
    public int deviceIndex = 0;
    public bool useAnyConnectedDevice = true;
    [Min(0.1f)] public float reconnectInterval = 2f;

    [Header("Stick")]
    public bool autoDetectStickSide = true;
    public bool useRightStick = false; // Joy-Cons typically use the single stick; keep false unless needed
    [Range(0f, 0.5f)] public float deadzone = 0.15f;
    public bool invertY = false;

    [Header("Debug")]
    public bool showDebugOverlay = true;
    public KeyCode toggleDebugKey = KeyCode.F3;
    [Min(0.1f)] public float missingDeviceWarningInterval = 5f;

    public Vector2 Stick { get; private set; }  // -1..1 (approx)
    public bool Connected { get; private set; }
    public int ActiveDeviceId { get; private set; } = -1;

    private int[] _handles = Array.Empty<int>();
    private int _id = -1;
    private float _nextReconnectTime;
    private float _nextMissingDeviceWarningTime;

    void Start()
    {
        Connect();
        _nextReconnectTime = Time.unscaledTime + Mathf.Max(0.1f, reconnectInterval);
    }

    void Update()
    {
        if (toggleDebugKey != KeyCode.None && Input.GetKeyDown(toggleDebugKey))
        {
            showDebugOverlay = !showDebugOverlay;
        }

        if (_handles.Length == 0 && Time.unscaledTime >= _nextReconnectTime)
        {
            Connect();
            _nextReconnectTime = Time.unscaledTime + Mathf.Max(0.1f, reconnectInterval);
        }

        if (_handles.Length == 0)
        {
            Connected = false;
            Stick = Vector2.zero;
            ActiveDeviceId = -1;
            return;
        }

        bool gotAnyState = false;
        Vector2 rawStick = Vector2.zero;
        float bestMagnitudeSq = -1f;
        int bestHandle = -1;

        if (useAnyConnectedDevice)
        {
            foreach (int handle in _handles)
            {
                if (!TryReadRawStick(handle, out Vector2 stick)) continue;
                gotAnyState = true;

                float magnitudeSq = stick.sqrMagnitude;
                if (magnitudeSq > bestMagnitudeSq)
                {
                    bestMagnitudeSq = magnitudeSq;
                    rawStick = stick;
                    bestHandle = handle;
                }
            }
        }
        else
        {
            if (_id < 0 && _handles.Length > 0)
                _id = _handles[Mathf.Clamp(deviceIndex, 0, _handles.Length - 1)];

            if (TryReadRawStick(_id, out Vector2 stick))
            {
                gotAnyState = true;
                rawStick = stick;
                bestHandle = _id;
            }
        }

        if (!gotAnyState)
        {
            Connected = false;
            Stick = Vector2.zero;
            ActiveDeviceId = -1;
            if (Time.unscaledTime >= _nextReconnectTime)
            {
                Connect();
                _nextReconnectTime = Time.unscaledTime + Mathf.Max(0.1f, reconnectInterval);
            }
            return;
        }

        if (invertY) rawStick.y = -rawStick.y;

        Vector2 v = rawStick;

        // Deadzone
        float mag = v.magnitude;
        if (mag < deadzone) v = Vector2.zero;
        else v = v * ((mag - deadzone) / (1f - deadzone)); // re-scale after deadzone

        // Clamp just in case
        Stick = Vector2.ClampMagnitude(v, 1f);
        Connected = true;
        ActiveDeviceId = bestHandle;
    }

    [ContextMenu("Reconnect")]
    public void Connect()
    {
        int count = JslConnectDevices();
        _handles = new int[Mathf.Max(0, count)];
        if (count > 0) JslGetConnectedDeviceHandles(_handles, _handles.Length);

        if (_handles.Length == 0)
        {
            if (Time.unscaledTime >= _nextMissingDeviceWarningTime)
            {
                Debug.LogWarning("JslStickInput: No JoyShockLibrary devices found.");
                _nextMissingDeviceWarningTime = Time.unscaledTime + Mathf.Max(0.1f, missingDeviceWarningInterval);
            }
            Connected = false;
            ActiveDeviceId = -1;
            _id = -1;
            return;
        }

        _id = _handles[Mathf.Clamp(deviceIndex, 0, _handles.Length - 1)];
        ActiveDeviceId = _id;
        Connected = true;
        Debug.Log($"JslStickInput: Connected handles={_handles.Length}, selectedHandle={_id}, useAnyConnectedDevice={useAnyConnectedDevice}");
    }

    private bool TryReadRawStick(int deviceId, out Vector2 rawStick)
    {
        rawStick = Vector2.zero;

        if (deviceId < 0 || !JslStillConnected(deviceId))
            return false;

        var st = JslGetSimpleState(deviceId);

        Vector2 left = new Vector2(st.stickLX, st.stickLY);
        Vector2 right = new Vector2(st.stickRX, st.stickRY);

        if (autoDetectStickSide)
            rawStick = left.sqrMagnitude >= right.sqrMagnitude ? left : right;
        else
            rawStick = useRightStick ? right : left;

        return true;
    }

    void OnGUI()
    {
        // quick visual debug (optional)
        if (!showDebugOverlay) return;

        GUI.Label(new Rect(10, 10, 600, 22), $"JSL Connected={Connected} ActiveHandle={ActiveDeviceId} Stick={Stick}");
    }
}
