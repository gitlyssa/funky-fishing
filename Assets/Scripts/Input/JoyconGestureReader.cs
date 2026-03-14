using UnityEngine;
using UnityEngine.InputSystem;
[DefaultExecutionOrder(-100)]
public class JoyconGestureReader : MonoBehaviour
{
    RhythmInputProcessor processor;
    public int deviceId = -1;
    private int _knownConnectionRevision = -1;

    public float sensitivity = 2.0f;
    private Vector3 currentEuler = Vector3.zero;
    private Vector3 currentPos = Vector3.zero;
    private Vector3 gyroOffset = Vector3.zero;

    public Transform pivotRod;

    [Header("Filter Settings")]
    public float filterStrength = 0.98f; // 0.98 trusts Gyro, 0.02 trusts Gravity
    public float gyroDeadzone = 0.5f;   // Ignore tiny noise below this dps
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        processor = GetComponent<RhythmInputProcessor>();
        currentPos = transform.position - Vector3.forward * sensitivity;
        ConnectDevice();
        _knownConnectionRevision = JoyConConnectionService.GetRevision();
    }

    // Update is called once per frame
    void Update()
    {
        int revision = JoyConConnectionService.GetRevision();
        if (revision != _knownConnectionRevision)
        {
            _knownConnectionRevision = revision;
            ConnectDevice();
        }

        if (deviceId < 0 || !JoyConConnectionService.IsHandleConnected(deviceId))
        {
            deviceId = -1;
            JoyConConnectionService.RequestScan();
            return;
        }

        JSL.MOTION_STATE motState;
        JSL.JOY_SHOCK_STATE state;
        try
        {
            motState = JSL.JslGetMotionState(deviceId);
            state = JSL.JslGetSimpleState(deviceId);
        }
        catch
        {
            deviceId = -1;
            JoyConConnectionService.RequestScan();
            return;
        }

        Quaternion currentRot = new Quaternion(motState.quatX, -motState.quatY, motState.quatZ, motState.quatW);

        Vector3 noseDir = currentRot * Vector3.forward;

        Debug.DrawRay(currentPos, noseDir * sensitivity, Color.green);
        if (pivotRod != null)
        {
            pivotRod.rotation = Quaternion.LookRotation(noseDir);
        }

        float xInput = 0;
        float yInput = 0;

        if (noseDir.z > 0.01f)
        {
            xInput = (noseDir.x / noseDir.z) * sensitivity;
            yInput = (noseDir.y / noseDir.z) * sensitivity;
        }
        else
        {
            xInput = noseDir.x * sensitivity * 2.0f;
            yInput = noseDir.y * sensitivity * 2.0f;
        }

        processor.RodInput = new Vector2(
            Mathf.Clamp(xInput, -1.5f, 1.5f),
            Mathf.Clamp(yInput, -1.5f, 1.5f)
        );


        bool northPressed = (state.buttons & (1 << JSL.ButtonMaskN)) != 0;
        bool upPressed = (state.buttons & (1 << JSL.ButtonMaskUp)) != 0;

        if (northPressed || upPressed)
        {
            JSL.JslResetContinuousCalibration(deviceId); 
            JSL.JslStartContinuousCalibration(deviceId);
        
            Debug.Log("JSL: Orientation & Drift Reset");
        }

        bool southPressed = (state.buttons & (1 << JSL.ButtonMaskS)) != 0;
        bool downPressed = (state.buttons & (1 << JSL.ButtonMaskDown)) != 0;

        processor.RawReelButton = southPressed || downPressed;

        float stickX = state.stickLX;
        float stickY = state.stickLY;

        processor.SpinInput = new Vector2(stickX, stickY);

    }

    private void ConnectDevice()
    {
        int[] handles = JoyConConnectionService.GetConnectedHandles();
        if (handles == null || handles.Length == 0)
        {
            deviceId = -1;
            JoyConConnectionService.RequestScan();
            Debug.LogWarning("No Joy-Con devices found.");
            return;
        }

        deviceId = handles[0];
        Debug.Log("Connected to Joy-Con with Device ID: " + deviceId);
        try
        {
            JSL.JslStartContinuousCalibration(deviceId);
        }
        catch
        {
            deviceId = -1;
            JoyConConnectionService.RequestScan();
        }
    }
}
