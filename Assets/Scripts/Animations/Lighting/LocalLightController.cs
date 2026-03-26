using UnityEngine;
using System.Collections;

public class LocalLightController : MonoBehaviour
{
    [Header("Child References")]
    [SerializeField] private MeshRenderer _bodyRenderer;
    [SerializeField] private MeshRenderer _glowRenderer;
    [SerializeField] private Transform _glowRing;
    [SerializeField] private Light _light;
    private float _baseIntensity;
    private float _baseRange;
    private Coroutine _effectCoroutine;
    private bool _isFlashing = false;

    private Vector3 _startPos;
    private float _pulseOffset;
    private float _moveOffset;
    private float _bpmRandomness; // Persistent individual variation
    private Material _bodyMaterial;
    private Material _glowMaterial;

    private bool _isBlackedOut = false;
    

    private float _currentBPM;
    private float _currentMoveSpeed;
    private float _currentMoveRange;

    public Vector3 Position => transform.position;
    private Coroutine _blackoutCoroutine;

    private void Awake()
    {   
        _startPos = transform.position;
        
        
        _pulseOffset = Random.Range(0f, 100f);
        _moveOffset = Random.Range(0f, 100f);
        _bpmRandomness = Random.Range(-0.1f, 0.1f);

        // Create a unique material instance so we don't modify the project asset
        if (_bodyRenderer != null) _bodyMaterial = _bodyRenderer.material;
        if (_glowRenderer != null) _glowMaterial = _glowRenderer.material;
    }

    private void OnEnable() => GlobalLightingManager.RegisterLight(this);
    private void OnDisable() => GlobalLightingManager.UnregisterLight(this);


    void Update()
    {
        if (GlobalLightingManager.Instance.currentProfile == null) return;

        Vector3 movement = new Vector3(
            Mathf.Sin(Time.time * _currentMoveSpeed + _moveOffset),
            Mathf.Cos(Time.time * _currentMoveSpeed * 0.8f + _moveOffset),
            Mathf.Sin(Time.time * _currentMoveSpeed * 1.2f + _moveOffset)
        ) * _currentMoveRange;

        transform.position = _startPos + movement;

        if (_isBlackedOut)
        {
            UpdateVisuals(0f, 0f); // Explicitly force off every frame
            return;
        }

        if (!_isFlashing)
        {

            if (_baseIntensity <= 0.01f)
            {
                UpdateVisuals(0f, 0f); 
                return;
            }

            float effectiveBPM = _currentBPM + (_currentBPM * _bpmRandomness);
            float freq = (effectiveBPM / 60f) * Mathf.PI * 2f;
            float rawSine = (Mathf.Sin((Time.time + _pulseOffset) * freq) + 1f) / 2f; 

            // Remap sine to 0.3 - 1.0 for the light, use rawSine for the alpha
            float flickerFactor = Mathf.Lerp(0.5f, 1.0f, rawSine);
            UpdateVisuals(_baseIntensity * flickerFactor, rawSine);
        }

        
    }

    public void SetBlackout(bool state, float duration)
    {
        if (_blackoutCoroutine != null) StopCoroutine(_blackoutCoroutine);
        _blackoutCoroutine = StartCoroutine(BlackoutRoutine(state, duration));
    }

    private IEnumerator BlackoutRoutine(bool targetState, float duration)
    {
        // If we are starting a blackout, cancel any active flashes
        if (targetState && _effectCoroutine != null)
        {
            StopCoroutine(_effectCoroutine);
            _isFlashing = false;
        }

        // If duration is 0, snap immediately
        if (duration <= 0)
        {
            _isBlackedOut = targetState;
            if (targetState) UpdateVisuals(0f, 0f);
            yield break;
        }

        float elapsed = 0;
        
        float startInt = _light.intensity;
        float startAlpha = _glowMaterial != null ? _glowMaterial.GetColor(_glowMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color").a / 0.6f : 0f;

        if (targetState) 
        {
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Drive values to zero
                UpdateVisuals(Mathf.Lerp(startInt, 0f, t), Mathf.Lerp(startAlpha, 0f, t));
                yield return null;
            }
            _isBlackedOut = true;
        }
        else 
        {
            // _isBlackedOut = false; 
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                float effectiveBPM = _currentBPM + (_currentBPM * _bpmRandomness);
                float freq = (effectiveBPM / 60f) * Mathf.PI * 2f;
                float rawSine = (Mathf.Sin((Time.time + _pulseOffset) * freq) + 1f) / 2f;
                float targetInt = _baseIntensity * Mathf.Lerp(0.5f, 1.0f, rawSine);

                UpdateVisuals(Mathf.Lerp(0f, targetInt, t), Mathf.Lerp(0f, rawSine, t));
                yield return null;
            }
                _isBlackedOut = false;
        }

