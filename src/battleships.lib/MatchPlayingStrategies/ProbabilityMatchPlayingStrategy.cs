using battleships.lib.MatchPlayingStrategies.Models;
using battleships.lib.Models;
using Microsoft.Extensions.Logging;

namespace battleships.lib.MatchPlayingStrategies;

/// <summary>
/// Probability match playing strategy.
/// In this strategy we are using a probability grid to find the next target cell. We have two modes: Searching and Targeting.
/// After each shot we are updating the probability grid. In searching mode we pick the cell with the highest probability.
/// In targeting mode we are using the last hit cell as a starting point and try to find the next cell by checking the
/// surrounding cells. If we hit a ship, we are adding the surrounding cells to a queue and try to hit them one by one.
/// </summary>
public class ProbabilityMatchPlayingStrategy : MatchPlayingStrategyBase, IMatchPlayingStrategy
{
    /// <summary>
    /// Callback for when the probability grid is updated.
    /// </summary>
    private Action<ProbabilitiesUpdatedArgs>? OnProbabilityGridUpdated { get; }

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The current play mode.
    /// </summary>
    private enum PlayMode
    {
        Searching,
        Targeting,
    }

    // remaining ship sizes 
    private readonly List<int> _remainingShipSizes = new() { 2, 3, 3, 4, 5, 9 };

    // the last target position
    private MatchNextTargetCell? _lastTarget;

    // the current play mode
    private PlayMode _playMode = PlayMode.Searching;

    // the queue of possible targets.
    private readonly Queue<GameCellPosition> _possibleTargets = new();

    // the list of hit positions in the current targeting session
    private readonly HashSet<GameCellPosition> _targetingSessionHitPositions = new();

    // the list of sunk ship positions
    private readonly HashSet<GameCellPosition> _sunkShipPositions = new();

    // The calculated field probability grid.
    private readonly GameField<int> _probabilitiesGrid;

    /// <summary>
    /// Initialises a new instance of the <see cref="ProbabilityMatchPlayingStrategy"/> class.
    /// </summary>
    /// <param name="loggerFactory"></param>
    /// <param name="onProbabilityGridUpdated"></param>
    public ProbabilityMatchPlayingStrategy(
        ILoggerFactory loggerFactory,
        Action<ProbabilitiesUpdatedArgs>? onProbabilityGridUpdated = null)
    {
        _logger = loggerFactory.CreateLogger<ProbabilityMatchPlayingStrategy>();
        _probabilitiesGrid = new GameField<int>(Constants.GridRows, Constants.GridColumns);
        OnProbabilityGridUpdated = onProbabilityGridUpdated;
    }

    /// <inheritdoc />
    public string Name => "Probability Match Playing Strategy";

    /// <inheritdoc />
    public MatchNextTargetCell GetNextTarget(MatchState matchState)
    {
        HandlePreviousShootingResult(matchState);
        AssertMatchNotFinished(matchState);
        
        UpdateProbabilityGrid(matchState);

        if (ShouldSwitchToTargeting(matchState))
        {
            SwitchToTargetingMode(matchState);
        }
        else if (ShouldSwitchToSearching())
        {
            SwitchToSearchingMode();
        }

        var nextTarget = _playMode == PlayMode.Targeting
            ? GetNextCellByTargeting(matchState)
            : GetNextCellBySearching(matchState);

        _lastTarget = nextTarget;

        return nextTarget;
    }

    /// <summary>
    /// Handles the result of the previous shot. Updates the targeting session hit positions and the
    /// sunk ship positions. Also updates the game field with revealed cells.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    private void HandlePreviousShootingResult(MatchState matchState)
    {
        if (_lastTarget != null && matchState.LastMoveWasHit)
        {
            _targetingSessionHitPositions.Add(_lastTarget.Position);
        }

        // Handle the result of the last shot by Avenger
        if (FiredWithAvenger())
        {
            UpdateAfterAvengerShot(matchState);
        }

        // we have some floating hits, can occur after Avenger's power
        if (_targetingSessionHitPositions.Count == 0)
        {
            var firstLonelyHit = GetLonelyHits(matchState).FirstOrDefault();
            if (firstLonelyHit != null)
            {
                _targetingSessionHitPositions.Add(firstLonelyHit);
            }
        }

        // we have ongoing session, but need to scan for adjacent hits not connected to the session, can occur after Avenger's power
        if (_targetingSessionHitPositions.Count > 0)
        {
            void AddAdjacentHits(GameCellPosition position)
            {
                var all2AdjacentHits = matchState.GameField.GetAllCrossAdjacentCellPositions(position)
                    .Where(pos => matchState.GameField.GetCellStatus(pos) == CellState.Ship
                                  && !_targetingSessionHitPositions.Contains(pos)).ToArray();

                foreach (var ah in all2AdjacentHits)
                {
                    _targetingSessionHitPositions.Add(ah);
                    AddAdjacentHits(ah); // Recursive
                }
            }

            foreach (var ah in _targetingSessionHitPositions.ToArray())
            {
                AddAdjacentHits(ah);
            }
        }

        // detect if the ship was sunk
        DetectAndProcessSunkenShip(matchState);
    }

