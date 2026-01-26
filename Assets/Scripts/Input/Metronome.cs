using UnityEngine;

public class Metronome : MonoBehaviour
{
    private MusicPlayer musicPlayer;
    private float bpm = 100f;    /// Specifically for jamBeatMap
    private float beatDurationMs;
    public float lastBeat;
    private float nextBeatPosition;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Find the MusicPlayer component in the scene
        musicPlayer = FindObjectOfType<MusicPlayer>();

        if (musicPlayer == null)
        {
            Debug.LogError("Metronome: No MusicPlayer found in the scene.");
        }

        beatDurationMs = 60 / bpm * 1000;   // Duration of a quarter note
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
}
