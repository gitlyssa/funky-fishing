using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum FunkyAudioCategory
{
    Music,
    Ambient,
    Sfx
}

[DisallowMultipleComponent]
public sealed class FunkyAudioSettings : MonoBehaviour
{
    private const string MasterVolumePrefKey = "FunkyFishing.Options.MasterVolume";
    private const string MusicVolumePrefKey = "FunkyFishing.Options.MusicVolume";
    private const string AmbientVolumePrefKey = "FunkyFishing.Options.AmbientVolume";
    private const string SfxVolumePrefKey = "FunkyFishing.Options.SfxVolume";
    private const float DefaultVolume = 1f;
    private const float AmbientMaxGain = 1.75f;
    private const string MasterBusPath = "bus:/";

    private static FunkyAudioSettings _instance;

    private readonly List<StudioEventEmitter> _cachedEmitters = new List<StudioEventEmitter>();
    private readonly Dictionary<int, FunkyAudioCategory> _emitterCategoryCache = new Dictionary<int, FunkyAudioCategory>();

    private Bus _masterBus;
    private float _nextEmitterRefreshTime;
    private float _masterVolume = DefaultVolume;
    private float _musicVolume = DefaultVolume;
    private float _ambientVolume = DefaultVolume;
    private float _sfxVolume = DefaultVolume;

    public static float MasterVolume => Instance._masterVolume;
    public static float MusicVolume => Instance._musicVolume;
    public static float AmbientVolume => Instance._ambientVolume;
    public static float SfxVolume => Instance._sfxVolume;

