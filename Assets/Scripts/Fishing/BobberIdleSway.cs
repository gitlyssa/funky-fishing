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

    private float phaseA;
    private float phaseB;

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
        if (!hangPoint) return;

        // Only sway when the bobber is "idle/hanging"
        if (bobberArcCaster != null && bobberArcCaster.CurrentState != BobberArcCaster.State.Idle)
            return;

        // If sway is off, gently return to hang point
        if (!swayEnabled)
        {
            transform.position = Vector3.Lerp(transform.position, hangPoint.position, 1f - Mathf.Exp(-smooth * Time.deltaTime));
            return;
        }

        float t = Time.time * speed;

        // Sway in the hangPoint's local right/forward directions (so it moves with the rod)
        float side = Mathf.Sin(t + phaseA) * amplitude;
        float fwd  = Mathf.Cos(t * 0.8f + phaseB) * amplitude * forwardMultiplier;
        float up   = Mathf.Sin(t * 1.3f + phaseB) * verticalBob;

        Vector3 target =
            hangPoint.position +
            hangPoint.right * side +
            hangPoint.forward * fwd +
            hangPoint.up * up;

        transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-smooth * Time.deltaTime));
    }
}
