using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RodLineVisualizer : MonoBehaviour
{
    public Transform rodTip;
    public Transform bobber;

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

    private LineRenderer line;
    private Vector3 lastBobberPos;
    private float bobberSpeed;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        if (line.positionCount < 2) line.positionCount = 2;
    }

    void Start()
    {
        if (bobber) lastBobberPos = bobber.position;
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

        DrawSagLine(rodTip.position, bobber.position, useSlack);
    }

    void DrawSagLine(Vector3 a, Vector3 b, float slackAmount)
    {
        line.positionCount = segments;

        float dist = Vector3.Distance(a, b);
        float sag = Mathf.Clamp(slackAmount * dist, 0f, maxSag);

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

            // Extra safety: never let any point go below water
            if (preventUnderWater && waterSurface)
                p.y = Mathf.Max(p.y, waterY);

            line.SetPosition(i, p);
        }
    }
}
