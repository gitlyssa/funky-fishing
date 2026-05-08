using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public class GlobalLightingManager : MonoBehaviour
{
    public static GlobalLightingManager Instance { get; private set; }
     void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }
    public LightingProfile currentProfile;
    private static List<LocalLightController> _allLights = new List<LocalLightController>();
    private Coroutine _transitionCoroutine;

    [Header("Post-Processing Volumes")]
    public Volume volumeA;
    public Volume volumeB;
    private bool _isUsingA = true;

    [Header("Effect Volume")]
    public Volume effectVolume; 
    private Bloom _bloom;
    private ChromaticAberration _chromatic;
    private LensDistortion _lens;

    [Header("Screen Effects")]
    [SerializeField] private bool enableScreenEffects = false;

    [Header("Global Time Cycle")]
    public bool isAutoCycling = true;
    public float timeSpeed = 0.1f;
    public LightingProfile dawn, day, sunset, midnight;
    public float exitBattleTransitionDuration = 3.0f;

    private float _globalTime = 1f;
    private int _lastHour = -1;
  
    private Material _runtimeSkybox;
    public Light mainSunlight; 
    public static void RegisterLight(LocalLightController l) => _allLights.Add(l);
    public static void UnregisterLight(LocalLightController l) => _allLights.Remove(l);


    void Start()
    {
        if (currentProfile != null)
            ApplyProfileImmediate(currentProfile);

        if (effectVolume != null && effectVolume.profile != null)
        {
            effectVolume.profile.TryGet(out _bloom);
            effectVolume.profile.TryGet(out _chromatic);
            effectVolume.profile.TryGet(out _lens);
        }

        ResetScreenEffects();
    }
    void Update()
    {
        _globalTime = (_globalTime + Time.unscaledDeltaTime * timeSpeed) % 4f;

        if (isAutoCycling)
        {
            int currentHour = Mathf.FloorToInt(_globalTime);

            if (currentHour != _lastHour)
            {
                SnapToBoundary(currentHour);
                _lastHour = currentHour;
            }

            ApplyCycleState(_globalTime);
        }
    }
    public LightingProfile GetProfileByHour(int hour)
    {
        hour = hour % 4;
        if (hour < 0) hour += 4;
        switch (hour)
        {
            case 0: return dawn;
            case 1: return day;
            case 2: return sunset;
            case 3: return midnight;
            default: return day;
        }
    }

    private void SnapToBoundary(int hour)
    {
        // LightingProfile profile = GetProfileByHour(hour);
        // ApplyProfileImmediate(profile);
    }

    private void ApplyCycleState(float clockTime)
    {
        int hour = Mathf.FloorToInt(clockTime);
        float subT = clockTime % 1f;

        LightingProfile from = GetProfileByHour(hour);
        LightingProfile to = GetProfileByHour(hour + 1);

    float localClockTime = (clockTime + 0.5f) % 4f;
    int localHour = Mathf.FloorToInt(localClockTime);
    float localSubT = localClockTime % 1f;

    LightingProfile localFrom = GetProfileByHour(localHour);
    LightingProfile localTo = GetProfileByHour(localHour + 1);

    float localInt = Mathf.Lerp(localFrom.localLightIntensity, localTo.localLightIntensity, localSubT);
    float localRange = Mathf.Lerp(localFrom.localLightRange, localTo.localLightRange, localSubT);

    SetEnvironmentState(from, to, subT, localFrom, localInt, localRange);


    }

    private void SetEnvironmentState(LightingProfile from, LightingProfile to, float subT, LightingProfile localFrom, float localInt, float localRange)
    {
        //Volumes
        volumeA.profile = from.volumeProfile;
        volumeB.profile = to.volumeProfile;
        volumeA.weight = 1f - subT;
        volumeB.weight = subT;

        // Sun
        if (mainSunlight != null)
        {
            mainSunlight.color = Color.Lerp(from.directionalLightColor, to.directionalLightColor, subT);
            mainSunlight.intensity = Mathf.Lerp(from.directionalLightIntensity, to.directionalLightIntensity, subT);
            mainSunlight.shadowStrength = Mathf.Lerp(from.directionalLightShadowStrength, to.directionalLightShadowStrength, subT);
            mainSunlight.transform.rotation = Quaternion.Slerp(
                Quaternion.Euler(from.directionalLightDirection),
                Quaternion.Euler(to.directionalLightDirection),
                subT);
        }

        // Ambient & Fog
        RenderSettings.ambientLight = Color.Lerp(from.ambientColor, to.ambientColor, subT);
        RenderSettings.ambientIntensity = Mathf.Lerp(from.ambientIntensity, to.ambientIntensity, subT);
        RenderSettings.fogColor = Color.Lerp(from.fogColor, to.fogColor, subT);
        RenderSettings.fogDensity = Mathf.Lerp(from.fogDensity, to.fogDensity, subT);

        // Skybox Params
        if (_runtimeSkybox != null)
        {
            _runtimeSkybox.SetFloat("_Exposure", Mathf.Lerp(from.skyboxExposure, to.skyboxExposure, subT));
            ApplyTintToRuntime(Color.Lerp(from.skyboxTint, to.skyboxTint, subT));
        }


        foreach (var light in _allLights) 
        {
            light.UpdateFromProfile(localFrom, 1f, localInt, localRange);
        }
    }

    public void ResumeAutoCycle(float duration)
    {
        isAutoCycling = false; 
        if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
        _transitionCoroutine = StartCoroutine(TransitionBackToClockRoutine(duration));
    }

    private IEnumerator TransitionBackToClockRoutine(float duration)
    {
        // 1. Capture the exact state of the pond the moment the song ended
        Color startAmb = RenderSettings.ambientLight;
        float startAmbInt = RenderSettings.ambientIntensity;
        Color startFog = RenderSettings.fogColor;
        float startFogDens = RenderSettings.fogDensity;

        Color startSun = mainSunlight != null ? mainSunlight.color : Color.white;
        float startSunInt = mainSunlight != null ? mainSunlight.intensity : 1f;
        Quaternion startSunRot = mainSunlight != null ? mainSunlight.transform.rotation : Quaternion.identity;

        float startSkyExp = _runtimeSkybox != null ? _runtimeSkybox.GetFloat("_Exposure") : 1f;
        Color startSkyTint = GetCurrentSkyTint();

        float startLocalInt = currentProfile != null ? currentProfile.localLightIntensity : 1.5f;
        float startLocalRange = currentProfile != null ? currentProfile.localLightRange : 10f;

        Volume activeVol = _isUsingA ? volumeA : volumeB;
        Volume targetVol = _isUsingA ? volumeB : volumeA;
        float startActiveWeight = activeVol.weight;

        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float curve = Mathf.SmoothStep(0, 1, t);

            // 2. Determine where the Clock target is RIGHT NOW
            float clockT = _globalTime;
            int currentHour = Mathf.FloorToInt(clockT);
            float subT = clockT % 1f;
            LightingProfile from = GetProfileByHour(currentHour);
            LightingProfile to = GetProfileByHour(currentHour + 1);

            // Calculate Target Values
            Color targetAmb = Color.Lerp(from.ambientColor, to.ambientColor, subT);
            float targetAmbInt = Mathf.Lerp(from.ambientIntensity, to.ambientIntensity, subT);
            Color targetFog = Color.Lerp(from.fogColor, to.fogColor, subT);
            float targetFogDens = Mathf.Lerp(from.fogDensity, to.fogDensity, subT);
            Color targetSun = Color.Lerp(from.directionalLightColor, to.directionalLightColor, subT);
            float targetSunInt = Mathf.Lerp(from.directionalLightIntensity, to.directionalLightIntensity, subT);
            Quaternion targetSunRot = Quaternion.Slerp(Quaternion.Euler(from.directionalLightDirection), Quaternion.Euler(to.directionalLightDirection), subT);
            float targetSkyExp = Mathf.Lerp(from.skyboxExposure, to.skyboxExposure, subT);
            Color targetSkyTint = Color.Lerp(from.skyboxTint, to.skyboxTint, subT);

            // 3. Lerp from Captured Start -> Moving Target
            RenderSettings.ambientLight = Color.Lerp(startAmb, targetAmb, curve);
            RenderSettings.ambientIntensity = Mathf.Lerp(startAmbInt, targetAmbInt, curve);
            RenderSettings.fogColor = Color.Lerp(startFog, targetFog, curve);
            RenderSettings.fogDensity = Mathf.Lerp(startFogDens, targetFogDens, curve);

            if (mainSunlight != null)
            {
                mainSunlight.color = Color.Lerp(startSun, targetSun, curve);
                mainSunlight.intensity = Mathf.Lerp(startSunInt, targetSunInt, curve);
                mainSunlight.transform.rotation = Quaternion.Slerp(startSunRot, targetSunRot, curve);
            }

            if (_runtimeSkybox != null)
            {
                _runtimeSkybox.SetFloat("_Exposure", Mathf.Lerp(startSkyExp, targetSkyExp, curve));
                ApplyTintToRuntime(Color.Lerp(startSkyTint, targetSkyTint, curve));
            }

            // Volume Swap: Fade battle out, pop the clock's profile in
            targetVol.profile = subT < 0.5f ? from.volumeProfile : to.volumeProfile;
            activeVol.weight = Mathf.Lerp(startActiveWeight, 0f, curve);
            targetVol.weight = Mathf.Lerp(0f, 1f, curve);

            // Local Lights
            float localClockT = (clockT + 0.5f) % 4f;
    int localHour = Mathf.FloorToInt(localClockT);
    float localSubT = localClockT % 1f;
    LightingProfile localFrom = GetProfileByHour(localHour);
    LightingProfile localTo = GetProfileByHour(localHour + 1);

    float targetLocalInt = Mathf.Lerp(localFrom.localLightIntensity, localTo.localLightIntensity, localSubT);
    float targetLocalRange = Mathf.Lerp(localFrom.localLightRange, localTo.localLightRange, localSubT);
            foreach (var light in _allLights)
    {
        light.UpdateFromProfile(localFrom, 1f, 
            Mathf.Lerp(startLocalInt, targetLocalInt, curve), 
            Mathf.Lerp(startLocalRange, targetLocalRange, curve));
    }

            yield return null;
        }

        // 4. Clean execution handoff
        SnapToBoundary(Mathf.FloorToInt(_globalTime));
        isAutoCycling = true;
        _transitionCoroutine = null;
    }



    
    public void TransitionToProfile(LightingProfile target, float duration)
    {
        if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
        _transitionCoroutine = StartCoroutine(LerpLighting(target, duration));
    }
    

    private IEnumerator LerpLighting(LightingProfile target, float duration)
    {
        float time = 0;

        // Capture Start States
        Color startAmbient = RenderSettings.ambientLight;
        float startAmbInt = RenderSettings.ambientIntensity;
        float startFogDensity = RenderSettings.fogDensity;
        Color startFogColor = RenderSettings.fogColor;

        // Sunlight Start States
    Color startSunColor = mainSunlight != null ? mainSunlight.color : Color.white;
    float startSunInt = mainSunlight != null ? mainSunlight.intensity : 0f;
    float startSunShadow = mainSunlight != null ? mainSunlight.shadowStrength : 0f;
    Quaternion startSunRot = mainSunlight != null ? mainSunlight.transform.rotation : Quaternion.identity;

        float startLocalInt = currentProfile != null ? currentProfile.localLightIntensity : 0f;
        float startLocalRange = currentProfile != null ? currentProfile.localLightRange : 0f;

        float startSkyExp = _runtimeSkybox != null ? _runtimeSkybox.GetFloat("_Exposure") : 0f;
        Color startSkyTint = GetCurrentSkyTint();

        bool materialChange = currentProfile.skyboxMaterial != target.skyboxMaterial;
        bool swapped = false;

        if (target.ambientEnabled)
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

        //fade out active
        Volume activeVolume = _isUsingA ? volumeA : volumeB;
        Volume targetVolume = _isUsingA ? volumeB : volumeA;

        targetVolume.profile = target.volumeProfile;
        targetVolume.weight = 0; 

        while (time < duration)
        {   
            float t = time / duration;

            // standard lerps
            RenderSettings.ambientLight = Color.Lerp(startAmbient, target.ambientColor, t);
            RenderSettings.ambientIntensity = Mathf.Lerp(startAmbInt, target.ambientIntensity, t);
            RenderSettings.fogColor = Color.Lerp(startFogColor, target.fogColor, t);
            RenderSettings.fogDensity = Mathf.Lerp(startFogDensity, target.fogDensity, t);

            if (mainSunlight != null)
            {
                mainSunlight.color = Color.Lerp(startSunColor, target.directionalLightColor, t);
                mainSunlight.intensity = Mathf.Lerp(startSunInt, target.directionalLightIntensity, t);
                mainSunlight.shadowStrength = Mathf.Lerp(startSunShadow, target.directionalLightShadowStrength, t);
                
                mainSunlight.transform.rotation = Quaternion.Slerp(startSunRot, Quaternion.Euler(target.directionalLightDirection), t);
            }

        if (_runtimeSkybox != null)
            {
                if (materialChange)
                {
                    if (t < 0.5f)
                    {
                        _runtimeSkybox.SetFloat("_Exposure", Mathf.Lerp(startSkyExp, 0f, t * 2f));
                    }
                    else
                    {
                        if (!swapped)
                        {
                            if (_runtimeSkybox != null) Destroy(_runtimeSkybox);
                            _runtimeSkybox = new Material(target.skyboxMaterial);
                            RenderSettings.skybox = _runtimeSkybox;
                            swapped = true;
                        }
                        _runtimeSkybox.SetFloat("_Exposure", Mathf.Lerp(0f, target.skyboxExposure, (t - 0.5f) * 2f));
                    }
                }
                else
                {
                    _runtimeSkybox.SetFloat("_Exposure", Mathf.Lerp(startSkyExp, target.skyboxExposure, t));
                }
                
                // Tint Update
                ApplyTintToRuntime(Color.Lerp(startSkyTint, target.skyboxTint, t));
            }
            

            // local lights
            foreach (var light in _allLights)
                light.UpdateFromProfile(target, t, startLocalInt, startLocalRange);

            // volumes
            activeVolume.weight = 1f - t;
            targetVolume.weight = t;

            time += Time.deltaTime;
            yield return null;
        }
        ApplyProfileImmediate(target);
        currentProfile = target;
    }

    public void ApplyProfileImmediate(LightingProfile profile)
    {   
        
        // Ambient Settings
        if (profile.ambientEnabled)
        { 
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = profile.ambientColor;
        } else 
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        }
        RenderSettings.ambientIntensity = profile.ambientIntensity;

        // Directional Light Settings
        if (mainSunlight != null)
        {
            mainSunlight.color = profile.directionalLightColor;
            mainSunlight.intensity = profile.directionalLightIntensity;
            mainSunlight.shadowStrength = profile.directionalLightShadowStrength;
            mainSunlight.transform.rotation = Quaternion.Euler(profile.directionalLightDirection);
        }

        // Fog Settings
        RenderSettings.fog = profile.fogEnabled;
        RenderSettings.fogColor = profile.fogColor;
        RenderSettings.fogDensity = profile.fogDensity;

        // Skybox Settings
        if (profile.skyboxMaterial != null)
        {

            if (_runtimeSkybox != null) Destroy(_runtimeSkybox);

            _runtimeSkybox = new Material(profile.skyboxMaterial);
            RenderSettings.skybox = _runtimeSkybox;

            _runtimeSkybox.SetFloat("_Exposure", profile.skyboxExposure);

            if (_runtimeSkybox.HasProperty("_Tint"))
                _runtimeSkybox.SetColor("_Tint", profile.skyboxTint);
            else if (_runtimeSkybox.HasProperty("_SkyTint"))
                _runtimeSkybox.SetColor("_SkyTint", profile.skyboxTint);
        }


        // volume profiles
        volumeA.profile = profile.volumeProfile;
        volumeA.weight = 1f;
        volumeB.weight = 0f;
        _isUsingA = true;


        // local lights
        foreach (var light in _allLights)
            light.UpdateFromProfile(profile, 1f, profile.localLightIntensity, profile.localLightRange);
    
        currentProfile = profile;
    }

    private Color GetCurrentSkyTint()
    {
        if (_runtimeSkybox == null) return Color.white;
        if (_runtimeSkybox.HasProperty("_Tint")) return _runtimeSkybox.GetColor("_Tint");
        if (_runtimeSkybox.HasProperty("_SkyTint")) return _runtimeSkybox.GetColor("_SkyTint");
        return Color.white;
    }

    private void ApplyTintToRuntime(Color color)
    {
        if (_runtimeSkybox == null) return;
        if (_runtimeSkybox.HasProperty("_Tint")) _runtimeSkybox.SetColor("_Tint", color);
        else if (_runtimeSkybox.HasProperty("_SkyTint")) _runtimeSkybox.SetColor("_SkyTint", color);
    }

    public void TriggerGlobalFlash(Color flashColor, float intensity, float duration)
    {
        if (!enableScreenEffects || _runtimeSkybox == null)
        {
            ResetScreenEffects();
            return;
        }

        StartCoroutine(LightningRoutine(flashColor, intensity, duration));
    }

    private IEnumerator LightningRoutine(Color flashColor, float intensity, float duration)
    {
        float originalExp = _runtimeSkybox.GetFloat("_Exposure");
        Color originalTint = GetCurrentSkyTint(); // Uses the helper from the previous step
        _runtimeSkybox.SetFloat("_Exposure", intensity);
        ApplyTintToRuntime(flashColor);
        
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            _runtimeSkybox.SetFloat("_Exposure", Mathf.Lerp(intensity, originalExp, t));
            ApplyTintToRuntime(Color.Lerp(flashColor, originalTint, t));
            
            yield return null;
        }
        
        _runtimeSkybox.SetFloat("_Exposure", originalExp);
        ApplyTintToRuntime(originalTint);
    }

    public void TriggerWave(Vector3 origin, float speed, float intensity, float duration)
    {
        foreach (var light in _allLights)
        {
            float distance = Vector3.Distance(origin, light.Position);
            float delay = distance / speed;
            light.TriggerOneShot(intensity, duration, delay);
        }
    }

    // Creates a "Firefly" glitter effect
    public void TriggerTwinkle(int count, float minIntensity, float maxIntensity)
    {
        for (int i = 0; i < count; i++)
        {
            if (_allLights.Count == 0) break;
            int idx = Random.Range(0, _allLights.Count);
            float randIntensity = Random.Range(minIntensity, maxIntensity);
            float randDur = Random.Range(0.3f, 0.7f);
            _allLights[idx].TriggerOneShot(randIntensity, randDur);
        }
    }

    public void TriggerAngularSweep(Vector3 origin, float rotationSpeed, float intensity, float duration)
{
    foreach (var light in _allLights)
    {
        Vector3 dir = light.Position - origin;
        // Calculate angle in degrees (0 to 360)
        float angle = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        // Delay is based on the angle instead of distance
        float delay = angle / rotationSpeed;
        light.TriggerOneShot(intensity, duration, delay);
    }
}

