using UnityEngine;

public class RhythmInputProcessor : MonoBehaviour, IRhythmInput
{
    [Header("Thresholds")]
    public float velocityThreshold = 2f; // Speed of the flick
    public float positionThreshold = 0.4f; // Minimum stick push distance
    public float deadzone = 0.15f; // Deadzone 

    public Vector2 RodInput { get; set; }

    [Header("Flick Logic")]
    private Vector2 lastInput;
    private bool hasTriggeredFlick = false;
    private Vector2 currentVelocity;
    private bool isFlickFrame = false;
    

    [Header("Spin Logic")]
    public float minSpinVelocity = 360f; // degrees per second
    public float spinResetTime = 0.5f; 
    private float totalSpinAngle = 0f;
    private float lastFrameAngle;
    private float spinTimer;
    private float currentAngularVelocity;
    public int spinDirection; // 1 for clockwise, -1 for counterclockwise, 0 for no spin
    public float GetTotalSpinAngle() => totalSpinAngle;
    
    public bool RawReelButton { get; set; }

    public Vector2 SpinInput { get; set; }


    void Update()
    {
        currentVelocity = (RodInput - lastInput) / Time.deltaTime;

        if (currentVelocity.magnitude > velocityThreshold && RodInput.magnitude > positionThreshold)
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

        if (currentVelocity.magnitude < velocityThreshold * 0.4f || RodInput.magnitude < deadzone) 
        {
            hasTriggeredFlick = false;
        }

        UpdateSpin();
        Debug.Log($"RodInput: {RodInput}, Velocity: {currentVelocity.magnitude}, FlickFrame: {isFlickFrame}, TotalSpin: {totalSpinAngle * spinDirection}, AngularVelocity: {currentAngularVelocity}");
    }

    void LateUpdate()
    {
        lastInput = RodInput;
    }



    public bool GetFlick(FlickDirection direction)
    {   
        if (isFlickFrame && IsAngleInDirectionZone(RodInput, direction))
        {
            return true;
        }
        return false;
    }

    public bool IsHolding(FlickDirection direction)
    {
        return RodInput.magnitude > positionThreshold && IsAngleInDirectionZone(RodInput, direction);
    }
    public float GetSpinVelocity()
    {
        if (RodInput.magnitude < deadzone) return 0f;

        float currentAngle = Mathf.Atan2(RodInput.y, RodInput.x) * Mathf.Rad2Deg;
        float delta = Mathf.Abs(Mathf.DeltaAngle(currentAngle, lastFrameAngle));
        lastFrameAngle = currentAngle;

        return delta / Time.deltaTime;
    }

    public void ResetSpin()
    {
        totalSpinAngle = 0;
        spinDirection = 0;
    }

    private void UpdateSpin()
    {

        float currentAngle = Mathf.Atan2(SpinInput.y, SpinInput.x) * Mathf.Rad2Deg;
        float delta = Mathf.DeltaAngle(currentAngle, lastFrameAngle);
        lastFrameAngle = currentAngle;

        currentAngularVelocity = Mathf.Abs(delta) / Time.deltaTime;

        int newDirection = (int)Mathf.Sign(delta);

        // spinning and changing direction
        if (Mathf.Abs(delta) > 0.1f && newDirection != spinDirection && spinDirection != 0)
        {
            // reset variables
            ResetSpin();
            spinDirection = newDirection;
            spinTimer = spinResetTime; 
        }

        // increment spin
        if (currentAngularVelocity > minSpinVelocity)
        {
            totalSpinAngle += Mathf.Abs(delta);
            spinTimer = spinResetTime; 
            
            if (spinDirection == 0) spinDirection = newDirection;
        }
        else //under threshold
        {
            spinTimer -= Time.deltaTime;
            if (spinTimer <= 0)
            {
                ResetSpin();
            }
        }
    }
        
    public void ConsumeFlick()
    {
        hasTriggeredFlick = true;
        isFlickFrame = false;
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