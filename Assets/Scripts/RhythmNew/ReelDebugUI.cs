using UnityEngine;
using UnityEngine.UI;

public class ReelDebugUI : MonoBehaviour
{

    /*
    This is a debug script to visualize the reel state since the actual rhythm wheel doesn't have visuals yet
    It is composed of a state square that shows the current rotation of the reel. This acts as the spinning reel for later
    The first progress bar shows the reel's active progress. When it fills up, the reel is complete
    The second progress bar shows the player's progress towards the goal. 
    If it is half filled up, the player has met the goal and the reel is considered cleared
    If it is fully filled up, the player has exceeded the goal and has obtained the maximum bonus points available
    */
    [Header("State Square")]
    public Image stateSquare;
    public float maxRotationSpeed = 360f; // Degrees per second

    [Header("Reel Progress Bar")]
    public Image reelProgressBar;
    public Image reelProgressFill;

    [Header("Player Progress Bar")]
    public Image playerProgressBar;
    public Image playerGoalFill;   // 0% to 100% (Goal)
    public Image playerBonusFill;  // 0% to 200% (Bonus)

    [Header("Colors")]
    public Color idleColor = Color.gray;
    public Color warmupColor = Color.white;
    public Color activeColor = Color.yellow;
    public Color fillColor = Color.green;

    private float _currentVisualRotation = 0f;
    private float _targetVisualSpeed = 0f;
    private float _currentVisualSpeed = 0f;
    private float _acceleration = 500f; // Degrees per second squared

    void Update()
    {
        var conductor = RhythmConductor.Instance;
        var reel = conductor.activeReel;

        if (reel == null)
        {
            _targetVisualSpeed = 0f;
            UpdateIdleState();
            return;
        } 
        else
        {
            float directionMult = (reel.Data.goalDegrees >= 0) ? 1f : -1f;

            if (reel.CurrentPhase == ReelPhase.LeadIn)
            {
                stateSquare.color = warmupColor;
                // Target accelerates from 0 to max over lead-in
                _targetVisualSpeed = Mathf.Lerp(0, maxRotationSpeed, reel.GetLeadInIntensity()) * directionMult;
            }
            else if (reel.CurrentPhase == ReelPhase.Active)
            {
                stateSquare.color = activeColor;
                reelProgressBar.color = warmupColor;
                playerProgressBar.color = warmupColor;
                _targetVisualSpeed = maxRotationSpeed * directionMult;
                UpdateProgressBars(reel, conductor.songTime);
            }
        }


        _currentVisualSpeed = Mathf.MoveTowards(_currentVisualSpeed, _targetVisualSpeed, _acceleration * Time.deltaTime);
        _currentVisualRotation += _currentVisualSpeed * Time.deltaTime;
        stateSquare.transform.localRotation = Quaternion.Euler(0, 0, _currentVisualRotation);

    }

    private void UpdateIdleState()
    {
        stateSquare.color = idleColor;
        reelProgressBar.color = idleColor;
        playerProgressBar.color = idleColor;
        reelProgressFill.fillAmount = 0;
        playerGoalFill.fillAmount = 0;
        playerBonusFill.fillAmount = 0;
    
    }

    private void UpdateProgressBars(RhythmReelNote reel, float songTime)
    {
        // Reel Time Progress
        float reelElapsed = songTime - reel.Data.startTime;
        float reelWindowT = Mathf.Clamp01(reelElapsed / reel.Data.duration);
        reelProgressFill.fillAmount = (reel.CurrentPhase == ReelPhase.Active) ? reelWindowT : 0f;

        // Player Spin Progress
        float playerProgress = reel.Progress; 
        playerGoalFill.fillAmount = Mathf.Clamp01(playerProgress);
        playerBonusFill.fillAmount = Mathf.Clamp01(playerProgress - 1.0f);
    }
}