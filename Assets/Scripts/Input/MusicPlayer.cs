// Temporary, will replace with FMOD

using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    public AudioClip musicClip; // Assign jamBeatMap.flac in the Inspector
    private AudioSource audioSource;

    // Public property to expose the current time position in milliseconds
    public float timePositionMs { get; private set; }

    void Start()
    {
        // Get the AudioSource component attached to the GameObject
        audioSource = GetComponent<AudioSource>();

        if (musicClip != null)
        {
            // Assign the audio clip to the AudioSource
            audioSource.clip = musicClip;
            Debug.Log("MusicPlayer: Audio clip assigned successfully.");

            // Play the audio clip
            audioSource.Play();
            Debug.Log("MusicPlayer: Audio is playing.");
        }
        else
        {
            Debug.LogError("MusicPlayer: No audio clip assigned to musicClip.");
        }
    }

    void Update()
    {
        // Update the current time position in milliseconds
        if (audioSource.isPlaying)
        {
            timePositionMs = audioSource.time * 1000f; // Convert seconds to milliseconds
        }
    }
}