public void TriggerDirectionalScan(Vector3 direction, float speed, float intensity, float duration)
{
    // Normalize the direction (e.g., Vector3.right)
    Vector3 scanAxis = direction.normalized;

    foreach (var light in _allLights)
    {
        // Project position onto the scan axis to get the "distance" along that path
        float projection = Vector3.Dot(light.Position, scanAxis);
        float delay = projection / speed;
        
        // Offset delay so it doesn't start at a negative time
        light.TriggerOneShot(intensity, duration, Mathf.Max(0, delay));
    }
}

public void TriggerLocalFlash(float intensity, float duration)
{
    foreach (var light in _allLights)
    {
        light.TriggerOneShot(intensity, duration, 0f);
    }
}

    public void TriggerBlackout(bool state, float duration)
    {
        foreach (var light in _allLights) light.SetBlackout(state, duration);
    }

    // Call this for a timed "blink" effect
    public void TriggerTimedBlackout(float duration, float fadeTime)
    {
        StartCoroutine(TimedBlackout(duration, fadeTime));
    }

    private IEnumerator TimedBlackout(float duration, float fadeTime)
    {
        TriggerBlackout(true, fadeTime);
        yield return new WaitForSeconds(duration + fadeTime);
        TriggerBlackout(false, fadeTime);
    }

    public void TriggerAgitation(float speed, float range, float duration)
    {
        foreach (var light in _allLights) light.SetAgitation(speed, range, duration);
    }

    public void TriggerBloomKick(float intensity, float duration)
    {
        if (!enableScreenEffects || _bloom == null || effectVolume == null)
        {
            ResetScreenEffects();
            return;
        }

        StartCoroutine(BloomKickRoutine(intensity, duration));
    }

    private IEnumerator BloomKickRoutine(float intensity, float duration)
    {
        _bloom.intensity.Override(intensity);
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            effectVolume.weight = Mathf.Lerp(1, 0, elapsed / duration);
            yield return null;
        }
        effectVolume.weight = 0;
        _bloom.intensity.Override(0);
    }

    public void TriggerGlitch(float intensity, float duration)
    {
        if (!enableScreenEffects || _chromatic == null || _lens == null || effectVolume == null)
        {
            ResetScreenEffects();
            return;
        }

        StartCoroutine(GlitchRoutine(intensity, duration));
    }

    private IEnumerator GlitchRoutine(float intensity, float duration)
    {
        _chromatic.intensity.Override(intensity);
        _lens.intensity.Override(-intensity * 0.5f); // Slight "suck in" effect
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            effectVolume.weight = Mathf.Lerp(1, 0, elapsed / duration);
            yield return null;
        }
        effectVolume.weight = 0;
        _chromatic.intensity.Override(0);
        _lens.intensity.Override(0);
    }

public void TriggerPulse(float intensity, int[] groups) => RhythmBeatPulse.Instance.TriggerBeat(intensity, groups);

    private void ResetScreenEffects()
    {
        if (effectVolume != null)
            effectVolume.weight = 0f;

        if (_bloom != null)
            _bloom.intensity.Override(0f);

        if (_chromatic != null)
            _chromatic.intensity.Override(0f);

        if (_lens != null)
            _lens.intensity.Override(0f);
    }





}
