using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RodLineVisualizer : MonoBehaviour
{
    public Transform rodTip;
    public Transform bobber;
    public BobberArcCaster bobberArcCaster;

    [Header("Water Clamp")]
    public Transform waterSurface;          // drag your Water plane here
    public bool preventUnderWater = true;
    public float waterClearance = 0.02f;    // keep line slightly above surface

    [Header("Line Shape")]
    [Range(2, 64)] public int segments = 20;
    public float slack = 0.6f;              // sag when idle
    public float slackInFlight = 0.15f;     // less sag when moving
    public float maxSag = 2.0f;

    [Header("Optional: reduce sag when bobber moves fast")]
    public bool reduceSagWhenMoving = true;
    public float movingSpeedForTightLine = 6f;

    [Header("Line Sway")]
    public bool lineSwayEnabled = true;
    public bool lineSwayOnlyWhenLanded = true;
    public float lineSwayAmplitude = 0.05f;
    public float lineSwaySpeed = 1.2f;
    public float lineSwayPivotBias = 1.0f; // 0 = broader swing, 1 = tighter near center (endpoints stay fixed)
    public bool lineSwayUseWaterHeuristic = true;
    public float waterLandedThreshold = 0.08f;

    private LineRenderer line;
    private Vector3 lastBobberPos;
    private float bobberSpeed;
    private float linePhase;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        if (line.positionCount < 2) line.positionCount = 2;

        if (bobberArcCaster == null)
        {
            if (bobber) bobberArcCaster = bobber.GetComponentInParent<BobberArcCaster>();
            if (bobberArcCaster == null) bobberArcCaster = FindObjectOfType<BobberArcCaster>();
        }
    }

    void Start()
    {
        if (bobber) lastBobberPos = bobber.position;
        linePhase = Random.Range(0f, 100f);
    }

    void LateUpdate()
    {
        if (!rodTip || !bobber) return;

        // estimate bobber speed
        bobberSpeed = (bobber.position - lastBobberPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        lastBobberPos = bobber.position;

        float useSlack = slack;

        if (reduceSagWhenMoving)
        {
            float t = Mathf.Clamp01(bobberSpeed / Mathf.Max(0.01f, movingSpeedForTightLine));
            useSlack = Mathf.Lerp(slack, slackInFlight, t);
        }

        bool doLineSway = lineSwayEnabled;
        if (lineSwayOnlyWhenLanded)
        {
            if (bobberArcCaster != null)
                doLineSway = doLineSway && bobberArcCaster.CurrentState == BobberArcCaster.State.Landed;
            else if (lineSwayUseWaterHeuristic && waterSurface != null)
                doLineSway = doLineSway && bobber.position.y <= waterSurface.position.y + waterClearance + waterLandedThreshold;
            else
                doLineSway = false;
        }

        DrawSagLine(rodTip.position, bobber.position, useSlack, doLineSway);
    }

    void DrawSagLine(Vector3 a, Vector3 b, float slackAmount, bool applyLineSway)
    {
        line.positionCount = segments;

        float dist = Vector3.Distance(a, b);
        float sag = Mathf.Clamp(slackAmount * dist, 0f, maxSag);

        Vector3 lineDir = (b - a);
        if (lineDir.sqrMagnitude < 0.0001f)
            lineDir = Vector3.forward;
        lineDir.Normalize();

        Vector3 lineRight = Vector3.Cross(Vector3.up, lineDir);
        if (lineRight.sqrMagnitude < 0.0001f)
            lineRight = Vector3.Cross(Vector3.forward, lineDir);
        lineRight.Normalize();

        // Clamp sag so midpoint never goes under water (parabola lowest at t=0.5)
        float waterY = waterSurface ? waterSurface.position.y + waterClearance : float.NegativeInfinity;
        if (preventUnderWater && waterSurface)
        {
            float baseMidY = (a.y + b.y) * 0.5f;           // y at t=0.5 without sag
            float maxAllowedSag = Mathf.Max(0f, baseMidY - waterY);
            sag = Mathf.Min(sag, maxAllowedSag);
        }

        for (int i = 0; i < segments; i++)
        {
            float t = i / (segments - 1f);

            Vector3 p = Vector3.Lerp(a, b, t);

            // 0 at ends, 1 at middle
            float parabola = 4f * t * (1f - t);

            // sag down
            p += Vector3.down * (parabola * sag);

            if (applyLineSway)
            {
                float swing = Mathf.Sin((Time.time * lineSwaySpeed) + linePhase);
                float swayExponent = Mathf.Lerp(0.8f, 2.2f, Mathf.Clamp01(lineSwayPivotBias));
                float swayWeight = Mathf.Pow(parabola, swayExponent);
                p += lineRight * (swing * lineSwayAmplitude * swayWeight);
            }

            // Extra safety: never let any point go below water
            if (preventUnderWater && waterSurface)
                p.y = Mathf.Max(p.y, waterY);

            line.SetPosition(i, p);
        }
    }
}
