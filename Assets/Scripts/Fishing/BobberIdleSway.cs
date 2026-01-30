using UnityEngine;

public class BobberIdleSway : MonoBehaviour
{
    [Header("References")]
    public Transform hangPoint;                 // BobberHangPoint
    public BobberArcCaster bobberArcCaster;     // the script that casts/yanks

    [Header("Toggle")]
    public bool swayEnabled = true;
    public KeyCode toggleKey = KeyCode.T;       // optional runtime toggle

    [Header("Sway Settings")]
    public float amplitude = 0.06f;             // world units (meters-ish)
    public float speed = 1.2f;                  // oscillations per second-ish
    public float smooth = 12f;                  // how smoothly it follows the sway target
    public float verticalBob = 0.01f;           // tiny up/down bob

    [Header("Shape")]
    public float forwardMultiplier = 0.5f;      // less forward/back than side sway

    [Header("Water Drift")]
    public bool waterMotionEnabled = true;
    public float waterSwayAmplitude = 0.06f;
    public float waterSwaySpeed = 0.8f;
    public float waterBob = 0.025f;
    public float waterSmooth = 6f;
    public Vector3 waterCurrentDirection = new Vector3(1f, 0f, 0.25f);
    public float waterCurrentSpeed = 0.005f;     // units per second
    public float waterSinkOffset = -0.02f;      // negative sinks lower

    private float phaseA;
    private float phaseB;
    private Vector3 landedAnchor;
    private Vector3 currentDrift;
    private BobberArcCaster.State lastState = BobberArcCaster.State.Idle;

    void Start()
    {
        phaseA = Random.Range(0f, 100f);
        phaseB = Random.Range(0f, 100f);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            swayEnabled = !swayEnabled;
    }

    void LateUpdate()
    {
        if (!hangPoint && bobberArcCaster == null) return;

        // If sway is off, gently return to hang point
        if (!swayEnabled)
        {
            if (hangPoint != null)
                transform.position = Vector3.Lerp(transform.position, hangPoint.position, 1f - Mathf.Exp(-smooth * Time.deltaTime));
            return;
        }

        if (bobberArcCaster != null)
        {
            if (bobberArcCaster.CurrentState == BobberArcCaster.State.Idle)
            {
                ApplyIdleSway();
            }
            else if (bobberArcCaster.CurrentState == BobberArcCaster.State.Landed)
            {
                if (lastState != BobberArcCaster.State.Landed)
                {
                    landedAnchor = transform.position;
                    currentDrift = Vector3.zero;
                }
                ApplyWaterMotion();
            }
        }
        else
        {
            ApplyIdleSway();
        }

        lastState = bobberArcCaster != null ? bobberArcCaster.CurrentState : lastState;
    }

    private void ApplyIdleSway()
    {
        if (!hangPoint) return;

        float t = Time.time * speed;

        // Sway in the hangPoint's local right/forward directions (so it moves with the rod)
        float side = Mathf.Sin(t + phaseA) * amplitude;
        float fwd = Mathf.Cos(t * 0.8f + phaseB) * amplitude * forwardMultiplier;
        float up = Mathf.Sin(t * 1.3f + phaseB) * verticalBob;

        Vector3 target =
            hangPoint.position +
            hangPoint.right * side +
            hangPoint.forward * fwd +
            hangPoint.up * up;

        transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-smooth * Time.deltaTime));
    }

    private void ApplyWaterMotion()
    {
        if (!waterMotionEnabled) return;

        Vector3 currentDir = waterCurrentDirection;
        currentDir.y = 0f;
        if (currentDir.sqrMagnitude < 0.0001f)
            currentDir = Vector3.forward;
        currentDir.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, currentDir).normalized;

        float t = Time.time * waterSwaySpeed;
        float side = Mathf.Sin(t + phaseA) * waterSwayAmplitude;
        float fwd = Mathf.Cos(t * 0.7f + phaseB) * waterSwayAmplitude * forwardMultiplier;
        float up = Mathf.Sin(t * 1.1f + phaseB) * waterBob;

        currentDrift += currentDir * waterCurrentSpeed * Time.deltaTime;

        Vector3 target =
            landedAnchor +
            currentDrift +
            right * side +
            currentDir * fwd +
            Vector3.up * (up + waterSinkOffset);

        transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-waterSmooth * Time.deltaTime));
    }
}
