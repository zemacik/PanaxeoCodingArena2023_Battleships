using battleships.lib.MatchPlayingStrategies.Models;
using battleships.lib.Models;
using Microsoft.Extensions.Logging;

namespace battleships.lib.MatchPlayingStrategies;

/// <summary>
/// Simple match playing strategy.
/// In this strategy we are using a simple approach to find the next target cell. We have two modes: Searching and Targeting.
/// In searching mode we are not scanning cells one by one, but randomly picking one of the untouched cells using parity
/// fields (like on the chess board). If we hit a ship, we are switching to targeting mode.
/// In targeting mode we are using the last hit cell as a starting point and try to find the next cell by checking the
/// surrounding cells. If we hit a ship, we are adding the surrounding cells to a queue and try to hit them one by one.
/// </summary>
public class SimpleMatchPlayingStrategy : MatchPlayingStrategyBase, IMatchPlayingStrategy
{
    /// <summary>
    ///  The logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The random number generator.
    /// </summary>
    private static readonly Random Random = new();

    /// <summary>
    /// Represents the current play mode.
    /// </summary>
    private enum PlayMode
    {
        Searching,
        Targeting,
    }

    /// <summary>
    /// Existing ship sizes 
    /// </summary>
    private readonly List<int> _remainingShipSizes = new() { 2, 3, 3, 4, 5, 9 };

    // the last target position
    private MatchNextTargetCell? _lastTargetPosition;
    
    /// the queue of possible targets.
    private readonly Queue<GameCellPosition> _possibleTargets = new();
    
    // the current play mode
    private PlayMode _playMode = PlayMode.Searching;
    
    // the list of hit positions in the current targeting session
    private readonly List<GameCellPosition> _targetingSessionHitPositions = new();

    /// <summary>
    /// Creates a new instance of the <see cref="SimpleMatchPlayingStrategy"/> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    public SimpleMatchPlayingStrategy(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<SimpleMatchPlayingStrategy>();
    }

    /// <inheritdoc />
    public string Name => "Simple Match Playing Strategy";

    /// <inheritdoc />
    public MatchNextTargetCell GetNextTarget(MatchState matchState)
    {
        if (matchState.MatchFinished)
        {
            throw new InvalidOperationException("Match is already finished.");
        }

        // if we have no possible targets, and did not hit anything we need to switch to searching mode
        if (_playMode == PlayMode.Targeting && _possibleTargets.Count == 0 && !matchState.LastMoveWasHit)
        {
            if (_possibleTargets.Count == 0)
            {
                // do we destroyed a ship?
                var shipLength = _targetingSessionHitPositions.Count;
                _remainingShipSizes.Remove(shipLength);

                _logger.LogTrace($"Destroyed ship with length {shipLength}.");
                _logger.LogTrace($"Remaining ship sizes: {string.Join(", ", _remainingShipSizes)}");
            }

            SwitchToSearchingMode();
        }

        if (matchState.LastMoveWasHit)
        {
            if (_playMode != PlayMode.Targeting)
                SwitchToTargetingMode();
        }

        var nextTargetCell = _playMode switch
        {
            PlayMode.Searching => GetNextCellBySearching(matchState),
            PlayMode.Targeting => GetNextCellByTargeting(matchState),
            _ => null
        };

        if (nextTargetCell == null)
        {
            throw new InvalidOperationException("No possible target cell found.");
        }

        _lastTargetPosition = nextTargetCell;

        return _lastTargetPosition;
    }
    
    /// <summary>
    /// Gets the next target cell by searching => meaning the cell with the highest probability.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    /// <returns>The next target cell.</returns>
    private MatchNextTargetCell GetNextCellBySearching(MatchState matchState)
    {
        if (_playMode != PlayMode.Searching)
        {
            throw new InvalidOperationException("Invalid play mode.");
        }

        // if we have a suggestion, use it
        if (_possibleTargets.Count > 0)
        {
            var nextTarget = _possibleTargets.Dequeue();
            return new MatchNextTargetCell(nextTarget);
        }

        var getUntouchedCells = matchState.GameField.Cells
            .Select((cellValue, index) =>
                new { cellValue, index, position = matchState.GameField.GetCellPosition(index) })

            // is unknown
            .Where(x => x.cellValue == CellState.Unknown)

            // using parity fields
            .Where(x =>
                (x.position.Row % 2 == 1 && x.position.Column % 2 == 1)
                || (x.position.Row % 2 == 0 && x.position.Column % 2 == 0))

            // has not surrounding hit cells
            .Where(x =>
            {
                var surroundingCells = matchState.GameField.GetAllCrossAdjacentCellPositions(x.position)
                    .Where(cp => matchState.GameField.GetCellStatus(cp) == CellState.Ship);

                return !surroundingCells.Any();
            })
            .ToList();

        var randomCell = getUntouchedCells[Random.Next(getUntouchedCells.Count)];
        var randomCellPosition = matchState.GameField.GetCellPosition(randomCell.index);

        _logger.LogDebug($"Mode: {_playMode} Random cell position: {randomCellPosition}");

        return new MatchNextTargetCell(randomCellPosition);
    }

    /// <summary>
    /// Gets the next target cell by targeting in the current targeting session.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    /// <returns>The next target cell.</returns>
    private MatchNextTargetCell GetNextCellByTargeting(MatchState matchState)
    {
        if (_playMode != PlayMode.Targeting)
        {
            throw new InvalidOperationException("Invalid play mode.");
        }

        if (_lastTargetPosition == null)
        {
            throw new InvalidOperationException("Last target position is null.");
        }

        if (matchState.LastMoveWasHit)
        {
            _targetingSessionHitPositions.Add(_lastTargetPosition.Position);
        }
        else
        {
            if (_possibleTargets.Count == 0)
            {
                // do we destroyed a ship?
                var shipLength = _targetingSessionHitPositions.Count;
                _remainingShipSizes.Remove(shipLength);

                _logger.LogTrace($"Destroyed ship with length {shipLength}.");
            }
        }

        if (_possibleTargets.Count > 0 && !matchState.LastMoveWasHit)
        {
            return new MatchNextTargetCell(_possibleTargets.Dequeue());
        }

        // determine surrounding cells
        var surroundingCells = matchState.GameField.GetAllCrossAdjacentCellPositions(_lastTargetPosition.Position)
            .Where(cp => matchState.GameField.GetCellStatus(cp) == CellState.Unknown);

        foreach (var surroundingCell in surroundingCells)
        {
            // check if we already have this cell in our queue
            if (_possibleTargets.Contains(surroundingCell))
            {
                continue;
            }

            _possibleTargets.Enqueue(surroundingCell);
        }

        if (_possibleTargets.Count == 0)
        {
            SwitchToSearchingMode();

            return GetNextCellBySearching(matchState);
        }

        return new MatchNextTargetCell(_possibleTargets.Dequeue());
    }

    /// <summary>
    /// Switches the play mode to searching mode.
    /// </summary>
    private void SwitchToTargetingMode()
    {
        if (_playMode == PlayMode.Targeting)
        {
            return;
        }

        _logger.LogTrace("Switching to targeting mode.");

        _playMode = PlayMode.Targeting;
        _targetingSessionHitPositions.Clear();
    }

    /// <summary>
    /// Switches the play mode to searching mode.
    /// </summary>
    private void SwitchToSearchingMode()
    {
        if (_playMode == PlayMode.Searching)
        {
            return;
        }

        _logger.LogTrace("Switching to searching mode.");
        _playMode = PlayMode.Searching;
    }
}