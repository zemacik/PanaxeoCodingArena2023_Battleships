using battleships.lib.MatchPlayingStrategies.Models;
using battleships.lib.Models;

namespace battleships.lib.MatchPlayingStrategies;

/// <summary>
/// Represents a strategy for playing a match.
/// </summary>
public interface IMatchPlayingStrategy
{
    /// <summary>
    /// User-friendly name of the strategy.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the next target to shoot at.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    /// <returns>The next target to shoot at.</returns>
    MatchNextTargetCell GetNextTarget(MatchState matchState);
}