    /// <summary>
    /// Checks if the last shot was fired with an Avenger power. 
    /// </summary>
    private bool FiredWithAvenger() => _lastTarget is { FireWithAvenger: true };

    /// <summary>
    /// Updates the game fields with the revealed cells after an Avenger shot.
    /// </summary>
    /// <param name="matchState"></param>
    private void UpdateAfterAvengerShot(MatchState matchState)
    {
        if (_lastTarget == null)
            throw new InvalidOperationException("Last target should not be null.");

        _logger.LogTrace("Updating after Avenger shot.");

        // Ironman's ability is to reveal the smallest remaining ship
        if (_lastTarget.Avenger == Avenger.Ironman)
        {
            foreach (var avengerFireResponse in matchState.AvengerFireResult)
            {
                var suggestedPosition =
                    new GameCellPosition(avengerFireResponse.Key.Row, avengerFireResponse.Key.Column);

                _possibleTargets.Enqueue(suggestedPosition);
            }
        }

        // Hulk's ability is to destroy the whole ship if you fire on a part of it
        else if (_lastTarget.Avenger == Avenger.Hulk)
        {
            if (!matchState.LastMoveWasHit)
                return;

            foreach (var avengerFireResponse in matchState.AvengerFireResult)
            {
                var suggestedPosition =
                    new GameCellPosition(avengerFireResponse.Key.Row, avengerFireResponse.Key.Column);

                _targetingSessionHitPositions.Add(suggestedPosition);
            }
        }

        // Thor's ability is to reveal 11 spaces at once with his lightning.
        // The one you’re firing at + 10 random spaces. All in a single turn.
        else if (_lastTarget.Avenger == Avenger.Thor)
        {
            // The game field is already updated with the revealed cells
        }
    }

    /// <summary>
    /// Asserts that the match is not finished.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    private void AssertMatchNotFinished(MatchState matchState)
    {
        if (matchState.MatchFinished)
        {
            throw new InvalidOperationException("Match is already finished.");
        }
    }

    /// <summary>
    /// Checks if the play mode should be switched to searching mode.
    /// </summary>
    /// <returns>True if the play mode should be switched to searching mode, false otherwise.</returns>
    private bool ShouldSwitchToSearching()
    {
        if (_playMode == PlayMode.Searching)
            return false;

        // The targeting session is over
        return _targetingSessionHitPositions.Count == 0;
    }

    /// <summary>
    /// Searches for a ship in the targeting session and removes it from the list of remaining ships.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    private void DetectAndProcessSunkenShip(MatchState matchState)
    {
        if (_targetingSessionHitPositions.Count > 1)
        {
            var allAdjacentKnown = _targetingSessionHitPositions.All(pos =>
                AreAllAdjacentCellsKnown(pos, matchState));

            // is we get information that avenger is available, we can assume that the ship is sunk
            const int theAvengersHelicarrierCells = 9;
            var isAvengersHelicarrierSunk = matchState.AvengerAvailable &&
                                            _targetingSessionHitPositions.Count == theAvengersHelicarrierCells;

            const int theCarrierCells = 5;
            var isCarrierSunk = GetNextTargetInLine(matchState) == null
                                && _targetingSessionHitPositions.Count == theCarrierCells
                                && !_remainingShipSizes.Contains(theAvengersHelicarrierCells);

            if (isAvengersHelicarrierSunk ||
                isCarrierSunk ||
                allAdjacentKnown)
            {
                RemoveSunkShipFromList();
                MarkAllAroundAdjacentCellsAsWater(matchState);
                StoreSunkShipPositions();

                _targetingSessionHitPositions.Clear(); // Clear the targeting session as you might have sunk the ship
            }
        }
    }

