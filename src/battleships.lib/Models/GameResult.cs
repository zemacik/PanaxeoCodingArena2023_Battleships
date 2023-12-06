namespace battleships.lib.Models;

/// <summary>
/// Represents the result of a game.
/// </summary>
public record GameResult
{
    /// <summary>
    /// The result of each match.
    /// </summary>
    public IList<MatchResult> MatchResults { get; init; } = new List<MatchResult>();

    /// <summary>
    /// Total number of moves made in all matches.
    /// </summary>
    public int TotalMoveCount { get; init; }
}