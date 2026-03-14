using UnityEngine;

public class ObjectPulseResponder : MonoBehaviour
{
    [Header("Pulse Settings")]
    public float pulseScaleAmount = 1.1f;
    public float returnSpeed = 5f;
    
    [Header("Visual")]
    public bool useIntensityMultiplier = true;

    private Vector3 _originalScale;
    private Vector3 _targetScale;

    void Start()
    {
        _originalScale = transform.localScale;
        _targetScale = _originalScale;
    }

    void OnEnable() => RhythmBeatPulse.OnBeat += Pulse;
    void OnDisable() => RhythmBeatPulse.OnBeat -= Pulse;

    private void Pulse()
    {
        float multiplier = 1f;
        
        // if (ScoreManager.Instance != null && ScoreManager.Instance.combo > 20) multiplier = 1.5f;

        transform.localScale = _originalScale * (pulseScaleAmount * multiplier);
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, _originalScale, Time.deltaTime * returnSpeed);
    }
}