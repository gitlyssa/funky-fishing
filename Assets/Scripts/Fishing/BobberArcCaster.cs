using System.Collections;
using UnityEngine;

public class BobberArcCaster : MonoBehaviour
{
    [Header("References")]
    public Transform rodTip;
    public Transform bobber;

    // Where the bobber rests when idle (hangs from the tip)
    public Transform bobberHangPoint;

    // We will cast to this (your CastMarker transform)
    public Transform targetMarker;

    [Header("Cast Arc")]
    public float castDuration = 0.75f;
    public float arcHeight = 3.0f; // extra height above straight line
    public AnimationCurve arcEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Yank / Retract")]
    public float yankDuration = 0.25f;
    public AnimationCurve yankEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public enum State { Idle, InFlight, Landed, Retracting }
    public State CurrentState { get; private set; } = State.Idle;

    private Coroutine _moveRoutine;

    void Start()
    {
        // Start bobber at hang point (preferred), otherwise at rod tip
        if (bobber != null)
        {
            if (bobberHangPoint != null) bobber.position = bobberHangPoint.position;
            else if (rodTip != null) bobber.position = rodTip.position;
        }
    }

    // Call this from your JoyCon gesture event
    public void Cast()
    {
        if (!rodTip || !bobber || !targetMarker) return;

        // Guard: don't cast again while moving
        if (CurrentState == State.InFlight || CurrentState == State.Retracting) return;

        Vector3 from = bobber.position;            // launch from current (hanging) position
        Vector3 to = targetMarker.position;        // land on the marker

        StartArcMove(from, to, castDuration, arcHeight, arcEase);
        CurrentState = State.InFlight;
    }

    // Call this from your JoyCon gesture event
    public void Yank()
    {
        if (!rodTip || !bobber) return;

        // Guard: don't yank if already idle/hanging
        if (CurrentState == State.Idle) return;

        Vector3 to = bobberHangPoint ? bobberHangPoint.position : rodTip.position;

        StartLinearMove(bobber.position, to, yankDuration, yankEase);
        CurrentState = State.Retracting;
    }

    private void StartArcMove(Vector3 from, Vector3 to, float duration, float height, AnimationCurve ease)
    {
        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = StartCoroutine(ArcMove(from, to, duration, height, ease));
    }

    private void StartLinearMove(Vector3 from, Vector3 to, float duration, AnimationCurve ease)
    {
        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = StartCoroutine(LinearMove(from, to, duration, ease));
    }

    private IEnumerator ArcMove(Vector3 from, Vector3 to, float duration, float height, AnimationCurve ease)
    {
        duration = Mathf.Max(0.01f, duration);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float u = ease.Evaluate(Mathf.Clamp01(t));

            Vector3 p = Vector3.Lerp(from, to, u);

            // Parabolic "up" offset peaking at u=0.5
            float parabola = 4f * u * (1f - u); // 0..1..0
            p += Vector3.up * (parabola * height);

            bobber.position = p;
            yield return null;
        }

        bobber.position = to;
        CurrentState = State.Landed;
        _moveRoutine = null;
    }

    private IEnumerator LinearMove(Vector3 from, Vector3 to, float duration, AnimationCurve ease)
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
