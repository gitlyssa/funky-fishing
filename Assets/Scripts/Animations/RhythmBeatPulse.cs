using UnityEngine;
using System;

public class RhythmBeatPulse : MonoBehaviour
{
    public static RhythmBeatPulse Instance;

    [Header("BPM Settings")]
    public float bpm = 120f;

    public static event Action OnBeat;

    private float _beatInterval;
    private float _timer;
    private bool _isBroadcasting;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        _beatInterval = 60f / bpm;
    }

    void Update()
    {
        if (RhythmConductor.Instance == null || RhythmConductor.rhythmMusicPlayer == null) return;
        
        UpdateBeatTimer();
    }

    private void UpdateBeatTimer()
    {
        _timer += Time.deltaTime;

        if (_timer >= _beatInterval)
        {
            _timer -= _beatInterval;
            TriggerPulse();
        }
    }

    private void TriggerPulse()
    {
        OnBeat?.Invoke();
        // Debug.Log("<color=cyan>BEAT!</color>");
    }

    public void ResetTimer() => _timer = 0f;
}