        _blackoutCoroutine = null;
    }

    // Called by Manager to sync with Global Profiles
    public void UpdateFromProfile(LightingProfile profile, float t, float startInt, float startRange)
    {
        _light.color = Color.Lerp(_light.color, profile.localLightColor, t);
        _baseIntensity = Mathf.Lerp(startInt, profile.localLightIntensity, t);
        _baseRange = Mathf.Lerp(startRange, profile.localLightRange, t);
        _light.range = _baseRange;
        _light.shadowStrength = profile.localLightShadowStrength;

        // randomly offset bpm by up to 10% to create a more natural, unsynced effect across multiple fireflies
        _currentBPM = Mathf.Lerp(_currentBPM, profile.fireflyBPM, t);
        _currentMoveSpeed = Mathf.Lerp(_currentMoveSpeed, profile.fireflyMoveSpeed, t);
        _currentMoveRange = Mathf.Lerp(_currentMoveRange, profile.fireflyMoveRange, t);
    }

    public void TriggerOneShot(float targetIntensity, float duration, float delay = 0f)
    {
        if (_effectCoroutine != null) StopCoroutine(_effectCoroutine);
        _effectCoroutine = StartCoroutine(EffectRoutine(targetIntensity, duration, delay));
    }

    private IEnumerator EffectRoutine(float targetIntensity, float duration, float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        _isFlashing = true;

        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float effectiveBPM = _currentBPM + (_currentBPM * _bpmRandomness);
            float freq = (effectiveBPM / 60f) * Mathf.PI * 2f;
            float rawSine = (Mathf.Sin((Time.time + _pulseOffset) * freq) + 1f) / 2f;
            
            float normalIntensity = _baseIntensity * Mathf.Lerp(0.5f, 1.0f, rawSine);

            float currentIntensity = Mathf.Lerp(targetIntensity, normalIntensity, t);
            
            float currentAlphaFactor = Mathf.Lerp(1f, rawSine, t);

            UpdateVisuals(currentIntensity, currentAlphaFactor);
            yield return null;
        }

        _isFlashing = false;
        _effectCoroutine = null;
    }

    private void UpdateVisuals(float intensity, float alphaFactor)
    {
        _light.intensity = intensity;

        if (_bodyMaterial != null)
        {
            _bodyMaterial.EnableKeyword("_EMISSION");
            if (_bodyMaterial.HasProperty("_BaseColor"))
            {
                _bodyMaterial.SetColor("_BaseColor", _light.color);
            }
            else if (_bodyMaterial.HasProperty("_Color"))
            {
                _bodyMaterial.SetColor("_Color", _light.color);
            }
            _bodyMaterial.SetColor("_EmissionColor", _light.color * intensity);
        }

        if (_glowMaterial != null)
        {
            float targetAlpha = Mathf.Lerp(0.0f, 0.6f, alphaFactor);
            
            if (_baseIntensity <= 0.01f) targetAlpha = 0f;

            Color glowColor = _light.color;
            glowColor.a = targetAlpha;

            if (_glowMaterial.HasProperty("_BaseColor"))
                _glowMaterial.SetColor("_BaseColor", glowColor);  
            else if (_glowMaterial.HasProperty("_Color"))
                _glowMaterial.SetColor("_Color", glowColor);
        }

        if (_glowRing != null)
        {
            if (alphaFactor <= 0.01f || _baseIntensity <= 0.01f) 
            {
                _glowRing.localScale = Vector3.zero;
                return;
            }
            float lockedScale = Mathf.Lerp(0.1f, 0.3f, alphaFactor) * (_baseIntensity);
            if (_baseIntensity <= 0.01f) lockedScale = 0f;
            
            _glowRing.localScale = Vector3.one * lockedScale;
        }
    }
}