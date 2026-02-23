using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    public int DisplayMode = 0; // 0 = Player, 1 = Dev, 2 = InDepth
    public DetailedStats stats = new DetailedStats();

    [Header("UI References")]
    public TextMeshProUGUI mainDisplay; // The big text
    public TextMeshProUGUI subDisplay;  // The smaller detail text

    void Awake() => Instance = this;
    
    public void RecordHit(RhythmJudge.JudgeRating rating, float timingDelta)
    {
        stats.timingOffsets.Add(timingDelta);
        UpdateAverageOffset();

        switch (rating)
        {
            case RhythmJudge.JudgeRating.Perfect:
                stats.perfects++;
                break;
            case RhythmJudge.JudgeRating.Good:
                if (timingDelta < 0) stats.earlyGoods++;
                else stats.lateGoods++;
                break;
        }
        RefreshUI();
    }

    public void RecordMiss(bool wasInputProvided, float timingDelta = 0)
    {
        if (!wasInputProvided) 
            stats.completeMisses++;
        else if (timingDelta < 0) 
            stats.earlyMisses++;
        else 
            stats.lateMisses++;

        RefreshUI();
    }

    private void UpdateAverageOffset()
    {
        float sum = 0;
        foreach (float f in stats.timingOffsets) sum += f;
        stats.averageOffset = sum / stats.timingOffsets.Count;
    }

    public void RefreshUI()
    {
        switch (DisplayMode)
        {
            case 0: // Player Mode
                mainDisplay.text = $"Score: {CalculateScore()}\nCombo: {GetCurrentCombo()}";
                subDisplay.text = "";
                break;

            case 1: // Dev Mode
                mainDisplay.text = $"P: {stats.perfects} | G: {stats.TotalGoods} | M: {stats.TotalMisses}";
                subDisplay.text = $"Reels: {stats.reelsCleared}/{stats.reelsCleared + stats.reelsFailed}";
                break;

            case 2: // InDepth Mode
                mainDisplay.text = $"Avg Offset: {stats.averageOffset:F4}s";
                subDisplay.text = $"E-Miss: {stats.earlyMisses} | L-Miss: {stats.lateMisses} | C-Miss: {stats.completeMisses}\n" +
                                  $"E-Good: {stats.earlyGoods} | L-Good: {stats.lateGoods}";
                break;
        }
    }

    private int CalculateScore() {  return 0; }
    private int GetCurrentCombo() {  return 0; }
    
}
