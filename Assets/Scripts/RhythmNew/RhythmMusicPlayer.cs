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
        // Check if the music has stopped
        musicInstance.getPlaybackState(out PLAYBACK_STATE playbackState);
        if (playbackState == PLAYBACK_STATE.STOPPED && !hasProcessedMusicEnd)
        {
            hasProcessedMusicEnd = false;
            musicInstance.setTimelinePosition(0);
            musicInstance.start();
        }
        else if (!inTension && wasInTension)
        {
            hasProcessedMusicEnd = false;
            musicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        }

        if (inTension && !hasProcessedMusicEnd)
        {
            musicInstance.getPlaybackState(out PLAYBACK_STATE playbackState);
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
