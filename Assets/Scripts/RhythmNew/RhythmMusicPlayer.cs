using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class RhythmMusicPlayer : MonoBehaviour
{
    [SerializeField] private EventReference musicEvent;

    public EventInstance musicInstance;
    private BobberArcCaster bobberArcCaster;
    private bool hasProcessedMusicEnd = false;
    private bool wasInTension = false;
    public bool inTension = false;


    void Awake()
    {
        RhythmConductor.rhythmMusicPlayer = this;
    }

    void Start()
    {
        if (musicEvent.IsNull)
        {
            Debug.LogError("RhythmMusicPlayer has no FMOD music event assigned.");
            return;
        }

        musicInstance = RuntimeManager.CreateInstance(musicEvent);
        ResolveBobberArcCaster();
    }

    void Update()
    {
        // 1. Declare the variable ONCE at the start of the loop
        PLAYBACK_STATE playbackState;
        musicInstance.getPlaybackState(out playbackState);

        // 2. Logic for Auto-Looping/Restarting
        if (playbackState == PLAYBACK_STATE.STOPPED && !hasProcessedMusicEnd)
        {
            hasProcessedMusicEnd = false; // Note: This was set to false in your original code; 
                                        // usually looping requires a reset.
            musicInstance.setTimelinePosition(0);
            musicInstance.start();
        }
        // Logic for stopping when tension breaks
        else if (!inTension && wasInTension)
        {
            hasProcessedMusicEnd = false;
            musicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        }

        // 3. Logic for ending the "Tension" state (Removed the second declaration)
        if (inTension && !hasProcessedMusicEnd)
        {
            // We already have playbackState from the top of Update!
            if (playbackState == PLAYBACK_STATE.STOPPED)
            {
                if (bobberArcCaster != null)
                    bobberArcCaster.ToggleTension();

                hasProcessedMusicEnd = true;
            }
        }

        wasInTension = inTension;
    }

    private void ResolveBobberArcCaster()
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
        if (RhythmConductor.rhythmMusicPlayer == this)
            RhythmConductor.rhythmMusicPlayer = null;

        if (!musicInstance.isValid())
            return;

        musicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        musicInstance.release();
    }
}
