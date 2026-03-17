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
    private bool sawPlaybackActiveInCurrentTension = false;
    private bool isPausedForGame = false;
    private bool tutorialLoopMode = false;
    private bool tutorialPlaybackSuppressed = false;

    void Awake()
    {
        RhythmConductor.rhythmMusicPlayer = this;
    }

    void OnEnable()
    {
        ResolveBobberArcCaster();
        wasInTension = false;
        hasProcessedMusicEnd = false;
        sawPlaybackActiveInCurrentTension = false;
    }

    void Start()
    {
        RecreateMusicInstance();
        ResolveBobberArcCaster();
    }

    void Update()
    {
        if (!musicInstance.isValid())
            return;

        FunkyAudioSettings.ApplyCategoryVolume(musicInstance, FunkyAudioCategory.Music);

        if (isPausedForGame)
            return;

        if (bobberArcCaster == null)
            ResolveBobberArcCaster();

        bool inTension = bobberArcCaster != null &&
                         bobberArcCaster.CurrentState == BobberArcCaster.State.Tension;

        if (tutorialPlaybackSuppressed)
        {
            StopRhythmPlayback();
            wasInTension = inTension;
            return;
        }

        if (inTension && !wasInTension)
        {
            sawPlaybackActiveInCurrentTension = false;
            StartRhythmPlayback();
        }
        // Logic for stopping when tension breaks
        else if (!inTension && wasInTension)
        {
            hasProcessedMusicEnd = false;
            sawPlaybackActiveInCurrentTension = false;
            tutorialLoopMode = false;
            StopRhythmPlayback();
        }

        // 3. Logic for ending the "Tension" state (Removed the second declaration)
        if (inTension && !hasProcessedMusicEnd)
        {
            musicInstance.getPlaybackState(out PLAYBACK_STATE playbackState);
            bool playbackActive =
                playbackState == PLAYBACK_STATE.PLAYING ||
                playbackState == PLAYBACK_STATE.STARTING ||
                playbackState == PLAYBACK_STATE.SUSTAINING;

            if (playbackActive)
            {
                wasInTension = bobberArcCaster != null &&
                               bobberArcCaster.CurrentState == BobberArcCaster.State.Tension;
                sawPlaybackActiveInCurrentTension = true;
                return;
            }
            
            // If tension is active but playback has not started yet, retry a fresh start.
            if (!sawPlaybackActiveInCurrentTension && playbackState == PLAYBACK_STATE.STOPPED)
            {
                StartRhythmPlayback();
                wasInTension = bobberArcCaster != null &&
                               bobberArcCaster.CurrentState == BobberArcCaster.State.Tension;
                return;
            }    

            // if playback has happened, tension is active, but not currently playing, the song is over
            // spawn the final reel note and enter overtime
            if (!RhythmConductor.Instance.isOvertime &&
                sawPlaybackActiveInCurrentTension &&
                playbackState == PLAYBACK_STATE.STOPPED &&
                !RhythmConductor.Instance.AreTutorialReelsSuppressed())
            {
                // // If there's no active reel and no notes left, spawn the final one
                // if (RhythmConductor.Instance.activeReel == null && RhythmConductor.Instance.activeNotes.Count == 0)
                // {
                //     RhythmConductor.Instance.StartOvertime();
                //     RhythmConductor.Instance.SpawnFinalPlaytestReel();
                    
                //     // sawPlaybackActiveInCurrentTension = false; 
                //     return;
                // }
            }

            if (RhythmConductor.Instance.activeReel != null)
            {
                // Still updating wasInTension so the tension-break logic doesn't fire
                wasInTension = bobberArcCaster != null && bobberArcCaster.CurrentState == BobberArcCaster.State.Tension;
                return; 
            }

            if (tutorialLoopMode && playbackState == PLAYBACK_STATE.STOPPED)
            {
                StartRhythmPlayback();
                wasInTension = bobberArcCaster != null &&
                               bobberArcCaster.CurrentState == BobberArcCaster.State.Tension;
                return;
            }

            // Only resolve encounter when FMOD reports the event actually stopped after being active.
            if (playbackState != PLAYBACK_STATE.STOPPED)
            {
                wasInTension = bobberArcCaster != null &&
                               bobberArcCaster.CurrentState == BobberArcCaster.State.Tension;
                return;
            }

            hasProcessedMusicEnd = true;
            StopRhythmPlayback();
            Debug.Log("Music ended, resolving tension encounter.");

            if (bobberArcCaster != null)
                bobberArcCaster.CompleteRhythmEncounter();
            else if (SceneLoading.Instance != null)
                SceneLoading.Instance.EndRhythmEncounter();
        }

        wasInTension = bobberArcCaster != null &&
                       bobberArcCaster.CurrentState == BobberArcCaster.State.Tension;
    }

    private void StartRhythmPlayback()
    {
        hasProcessedMusicEnd = false;

        // Keep tutorial practice notes alive across music loops.
        if (RhythmConductor.Instance != null && !tutorialLoopMode)
            RhythmConductor.Instance.ResetBeatmapForReplay();

        StopRhythmPlayback();
        musicInstance.setTimelinePosition(0);
        musicInstance.start();
            if (RhythmBeatPulse.Instance != null)
            RhythmBeatPulse.Instance.ResetTimer();

        musicInstance.start();
    }

    private void StopRhythmPlayback()
    {
        if (!musicInstance.isValid())
            return;

        musicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        musicInstance.setTimelinePosition(0);
    }

    public void PauseRhythmForGamePause()
    {
        if (!musicInstance.isValid())
            return;

        isPausedForGame = true;
        musicInstance.setPaused(true);
    }

    public void ResumeRhythmFromGamePause()
    {
        if (!musicInstance.isValid())
            return;

        musicInstance.setPaused(false);
        isPausedForGame = false;
    }

    public void ForceStopPlaybackAndBeatmap()
    {
        hasProcessedMusicEnd = false;
        wasInTension = false;
        sawPlaybackActiveInCurrentTension = false;
        isPausedForGame = false;
        tutorialLoopMode = false;
        StopRhythmPlayback();

        if (RhythmConductor.Instance != null)
            RhythmConductor.Instance.ResetBeatmapForReplay();
    }

    public void SetMusicEvent(EventReference newMusicEvent)
    {
        musicEvent = newMusicEvent;
        RecreateMusicInstance();
    }

    private void ResolveBobberArcCaster()
    {
        if (bobberArcCaster == null)
            bobberArcCaster = FindObjectOfType<BobberArcCaster>();
    }

    private void RecreateMusicInstance()
    {
        if (musicInstance.isValid())
        {
            musicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            musicInstance.release();
        }

        if (musicEvent.IsNull)
        {
            Debug.LogError("RhythmMusicPlayer has no FMOD music event assigned.");
            return;
        }

        musicInstance = RuntimeManager.CreateInstance(musicEvent);
        FunkyAudioSettings.ApplyCategoryVolume(musicInstance, FunkyAudioCategory.Music);
    }
    
    public void SetTutorialLoopMode(bool enabled)
    {
        tutorialLoopMode = enabled;
    }

    public void SetTutorialPlaybackSuppressed(bool suppressed)
    {
        tutorialPlaybackSuppressed = suppressed;

        if (suppressed)
            StopRhythmPlayback();
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