    /// <summary>
    /// Removes the sunk ship from the list of remaining ships.
    /// </summary>
    private void RemoveSunkShipFromList()
    {
        _remainingShipSizes.Remove(_targetingSessionHitPositions.Count);
    }

    /// <summary>
    /// Saves the positions of the sunk ship.
    /// </summary>
    private void StoreSunkShipPositions()
    {
        foreach (var pos in _targetingSessionHitPositions)
        {
            _sunkShipPositions.Add(pos);
        }
    }

    /// <summary>
    /// Marks all surrounding cells of the sunk ship as water.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    private void MarkAllAroundAdjacentCellsAsWater(MatchState matchState)
    {
        foreach (var pos in _targetingSessionHitPositions)
        foreach (var apos in matchState.GameField.GetAllAroundAdjacentCellPositions(pos))
        {
            var cs = matchState.GameField.GetCellStatus(apos);
            if (cs == CellState.Unknown)
            {
                matchState.GameField.SetCellStatus(apos, CellState.Water);
            }
        }
    }

    /// <summary>
    /// Checks if the play mode should be switched to targeting mode.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    /// <returns>True if the play mode should be switched to targeting mode, false otherwise.</returns>
    private bool ShouldSwitchToTargeting(MatchState matchState)
    {
        // Switch to targeting mode if the last move was a hit and not already in targeting mode
        return _playMode != PlayMode.Targeting
               && (matchState.LastMoveWasHit
                   || _targetingSessionHitPositions.Count > 0
                   || (_targetingSessionHitPositions.Count == 0 && ExistsLonelyHit(matchState)));
    }

    /// <summary>
    /// Switches the play mode to searching mode.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    private void SwitchToTargetingMode(MatchState matchState)
    {
        if (!ShouldSwitchToTargeting(matchState))
            throw new Exception("Targeting mode not applicable.");

        _logger.LogTrace("Switching to targeting mode.");

        _playMode = PlayMode.Targeting;
    }

    /// <summary>
    /// Switches the play mode to searching mode.
    /// </summary>
    private void SwitchToSearchingMode()
    {
        _logger.LogTrace("Switching to searching mode.");

        _playMode = PlayMode.Searching;
    }

    /// <summary>
    /// Check if all adjacent cells to the given position are known (not unknown)
    /// </summary>
    private bool AreAllAdjacentCellsKnown(GameCellPosition position, MatchState matchState) =>
        matchState.GameField.GetAllCrossAdjacentCellPositions(position)
            .All(pos => matchState.GameField.GetCellStatus(pos) != CellState.Unknown);

    /// <summary>
    /// Gets the next target cell by searching => meaning the cell with the highest probability.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    /// <returns>The next target cell.</returns>
    private MatchNextTargetCell GetNextCellBySearching(MatchState matchState)
    {
        // Find the cell with the highest probability
        var suggestedCell = FindHighestProbabilityCell();

        // Decide whether to use an Avenger power
        var (useAvenger, avenger) = ShouldUseAvengerPower(matchState, suggestedCell);

        return new MatchNextTargetCell(suggestedCell)
        {
            FireWithAvenger = useAvenger,
            Avenger = avenger
        };
    }

    /// <summary>
    /// Gets the next target cell by targeting in the current targeting session.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    /// <returns>The next target cell.</returns>
    private MatchNextTargetCell GetNextCellByTargeting(MatchState matchState)
    {
        // If the direction of the ship is known, continue along that direction
        if (_targetingSessionHitPositions.Count > 1)
        {
            var nextTarget = GetNextTargetInLine(matchState);
            if (nextTarget != null)
            {
                return new MatchNextTargetCell(nextTarget);
            }
        }

        // If the direction is not known, check adjacent cells
        var adjacentTargets = GetUnknownAdjacentTargets(matchState, _targetingSessionHitPositions.Last());
        foreach (var target in adjacentTargets)
        {
            return new MatchNextTargetCell(target);
        }

        // get adjacent targets from targetingSessionHitPositions
        foreach (var hitPosition in _targetingSessionHitPositions)
        {
            if (_lastTarget!.Position == hitPosition)
                continue;

            var adjacentTargetsFromHitPosition = GetUnknownAdjacentTargets(matchState, hitPosition);
            foreach (var target in adjacentTargetsFromHitPosition)
            {
                return new MatchNextTargetCell(target);
            }
        }

        throw new InvalidOperationException("No suitable targeting cell found.");
    }

