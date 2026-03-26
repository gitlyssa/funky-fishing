using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Light))]
public class LocalLightController : MonoBehaviour
{
    private Light _light;
    private float _baseIntensity;
    private float _baseRange;
    private Coroutine _effectCoroutine;

    public Vector3 Position => transform.position;

    private void Awake() => _light = GetComponent<Light>();

    private void OnEnable() => GlobalLightingManager.RegisterLight(this);
    private void OnDisable() => GlobalLightingManager.UnregisterLight(this);

    // Called by Manager to sync with Global Profiles
    public void UpdateFromProfile(LightingProfile profile, float t, float startInt, float startRange)
    {
        _light.color = Color.Lerp(_light.color, profile.localLightColor, t);
        _baseIntensity = Mathf.Lerp(startInt, profile.localLightIntensity, t);
        _baseRange = Mathf.Lerp(startRange, profile.localLightRange, t);
        
        // If an effect isn't running, stay at base. If one is, it will lerp back to this base.
        if (_effectCoroutine == null)
        {
            _light.intensity = _baseIntensity;
            _light.range = _baseRange;
        }
        
        _light.shadowStrength = profile.localLightShadowStrength;
    }

    public void TriggerOneShot(float targetIntensity, float duration, float delay = 0f)
    {
        if (_effectCoroutine != null) StopCoroutine(_effectCoroutine);
        _effectCoroutine = StartCoroutine(EffectRoutine(targetIntensity, duration, delay));
    }

    private IEnumerator EffectRoutine(float target, float duration, float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        
        float elapsed = 0;
        float startFlashIntensity = target;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            _light.intensity = Mathf.Lerp(startFlashIntensity, _baseIntensity, t);
            yield return null;
        }
        
        _light.intensity = _baseIntensity;
        _effectCoroutine = null;
    }
}