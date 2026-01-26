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

    [Header("Stick")]
    public bool useRightStick = false; // Joy-Cons typically use the single stick; keep false unless needed
    [Range(0f, 0.5f)] public float deadzone = 0.15f;
    public bool invertY = false;

    public Vector2 Stick { get; private set; }  // -1..1 (approx)
    public bool Connected { get; private set; }

    private int[] _handles = Array.Empty<int>();
    private int _id;

    void Start()
    {
        Connect();
    }

    void Update()
    {
        if (!Connected || !JslStillConnected(_id))
        {
            Connected = false;
            Stick = Vector2.zero;
            return;
        }

        var st = JslGetSimpleState(_id);

        float x = useRightStick ? st.stickRX : st.stickLX;
        float y = useRightStick ? st.stickRY : st.stickLY;

        if (invertY) y = -y;

        Vector2 v = new Vector2(x, y);

        // Deadzone
        float mag = v.magnitude;
        if (mag < deadzone) v = Vector2.zero;
        else v = v * ((mag - deadzone) / (1f - deadzone)); // re-scale after deadzone

        // Clamp just in case
        Stick = Vector2.ClampMagnitude(v, 1f);
    }

    [ContextMenu("Reconnect")]
    public void Connect()
    {
        int count = JslConnectDevices();
        _handles = new int[Mathf.Max(0, count)];
        if (count > 0) JslGetConnectedDeviceHandles(_handles, _handles.Length);

        if (_handles.Length == 0)
        {
            Debug.LogWarning("JslStickInput: No JoyShockLibrary devices found.");
            Connected = false;
            return;
        }

        _id = _handles[Mathf.Clamp(deviceIndex, 0, _handles.Length - 1)];
        Connected = true;
        Debug.Log($"JslStickInput: Connected handle={_id} (deviceIndex={deviceIndex}, total={_handles.Length})");
    }

    void OnGUI()
    {
        // quick visual debug (optional)
        GUI.Label(new Rect(10, 10, 400, 22), $"JSL Connected={Connected} Stick={Stick}");
    }
}
