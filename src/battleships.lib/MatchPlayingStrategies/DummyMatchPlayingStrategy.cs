using battleships.lib.MatchPlayingStrategies.Models;
using battleships.lib.Models;

namespace battleships.lib.MatchPlayingStrategies;

/// <summary>
/// In this strategy, we just go through the grid from left to right, top to bottom.
/// </summary>
public class DummyMatchPlayingStrategy : MatchPlayingStrategyBase, IMatchPlayingStrategy
{
    /// <inheritdoc />
    public string Name => "Dummy Match Playing Strategy";

    /// <inheritdoc />
    public MatchNextTargetCell GetNextTarget(MatchState matchState)
    {
        if (matchState.MatchFinished)
        {
            throw new InvalidOperationException("Match is already finished.");
        }

        var firstUnknownGridCellIndex = Array.IndexOf(matchState.GameField.Cells, CellState.Unknown);

        if (firstUnknownGridCellIndex == -1)
        {
            throw new InvalidOperationException("Match is already finished.");
        }

        var position = matchState.GameField.GetCellPosition(firstUnknownGridCellIndex);

        return new MatchNextTargetCell(position);
    }
}