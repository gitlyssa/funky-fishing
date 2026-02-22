using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class RhythmMusicPlayer : MonoBehaviour
{
    [SerializeField] private EventReference musicEvent;

    public EventInstance musicInstance;
    private BobberArcCaster bobberArcCaster; // Reference to BobberArcCaster
    private bool hasProcessedMusicEnd = false; // Flag to track if music end has been processed

    void Start()
    {
        musicInstance = RuntimeManager.CreateInstance(musicEvent);
        musicInstance.start();

        // Find the BobberArcCaster in the active scene
        bobberArcCaster = FindObjectOfType<BobberArcCaster>();
        if (bobberArcCaster == null)
        {
            Debug.LogError("BobberArcCaster not found in the active scene!");
        }
    }

    void Update()
    {
        // Check if the music has stopped
        musicInstance.getPlaybackState(out PLAYBACK_STATE playbackState);
        if (playbackState == PLAYBACK_STATE.STOPPED && !hasProcessedMusicEnd)
        {
            // Toggle the tension state back to Landed
            if (bobberArcCaster != null && bobberArcCaster.CurrentState == BobberArcCaster.State.Tension)
            {
                bobberArcCaster.ToggleTension();
                hasProcessedMusicEnd = true; // Mark the music end as processed
            }
        }

        // Automatically restart music if the tension state is re-entered
        if (bobberArcCaster != null && bobberArcCaster.CurrentState == BobberArcCaster.State.Tension && hasProcessedMusicEnd)
        {
            RestartMusic();
        }
    }

    private void RestartMusic()
    {
        hasProcessedMusicEnd = false; // Reset the flag
        if (RhythmConductor.Instance != null)
        {
            RhythmConductor.Instance.ResetBeatmapForReplay();
        }
        musicInstance.setTimelinePosition(0);
        musicInstance.start(); // Restart the music
        Debug.Log("Music restarted.");
    }

    void OnDestroy()
    {
        musicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        musicInstance.release();
    }
}
