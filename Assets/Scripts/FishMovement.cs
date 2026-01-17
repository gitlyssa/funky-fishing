using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FishMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 2f;
    [SerializeField] private float directionChangeInterval = 2.5f;
    [SerializeField] private bool constrainToXZ = true;
    [SerializeField, Range(0f, 1f)] private float wallDeflectBlend = 0.6f;
    [Header("Attraction")]
    [SerializeField] private bool useAttraction = true;
    [SerializeField] private Transform attractionTarget;
    [SerializeField] private Vector3 attractionPoint = Vector3.zero;
    [SerializeField] private float attractionStrength = 0.6f;
    [SerializeField] private float attractionRadius = 1.5f;
    [SerializeField] private float roamDuration = 3f;
    [SerializeField, Range(0f, 1f)] private float attractionTurnBias = 0.25f;
    [SerializeField] private float maxReturnDuration = 8f;
    [SerializeField] private bool logAttractionState = false;
    [SerializeField] private float logAttractionInterval = 2f;

    private Rigidbody rb;
    private Vector3 moveDirection;
    private float directionTimer;
    private float attractionTimer;
    private bool isReturningToAttraction = true;
    private float attractionTurnSign = 1f;
    private float logTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        if (constrainToXZ)
        {
            rb.constraints |= RigidbodyConstraints.FreezePositionY;
        }
    }

    private void Start()
    {
        attractionTurnSign = Random.value < 0.5f ? -1f : 1f;
        PickRandomDirection();
        if (useAttraction && logAttractionState)
        {
            LogAttractionState("initial");
        }
    }

    private void FixedUpdate()
    {
        UpdateAttractionState();
        if (directionChangeInterval > 0f)
        {
            directionTimer += Time.fixedDeltaTime;
            if (directionTimer >= directionChangeInterval)
            {
                PickRandomDirection();
            }
        }
        if (useAttraction && logAttractionState && logAttractionInterval > 0f)
        {
            logTimer += Time.fixedDeltaTime;
            if (logTimer >= logAttractionInterval)
            {
                LogAttractionState("periodic");
                logTimer = 0f;
            }
        }

        if (useAttraction && isReturningToAttraction)
        {
            Vector3 toTarget = GetAttractionPoint() - rb.position;
            if (constrainToXZ)
            {
                toTarget.y = 0f;
            }
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Vector3 toward = toTarget.normalized;
                if (attractionTurnBias > 0f)
                {
                    Vector3 tangent = Vector3.Cross(Vector3.up, toward) * attractionTurnSign;
                    if (constrainToXZ)
                    {
                        tangent.y = 0f;
                    }
                    if (tangent.sqrMagnitude > 0.0001f)
                    {
                        toward = (toward + tangent.normalized * attractionTurnBias).normalized;
                    }
                }
                moveDirection = Vector3.Slerp(moveDirection, toward, attractionStrength * Time.fixedDeltaTime);
                moveDirection = moveDirection.normalized;
            }
        }

        rb.linearVelocity = moveDirection * speed;
        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            Vector3 forward = constrainToXZ ? new Vector3(moveDirection.x, 0f, moveDirection.z) : moveDirection;
            if (forward.sqrMagnitude > 0.0001f)
            {
                rb.MoveRotation(Quaternion.LookRotation(forward.normalized, Vector3.up));
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.contactCount == 0)
        {
            return;
        }

        Vector3 normal = collision.GetContact(0).normal;
        Vector3 reflected = Vector3.Reflect(moveDirection, normal);
        if (constrainToXZ)
        {
            reflected.y = 0f;
        }
        Vector3 tangent = Vector3.ProjectOnPlane(moveDirection, normal);
        if (constrainToXZ)
        {
            tangent.y = 0f;
        }
        if (tangent.sqrMagnitude > 0.0001f)
        {
            reflected = Vector3.Slerp(reflected.normalized, tangent.normalized, wallDeflectBlend);
        }
        reflected = reflected.normalized;
        if (Vector3.Dot(reflected, normal) <= 0.05f)
        {
            Vector3 fallbackTangent = Vector3.Cross(normal, Vector3.up);
            if (fallbackTangent.sqrMagnitude < 0.0001f)
            {
                fallbackTangent = Vector3.right;
            }
            fallbackTangent = Vector3.ProjectOnPlane(fallbackTangent, normal).normalized;
            reflected = Vector3.Slerp(normal, fallbackTangent, 0.35f).normalized;
            if (constrainToXZ)
            {
                reflected.y = 0f;
                reflected = reflected.normalized;
            }
        }
        moveDirection = reflected;
        directionTimer = 0f;
    }

    private void UpdateAttractionState()
    {
        if (!useAttraction)
        {
            return;
        }

        attractionTimer += Time.fixedDeltaTime;
        Vector3 toTarget = GetAttractionPoint() - rb.position;
        if (constrainToXZ)
        {
            toTarget.y = 0f;
        }

        if (isReturningToAttraction && toTarget.sqrMagnitude <= attractionRadius * attractionRadius)
        {
            isReturningToAttraction = false;
            attractionTimer = 0f;
            if (logAttractionState)
            {
                LogAttractionState("entered roaming");
                logTimer = 0f;
            }
        }
        else if (isReturningToAttraction && maxReturnDuration > 0f && attractionTimer >= maxReturnDuration)
        {
            isReturningToAttraction = false;
            attractionTimer = 0f;
            if (logAttractionState)
            {
                LogAttractionState("return timeout -> roaming");
                logTimer = 0f;
            }
        }
        else if (!isReturningToAttraction && attractionTimer >= roamDuration)
        {
            isReturningToAttraction = true;
            attractionTimer = 0f;
            attractionTurnSign = Random.value < 0.5f ? -1f : 1f;
            if (logAttractionState)
            {
                LogAttractionState("returning to target");
                logTimer = 0f;
            }
        }
    }

    private Vector3 GetAttractionPoint()
    {
        return attractionTarget != null ? attractionTarget.position : attractionPoint;
    }

    private void LogAttractionState(string reason)
    {
        Vector3 toTarget = GetAttractionPoint() - rb.position;
        if (constrainToXZ)
        {
            toTarget.y = 0f;
        }
        float distance = toTarget.magnitude;
        if (isReturningToAttraction)
        {
            Debug.Log($"{name} returning to target ({reason}) dist={distance:0.00}");
        }
        else
        {
            float remaining = Mathf.Max(0f, roamDuration - attractionTimer);
            Debug.Log($"{name} roaming ({reason}) return in {remaining:0.00}s dist={distance:0.00}");
        }
    }

    private void PickRandomDirection()
    {
        Vector3 random = Random.onUnitSphere;
        if (constrainToXZ)
        {
            random.y = 0f;
            if (random.sqrMagnitude < 0.0001f)
            {
                random = Vector3.right;
            }
        }
        moveDirection = SwapXZAxes(random.normalized);
        directionTimer = 0f;
    }

    private static Vector3 SwapXZAxes(Vector3 direction)
    {
        return new Vector3(direction.z, direction.y, -direction.x);
    }
}
