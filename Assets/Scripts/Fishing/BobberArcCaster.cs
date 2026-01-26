using System.Collections;
using UnityEngine;

public class BobberArcCaster : MonoBehaviour
{
    [Header("References")]
    public Transform rodTip;
    public Transform bobber;

    // Use either one of these ways to get the target point:
    public Transform targetMarker;                 // simplest: use marker position
    public CursorCastTargeting cursorTargeting;    // optional: your targeting script (if you use it)

    [Header("Cast Arc")]
    public float castDuration = 0.75f;
    public float arcHeight = 3.0f;                 // extra height above straight line
    public AnimationCurve arcEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Yank / Retract")]
    public float yankDuration = 0.25f;
    public AnimationCurve yankEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Testing Keys")]
    public KeyCode castKey = KeyCode.C;
    public KeyCode yankKey = KeyCode.Y;

    public enum State { Idle, InFlight, Landed, Retracting }
    public State CurrentState { get; private set; } = State.Idle;

    Coroutine _moveRoutine;

    void Start()
    {
        // start bobber at rod tip
        if (rodTip && bobber)
            bobber.position = rodTip.position;
    }

    void Update()
    {
        // keyboard test first
        if (Input.GetKeyDown(castKey)) Cast();
        if (Input.GetKeyDown(yankKey)) Yank();
    }

    public void Cast()
    {
        if (!rodTip || !bobber) return;

        Vector3 target = GetTargetPoint();
        // Keep it on the water plane height (marker is already there, but just in case)
        // target.y is fine as-is.

        StartArcMove(rodTip.position, target, castDuration, arcHeight, arcEase);
        CurrentState = State.InFlight;
    }

    public void Yank()
    {
        if (!rodTip || !bobber) return;

        StartLinearMove(bobber.position, rodTip.position, yankDuration, yankEase);
        CurrentState = State.Retracting;
    }

    Vector3 GetTargetPoint()
    {
        // Prefer cursorTargeting if assigned and has a target
        if (cursorTargeting != null && cursorTargeting.HasTarget)
            return cursorTargeting.CurrentTargetPoint;

        // Otherwise use the marker
        if (targetMarker != null)
            return targetMarker.position;

        // Fallback: straight ahead
        return rodTip.position + rodTip.forward * 8f;
    }

    void StartArcMove(Vector3 from, Vector3 to, float duration, float height, AnimationCurve ease)
    {
        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = StartCoroutine(ArcMove(from, to, duration, height, ease));
    }

    void StartLinearMove(Vector3 from, Vector3 to, float duration, AnimationCurve ease)
    {
        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = StartCoroutine(LinearMove(from, to, duration, ease));
    }

    IEnumerator ArcMove(Vector3 from, Vector3 to, float duration, float height, AnimationCurve ease)
    {
        duration = Mathf.Max(0.01f, duration);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float u = ease.Evaluate(Mathf.Clamp01(t));

            // Base lerp along the line
            Vector3 p = Vector3.Lerp(from, to, u);

            // Add a parabolic "up" offset that peaks at u=0.5
            float parabola = 4f * u * (1f - u); // 0..1..0
            p += Vector3.up * (parabola * height);

            bobber.position = p;
            yield return null;
        }

        bobber.position = to;
        CurrentState = State.Landed;
        _moveRoutine = null;
    }

    IEnumerator LinearMove(Vector3 from, Vector3 to, float duration, AnimationCurve ease)
    {
        duration = Mathf.Max(0.01f, duration);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float u = ease.Evaluate(Mathf.Clamp01(t));
            bobber.position = Vector3.Lerp(from, to, u);
            yield return null;
        }

        bobber.position = to;
        CurrentState = State.Idle;
        _moveRoutine = null;
    }
}
