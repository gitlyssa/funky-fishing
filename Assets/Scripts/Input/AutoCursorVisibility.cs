using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(10000)]
public class AutoCursorVisibility : MonoBehaviour
{
    [SerializeField, Min(0f)] private float mouseMoveThreshold = 0.5f;
    [SerializeField, Min(0f)] private float stickActiveThreshold = 0.2f;
    [SerializeField, Min(0f)] private float triggerActiveThreshold = 0.2f;

    private bool _cursorShouldBeVisible = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindObjectOfType<AutoCursorVisibility>() != null)
            return;

        GameObject go = new GameObject("AutoCursorVisibility");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoCursorVisibility>();
    }

    private void Awake()
    {
        _cursorShouldBeVisible = Cursor.visible;
    }

    private void Update()
    {
        bool usedController = IsGamepadUsedThisFrame() || IsJoyConUsedThisFrame();
        if (usedController)
            _cursorShouldBeVisible = false;

        if (IsMouseUsedThisFrame())
            _cursorShouldBeVisible = true;
    }

    private void LateUpdate()
    {
        ApplyCursorState(_cursorShouldBeVisible);
    }

    private bool IsMouseUsedThisFrame()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return false;

        float moveSq = mouse.delta.ReadValue().sqrMagnitude;
        float thresholdSq = mouseMoveThreshold * mouseMoveThreshold;
        return moveSq > thresholdSq;
    }

    private bool IsGamepadUsedThisFrame()
    {
        if (Gamepad.all.Count == 0)
            return false;

        float stickThresholdSq = stickActiveThreshold * stickActiveThreshold;
        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            Gamepad pad = Gamepad.all[i];
            if (pad == null)
                continue;

            if (pad.leftStick.ReadValue().sqrMagnitude > stickThresholdSq)
                return true;

            if (pad.rightStick.ReadValue().sqrMagnitude > stickThresholdSq)
                return true;

            if (pad.dpad.ReadValue().sqrMagnitude > 0.001f)
                return true;

            if (pad.leftTrigger.ReadValue() > triggerActiveThreshold || pad.rightTrigger.ReadValue() > triggerActiveThreshold)
                return true;

            if (pad.buttonSouth.isPressed ||
                pad.buttonNorth.isPressed ||
                pad.buttonWest.isPressed ||
                pad.buttonEast.isPressed ||
                pad.leftShoulder.isPressed ||
                pad.rightShoulder.isPressed ||
                pad.startButton.isPressed ||
                pad.selectButton.isPressed)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsJoyConUsedThisFrame()
    {
        int[] handles = JoyConConnectionService.GetConnectedHandles();
        if (handles == null || handles.Length == 0)
            return false;

        float stickThresholdSq = stickActiveThreshold * stickActiveThreshold;
        for (int i = 0; i < handles.Length; i++)
        {
            int handle = handles[i];
            if (!JoyConConnectionService.IsHandleConnected(handle))
                continue;

            JSL.JOY_SHOCK_STATE state;
            try
            {
                state = JSL.JslGetSimpleState(handle);
            }
            catch
            {
                continue;
            }

            if (state.buttons != 0)
                return true;

            if (state.lTrigger > triggerActiveThreshold || state.rTrigger > triggerActiveThreshold)
                return true;

            Vector2 left = new Vector2(state.stickLX, state.stickLY);
            Vector2 right = new Vector2(state.stickRX, state.stickRY);
            if (left.sqrMagnitude > stickThresholdSq || right.sqrMagnitude > stickThresholdSq)
                return true;
        }

        return false;
    }

    private static void ApplyCursorState(bool visible)
    {
        if (Cursor.visible == visible)
            return;

        Cursor.visible = visible;
        if (visible && Cursor.lockState != CursorLockMode.None)
            Cursor.lockState = CursorLockMode.None;
    }
}
