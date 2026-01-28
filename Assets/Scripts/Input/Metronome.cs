using UnityEngine;

public class Metronome : MonoBehaviour
{
    private MusicPlayer musicPlayer;
    public float bpm = 100f;    /// Specifically for jamBeatMap
    public float beatDurationMs;
    public float lastBeat;
    private float nextBeatPosition;
    private float songStartTime;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Find the MusicPlayer component in the scene
        musicPlayer = FindObjectOfType<MusicPlayer>();

        if (musicPlayer == null)
        {
            Debug.LogError("Metronome: No MusicPlayer found in the scene.");
        }

        songStartTime = Time.time;
        beatDurationMs = 60 / bpm * 1000 / 4;   // Duration of a quarter note
        lastBeat = 0;   // Tracks the number of beats
        nextBeatPosition = beatDurationMs;  // Initialize first beat position
    }

    // Update is called once per frame
    void Update()
    {
        if (musicPlayer != null)
        {
            float position = musicPlayer.timePositionMs;

            if (position >= nextBeatPosition)
            {
                lastBeat += 1;
                Debug.Log($"beat: {lastBeat}");
                nextBeatPosition += beatDurationMs;
            }

            // // Use the current time for metronome logic (placeholder for now)
            // Debug.Log($"Metronome: Current song time is {currentTime} ms.");
        }
    }

    // Returns the continuous current beat based on song position
    public float CurrentBeat
    {
        get
        {
            if (musicPlayer == null) return 0f;
            float secondsPerBeat = 60f / bpm;
            return (musicPlayer.timePositionMs / 1000f) / secondsPerBeat;
        }
    }

    public float GetTimeForBeat(float beat)
    {
        float secondsPerBeat = 60f / bpm;
        return songStartTime + beat * secondsPerBeat;
    }
}
