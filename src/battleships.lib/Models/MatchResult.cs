namespace battleships.lib.Models;

/// <summary>
/// Represents the result of a match.
/// </summary>
public record MatchResult
{
    /// <summary>
    /// Number of moves made in the match.
    /// </summary>
    public int MoveCount { get; init; }
    
    /// <summary>
    /// The identifier of the map.
    /// </summary>
    public int MapId { get; init; }
}