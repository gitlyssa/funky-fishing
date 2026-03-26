using UnityEngine;

public class ObjectPulseResponder : MonoBehaviour
{
    [Header("Pulse Settings")]
    public float pulseScaleAmount = 1.2f;
    public float returnSpeed = 10f;

    private Vector3 _originalScale;

    void Start()
    {
        _originalScale = transform.localScale;
    }

    private void OnEnable() => RhythmBeatPulse.OnBeat += Pulse;
    private void OnDisable() => RhythmBeatPulse.OnBeat -= Pulse;

    private void Pulse()
    {
        transform.localScale = _originalScale * pulseScaleAmount;
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