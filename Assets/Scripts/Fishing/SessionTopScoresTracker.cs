using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public static class SessionTopScoresTracker
{
    [System.Serializable]
    private sealed class SavedScoreEntry
    {
        public string name;
        public int score;
    }

    [System.Serializable]
    private sealed class SaveData
    {
        public List<SavedScoreEntry> entries = new List<SavedScoreEntry>();
    }

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
    private const string SaveFileName = "session_top_scores.json";

    private static readonly List<ScoreEntry> topScores = new List<ScoreEntry>(MaxTrackedScores);
    private static int pendingNameEntryIndex = -1;
    private static bool isInitialized;

    public static IReadOnlyList<ScoreEntry> TopScores
    {
        get
        {
            EnsureInitialized();
            return topScores;
        }
    }

    public static bool HasPendingNameEntry
    {
        get
        {
            EnsureInitialized();
            return pendingNameEntryIndex >= 0 && pendingNameEntryIndex < topScores.Count;
        }
    }

    public static ScoreEntry PendingNameEntry
    {
        get
        {
            EnsureInitialized();
            return HasPendingNameEntry ? topScores[pendingNameEntryIndex] : default;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        topScores.Clear();
        pendingNameEntryIndex = -1;
        isInitialized = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeOnLoad()
    {
        EnsureInitialized();
    }

    public static bool TryRecordScore(int score, out ScoreEntry entry)
    {
        EnsureInitialized();
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

        SaveScores();
        Debug.Log($"Session top 5 update: {score} is now in place #{entry.Rank}.");
        Debug.Log(BuildTopScoresLog());
        return true;
    }

    public static bool TrySubmitPendingName(string rawName)
    {
        EnsureInitialized();

        if (!HasPendingNameEntry)
            return false;

        string sanitizedName = SanitizeName(rawName);
        if (string.IsNullOrEmpty(sanitizedName))
            return false;

        ScoreEntry pendingEntry = topScores[pendingNameEntryIndex];
        topScores[pendingNameEntryIndex] = new ScoreEntry(sanitizedName, pendingEntry.Score, pendingEntry.Rank);
        pendingNameEntryIndex = -1;
        SaveScores();
        Debug.Log(BuildTopScoresLog());
        return true;
    }

    public static void ResetAllScores()
    {
        EnsureInitialized();

        topScores.Clear();
        pendingNameEntryIndex = -1;

        string path = GetSavePath();
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException ex)
        {
            Debug.LogWarning($"Failed to delete top score save file at {path}: {ex.Message}");
        }
        catch (System.UnauthorizedAccessException ex)
        {
            Debug.LogWarning($"Failed to delete top score save file at {path}: {ex.Message}");
        }

        Debug.Log("Top scores reset.");
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

    private static void EnsureInitialized()
    {
        if (isInitialized)
            return;

        isInitialized = true;
        LoadScores();
    }

    private static void LoadScores()
    {
        topScores.Clear();
        pendingNameEntryIndex = -1;

        string path = GetSavePath();
        if (!File.Exists(path))
            return;

        try
        {
            string json = File.ReadAllText(path);
            SaveData saveData = JsonUtility.FromJson<SaveData>(json);
            if (saveData?.entries == null)
                return;

            for (int i = 0; i < saveData.entries.Count && topScores.Count < MaxTrackedScores; i++)
            {
                SavedScoreEntry savedEntry = saveData.entries[i];
                if (savedEntry == null || savedEntry.score <= 0)
                    continue;

                topScores.Add(new ScoreEntry(
                    SanitizeLoadedName(savedEntry.name),
                    savedEntry.score,
                    topScores.Count + 1));
            }

            topScores.Sort((a, b) => b.Score.CompareTo(a.Score));
            RebuildRanks();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to load top scores from {path}: {ex.Message}");
            topScores.Clear();
            pendingNameEntryIndex = -1;
        }
    }

    private static void SaveScores()
    {
        SaveData saveData = new SaveData();
        for (int i = 0; i < topScores.Count; i++)
        {
            ScoreEntry entry = topScores[i];
            saveData.entries.Add(new SavedScoreEntry
            {
                name = entry.Name,
                score = entry.Score
            });
        }

        string path = GetSavePath();
        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, JsonUtility.ToJson(saveData, true));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to save top scores to {path}: {ex.Message}");
        }
    }

    private static string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    private static string SanitizeLoadedName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        if (rawName.Length <= 3)
            return SanitizeName(rawName);

        return SanitizeName(rawName.Substring(0, 3));
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
