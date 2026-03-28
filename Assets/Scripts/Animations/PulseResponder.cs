using UnityEngine;

public class ObjectPulseResponder : MonoBehaviour
{
    [Header("Pulse Settings")]
    public int groupID; // Assign this in Inspector (e.g., 1 for Ring, 2 for Water)
    public float pulseScaleAmount = 1.2f;
    public float returnSpeed = 10f;
    private Vector3 _originalScale;

    void Start()
    {
        _originalScale = transform.localScale;
    }

    private void OnEnable() => RhythmBeatPulse.OnBeat += HandlePulse;
    private void OnDisable() => RhythmBeatPulse.OnBeat -= HandlePulse;

    private void HandlePulse(float intensity, int[] groups)
    {
        // If groups is empty, pulse everyone. Otherwise, check for ID.
        bool shouldPulse = groups == null || groups.Length == 0;
        if (!shouldPulse)
        {
            foreach (int id in groups) { if (id == groupID) { shouldPulse = true; break; } }
        }

        if (shouldPulse) transform.localScale = _originalScale * (1f + (pulseScaleAmount - 1f) * intensity);
    }

    void Update()
    {
        if (transform.localScale != _originalScale)
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale, 
                _originalScale, 
                Time.deltaTime * returnSpeed
            );
        }
    }
}