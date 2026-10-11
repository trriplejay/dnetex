namespace SoftwareFactorySurvivors.Core;

public sealed record HighScore(string Initials, int Score);

/// <summary>
/// The best scores, highest first. Kept in memory only, so it lasts while the game is running.
/// </summary>
public sealed class HighScoreTable
{
    public const int Capacity = 10;

    private readonly List<HighScore> _entries = [];

    public IReadOnlyList<HighScore> Entries => _entries;

    /// <summary>
    /// Records a score and returns its 0-based rank, or null if it didn't make the table.
    /// Ties go below the scores already recorded.
    /// </summary>
    public int? Add(string initials, int score)
    {
        if (!InitialsEntry.IsValid(initials))
            throw new ArgumentException("Initials must be 1 to 3 letters A-Z.", nameof(initials));

        var rank = _entries.FindIndex(entry => entry.Score < score);
        if (rank < 0)
            rank = _entries.Count;
        if (rank >= Capacity)
            return null;

        _entries.Insert(rank, new HighScore(initials, score));
        if (_entries.Count > Capacity)
            _entries.RemoveAt(Capacity);
        return rank;
    }
}
