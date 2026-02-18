using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[DefaultExecutionOrder(200)]
public class XboxFishingInput : MonoBehaviour
{
    public enum PadButton
    {
        A,
        B,
        X,
        Y,
        LeftShoulder,
        RightShoulder,
        DpadUp,
        DpadDown,
        DpadLeft,
        DpadRight,
        Start,
        Select
    }

    [Header("References")]
    [SerializeField] private BobberArcCaster caster;
    [SerializeField] private CursorCastTargeting targeting;

    [Header("Cast / Yank")]
    [SerializeField] private bool useSingleButtonForCastYank = true;
    [SerializeField] private PadButton castYankButton = PadButton.A; // A on Xbox
    [SerializeField] private PadButton castButton = PadButton.RightShoulder;
    [SerializeField] private PadButton yankButton = PadButton.LeftShoulder;
    [SerializeField] private bool useTriggerFallback = false;
    [SerializeField, Range(0.01f, 0.99f)] private float triggerPressThreshold = 0.5f;

    [Header("Targeting")]
    [SerializeField] private bool useRightStickForTargeting = true;
    [SerializeField, Range(0.05f, 0.95f)] private float targetingStickDeadzone = 0.2f;

    [Header("Tension")]
    [SerializeField] private bool enableTensionToggle = true;
    [SerializeField] private PadButton tensionToggleButton = PadButton.B; // B on Xbox

    [Header("Tension Swing")]
    [SerializeField] private bool onlySendSwingInTension = true;
    [SerializeField] private bool useDpadForSwing = true;
    [SerializeField] private bool useLeftStickForSwing = true;
    [SerializeField] private bool useRightStickForSwing = false;
    [SerializeField, Range(0.1f, 0.95f)] private float leftStickCardinalThreshold = 0.7f;
    [SerializeField, Range(0.1f, 0.95f)] private float rightStickCardinalThreshold = 0.7f;

    private bool rtPrev;
    private bool ltPrev;

    void Update()
    {
        var pad = Gamepad.current;
        if (pad == null)
        {
            if (useRightStickForTargeting && targeting != null)
                targeting.SetExternalStickInput(Vector2.zero);
            return;
        }

        Vector2 rightStick = pad.rightStick.ReadValue();
        Vector2 leftStick = pad.leftStick.ReadValue();

        if (useRightStickForTargeting && targeting != null)
        {
            Vector2 targetStick = rightStick.magnitude >= targetingStickDeadzone ? rightStick : Vector2.zero;
            targeting.SetExternalStickInput(targetStick);
        }

        if (caster == null) return;

        HandleCastYank(pad);

        if (enableTensionToggle && WasPressedThisFrame(pad, tensionToggleButton))
            caster.ToggleTension();

        bool canSendSwing =
            !onlySendSwingInTension ||
            caster.CurrentState == BobberArcCaster.State.Tension;

        if (!canSendSwing)
        {
            caster.SetDirectionalSwingHeld(false, false, false);
            return;
        }

        // Directional swing held (D-pad and/or left stick and/or right stick)
        bool up = false, left = false, right = false;

        if (useDpadForSwing)
        {
            up |= pad.dpad.up.isPressed;
            left |= pad.dpad.left.isPressed;
            right |= pad.dpad.right.isPressed;
        }

        if (useLeftStickForSwing)
        {
            if (leftStick.y > leftStickCardinalThreshold) up = true;
            if (leftStick.x < -leftStickCardinalThreshold) left = true;
            if (leftStick.x > leftStickCardinalThreshold) right = true;
        }

        if (useRightStickForSwing)
        {
            if (rightStick.y > rightStickCardinalThreshold) up = true;
            if (rightStick.x < -rightStickCardinalThreshold) left = true;
            if (rightStick.x > rightStickCardinalThreshold) right = true;
        }

        caster.SetDirectionalSwingHeld(up, left, right);
    }

    private void HandleCastYank(Gamepad pad)
    {
        if (useSingleButtonForCastYank)
        {
            if (WasPressedThisFrame(pad, castYankButton))
            {
                if (caster.CurrentState == BobberArcCaster.State.Idle) caster.Cast();
                else caster.Yank();
            }
            return;
        }

        if (WasPressedThisFrame(pad, castButton)) caster.Cast();
        if (WasPressedThisFrame(pad, yankButton)) caster.Yank();

        if (!useTriggerFallback) return;

        bool rt = pad.rightTrigger.ReadValue() > triggerPressThreshold;
        if (rt && !rtPrev) caster.Cast();
        rtPrev = rt;

        bool lt = pad.leftTrigger.ReadValue() > triggerPressThreshold;
        if (lt && !ltPrev) caster.Yank();
        ltPrev = lt;
    }

    private static bool WasPressedThisFrame(Gamepad pad, PadButton button)
    {
        ButtonControl control = GetButtonControl(pad, button);
        return control != null && control.wasPressedThisFrame;
    }

    private static ButtonControl GetButtonControl(Gamepad pad, PadButton button)
    {
        switch (button)
        {
            case PadButton.A: return pad.buttonSouth;
            case PadButton.B: return pad.buttonEast;
            case PadButton.X: return pad.buttonWest;
            case PadButton.Y: return pad.buttonNorth;
            case PadButton.LeftShoulder: return pad.leftShoulder;
            case PadButton.RightShoulder: return pad.rightShoulder;
            case PadButton.DpadUp: return pad.dpad.up;
            case PadButton.DpadDown: return pad.dpad.down;
            case PadButton.DpadLeft: return pad.dpad.left;
            case PadButton.DpadRight: return pad.dpad.right;
            case PadButton.Start: return pad.startButton;
            case PadButton.Select: return pad.selectButton;
            default: return null;
        }
    }
}
