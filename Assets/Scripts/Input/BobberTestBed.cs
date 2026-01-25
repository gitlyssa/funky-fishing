using System.Collections;
using UnityEngine;

// Simple harness to move a bobber between two points so input/gesture work can be tested
// without needing full fishing gameplay systems online.
public class BobberTestBed : MonoBehaviour
{
    [Header("References")]
    public Transform bobber;
    public Transform startPoint;
    public Transform targetPoint;

    [Header("Motion")]
    public float castDuration = 0.35f;
    public float yankDuration = 0.25f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Keyboard test")]
    public KeyCode castKey = KeyCode.C;
    public KeyCode yankKey = KeyCode.Y;

    private Coroutine moveRoutine;

    void Start()
    {
        if (bobber != null && startPoint != null)
            bobber.position = startPoint.position;
    }

    void Update()
    {
        // Temporary keyboard controls to exercise the motion logic.
        if (Input.GetKeyDown(castKey)) Cast();
        if (Input.GetKeyDown(yankKey)) Yank();
    }

    // Call this when a forward-cast gesture is detected.
    public void Cast()
    {
        if (bobber == null || targetPoint == null) return;
        StartMove(targetPoint.position, castDuration);
    }

    // Call this when a yank-back gesture is detected.
    public void Yank()
    {
        if (bobber == null || startPoint == null) return;
        StartMove(startPoint.position, yankDuration);
    }

    private void StartMove(Vector3 to, float duration)
    {
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveTo(to, duration));
    }

    private IEnumerator MoveTo(Vector3 to, float duration)
    {
        Vector3 from = bobber.position;
        float t = 0f;

        duration = Mathf.Max(0.01f, duration);

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float e = ease.Evaluate(Mathf.Clamp01(t));
            bobber.position = Vector3.LerpUnclamped(from, to, e);
            yield return null;
        }

        bobber.position = to;
        moveRoutine = null;
    }

    void OnDrawGizmos()
    {
        // Visualize the cast path in the editor.
        if (startPoint == null || targetPoint == null) return;
        Gizmos.DrawLine(startPoint.position, targetPoint.position);
        Gizmos.DrawWireSphere(startPoint.position, 0.15f);
        Gizmos.DrawWireSphere(targetPoint.position, 0.15f);
    }
}
