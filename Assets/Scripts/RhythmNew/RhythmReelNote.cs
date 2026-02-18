using UnityEngine;

public enum ReelPhase { None, LeadIn, Active, Resolved }

public class RhythmReelNote : MonoBehaviour
{
    /*
    The reel note is a continuous note that has essentially 2 stages
    The lead in is when the big visual wheel starts spinning and it indicates to the player that they have an upcoming reel note
    the active portion is when the player can actually input. The player must meet the goal number of rotations within the duration
    of the note.

    Similar to the arc notes, this class mostly contains the logic for the visual side of the reel note
    The conductor uses it to update its own active reel state, which the judger checks to determine if the player
    input matches what the reel note is looking for.
    */

    [SerializeField] private ReelData _data;
    [SerializeField] private bool _isInitialized = false;
    [SerializeField] private float _accumulatedSpin = 0f;
    [SerializeField] private float _lastRotationCheckpoint = 0f;
    [SerializeField] private bool _isResolved = false;

    // State for Visuals to read
    public ReelPhase CurrentPhase { get; private set; } = ReelPhase.None;
    public ReelData Data => _data;

    public bool isClockwise => _data.isClockwise;

    public float Progress => Mathf.Abs(_accumulatedSpin / _data.goalDegrees);
    public float TotalSpin => _accumulatedSpin;

    public void Initialize(ReelData data)
    {
        _data = data;
        _isInitialized = true;
    }
   
    void Update()
    {
        if (!_isInitialized || _isResolved) return;

        float songTime = RhythmConductor.Instance.songTime;

        // Lead-In
        if (songTime >= _data.startTime - _data.leadInTime && songTime < _data.startTime)
        {
            CurrentPhase = ReelPhase.LeadIn;
        }
        // Active
        else if (songTime >= _data.startTime && songTime < _data.startTime + _data.duration)
        {
            CurrentPhase = ReelPhase.Active;
        }
        //Expired
        else if (songTime >= _data.startTime + _data.duration)
        {
            CurrentPhase = ReelPhase.Resolved;
        }
    }

    public void AddSpin(float deltaDegrees)
    {
        
        bool isCorrectDirection = (_data.goalDegrees > 0) ? (deltaDegrees > 0) : (deltaDegrees < 0);

        if (isCorrectDirection)
        {
            _accumulatedSpin += Mathf.Abs(deltaDegrees);

            if (_accumulatedSpin >= _lastRotationCheckpoint + 360f)
            {
                _lastRotationCheckpoint += 360f;
                OnRotationComplete();
            }
        }
    }

    private void OnRotationComplete()
    {
        // we can have some effects for every rotation the player completes 
        Debug.Log("Full Rotation Completed!");
    }

    public void OnClear()
    {
        // on clear effects
        _isResolved = true;
        Destroy(gameObject);
    }

    public void OnFail()
    {        // on fail effects
        _isResolved = true;
        Destroy(gameObject);
    }
    
    public float GetLeadInIntensity()
    {
        if (CurrentPhase != ReelPhase.LeadIn) return 0f;
        return (RhythmConductor.Instance.songTime - (_data.startTime - _data.leadInTime)) / _data.leadInTime;
    }
}