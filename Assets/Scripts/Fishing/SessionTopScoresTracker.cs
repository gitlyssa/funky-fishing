using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class SessionTopScoresTracker
{
    public readonly struct ScoreEntry
    {
        public ScoreEntry(string name, int score, int rank)
        {
            Name = name;
            Score = score;
            Rank = rank;
        }

        public string Name { get; }
        public int Score { get; }
        public int Rank { get; }
    }

    public const int MaxTrackedScores = 5;

    private static readonly List<ScoreEntry> topScores = new List<ScoreEntry>(MaxTrackedScores);
    private static int pendingNameEntryIndex = -1;

    public static IReadOnlyList<ScoreEntry> TopScores => topScores;
    public static bool HasPendingNameEntry => pendingNameEntryIndex >= 0 && pendingNameEntryIndex < topScores.Count;
    public static ScoreEntry PendingNameEntry => HasPendingNameEntry ? topScores[pendingNameEntryIndex] : default;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetScores()
    {
        topScores.Clear();
        pendingNameEntryIndex = -1;
    }

    public static bool TryRecordScore(int score, out ScoreEntry entry)
    {
        entry = default;

        if (score <= 0)
            return false;

        bool isNewHighScore = topScores.Count == 0 || score > topScores[0].Score;
        int insertIndex = topScores.FindIndex(existingEntry => score >= existingEntry.Score);
        int recordedRank;

        if (insertIndex >= 0)
        {
            while (insertIndex < topScores.Count && topScores[insertIndex].Score == score)
                insertIndex++;

            topScores.Insert(insertIndex, new ScoreEntry(string.Empty, score, 0));
            recordedRank = insertIndex + 1;
        }
        else if (topScores.Count < MaxTrackedScores)
        {
            topScores.Add(new ScoreEntry(string.Empty, score, 0));
            recordedRank = topScores.Count;
        }
        else
        {
            return false;
        }

        if (topScores.Count > MaxTrackedScores)
            topScores.RemoveAt(MaxTrackedScores);

        pendingNameEntryIndex = recordedRank - 1;
        RebuildRanks();
        entry = topScores[pendingNameEntryIndex];

        if (isNewHighScore)
            Debug.Log($"New session high score: {score}");

        Debug.Log($"Session top 5 update: {score} is now in place #{entry.Rank}.");
        Debug.Log(BuildTopScoresLog());
        return true;
    }

    public static bool TrySubmitPendingName(string rawName)
    {
        if (!HasPendingNameEntry)
            return false;

        string sanitizedName = SanitizeName(rawName);
        if (string.IsNullOrEmpty(sanitizedName))
            return false;

        ScoreEntry pendingEntry = topScores[pendingNameEntryIndex];
        topScores[pendingNameEntryIndex] = new ScoreEntry(sanitizedName, pendingEntry.Score, pendingEntry.Rank);
        pendingNameEntryIndex = -1;
        Debug.Log(BuildTopScoresLog());
        return true;
    }

    public static string SanitizeName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        StringBuilder builder = new StringBuilder(3);
        for (int i = 0; i < rawName.Length && builder.Length < 3; i++)
        {
            char c = char.ToUpperInvariant(rawName[i]);
            if (c >= 'A' && c <= 'Z')
                builder.Append(c);
        }

        return builder.ToString();
    }

    private static void RebuildRanks()
    {
        for (int i = 0; i < topScores.Count; i++)
        {
            ScoreEntry entry = topScores[i];
            topScores[i] = new ScoreEntry(entry.Name, entry.Score, i + 1);
        }
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
        {
            ScoreEntry entry = topScores[i];
            string name = string.IsNullOrEmpty(entry.Name) ? "---" : entry.Name;
            builder.Append($" #{i + 1}: {name} {entry.Score}");
        }

        return builder.ToString();
    }
}
