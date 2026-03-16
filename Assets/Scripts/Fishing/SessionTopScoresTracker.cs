using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class SessionTopScoresTracker
{
    public readonly struct ScoreEntry
    {
        public ScoreEntry(int score, int rank)
        {
            Score = score;
            Rank = rank;
        }

        public int Score { get; }
        public int Rank { get; }
    }

    public const int MaxTrackedScores = 5;

    private static readonly List<int> topScores = new List<int>(MaxTrackedScores);

    public static IReadOnlyList<int> TopScores => topScores;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetScores()
    {
        topScores.Clear();
    }

    public static bool TryRecordScore(int score, out ScoreEntry entry)
    {
        entry = default;

        if (score <= 0)
            return false;

        bool isNewHighScore = topScores.Count == 0 || score > topScores[0];
        int insertIndex = topScores.FindIndex(existingScore => score >= existingScore);

        int recordedRank;

        if (insertIndex >= 0)
        {
            while (insertIndex < topScores.Count && topScores[insertIndex] == score)
                insertIndex++;

            topScores.Insert(insertIndex, score);
            recordedRank = insertIndex + 1;
        }
        else if (topScores.Count < MaxTrackedScores)
        {
            topScores.Add(score);
            recordedRank = topScores.Count;
        }
        else
        {
            return false;
        }

        if (topScores.Count > MaxTrackedScores)
            topScores.RemoveAt(MaxTrackedScores);

        entry = new ScoreEntry(score, recordedRank);

        if (isNewHighScore)
            Debug.Log($"New session high score: {score}");

        Debug.Log($"Session top 5 update: {score} is now in place #{entry.Rank}.");
        Debug.Log(BuildTopScoresLog());
        return true;
    }

    private static string BuildTopScoresLog()
    {
        StringBuilder builder = new StringBuilder("Top 5 session scores:");

        if (topScores.Count == 0)
        {
            builder.Append(" none");
            return builder.ToString();
        }

        for (int i = 0; i < topScores.Count; i++)
            builder.Append($" #{i + 1}: {topScores[i]}");

        return builder.ToString();
    }
}
