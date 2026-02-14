using UnityEngine;

public class MouseKeyboardRhythmProvider : MonoBehaviour, IRhythmInputT
{
    // --- Interface Events ---
    public event System.Action<FlickDirection> OnFlick;
    public event System.Action<int> OnButtonDown;

    [Header("Flick Settings")]
    public float maxRadius = 300f; 
    public float flickVelocityThreshold = 8f; // How fast you must jerk the mouse

    [Header("State")]
    private Vector2 _virtualStick;
    private Vector2 _lastVirtualStick;
    private float _lastAngle;
    private float _currentSpinVelocity;
    private bool _hasTriggeredFlick;
    private bool _isReeling;

    public Vector2 GetReelStickDirection() => _virtualStick;
    public float GetTotalAccumulatedSpin() => _currentSpinVelocity;
    public void ResetAccumulatedSpin() => _currentSpinVelocity = 0f;

    public Vector2 DirectionalInput => _virtualStick;

    void Update()
    {
        _isReeling = Input.GetMouseButton(0);

        // 1. Calculate Normalized Position
        Vector2 mousePos = Input.mousePosition;
        Vector2 center = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Vector2 offset = mousePos - center;
        _virtualStick = Vector2.ClampMagnitude(offset / maxRadius, 1f);

        // 2. Flick Logic (Velocity-based)
        float velocity = (_virtualStick - _lastVirtualStick).magnitude / Time.deltaTime;

        if (velocity > flickVelocityThreshold && !_hasTriggeredFlick)
        {
            FlickDirection dir = GetDirectionFromVector(_virtualStick);
            if (dir != FlickDirection.None)
            {
                OnFlick?.Invoke(dir);
                _hasTriggeredFlick = true;
            }
        }

        // Reset flick trigger when slowing down or returning to center
        if (velocity < flickVelocityThreshold * 0.5f || _virtualStick.magnitude < 0.1f)
        {
            _hasTriggeredFlick = false;
        }

        // 3. Reeling Logic
        if (_isReeling)
        {
            HandleSpinCalculation(_virtualStick);
        }
        else
        {
            _currentSpinVelocity = 0;
        }

        // 4. Buttons
        if (Input.GetKeyDown(KeyCode.Space)) OnButtonDown?.Invoke(0);
        
        _lastVirtualStick = _virtualStick;
    }

    private void HandleSpinCalculation(Vector2 input)
    {
        if (input.magnitude < 0.2f) return;

        float currentAngle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
        float delta = Mathf.DeltaAngle(currentAngle, _lastAngle);
        _lastAngle = currentAngle;
        
        _currentSpinVelocity = delta / Time.deltaTime;
    }

    // --- IRhythmInputT Implementation ---

    public bool IsHoldingDirection(FlickDirection direction)
    {
        return GetDirectionFromVector(_virtualStick) == direction;
    }

    public float GetSpinVelocity() => _currentSpinVelocity;

    public bool GetButton(int index)
    {
        if (index == 0) return Input.GetKey(KeyCode.Space);
        return false;
    }

    // --- Shared Directional Math ---
    private FlickDirection GetDirectionFromVector(Vector2 v)
    {
        if (v.magnitude < 0.3f) return FlickDirection.None; // Threshold to count as a "direction"
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