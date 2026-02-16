using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;

public class KeyboardRhythmProvider : MonoBehaviour, IRhythmInputT
{
    public event System.Action<FlickDirection> OnFlick;
    public event System.Action<int> OnButtonDown;
    public EventReference flickSoundEvent;

    [Header("Simulation Settings")]
    public float lerpSpeed = 50f; // how quickly the virtual stick moves for both reeling and flicking
    public float flickVelocityThreshold = 10f; // Speed required to trigger the OnFlick event

    private Vector2 _virtualStick;
    private Vector2 _lastVirtualStick;
    private float _currentSpinVelocity;
    private float _lastAngle;
    private bool _hasTriggeredFlick;

    public float reelLerpSpeed = 15f; 
    private Vector2 _virtualReelStick;
    private float _accumulatedSpin;

    public float GetTotalAccumulatedSpin() => _accumulatedSpin;
    public void ResetAccumulatedSpin() => _accumulatedSpin = 0f;
    public Vector2 GetReelStickDirection() => _virtualReelStick;

    // Interface Properties/Methods
    public Vector2 DirectionalInput => _virtualStick;

    void Update()
    {
        HandleVirtualStick();
        HandleKeyboardSpin();
        HandleButtons();
    }

    private void HandleVirtualStick()
    {
        Vector2 targetInput = Vector2.zero;

        if (Keyboard.current.wKey.isPressed) targetInput.y += 1;
        if (Keyboard.current.sKey.isPressed) targetInput.y -= 1;
        if (Keyboard.current.aKey.isPressed) targetInput.x -= 1;
        if (Keyboard.current.dKey.isPressed) targetInput.x += 1;

        _virtualStick = targetInput;

        float velocity = (_virtualStick - _lastVirtualStick).magnitude / Time.deltaTime;

        if (velocity > flickVelocityThreshold && !_hasTriggeredFlick)
        {
            FlickDirection dir = GetDirectionFromVector(_virtualStick);
            if (dir != FlickDirection.None)
            {
                OnFlick?.Invoke(dir);
                _hasTriggeredFlick = true;
                // Debug.Log($"Flick Detected: {dir} with velocity {velocity}");
                RuntimeManager.PlayOneShot(flickSoundEvent, transform.position);
            }
        }

        if (velocity < flickVelocityThreshold * 0.4f || _virtualStick.magnitude < 0.1f)
        {
            _hasTriggeredFlick = false;
        }

        _lastVirtualStick = _virtualStick;
    }

    private void HandleKeyboardSpin()
    {
        Vector2 spinInput = Vector2.zero;
        if (Keyboard.current.leftArrowKey.isPressed) spinInput.x -= 1;
        if (Keyboard.current.rightArrowKey.isPressed) spinInput.x += 1;
        if (Keyboard.current.upArrowKey.isPressed) spinInput.y += 1;
        if (Keyboard.current.downArrowKey.isPressed) spinInput.y -= 1;


        _virtualReelStick = Vector2.Lerp(_virtualReelStick, spinInput.normalized, Time.deltaTime * reelLerpSpeed);

        if (_virtualReelStick.magnitude > 0.1f)
        {
            float currentAngle = Mathf.Atan2(_virtualReelStick.y, _virtualReelStick.x) * Mathf.Rad2Deg;
            
            
            float delta = Mathf.DeltaAngle(_lastAngle, currentAngle);
            _lastAngle = currentAngle;
            
            _currentSpinVelocity = delta / Time.deltaTime;

            _accumulatedSpin += delta;
        }
        else
        {  
            _currentSpinVelocity = Mathf.Lerp(_currentSpinVelocity, 0, Time.deltaTime * 10f);
        }
    }

    private void HandleButtons()
    {
        // Space bar is Button 0
        if (Input.GetKeyDown(KeyCode.Space))
        {
            OnButtonDown?.Invoke(0);
        }
    }



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

    // --- Helper Math ---
    private FlickDirection GetDirectionFromVector(Vector2 v)
    {
        if (v.magnitude < 0.5f) return FlickDirection.None;
        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;

        // 8-Direction detection logic
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