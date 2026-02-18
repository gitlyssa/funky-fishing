using UnityEngine;
using UnityEngine.InputSystem;

public class GamepadRhythmProvider : MonoBehaviour, IRhythmInputT
{
    /*
    Unity supports gamepad input through the input system by default. Its basically the same as keyboard input
    don't need a library.
    I basically have the two thumbsticks mapped as 2D vectors representing its current position.
     */
    public event System.Action<FlickDirection> OnFlick;
    public event System.Action<int> OnButtonDown;

    [Header("Thresholds")]
    public float flickVelocityThreshold = 8f; 
    public float deadzone = 0.15f;

    private Vector2 _leftStick;
    private Vector2 _lastLeftStick;
    private float _currentSpinVelocity;
    private float _lastRightAngle;
    private bool _hasTriggeredFlick;
    private float _accumulatedSpin;
    public float GetTotalAccumulatedSpin() => _accumulatedSpin;
    public void ResetAccumulatedSpin() => _accumulatedSpin = 0f;
    public Vector2 GetReelStickDirection() => Gamepad.current.rightStick.ReadValue();

    public Vector2 DirectionalInput => _leftStick;


    void Update()
    {
        if (Gamepad.current == null) return;

        HandleLeftStickFlick();
        HandleRightStickSpin();
        HandleButtons();
    }

    private void HandleLeftStickFlick()
    {
        // a flick is detected by checking the velocity of the stick movement. 
        // If the velocity exceeds a certain threshold, we consider the player to be flicking
        // I then check the direction of the flick, and trigger the event
        // A flick cannot occur again until the velocity drops below the threshold, meaning  distinct flicks can only hit
        // one note.
        _leftStick = Gamepad.current.leftStick.ReadValue();

        float velocity = (_leftStick - _lastLeftStick).magnitude / Time.deltaTime;

        if (velocity > flickVelocityThreshold && !_hasTriggeredFlick)
        {
            FlickDirection dir = GetDirectionFromVector(_leftStick);
            if (dir != FlickDirection.None)
            {
                OnFlick?.Invoke(dir);
                _hasTriggeredFlick = true;
                Debug.Log($"Gamepad Flick: {dir} | Vel: {velocity}");
            }
        }

        if (velocity < flickVelocityThreshold * 0.4f || _leftStick.magnitude < deadzone)
        {
            _hasTriggeredFlick = false;
        }

        _lastLeftStick = _leftStick;
    }

    private void HandleRightStickSpin()
    {
        // similarly, i check the nagle of the right stick, calculate angular velocity
        Vector2 rightStick = Gamepad.current.rightStick.ReadValue();

        if (rightStick.magnitude > deadzone)
        {
            float currentAngle = Mathf.Atan2(rightStick.y, rightStick.x) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(_lastRightAngle, currentAngle);
            
            _currentSpinVelocity = delta / Time.deltaTime;
            _lastRightAngle = currentAngle;
            _accumulatedSpin += delta;
        }
        else
        {
            _currentSpinVelocity = 0;
        }
    }

    private void HandleButtons()
    {
        // just for testing, don't have button controls yet but you would add them here
        if (Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            OnButtonDown?.Invoke(0);
        }
    }

    public bool IsHoldingDirection(FlickDirection direction)
    {
        return GetDirectionFromVector(_leftStick) == direction;
    }

    public float GetSpinVelocity() => _currentSpinVelocity;

    public bool GetButton(int index)
    {
        if (index == 0) return Gamepad.current.buttonSouth.isPressed;
        return false;
    }

    private FlickDirection GetDirectionFromVector(Vector2 v)
    {
        if (v.magnitude < 0.5f) return FlickDirection.None;
        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;

        if (angle <= 22.5f || angle > 337.5f) return FlickDirection.Right;
        if (angle > 22.5f && angle <= 67.5f)   return FlickDirection.UpRight;
        if (angle > 67.5f && angle <= 112.5f)  return FlickDirection.Up;
        if (angle > 112.5f && angle <= 157.5f) return FlickDirection.UpLeft;
        if (angle > 157.5f && angle <= 202.5f) return FlickDirection.Left;
        if (angle > 202.5f && angle <= 247.5f) return FlickDirection.DownLeft;
        if (angle > 247.5f && angle <= 292.5f) return FlickDirection.Down;
        if (angle > 292.5f && angle <= 337.5f) return FlickDirection.DownRight;

        return FlickDirection.None;
    }

}
