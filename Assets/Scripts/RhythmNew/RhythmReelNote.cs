using UnityEngine;

public enum ReelPhase { None, LeadIn, Active, Resolved }

public class RhythmReelNote : MonoBehaviour
{
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
        //trigger effects here 
        // RhythmWheel.Instance.PlayRotationPulse(); 
        Debug.Log("Full Rotation Completed!");
    }

    public void OnClear()
    {
        _isResolved = true;
       
        Destroy(gameObject);
    }

    public void OnFail()
    {
        _isResolved = true;
        
        Destroy(gameObject);
    }
    
    public float GetLeadInIntensity()
    {
        if (CurrentPhase != ReelPhase.LeadIn) return 0f;
        return (RhythmConductor.Instance.songTime - (_data.startTime - _data.leadInTime)) / _data.leadInTime;
    }
}