    /// <summary>
    /// Detects if the hits in the targeting session are in a line and returns the next target cell in that line.
    /// At the beginning or at the end of the line.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    /// <returns>The next target cell in the line.</returns>
    private GameCellPosition? GetNextTargetInLine(MatchState matchState)
    {
        var lastHit = _targetingSessionHitPositions.Last();
        var firstHit = _targetingSessionHitPositions.First(); // First hit in the current targeting session

        // Determine ship's orientation based on the first two hits
        var hitPositionsArray = _targetingSessionHitPositions.ToArray();
        var isHorizontal = firstHit.Row == hitPositionsArray[1].Row;
        var isVertical = firstHit.Column == hitPositionsArray[1].Column;

        // Calculate potential next target cells based on the ship's orientation
        if (isHorizontal)
        {
            // Check cells to the left and right of the last hit
            var orderedHitsHorizontally = _targetingSessionHitPositions.OrderBy(pos => pos.Column).ToArray();
            var mostLeftHit = orderedHitsHorizontally.First();
            var mostRightHit = orderedHitsHorizontally.Last();

            var leftCell = mostLeftHit with { Column = mostLeftHit.Column - 1 };
            var rightCell = mostRightHit with { Column = mostRightHit.Column + 1 };

            // If the last hit was water, target the opposite direction from the first hit
            if (matchState.GameField.GetCellStatus(lastHit) == CellState.Water)
            {
                return GetOppositeDirectionTarget(matchState, firstHit, isHorizontal);
            }

            // Choose the next target cell in the line that is within the grid and not already known
            return GetNextValidCell(matchState, leftCell, rightCell);
        }

        if (isVertical)
        {
            // Check cells above and below the last hit
            var orderedHitsVertically = _targetingSessionHitPositions.OrderBy(pos => pos.Column).ToArray();
            var mostUpperHit = orderedHitsVertically.First();
            var mostLowerHit = orderedHitsVertically.Last();

            var upCell = mostUpperHit with { Row = mostUpperHit.Row - 1 };
            var downCell = mostLowerHit with { Row = mostLowerHit.Row + 1 };

            // If the last hit was water, target the opposite direction from the first hit
            if (matchState.GameField.GetCellStatus(lastHit) == CellState.Water)
            {
                return GetOppositeDirectionTarget(matchState, firstHit, isHorizontal);
            }

            // Choose the next target cell in the line that is within the grid and not already known
            return GetNextValidCell(matchState, upCell, downCell);
        }

        // If no valid next target cell is found
        return null;
    }

    private GameCellPosition? GetOppositeDirectionTarget(MatchState matchState, GameCellPosition position,
        bool isHorizontal)
    {
        var oppositeCell = isHorizontal
            ? position with { Column = position.Column - 1 }
            : position with { Row = position.Row - 1 };

        if (IsWithinGrid(matchState, oppositeCell) && IsCellUnknown(matchState, oppositeCell))
        {
            return oppositeCell;
        }

        return null;
    }

    private GameCellPosition? GetNextValidCell(MatchState matchState, GameCellPosition cell1, GameCellPosition cell2)
    {
        if (IsWithinGrid(matchState, cell1) && IsCellUnknown(matchState, cell1))
        {
            return cell1;
        }

        if (IsWithinGrid(matchState, cell2) && IsCellUnknown(matchState, cell2))
        {
            return cell2;
        }

        return null;
    }

    /// <summary>
    /// Checks if the given cell is within the grid.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    /// <param name="cell">The cell to check.</param>
    /// <returns>True if the cell is within the grid, false otherwise.</returns>
    private bool IsWithinGrid(MatchState matchState, GameCellPosition cell) =>
        matchState.GameField.IsCellPositionInsideField(cell);
    //cell.Row >= 0 && cell.Row < matchState.Rows && cell.Column >= 0 && cell.Column < matchState.Columns;

    /// <summary>
    /// Checks if the given cell is unknown.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    /// <param name="cell">The cell to check.</param>
    /// <returns>True if the cell is unknown, false otherwise.</returns>
    private bool IsCellUnknown(MatchState matchState, GameCellPosition cell) =>
        matchState.GameField.GetCellStatus(cell) == CellState.Unknown;

