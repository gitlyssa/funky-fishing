using UnityEngine;

public class SnailBehavior : MonoBehaviour
{
    [Header("Circular Movement")]
    public float circleRadius = 1.5f;
    public float moveSpeed = 1f;

    [Header("Squash & Stretch")]
    public float stretchSpeed = 3f;
    public float stretchAmount = 0.08f; 

    private Vector3 startPosition;
    private Vector3 baseScale;

    void Start()
    {
        startPosition = transform.position;
        baseScale = transform.localScale;
    }

    void Update()
    {
        float currentAngle = Time.time * moveSpeed;
        
        float newX = startPosition.x + (Mathf.Cos(currentAngle) * circleRadius);
        float newZ = startPosition.z + (Mathf.Sin(currentAngle) * circleRadius);
        transform.position = new Vector3(newX, startPosition.y, newZ);

        Vector3 tangentDirection = new Vector3(-Mathf.Sin(currentAngle), 0f, Mathf.Cos(currentAngle));
        
        if (tangentDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(tangentDirection);
        }

        float sineWave = Mathf.Sin(Time.time * stretchSpeed);

        float stretchFactor = 1f + (sineWave * stretchAmount); 
        float squashFactor = 1f - (sineWave * stretchAmount * 0.5f); 

        float xScale = baseScale.x * squashFactor;
        float yScale = baseScale.y * squashFactor;
        float zScale = baseScale.z * stretchFactor;

        transform.localScale = new Vector3(xScale, yScale, zScale);
    }
}