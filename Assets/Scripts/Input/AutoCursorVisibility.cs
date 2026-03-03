using UnityEngine;
using UnityEngine.InputSystem;

public class AutoCursorVisibility : MonoBehaviour
{
    [SerializeField, Min(0f)] private float mouseMoveThreshold = 0.5f;
    [SerializeField, Min(0f)] private float stickActiveThreshold = 0.2f;
    [SerializeField, Min(0f)] private float triggerActiveThreshold = 0.2f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindObjectOfType<AutoCursorVisibility>() != null)
            return;

        GameObject go = new GameObject("AutoCursorVisibility");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoCursorVisibility>();
    }

    private void Update()
    {
        bool usedController = IsGamepadUsedThisFrame() || IsJoyConUsedThisFrame();
        if (usedController)
            SetCursorVisible(false);

        if (IsMouseUsedThisFrame())
            SetCursorVisible(true);
    }

    private bool IsMouseUsedThisFrame()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return false;

        float moveSq = mouse.delta.ReadValue().sqrMagnitude;
        float thresholdSq = mouseMoveThreshold * mouseMoveThreshold;
        if (moveSq > thresholdSq)
            return true;

        if (mouse.leftButton.wasPressedThisFrame ||
            mouse.rightButton.wasPressedThisFrame ||
            mouse.middleButton.wasPressedThisFrame)
            return true;

        return mouse.scroll.ReadValue().sqrMagnitude > 0.0001f;
    }

    private bool IsGamepadUsedThisFrame()
    {
        Gamepad pad = Gamepad.current;
        if (pad == null)
            return false;

        if (pad.leftStick.ReadValue().sqrMagnitude > (stickActiveThreshold * stickActiveThreshold))
            return true;

        if (pad.rightStick.ReadValue().sqrMagnitude > (stickActiveThreshold * stickActiveThreshold))
            return true;

        if (pad.dpad.ReadValue().sqrMagnitude > 0.001f)
            return true;

        if (pad.leftTrigger.ReadValue() > triggerActiveThreshold || pad.rightTrigger.ReadValue() > triggerActiveThreshold)
            return true;

        return pad.buttonSouth.wasPressedThisFrame ||
               pad.buttonNorth.wasPressedThisFrame ||
               pad.buttonWest.wasPressedThisFrame ||
               pad.buttonEast.wasPressedThisFrame ||
               pad.leftShoulder.wasPressedThisFrame ||
               pad.rightShoulder.wasPressedThisFrame ||
               pad.startButton.wasPressedThisFrame ||
               pad.selectButton.wasPressedThisFrame;
    }

    private bool IsJoyConUsedThisFrame()
    {
        if (!JoyConMenuInput.AnyConnected)
            return false;

        if (JoyConMenuInput.SubmitPressedThisFrame || JoyConMenuInput.PausePressedThisFrame)
            return true;

        Vector2 stick = JoyConMenuInput.NavigationStick;
        return stick.sqrMagnitude > (stickActiveThreshold * stickActiveThreshold);
    }

    private static void SetCursorVisible(bool visible)
    {
        if (Cursor.visible == visible)
            return;

        Cursor.visible = visible;
        if (visible && Cursor.lockState != CursorLockMode.None)
            Cursor.lockState = CursorLockMode.None;
    }
}
