using UnityEngine;
using System;

public class RhythmBeatPulse : MonoBehaviour
{
    public static RhythmBeatPulse Instance;
    public static event Action OnBeat;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void TriggerPulse()
    {
        OnBeat?.Invoke();
    }

}