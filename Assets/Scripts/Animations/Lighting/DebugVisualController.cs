using UnityEngine;
using System.Collections.Generic;

public class DebugVisualController : MonoBehaviour
{
    [Header("Manager Reference")]
    public GlobalLightingManager lightingManager;
    public RhythmBeatPulse rhythmBeatPulse;
    private bool _isBlackedOut = false;

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
        // q to just reload current profile
            if (Input.GetKeyDown(KeyCode.Q))
            {
                lightingManager.TransitionToProfile(moodProfiles[_currentMoodIndex], transitionDuration);
                Debug.Log($"Reapplying Mood: {moodProfiles[_currentMoodIndex].name}");
            }

        // trigger various effects based on key input
        if (Input.GetKeyDown(KeyCode.P))
        {
            lightingManager.TriggerPulse(1.0f, new int[] { 0 }); 
            Debug.Log("Triggered Pulse: Intensity 1.0, Group 0");
        }

        // F: Flash - Trigger a global lightning flash
        if (Input.GetKeyDown(KeyCode.F))
        {
            lightingManager.TriggerGlobalFlash(lightningColor, flashIntensity, flashDuration);
            Debug.Log("Triggered Global Flash");
        }

        // R: Ripple - Trigger a wave from the center (0,0,0)
        if (Input.GetKeyDown(KeyCode.R))
        {
            lightingManager.TriggerWave(Vector3.zero, 10.0f, 2.0f, 1.0f);
            Debug.Log("Triggered Ripple from Center");
        }

        // B: Blackout - Toggle the forced-off state
        if (Input.GetKeyDown(KeyCode.B))
        {
            _isBlackedOut = !_isBlackedOut;
            lightingManager.TriggerBlackout(_isBlackedOut, 0.5f);
            Debug.Log($"Blackout State: {_isBlackedOut}");
        }

        // A: Agitation - Make fireflies "scared" and erratic
        if (Input.GetKeyDown(KeyCode.A))
        {
            lightingManager.TriggerAgitation(5.0f, 3.0f, 2.0f);
            Debug.Log("Triggered Firefly Agitation");
        }

        // K: Bloom Kick - Spike the 3rd Volume's Bloom
        if (Input.GetKeyDown(KeyCode.K))
        {
            lightingManager.TriggerBloomKick(10.0f, 0.3f);
            Debug.Log("Triggered Bloom Kick");
        }

        // G: Glitch - Spike Chromatic Aberration and Lens Distortion
        if (Input.GetKeyDown(KeyCode.G))
        {
            lightingManager.TriggerGlitch(0.8f, 0.4f);
            Debug.Log("Triggered Glitch Effect");
        }
    }
}