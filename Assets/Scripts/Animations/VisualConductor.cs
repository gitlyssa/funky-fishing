using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

public class VisualConductor : MonoBehaviour
{
    public static VisualConductor Instance;

    [Header("References")]
    public GlobalLightingManager lightingManager;
    
    [Header("Profile Library")]
    public List<LightingProfile> availableProfiles;
    private Dictionary<string, LightingProfile> _profileMap = new Dictionary<string, LightingProfile>();
    
    private struct VisualEvent
    {
        public float timestamp;
        public string command;
        public string[] parameters;
    }

    private List<VisualEvent> _eventList = new List<VisualEvent>();
    private int _nextEventIndex = 0;
    private bool _isActive = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        foreach (var p in availableProfiles)
        {
            if (!_profileMap.ContainsKey(p.name)) _profileMap.Add(p.name, p);
        }
    }

    public void LoadVisualScript(TextAsset scriptAsset)
    {
        if (scriptAsset == null)
        {
            _isActive = false;
            return;
        }

        _eventList.Clear();
        _nextEventIndex = 0;

        string[] lines = scriptAsset.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        
        for (int i = 2; i < lines.Length; i++) // Skip header and bpm  line
        {
            string[] parts = lines[i].Split(',');
            if (parts.Length < 3) continue;

            _eventList.Add(new VisualEvent {
                timestamp = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                command = parts[1].Trim(),
                parameters = parts[2].Split(' ')
            });
        }
        
        _eventList.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));
        _isActive = true;
    }
    
    public void StopAndReset()
    {
        _isActive = false;
        _eventList.Clear();
        // Return the pond to a neutral state so fishing looks normal again
        if (lightingManager != null) lightingManager.TriggerBlackout(false, 1.0f);
    }

    void Update()
    {
        // Follow the pause logic of the RhythmConductor
        if (!_isActive || RhythmConductor.Instance == null) return;
        if (RhythmConductor.rhythmMusicPlayer.IsPausedForGamePause) return;

        float currentTime = RhythmConductor.Instance.songTime;
        // Process all events that have passed since the last frame
        while (_nextEventIndex < _eventList.Count && currentTime >= _eventList[_nextEventIndex].timestamp)
        {
            ExecuteEvent(_eventList[_nextEventIndex]);
            _nextEventIndex++;
        }
    }

    void ExecuteEvent(VisualEvent ev)
{
    string[] p = ev.parameters;
    

    if (p.Length > 0 && p[0] == "repeat")
    {
        float bpm = float.Parse(p[1], CultureInfo.InvariantCulture);
        float totalDuration = float.Parse(p[2], CultureInfo.InvariantCulture);
        
        string[] originalParams = new string[p.Length - 3];
        System.Array.Copy(p, 3, originalParams, 0, p.Length - 3);
        
        StartCoroutine(RepeatSyncRoutine(ev.command, bpm, totalDuration, originalParams));
        return; 
    }

    ExecuteSingleCommand(ev.command, p);
}

private System.Collections.IEnumerator RepeatSyncRoutine(string command, float bpm, float totalDuration, string[] p)
    {
        float interval = 60f / bpm;
        float startTime = RhythmConductor.Instance.songTime;
        int beatsTriggered = 0;

        while (true)
        {
            float targetTime = startTime + (beatsTriggered * interval);
            float relativeElapsed = targetTime - startTime;

            if (relativeElapsed >= totalDuration) break;

            while (RhythmConductor.Instance.songTime < targetTime)
            {
                if (!_isActive || RhythmConductor.Instance == null) yield break;
                yield return null;
            }

            ExecuteSingleCommand(command, p);
            beatsTriggered++;
        }
    }

private void ExecuteSingleCommand(string command, string[] p)
    {
        switch (command)
        {
            case "Profile":
                if (_profileMap.TryGetValue(p[0], out LightingProfile profile))
                    lightingManager.TransitionToProfile(profile, float.Parse(p[1]));
                break;

            case "Pulse":
                float pulseIntensity = float.Parse(p[0]);
                int[] groups = new int[p.Length - 1];
                for (int i = 1; i < p.Length; i++) groups[i - 1] = int.Parse(p[i]);
                lightingManager.TriggerPulse(pulseIntensity, groups);
                break;

            case "Flash":
                if (ColorUtility.TryParseHtmlString(p[0], out Color c))
                // show colour or if invalid, flash white    
                if (c == default) c = Color.white;
                    lightingManager.TriggerGlobalFlash(c, float.Parse(p[1]), float.Parse(p[2]));
                break;

            case "FireflyPulse":
                lightingManager.TriggerLocalFlash(float.Parse(p[0]), float.Parse(p[1]));
                break;

            case "Blackout":
                lightingManager.TriggerBlackout(bool.Parse(p[0]), float.Parse(p[1]));
                break;

            case "FireflyAgitation":
                lightingManager.TriggerAgitation(float.Parse(p[0]), float.Parse(p[1]), float.Parse(p[2]));
                break;

            case "BloomKick":
                lightingManager.TriggerBloomKick(float.Parse(p[0]), float.Parse(p[1]));
                break;

            case "Glitch":
                lightingManager.TriggerGlitch(float.Parse(p[0]), float.Parse(p[1]));
                break;

            case "Ripple":
                Vector3 pos = new Vector3(float.Parse(p[0]), float.Parse(p[1]), float.Parse(p[2]));
                lightingManager.TriggerWave(pos, float.Parse(p[3]), float.Parse(p[4]), float.Parse(p[5]));
                break;
        }
    }




    private string PartOr(string[] p, int idx, string def) => (p.Length > idx) ? p[idx] : def;

    public void StopConducting() => _isActive = false;
}
