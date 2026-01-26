using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Beatmap", menuName = "Scriptable Objects/Beatmap")]
public class Beatmap : ScriptableObject
{
    public List<BeatEvent> events;
}

[System.Serializable]
public class BeatEvent
{
    public int beat;
    public float sAngle;
    public float eAngle;
}