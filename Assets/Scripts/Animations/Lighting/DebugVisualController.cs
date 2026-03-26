using UnityEngine;
using System.Collections.Generic;

public class DebugVisualController : MonoBehaviour
{
    [Header("Manager Reference")]
    public GlobalLightingManager lightingManager;

    [Header("Mood Profiles")]
    public List<LightingProfile> moodProfiles;
    private int _currentMoodIndex = 0;

    [Header("Settings")]
    public float transitionDuration = 1.5f;
    public float flashIntensity = 5f;
    public float flashDuration = 0.2f;

    public Color lightningColor = Color.white;
    public Color synthwaveColor = new Color(1f, 0f, 1f); // Neon Purple

    void Update()
    {
        if (lightingManager == null || moodProfiles.Count == 0) return;

        // TAB: Cycle through all Lighting Profiles in the list
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            _currentMoodIndex = (_currentMoodIndex + 1) % moodProfiles.Count;
            lightingManager.TransitionToProfile(moodProfiles[_currentMoodIndex], transitionDuration);
            Debug.Log($"Transitioning to Mood: {moodProfiles[_currentMoodIndex].name}");
        }

        // SPACE: Trigger Skybox Lightning Flash
        if (Input.GetKeyDown(KeyCode.Space))
        {
            lightingManager.TriggerSkyboxFlash(lightningColor, flashIntensity, flashDuration);
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            lightingManager.TriggerSkyboxFlash(synthwaveColor, 4.0f, 0.5f);
        }

        // F: Trigger a Spatial Wave flash from the center (0,0,0)
        if (Input.GetKeyDown(KeyCode.F))
        {
            lightingManager.TriggerWave(Vector3.zero, 20f, flashIntensity, flashDuration);
        }
    }
}