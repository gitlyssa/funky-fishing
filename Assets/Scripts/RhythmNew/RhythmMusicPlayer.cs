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
        if (!musicInstance.isValid())
            return;

        ResolveBobberArcCaster();
        bool inTension = bobberArcCaster != null && bobberArcCaster.CurrentState == BobberArcCaster.State.Tension;

        if (inTension && !wasInTension)
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
        if (bobberArcCaster == null)
            bobberArcCaster = FindObjectOfType<BobberArcCaster>();
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
