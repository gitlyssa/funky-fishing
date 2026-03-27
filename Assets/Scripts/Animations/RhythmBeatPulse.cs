using UnityEngine;
using System;

public class RhythmBeatPulse : MonoBehaviour
{
    public static RhythmBeatPulse Instance;
    public static event System.Action<float, int[]> OnBeat;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void TriggerBeat(float intensity, int[] groups)
    {
        OnBeat?.Invoke(intensity, groups);
    }

}