    private static FunkyAudioSettings Instance
    {
        get
        {
            if (_instance == null)
                EnsureInstance();

            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (_instance != null)
            return;

        GameObject go = new GameObject(nameof(FunkyAudioSettings));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<FunkyAudioSettings>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        LoadPrefs();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (_instance != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        _instance = null;
    }

    private void Start()
    {
        RefreshEmitterCache();
        ApplyAllVolumes();
    }

    private void Update()
    {
        ApplyMasterVolume();
        ApplyEmitterVolumes();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshEmitterCache();
        ApplyAllVolumes();
    }

    public static void SetMasterVolume(float value)
    {
        Instance.SetMasterVolumeInternal(value);
    }

    public static void SetMusicVolume(float value)
    {
        Instance.SetCategoryVolumeInternal(FunkyAudioCategory.Music, value);
    }

    public static void SetAmbientVolume(float value)
    {
        Instance.SetCategoryVolumeInternal(FunkyAudioCategory.Ambient, value);
    }

    public static void SetSfxVolume(float value)
    {
        Instance.SetCategoryVolumeInternal(FunkyAudioCategory.Sfx, value);
    }

    public static void ResetToDefaults()
    {
        Instance.ResetToDefaultsInternal();
    }

    public static float GetCategoryVolume(FunkyAudioCategory category)
    {
        return Instance.GetCategoryVolumeInternal(category);
    }

    public static void ApplyCategoryVolume(EventInstance instance, FunkyAudioCategory category)
    {
        if (!instance.isValid())
            return;

        instance.setVolume(GetCategoryVolume(category));
    }

    public static void PlayOneShot(string eventPath, Vector3 position, FunkyAudioCategory category)
    {
        if (string.IsNullOrWhiteSpace(eventPath))
            return;

        EventInstance instance = RuntimeManager.CreateInstance(eventPath);
        PlayManagedOneShot(instance, position, category);
    }

    public static void PlayOneShot(EventReference eventReference, Vector3 position, FunkyAudioCategory category)
    {
        if (eventReference.IsNull)
            return;

        EventInstance instance = RuntimeManager.CreateInstance(eventReference);
        PlayManagedOneShot(instance, position, category);
    }

    private static void PlayManagedOneShot(EventInstance instance, Vector3 position, FunkyAudioCategory category)
    {
        if (!instance.isValid())
            return;

        instance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
        ApplyCategoryVolume(instance, category);
        instance.start();
        instance.release();
    }

    private void SetMasterVolumeInternal(float value)
    {
        _masterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MasterVolumePrefKey, _masterVolume);
        PlayerPrefs.Save();
        ApplyMasterVolume();
    }

    private void SetCategoryVolumeInternal(FunkyAudioCategory category, float value)
    {
        float clamped = Mathf.Clamp01(value);
        switch (category)
        {
            case FunkyAudioCategory.Music:
                _musicVolume = clamped;
                PlayerPrefs.SetFloat(MusicVolumePrefKey, _musicVolume);
                break;
            case FunkyAudioCategory.Ambient:
                _ambientVolume = clamped;
                PlayerPrefs.SetFloat(AmbientVolumePrefKey, _ambientVolume);
                break;
            case FunkyAudioCategory.Sfx:
                _sfxVolume = clamped;
                PlayerPrefs.SetFloat(SfxVolumePrefKey, _sfxVolume);
                break;
        }

        PlayerPrefs.Save();
        ApplyEmitterVolumes(forceRefreshCache: false);
    }

    private void ResetToDefaultsInternal()
    {
        _masterVolume = DefaultVolume;
        _musicVolume = DefaultVolume;
        _ambientVolume = DefaultVolume;
        _sfxVolume = DefaultVolume;

        PlayerPrefs.SetFloat(MasterVolumePrefKey, _masterVolume);
        PlayerPrefs.SetFloat(MusicVolumePrefKey, _musicVolume);
        PlayerPrefs.SetFloat(AmbientVolumePrefKey, _ambientVolume);
        PlayerPrefs.SetFloat(SfxVolumePrefKey, _sfxVolume);
        PlayerPrefs.Save();

        ApplyAllVolumes();
    }

    private float GetCategoryVolumeInternal(FunkyAudioCategory category)
    {
        switch (category)
        {
            case FunkyAudioCategory.Music:
                return _musicVolume;
            case FunkyAudioCategory.Ambient:
                return _ambientVolume * AmbientMaxGain;
            case FunkyAudioCategory.Sfx:
            default:
                return _sfxVolume;
        }
    }

    private void LoadPrefs()
    {
        _masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumePrefKey, DefaultVolume));
        _musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumePrefKey, DefaultVolume));
        _ambientVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(AmbientVolumePrefKey, DefaultVolume));
        _sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumePrefKey, DefaultVolume));
    }

    private void ApplyAllVolumes()
    {
        ApplyMasterVolume();
        ApplyEmitterVolumes(forceRefreshCache: true);
    }

    private void ApplyMasterVolume()
    {
        if (!_masterBus.isValid())
            _masterBus = RuntimeManager.GetBus(MasterBusPath);

        if (_masterBus.isValid())
            _masterBus.setVolume(_masterVolume);

        AudioListener.volume = _masterVolume;
    }

    private void ApplyEmitterVolumes(bool forceRefreshCache = false)
    {
        if (forceRefreshCache || Time.unscaledTime >= _nextEmitterRefreshTime || _cachedEmitters.Count == 0)
            RefreshEmitterCache();

        for (int i = _cachedEmitters.Count - 1; i >= 0; i--)
        {
            StudioEventEmitter emitter = _cachedEmitters[i];
            if (emitter == null)
            {
                _cachedEmitters.RemoveAt(i);
                continue;
            }

            EventInstance instance = emitter.EventInstance;
            if (!instance.isValid())
                continue;

            ApplyCategoryVolume(instance, ResolveEmitterCategory(emitter, instance));
        }
    }

    private void RefreshEmitterCache()
    {
        _nextEmitterRefreshTime = Time.unscaledTime + 1f;
        _cachedEmitters.Clear();

        StudioEventEmitter[] emitters = FindObjectsByType<StudioEventEmitter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < emitters.Length; i++)
        {
            if (emitters[i] != null)
                _cachedEmitters.Add(emitters[i]);
        }
    }

    private FunkyAudioCategory ResolveEmitterCategory(StudioEventEmitter emitter, EventInstance instance)
    {
        int emitterId = emitter.GetInstanceID();
        if (_emitterCategoryCache.TryGetValue(emitterId, out FunkyAudioCategory cachedCategory))
            return cachedCategory;

        FunkyAudioCategory category = ResolveCategoryFromInstance(instance);
        _emitterCategoryCache[emitterId] = category;
        return category;
    }

    private static FunkyAudioCategory ResolveCategoryFromInstance(EventInstance instance)
    {
        if (!instance.isValid())
            return FunkyAudioCategory.Sfx;

        FMOD.RESULT descriptionResult = instance.getDescription(out EventDescription description);
        if (descriptionResult != FMOD.RESULT.OK || !description.isValid())
            return FunkyAudioCategory.Sfx;

        FMOD.RESULT pathResult = description.getPath(out string path);
        if (pathResult != FMOD.RESULT.OK)
            return FunkyAudioCategory.Sfx;

        return ClassifyPath(path);
    }

    private static FunkyAudioCategory ClassifyPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return FunkyAudioCategory.Sfx;

        if (path.StartsWith("event:/Music/", System.StringComparison.OrdinalIgnoreCase))
            return FunkyAudioCategory.Music;

        if (path.StartsWith("event:/Ambience/", System.StringComparison.OrdinalIgnoreCase))
            return FunkyAudioCategory.Ambient;

        return FunkyAudioCategory.Sfx;
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class CategorizedAudioSource : MonoBehaviour
{
    [SerializeField] private FunkyAudioCategory category = FunkyAudioCategory.Ambient;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private float baseVolume = 1f;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
            baseVolume = audioSource.volume;
    }

    private void Update()
    {
        if (audioSource == null)
            return;

        audioSource.volume = baseVolume * FunkyAudioSettings.GetCategoryVolume(category);
    }
}
