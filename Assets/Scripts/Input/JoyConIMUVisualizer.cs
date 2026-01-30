using System;
using System.Runtime.InteropServices;
using UnityEngine;

// On-screen IMU dashboard to see Joy-Con accel/gyro values while tuning axes and thresholds.
public class JoyConIMUVisualizer : MonoBehaviour
{
    private const string DLL = "JoyShockLibrary";

    [StructLayout(LayoutKind.Sequential)]
    public struct IMU_STATE
    {
        public float accelX, accelY, accelZ; // in g
        public float gyroX, gyroY, gyroZ;    // in dps
    }

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern int JslConnectDevices();

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern int JslGetConnectedDeviceHandles([Out] int[] deviceHandleArray, int size);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern IMU_STATE JslGetIMUState(int deviceId);

    [Header("Device")]
    public int deviceIndex = 0;

    [Header("Filtering (for linear accel)")]
    public float gravityFollow = 12f;   // higher = gravity estimate follows faster
    public float linAccelSmooth = 25f;  // higher = smoother lin accel

    [Header("Display scaling")]
    public float barScaleG = 1.2f;      // g shown to full bar
    public float barScaleDps = 500f;    // dps shown to full bar

    private int[] handles = Array.Empty<int>();
    private int id;

    private Vector3 gravity;      // estimated gravity (g)
    private Vector3 linAccelFilt; // linear accel (g), filtered
    private Vector3 accelRaw;
    private Vector3 gyroRaw;

    // Optional "freeze" to inspect values
    private bool frozen = false;

    Texture2D whiteTex;

    void Start()
    {
        // Discover and select a Joy-Con device to read IMU data from.
        int count = JslConnectDevices();
        handles = new int[Mathf.Max(0, count)];
        if (count > 0) JslGetConnectedDeviceHandles(handles, handles.Length);

        if (handles.Length == 0)
        {
            Debug.LogError("No JoyShockLibrary devices found.");
            enabled = false;
            return;
        }

        id = handles[Mathf.Clamp(deviceIndex, 0, handles.Length - 1)];

        // 1x1 white texture used for drawing simple UI bars.
        whiteTex = new Texture2D(1, 1);
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();
    }

    void Update()
    {
        // Freeze toggle (Space)
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current?.spaceKey.wasPressedThisFrame == true)
            frozen = !frozen;
#else
        if (Input.GetKeyDown(KeyCode.Space))
            frozen = !frozen;
#endif

        if (frozen) return;

        float dt = Time.deltaTime;

        var imu = JslGetIMUState(id);

        accelRaw = new Vector3(imu.accelX, imu.accelY, imu.accelZ);
        gyroRaw  = new Vector3(imu.gyroX,  imu.gyroY,  imu.gyroZ);

        // Estimate gravity and remove it.
        gravity = Vector3.Lerp(gravity, accelRaw, 1f - Mathf.Exp(-gravityFollow * dt));
        var lin = accelRaw - gravity;

        // Smooth linear accel for display.
        linAccelFilt = Vector3.Lerp(linAccelFilt, lin, 1f - Mathf.Exp(-linAccelSmooth * dt));
    }

    void OnGUI()
    {
        // Immediate-mode UI for quick visualization in play mode.
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            normal = { textColor = Color.white }
        };

        float x = 15, y = 15;

        GUI.Label(new Rect(x, y, 800, 25),
            $"Joy-Con IMU  (Space = {(frozen ? "UNFREEZE" : "FREEZE")})", style);
        y += 26;

        GUI.Label(new Rect(x, y, 900, 25),
            $"RAW accel (g):  x={accelRaw.x:+0.00;-0.00}  y={accelRaw.y:+0.00;-0.00}  z={accelRaw.z:+0.00;-0.00}", style);
        y += 22;

        GUI.Label(new Rect(x, y, 900, 25),
            $"LIN accel (g):  x={linAccelFilt.x:+0.00;-0.00}  y={linAccelFilt.y:+0.00;-0.00}  z={linAccelFilt.z:+0.00;-0.00}", style);
        y += 22;

        GUI.Label(new Rect(x, y, 900, 25),
            $"gyro (dps):     x={gyroRaw.x:+0;-0}  y={gyroRaw.y:+0;-0}  z={gyroRaw.z:+0;-0}", style);
        y += 30;

        // Bars for each axis.
        y = DrawAxisBars("LIN accel (g)", linAccelFilt, barScaleG, x, y);
        y = DrawAxisBars("gyro (dps)", gyroRaw, barScaleDps, x, y);

        // Dominant axis hint (by absolute value).
        var domLin = DominantAxis(linAccelFilt);
        var domGyro = DominantAxis(gyroRaw);

        y += 10;
        GUI.Label(new Rect(x, y, 900, 25), $"Dominant LIN axis right now: {domLin}", style);
        y += 22;
        GUI.Label(new Rect(x, y, 900, 25), $"Dominant GYRO axis right now: {domGyro}", style);
    }

    float DrawAxisBars(string title, Vector3 v, float fullScale, float x, float y)
    {
        // Section header plus X/Y/Z bars.
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            normal = { textColor = Color.white }
        };

        GUI.Label(new Rect(x, y, 300, 20), title, style);
        y += 22;

        y = DrawOneBar("X", v.x, fullScale, x, y);
        y = DrawOneBar("Y", v.y, fullScale, x, y);
        y = DrawOneBar("Z", v.z, fullScale, x, y);
        y += 10;

        return y;
    }

    float DrawOneBar(string name, float value, float fullScale, float x, float y)
    {
        float w = 320, h = 18;
        float mid = x + 160; // center line

        // Background.
        GUI.color = new Color(1, 1, 1, 0.15f);
        GUI.DrawTexture(new Rect(x, y, w, h), whiteTex);

        // Center line.
        GUI.color = new Color(1, 1, 1, 0.4f);
        GUI.DrawTexture(new Rect(mid, y, 2, h), whiteTex);

        float t = Mathf.Clamp(value / Mathf.Max(0.0001f, fullScale), -1f, 1f);
        float barW = Mathf.Abs(t) * 160f;

        // Bar (green positive, red negative).
        GUI.color = t >= 0 ? new Color(0.2f, 1f, 0.2f, 0.85f) : new Color(1f, 0.2f, 0.2f, 0.85f);
        float barX = t >= 0 ? mid : (mid - barW);
        GUI.DrawTexture(new Rect(barX, y, barW, h), whiteTex);

        // Value label.
        GUI.color = Color.white;
        GUI.Label(new Rect(x + w + 10, y - 2, 300, 22), $"{name}: {value:+0.00;-0.00}", GUI.skin.label);

        return y + 22;
    }

    string DominantAxis(Vector3 v)
    {
        // Return axis with largest absolute magnitude for quick tuning guidance.
        float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
        if (ax >= ay && ax >= az) return $"X ({v.x:+0.00;-0.00})";
        if (ay >= ax && ay >= az) return $"Y ({v.y:+0.00;-0.00})";
        return $"Z ({v.z:+0.00;-0.00})";
    }
}