    /// <summary>
    /// Gets all the unknown adjacent targets of the given position.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    /// <param name="position">The position to check.</param>
    /// <returns>The unknown adjacent targets.</returns>
    private IEnumerable<GameCellPosition> GetUnknownAdjacentTargets(MatchState matchState, GameCellPosition position) =>
        matchState.GameField.GetAllCrossAdjacentCellPositions(position).Where(pos => IsCellUnknown(matchState, pos));

    /// <summary>
    /// Checks if there is a lonely hit on the game field. Meaning a hit that is not connected to any other hit.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    /// <returns>True if there is a lonely hit, false otherwise.</returns>
    private bool ExistsLonelyHit(MatchState matchState)
    {
        return GetLonelyHits(matchState).Any();
    }

    /// <summary>
    /// Gets the lonely hits on the game field. Meaning hits that are not connected to any other hit.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    /// <returns>The lonely hits positions.</returns>
    private IEnumerable<GameCellPosition> GetLonelyHits(MatchState matchState)
    {
        var lonelyShip = matchState.GameField.Cells
            .Select((cellValue, index) => new
            {
                cellValue,
                position = matchState.GameField.GetCellPosition(index)
            })
            .Where(x =>
                x.cellValue == CellState.Ship
                && !_sunkShipPositions.Contains(x.position)
                && !_targetingSessionHitPositions.Contains(x.position))
            .Select(x => x.position);

        return lonelyShip;
    }

    /// <summary>
    /// Determines whether we should use an Avenger power and which one.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    /// <param name="targetCell">The target cell.</param>
    /// <returns>True if the we should use an Avenger power and which one, false otherwise.</returns>
    private (bool, Avenger) ShouldUseAvengerPower(MatchState matchState, GameCellPosition targetCell)
    {
        // Check if the Avenger power is available and can be used
        if (!matchState.AvengerAvailable || matchState.AvengerUsed)
        {
            return (false, Avenger.Undefined);
        }

        // Decision logic for each Avenger's power
        if (ShouldUseThorPower(matchState))
        {
            return (true, Avenger.Thor);
        }

        if (_playMode == PlayMode.Targeting && ShouldUseHulkPower(matchState, targetCell))
        {
            return (true, Avenger.Hulk);
        }

        if (ShouldUseIronManPower(matchState))
        {
            return (true, Avenger.Ironman);
        }

        return (false, Avenger.Undefined);
    }

    /// <summary>
    /// Checks whether we should use the thor's power. I use Thor when there are many unexplored cells.
    /// To be exact when the count of unknown cells is more then half of game field. 
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    /// <returns>True if the we should use thor's power, false otherwise.</returns>
    private bool ShouldUseThorPower(MatchState matchState)
    {
        var unknownCells = matchState.GameField.Cells.Count(cell => cell == CellState.Unknown);
        return unknownCells > (matchState.Rows * matchState.Columns) / 2;
    }

    /// <summary>
    /// Checks whether we should use the hulk's power.
    /// Use Hulk's power to sink a ship when a part of it is already hit. It is ideal in targeting mode when
    /// we've sure you've hit a ship.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    /// <param name="targetCell">The target cell.</param>
    /// <returns>True if the we should use hulk's power, false otherwise.</returns>
    private bool ShouldUseHulkPower(MatchState matchState, GameCellPosition targetCell)
    {
        return matchState.GameField.GetCellStatus(targetCell) == CellState.Ship;
    }

    /// <summary>
    /// Checks whether we should use the ironman's power.
    /// We should use it when the remaining ships are small 2 or 3 cells and the count of
    /// unknown cells is more then one quarter of game field. 
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    /// <returns>True if the we should use ironman's power, false otherwise.</returns>
    private bool ShouldUseIronManPower(MatchState matchState)
    {
        // Use Iron Man when remaining ships are small and hard to find
        // This can be determined by the sizes of remaining ships and the state of the game field.

        return _remainingShipSizes.All(size => size <= 3) &&
               matchState.GameField.Cells.Count(cell => cell == CellState.Unknown) >
               (matchState.Rows * matchState.Columns) / 4;
    }

    /// <summary>
    /// Searches for the cell with the highest probability.
    /// </summary>
    /// <returns>The cell with the highest probability.</returns>
    private GameCellPosition FindHighestProbabilityCell()
    {
        // get cells with highest probability
        var z = _probabilitiesGrid.Cells.Select((cellValue, index) => new { cellValue, index })
            .Where(x => x.cellValue == _probabilitiesGrid.Cells.Max())
            .ToArray();

        var highestProbCell = _probabilitiesGrid.GetCellPosition(z.First().index);

        if (highestProbCell == null)
        {
            throw new InvalidOperationException("No valid cell found for targeting.");
        }

        return highestProbCell;
    }

