using UnityEngine;

public class RhythmWheelOld : MonoBehaviour
{

    [Header("Movement Properties")]
    public float maxSpinSpeed = 360f;
    public float acceleration = 500f; 
    public float deceleration = 300f;

    private float currentSpeed = 0f;
    private int targetDirection = 0; // -1 (Left), 0 (None), 1 (Right)
    void Start()
    {
        
    }

    void Update()
    {
        float targetVelocity = targetDirection * maxSpinSpeed;

        float lerpStep = (targetDirection == 0) ? deceleration : acceleration;
        
        float previousSpeed = currentSpeed;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetVelocity, lerpStep * Time.deltaTime);


        // if (Mathf.Sign(previousSpeed) != Mathf.Sign(currentSpeed) && Mathf.Abs(currentSpeed) > 10f)
        // {
        //     TriggerReversalEffects();
        // }

        //Rotation
        transform.Rotate(Vector3.up, currentSpeed * Time.deltaTime);
    }

    public void SetDirection(int dir)
    {
        // dir should be -1, 0, or 1
        targetDirection = Mathf.Clamp(dir, -1, 1);
    }

    // private void TriggerReversalEffects()
    // {
    //     if (steamBurst != null) steamBurst.Play();
    //     if (clangSound != null) clangSound.Play();
        
    // }
}
