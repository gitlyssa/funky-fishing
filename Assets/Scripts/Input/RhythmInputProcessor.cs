using UnityEngine;

public class RhythmInputProcessor : MonoBehaviour, IRhythmInput
{
    [Header("Thresholds")]
    public float velocityThreshold = 2f; // Speed of the flick
    public float positionThreshold = 0.4f; // Minimum stick push distance
    public float deadzone = 0.15f; // Deadzone 

    private Vector2 lastInput;
    private bool hasTriggeredFlick = false;
    private float lastAngle;
    private Vector2 currentVelocity;
    private bool isFlickFrame = false;
    public Vector2 RawInput { get; set; }
    public bool RawReelButton { get; set; }


    void Update()
    {
        currentVelocity = (RawInput - lastInput) / Time.deltaTime;

        if (currentVelocity.magnitude > velocityThreshold && RawInput.magnitude > positionThreshold)
        {
            if (!hasTriggeredFlick)
            {
                isFlickFrame = true; 
                hasTriggeredFlick = true;
            }
            else
            {
                isFlickFrame = false;
            }
        }
        else
        {
            isFlickFrame = false;
        }

        if (currentVelocity.magnitude < velocityThreshold * 0.4f || RawInput.magnitude < deadzone) 
        {
            hasTriggeredFlick = false;
        }
    }

    void LateUpdate()
    {
        lastInput = RawInput;
    }



    public bool GetFlick(FlickDirection direction)
    {   
        if (isFlickFrame && IsAngleInDirectionZone(RawInput, direction))
        {
            return true;
        }
        return false;
    }

    public bool IsHolding(FlickDirection direction)
    {
        return RawInput.magnitude > positionThreshold && IsAngleInDirectionZone(RawInput, direction);
    }
    public float GetSpinVelocity()
    {
        if (RawInput.magnitude < deadzone) return 0f;

        float currentAngle = Mathf.Atan2(RawInput.y, RawInput.x) * Mathf.Rad2Deg;
        float delta = Mathf.Abs(Mathf.DeltaAngle(currentAngle, lastAngle));
        lastAngle = currentAngle;

        return delta / Time.deltaTime;
    }

    private bool IsAngleInDirectionZone(Vector2 input, FlickDirection targetDir)
        {
            if (input.magnitude < deadzone) return false;

        float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;
        return targetDir switch
        {
            FlickDirection.Right     => IsAngleWithinRange(angle, 0, 45),
            FlickDirection.UpRight   => IsAngleWithinRange(angle, 45, 45),
            FlickDirection.Up        => IsAngleWithinRange(angle, 90, 45),
            FlickDirection.UpLeft    => IsAngleWithinRange(angle, 135, 45),
            FlickDirection.Left      => IsAngleWithinRange(angle, 180, 45),
            FlickDirection.DownLeft  => IsAngleWithinRange(angle, 225, 45),
            FlickDirection.Down      => IsAngleWithinRange(angle, 270, 45),
            FlickDirection.DownRight => IsAngleWithinRange(angle, 315, 45),
            _ => false
        };
    }
    private bool IsAngleWithinRange(float angle, float targetCenter, float halfWindow)
    {
        float diff = Mathf.Abs(Mathf.DeltaAngle(angle, targetCenter));
        return diff <= halfWindow;
    }
}