    /// <summary>
    /// Updates the probability grid.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    private void UpdateProbabilityGrid(MatchState matchState)
    {
        // Reset the probability grid
        _probabilitiesGrid.Initialize(new int[matchState.Rows * matchState.Columns]);

        foreach (var shipSize in _remainingShipSizes)
        {
            // simplified solution for now
            var ship = shipSize switch
            {
                2 => ShipShape.Boat,
                3 => ShipShape.Submarine, // or ShipShape.Destroyer, both are 3x1
                4 => ShipShape.Battleship,
                5 => ShipShape.Carrier,
                9 => ShipShape.AvengersHelicarrier,
                _ => throw new InvalidOperationException("Invalid ship size.")
            };

            UpdateProbabilitiesForShip(matchState, ship);
        }

        _logger.LogTrace("Probability grid updated.");

        // Notify the probability grid has been updated
        if (OnProbabilityGridUpdated != null)
        {
            OnProbabilityGridUpdated(new ProbabilitiesUpdatedArgs
            (
                Probabilities: _probabilitiesGrid.Cells.ToArray(),
                Rows: _probabilitiesGrid.Rows,
                Columns: _probabilitiesGrid.Columns
            ));
        }
    }

    /// <summary>
    /// Updates the probabilities for the given ship shape.
    /// Try to fit the ship in all possible positions allowed (vertical and horizontal) in all cells.
    /// </summary>
    /// <param name="matchState">The current state of the match.</param>
    /// <param name="ship">The ship shape.</param>
    private void UpdateProbabilitiesForShip(MatchState matchState, ShipShape ship)
    {
        for (var row = 0; row < matchState.Rows; row++)
        for (var col = 0; col < matchState.Columns; col++)
        {
            var position = new GameCellPosition(row, col);

            if (CanShipFit(position, ship, matchState))
            {
                IncrementProbabilities(position, ship);
            }

            var rotatedShip = ship.Rotate();
            if (CanShipFit(position, rotatedShip, matchState))
            {
                IncrementProbabilities(position, rotatedShip);
            }
        }
    }

    /// <summary>
    /// Increments the probabilities for all the cells that the ship can fit in. 
    /// </summary>
    /// <param name="startPosition">The starting position.</param>
    /// <param name="ship">The ship shape.</param>
    private void IncrementProbabilities(GameCellPosition startPosition, ShipShape ship)
    {
        var rows = ship.Shape.GetLength(0);
        var cols = ship.Shape.GetLength(1);

        for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
        {
            if (!ship.Shape[r, c])
                continue;

            var position = new GameCellPosition(startPosition.Row + r, startPosition.Column + c);
            _probabilitiesGrid.SetCellStatus(position, _probabilitiesGrid.GetCellStatus(position) + 1);
        }
    }

    /// <summary>
    /// Checks if the ship can fit starting from the given position.
    /// Iterates through the ship's shape and checks if the cells are within the grid and unknown.
    /// </summary>
    /// <param name="position">The starting position.</param>
    /// <param name="ship">The ship shape.</param>
    /// <param name="matchState">The current state of the match.</param>
    /// <returns>True if the ship can fit, false otherwise.</returns>
    private bool CanShipFit(GameCellPosition position, ShipShape ship, MatchState matchState)
    {
        var rows = ship.Shape.GetLength(0);
        var cols = ship.Shape.GetLength(1);

        for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
        {
            if (!ship.Shape[r, c])
                continue;

            var checkRow = position.Row + r;
            var checkCol = position.Column + c;

            if (checkRow >= matchState.Rows || checkCol >= matchState.Columns)
                return false;

            var checkPosition = new GameCellPosition(checkRow, checkCol);
            if (matchState.GameField.GetCellStatus(checkPosition) != CellState.Unknown)
                return false;
        }

        return true;
    }
}

/// <summary>
/// The callback for when the probability grid is updated.
/// </summary>
/// <param name="Probabilities">The probabilities for each cell.</param>
/// <param name="Rows">The number of rows in the grid.</param>
/// <param name="Columns">The number of columns in the grid.</param>
public record ProbabilitiesUpdatedArgs(int[] Probabilities, int Rows, int Columns);