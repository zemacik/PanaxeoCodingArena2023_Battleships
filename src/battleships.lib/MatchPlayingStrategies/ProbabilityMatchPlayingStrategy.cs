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
    /// The current play mode.
    /// </summary>
    private enum PlayMode
    {
        Searching,
        Targeting,
    }

    /// <summary>
    /// Callback for when the probability grid is updated.
    /// </summary>
    private Action<ProbabilitiesUpdatedArgs>? OnProbabilityGridUpdated { get; }

    /// <summary>
    /// The logger.
    /// </summary>
    protected ILogger Logger { get; init; }

    // the list of hit positions in the current targeting session
    protected readonly HashSet<GameCellPosition> TargetingSessionHitPositions = new();

    // the last target position
    protected MatchNextTargetCell? LastTarget;

    // remaining ship sizes 
    protected readonly List<int> RemainingShipSizes = new() { 2, 3, 3, 4, 5, 9 };

    // the queue of possible targets.
    protected readonly Queue<GameCellPosition> PossibleTargets = new();

    // the current match state
    protected MatchState MatchState;

    // the current play mode
    private PlayMode _playMode = PlayMode.Searching;

    private GameCellPosition? _ironmansRevealedPosition;

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
        Logger = loggerFactory.CreateLogger<ProbabilityMatchPlayingStrategy>();
        _probabilitiesGrid = new GameField<int>(Constants.GridRows, Constants.GridColumns);
        OnProbabilityGridUpdated = onProbabilityGridUpdated;
    }

    /// <inheritdoc />
    public virtual string Name => "Probability Match Playing Strategy";

    /// <inheritdoc />
    public MatchNextTargetCell GetNextTarget(MatchState matchState)
    {
        if (matchState is null)
        {
            throw new ArgumentNullException(nameof(matchState));
        }

        MatchState = matchState;

        HandlePreviousShootingResult();
        AssertMatchNotFinished();

        UpdateProbabilityGrid();

        if (ShouldSwitchToTargeting())
        {
            SwitchToTargetingMode();
        }
        else if (ShouldSwitchToSearching())
        {
            SwitchToSearchingMode();
        }

        var nextTarget = _playMode == PlayMode.Targeting
            ? GetNextCellByTargeting()
            : GetNextCellBySearching();

        LastTarget = nextTarget;

        return nextTarget;
    }

    /// <summary>
    /// Handles the result of the previous shot. Updates the targeting session hit positions and the
    /// sunk ship positions. Also updates the game field with revealed cells.
    /// </summary>
    protected void HandlePreviousShootingResult()
    {
        if (LastTarget != null && MatchState.LastMoveWasHit)
        {
            TargetingSessionHitPositions.Add(LastTarget.Position);
        }

        // Handle the result of the last shot by Avenger
        if (FiredWithAvenger())
        {
            UpdateAfterAvengerShot();
        }

        // Process unmarked ships to sunk, or targetting session
        ProccessHitIslands();
    }

    private void ProccessHitIslands()
    {
        // we have some floating hits, can occur after Avenger's power
        if (TargetingSessionHitPositions.Count == 0)
        {
            var firstLonelyHit = GetLonelyHits().FirstOrDefault();
            if (firstLonelyHit != null)
            {
                TargetingSessionHitPositions.Add(firstLonelyHit);
            }
        }

        // we have ongoing session, but need to scan for adjacent hits not connected to the session, can occur after Avenger's power
        if (TargetingSessionHitPositions.Count > 0)
        {
            void AddAdjacentHits(GameCellPosition position)
            {
                var all2AdjacentHits = MatchState.GameField.GetAllCrossAdjacentCellPositions(position)
                    .Where(pos => MatchState.GameField.GetCellStatus(pos) == CellState.Ship
                                  && !TargetingSessionHitPositions.Contains(pos)).ToArray();

                foreach (var ah in all2AdjacentHits)
                {
                    TargetingSessionHitPositions.Add(ah);
                    AddAdjacentHits(ah); // Recursive
                }
            }

            foreach (var ah in TargetingSessionHitPositions.ToArray())
            {
                AddAdjacentHits(ah);
            }
        }

        // detect if the ship was sunk
        var shipWasSunk = DetectAndProcessSunkenShip();

        if (shipWasSunk)
        {
            ProccessHitIslands();
        }
    }

    /// <summary>
    /// Checks if the last shot was fired with an Avenger power. 
    /// </summary>
    protected bool FiredWithAvenger() => LastTarget is { FireWithAvenger: true };

    /// <summary>
    /// Updates the game fields with the revealed cells after an Avenger shot.
    /// </summary>
    /// <param name="matchState"></param>
    protected void UpdateAfterAvengerShot()
    {
        if (LastTarget == null)
            throw new InvalidOperationException("Last target should not be null.");

        Logger.LogTrace("Updating after Avenger shot.");

        // Ironman's ability is to reveal the smallest remaining ship
        if (LastTarget.Avenger == Avenger.Ironman)
        {
            foreach (var avengerFireResponse in MatchState.AvengerFireResult)
            {
                var suggestedPosition =
                    new GameCellPosition(avengerFireResponse.Key.Row, avengerFireResponse.Key.Column);

                _ironmansRevealedPosition = suggestedPosition;
                //PossibleTargets.Enqueue(suggestedPosition);
            }
        }

        // Hulk's ability is to destroy the whole ship if you fire on a part of it
        else if (LastTarget.Avenger == Avenger.Hulk)
        {
            if (!MatchState.LastMoveWasHit)
                return;

            foreach (var avengerFireResponse in MatchState.AvengerFireResult)
            {
                var suggestedPosition =
                    new GameCellPosition(avengerFireResponse.Key.Row, avengerFireResponse.Key.Column);

                TargetingSessionHitPositions.Add(suggestedPosition);
            }
        }

        // Thor's ability is to reveal 11 spaces at once with his lightning.
        // The one you’re firing at + 10 random spaces. All in a single turn.
        else if (LastTarget.Avenger == Avenger.Thor)
        {
            // The game field is already updated with the revealed cells
        }
    }

    /// <summary>
    /// Asserts that the match is not finished.
    /// </summary>
    protected void AssertMatchNotFinished()
    {
        if (MatchState.MatchFinished)
        {
            throw new InvalidOperationException("Match is already finished.");
        }
    }

    /// <summary>
    /// Checks if the play mode should be switched to searching mode.
    /// </summary>
    /// <returns>True if the play mode should be switched to searching mode, false otherwise.</returns>
    protected bool ShouldSwitchToSearching()
    {
        if (_playMode == PlayMode.Searching)
            return false;

        // The targeting session is over
        return TargetingSessionHitPositions.Count == 0;
    }

    /// <summary>
    /// Searches for a ship in the targeting session and removes it from the list of remaining ships.
    /// </summary>
    protected virtual bool DetectAndProcessSunkenShip()
    {
        if (TargetingSessionHitPositions.Count > 1)
        {
            var allAdjacentKnown = TargetingSessionHitPositions.All(pos =>
                AreAllAdjacentCellsKnown(pos));

            // is we get information that avenger is available, we can assume that the ship is sunk
            const int theAvengersHelicarrierCells = 9;
            var isAvengersHelicarrierSunk = MatchState.AvengerAvailable &&
                                            TargetingSessionHitPositions.Count == theAvengersHelicarrierCells;

            const int theCarrierCells = 5;
            var isCarrierSunk = GetNextTargetInLine() == null
                                && TargetingSessionHitPositions.Count == theCarrierCells
                                && !RemainingShipSizes.Contains(theAvengersHelicarrierCells);

            if (isAvengersHelicarrierSunk ||
                isCarrierSunk ||
                allAdjacentKnown)
            {
                RemoveSunkShipFromList();
                MarkAllAroundAdjacentCellsAsWater();
                StoreSunkShipPositions();

                TargetingSessionHitPositions.Clear(); // Clear the targeting session as you might have sunk the ship
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes the sunk ship from the list of remaining ships.
    /// </summary>
    protected void RemoveSunkShipFromList()
    {
        RemainingShipSizes.Remove(TargetingSessionHitPositions.Count);
    }

    /// <summary>
    /// Saves the positions of the sunk ship.
    /// </summary>
    protected void StoreSunkShipPositions()
    {
        foreach (var pos in TargetingSessionHitPositions)
        {
            _sunkShipPositions.Add(pos);
        }
    }

    /// <summary>
    /// Marks all surrounding cells of the sunk ship as water.
    /// </summary>
    protected void MarkAllAroundAdjacentCellsAsWater()
    {
        foreach (var pos in TargetingSessionHitPositions)
            foreach (var apos in MatchState.GameField.GetAllAroundAdjacentCellPositions(pos))
            {
                var cs = MatchState.GameField.GetCellStatus(apos);
                if (cs == CellState.Unknown)
                {
                    MatchState.GameField.SetCellStatus(apos, CellState.Water);
                }
            }
    }

    /// <summary>
    /// Checks if the play mode should be switched to targeting mode.
    /// </summary>
    /// <returns>True if the play mode should be switched to targeting mode, false otherwise.</returns>
    protected bool ShouldSwitchToTargeting()
    {
        // Switch to targeting mode if the last move was a hit and not already in targeting mode
        return _playMode != PlayMode.Targeting
               && (MatchState.LastMoveWasHit
                   || TargetingSessionHitPositions.Count > 0
                   || (TargetingSessionHitPositions.Count == 0 && ExistsLonelyHit()));
    }

    /// <summary>
    /// Switches the play mode to searching mode.
    /// </summary>
    protected void SwitchToTargetingMode()
    {
        if (!ShouldSwitchToTargeting())
            throw new Exception("Targeting mode not applicable.");

        Logger.LogTrace("Switching to targeting mode.");

        _playMode = PlayMode.Targeting;
    }

    /// <summary>
    /// Switches the play mode to searching mode.
    /// </summary>
    protected void SwitchToSearchingMode()
    {
        Logger.LogTrace("Switching to searching mode.");

        _playMode = PlayMode.Searching;
    }

    /// <summary>
    /// Check if all adjacent cells to the given position are known (not unknown)
    /// </summary>
    protected bool AreAllAdjacentCellsKnown(GameCellPosition position) =>
        MatchState.GameField.GetAllCrossAdjacentCellPositions(position)
            .All(pos => MatchState.GameField.GetCellStatus(pos) != CellState.Unknown);

    /// <summary>
    /// Gets the next target cell by searching => meaning the cell with the highest probability.
    /// </summary>
    /// <returns>The next target cell.</returns>
    protected MatchNextTargetCell GetNextCellBySearching()
    {
        var suggestedPosition = GetNextCellFromPossibleTargets();
        if (suggestedPosition != null)
        {
            return suggestedPosition;
        }

        if(_ironmansRevealedPosition != null)
        {
            var nextTarget = new MatchNextTargetCell(_ironmansRevealedPosition);
            _ironmansRevealedPosition = null;

            return nextTarget;
        }

        // Find the cell with the highest probability
        var suggestedCell = FindHighestProbabilityCell();

        // Decide whether to use an Avenger power
        var (useAvenger, avenger) = ShouldUseAvengerPower();

        return new MatchNextTargetCell(suggestedCell)
        {
            FireWithAvenger = useAvenger,
            Avenger = avenger
        };
    }

    /// <summary>
    /// Gets the next target cell by targeting in the current targeting session.
    /// </summary>
    /// <returns>The next target cell.</returns>
    protected virtual MatchNextTargetCell GetNextCellByTargeting()
    {
        // If the direction of the ship is known, continue along that direction
        if (TargetingSessionHitPositions.Count > 1)
        {
            var nextTarget = GetNextTargetInLine();
            if (nextTarget != null)
            {
                return new MatchNextTargetCell(nextTarget);
            }
        }

        // Decide whether to use an Avenger power
        var (useAvenger, avenger) = ShouldUseAvengerPower();

        // If the direction is not known, check adjacent cells
        var adjacentTargets = GetUnknownAdjacentTargets(TargetingSessionHitPositions.Last());
        foreach (var target in adjacentTargets)
        {
            return new MatchNextTargetCell(target) with { FireWithAvenger = useAvenger, Avenger = avenger };
        }

        // get adjacent targets from targetingSessionHitPositions
        foreach (var hitPosition in TargetingSessionHitPositions)
        {
            if (LastTarget!.Position == hitPosition)
                continue;

            var adjacentTargetsFromHitPosition = GetUnknownAdjacentTargets(hitPosition);
            foreach (var target in adjacentTargetsFromHitPosition)
            {
                return new MatchNextTargetCell(target) with { FireWithAvenger = useAvenger, Avenger = avenger };
            }
        }

        throw new InvalidOperationException("No suitable targeting cell found.");
    }

    /// <summary>
    /// Detects if the hits in the targeting session are in a line and returns the next target cell in that line.
    /// At the beginning or at the end of the line.
    /// </summary>
    /// <returns>The next target cell in the line.</returns>
    protected virtual GameCellPosition? GetNextTargetInLine()
    {
        if (!AreHitsInLine())
            return null;

        // Calculate potential next target cells based on the ship's orientation
        GameCellPosition possibleWaterOneEndCell;
        GameCellPosition possibleWaterSecondEndCell;

        // Determine ship's orientation based on the first two hits
        var isHorizontal = AreHitsInHorizontalLine();
        if (isHorizontal)
        {
            // Check cells to the left and right of the last hit
            var orderedHitsHorizontally = TargetingSessionHitPositions.OrderBy(pos => pos.Column).ToArray();
            var mostLeftHit = orderedHitsHorizontally.First();
            var mostRightHit = orderedHitsHorizontally.Last();

            possibleWaterOneEndCell = mostLeftHit with { Column = mostLeftHit.Column - 1 };
            possibleWaterSecondEndCell = mostRightHit with { Column = mostRightHit.Column + 1 };
        }
        else
        {
            // Check cells above and below the last hit
            var orderedHitsVertically = TargetingSessionHitPositions.OrderBy(pos => pos.Row).ToArray();
            var mostUpperHit = orderedHitsVertically.First();
            var mostLowerHit = orderedHitsVertically.Last();

            possibleWaterOneEndCell = mostUpperHit with { Row = mostUpperHit.Row - 1 };
            possibleWaterSecondEndCell = mostLowerHit with { Row = mostLowerHit.Row + 1 };
        }

        if (IsWithinGrid(possibleWaterOneEndCell) && IsCellUnknown(possibleWaterOneEndCell))
        {
            return possibleWaterOneEndCell;
        }
        
        if (IsWithinGrid(possibleWaterSecondEndCell) && IsCellUnknown(possibleWaterSecondEndCell))
        {
            return possibleWaterSecondEndCell;
        }

        return null;        
    }

    /// <summary>
    /// Checks if the hits in the targeting session are in a line.
    /// </summary>
    protected bool AreHitsInLine() => AreHitsInHorizontalLine() || AreHitsInVerticalLine();
    
    /// <summary>
    /// Checks if the hits in the targeting session are in horizontal line.
    /// </summary>
    /// <returns>True if the hits are in horizontal line, false otherwise.</returns>    
    protected bool AreHitsInHorizontalLine() => TargetingSessionHitPositions.Select(x => x.Row).Distinct().Count() == 1;
    
    /// <summary>
    /// Checks if the hits in the targeting session are in vertical line.
    /// </summary>
    /// <returns>True if the hits are in vertical line, false otherwise.</returns>
    protected bool AreHitsInVerticalLine() => TargetingSessionHitPositions.Select(x => x.Column).Distinct().Count() == 1;
    
    /// <summary>
    /// Gets ordered target positions in line
    /// </summary>
    /// <returns>Ordered target positions in line</returns>
    protected IEnumerable<GameCellPosition> GetOrderedTargetingSessionHitPositionsInLine()
    {
        if (AreHitsInHorizontalLine())
        {
            return TargetingSessionHitPositions.OrderBy(x => x.Column);
        }

        if (AreHitsInVerticalLine())
        {
            return TargetingSessionHitPositions.OrderBy(x => x.Row);
        }

        return TargetingSessionHitPositions;
    }

    /// <summary>
    /// Checks if the given cell is within the grid.
    /// </summary>
    /// <param name="cell">The cell to check.</param>
    /// <returns>True if the cell is within the grid, false otherwise.</returns>
    protected bool IsWithinGrid(GameCellPosition cell) =>
        MatchState.GameField.IsCellPositionInsideField(cell);

    /// <summary>
    /// Checks if the given cell is unknown.
    /// </summary>
    /// <param name="cell">The cell to check.</param>
    /// <returns>True if the cell is unknown, false otherwise.</returns>
    protected bool IsCellUnknown(GameCellPosition cell) =>
        MatchState.GameField.GetCellStatus(cell) == CellState.Unknown;

    /// <summary>
    /// Gets all the unknown adjacent targets of the given position.
    /// </summary>
    /// <param name="position">The position to check.</param>
    /// <returns>The unknown adjacent targets.</returns>
    protected IEnumerable<GameCellPosition> GetUnknownAdjacentTargets(GameCellPosition position) =>
        MatchState.GameField.GetAllCrossAdjacentCellPositions(position).Where(IsCellUnknown);

    /// <summary>
    /// Checks if there is a lonely hit on the game field. Meaning a hit that is not connected to any other hit.
    /// </summary>
    /// <returns>True if there is a lonely hit, false otherwise.</returns>
    protected bool ExistsLonelyHit()
    {
        return GetLonelyHits().Any();
    }

    /// <summary>
    /// Gets the lonely hits on the game field. Meaning hits that are not connected to any other hit.
    /// </summary>
    /// <returns>The lonely hits positions.</returns>
    protected IEnumerable<GameCellPosition> GetLonelyHits()
    {
        var lonelyShip = MatchState.GameField.Cells
            .Select((cellValue, index) => new
            {
                cellValue,
                position = MatchState.GameField.GetCellPosition(index)
            })
            .Where(x =>
                x.cellValue == CellState.Ship
                && !_sunkShipPositions.Contains(x.position)
                && !TargetingSessionHitPositions.Contains(x.position))
            .Select(x => x.position);

        return lonelyShip;
    }

    /// <summary>
    /// Determines whether we should use an Avenger power and which one.
    /// </summary>
    /// <returns>True if the we should use an Avenger power and which one, false otherwise.</returns>
    protected (bool, Avenger) ShouldUseAvengerPower()
    {
        // Check if the Avenger power is available and can be used
        if (!MatchState.AvengerAvailable || MatchState.AvengerUsed)
        {
            return (false, Avenger.Undefined);
        }

        if (_playMode == PlayMode.Searching && ShouldUseIronManPower())
        {
            return (true, Avenger.Ironman);
        }

        // Decision logic for each Avenger's power
        if (_playMode == PlayMode.Searching && ShouldUseThorPower())
        {
            return (true, Avenger.Thor);
        }

        if (_playMode == PlayMode.Targeting && ShouldUseHulkPower())
        {
            return (true, Avenger.Hulk);
        }

        return (false, Avenger.Undefined);
    }

    /// <summary>
    /// Checks whether we should use the thor's power. I use Thor when there are many unexplored cells.
    /// To be exact when the count of unknown cells is more then half of game field. 
    /// </summary>
    /// <returns>True if the we should use thor's power, false otherwise.</returns>
    protected bool ShouldUseThorPower()
    {
        var unknownCells = MatchState.GameField.Cells.Count(cell => cell == CellState.Unknown);
        return unknownCells > (MatchState.Rows * MatchState.Columns) / 2;
    }

    /// <summary>
    /// Checks whether we should use the hulk's power.
    /// Use Hulk's power to sink a ship when a part of it is already hit. It is ideal in targeting mode when
    /// we've sure you've hit a ship.
    /// </summary>
    /// <param name="targetCell">The target cell.</param>
    /// <returns>True if the we should use hulk's power, false otherwise.</returns>
    protected bool ShouldUseHulkPower()
    {
        return TargetingSessionHitPositions.Count == 1
            && !RemainingShipSizes.Contains(2)
            && !RemainingShipSizes.Contains(3)
            && (RemainingShipSizes.Contains(4) || RemainingShipSizes.Contains(5));
    }

    /// <summary>
    /// Checks whether we should use the ironman's power.
    /// We should use it when the remaining ships are small 2 or 3 cells and the count of
    /// unknown cells is more then one quarter of game field. 
    /// </summary>
    /// <returns>True if the we should use ironman's power, false otherwise.</returns>
    protected bool ShouldUseIronManPower()
    {
        // Use Iron Man when remaining ships are small and hard to find
        // This can be determined by the sizes of remaining ships and the state of the game field.

        return RemainingShipSizes.All(size => size <= 3) &&
               MatchState.GameField.Cells.Count(cell => cell == CellState.Unknown) >
               (MatchState.Rows * MatchState.Columns) / 4;
    }

    /// <summary>
    /// Gets the next cell from the queue of possible targets.
    /// </summary>
    /// <returns>The next cell from the queue of possible targets.</returns>
    protected MatchNextTargetCell? GetNextCellFromPossibleTargets()
    {
        if (PossibleTargets.Count <= 0) 
            return null;
        
        var suggestedPosition = PossibleTargets.Dequeue();

        return MatchState.GameField.GetCellStatus(suggestedPosition) == CellState.Unknown 
            ? new MatchNextTargetCell(suggestedPosition) 
            : GetNextCellFromPossibleTargets();
    }

    /// <summary>
    /// Searches for the cell with the highest probability.
    /// </summary>
    /// <returns>The cell with the highest probability.</returns>
    protected GameCellPosition FindHighestProbabilityCell()
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
    protected void UpdateProbabilityGrid()
    {
        // Reset the probability grid
        _probabilitiesGrid.Initialize(new int[MatchState.Rows * MatchState.Columns]);

        foreach (var shipSize in RemainingShipSizes)
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

            UpdateProbabilitiesForShip(ship);
        }

        Logger.LogTrace("Probability grid updated.");

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
    /// <param name="ship">The ship shape.</param>
    protected void UpdateProbabilitiesForShip(ShipShape ship)
    {
        for (var row = 0; row < MatchState.Rows; row++)
            for (var col = 0; col < MatchState.Columns; col++)
            {
                var position = new GameCellPosition(row, col);

                if (CanShipFit(position, ship))
                {
                    IncrementProbabilities(position, ship);
                }

                var rotatedShip = ship.Rotate();
                if (CanShipFit(position, rotatedShip))
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
    protected void IncrementProbabilities(GameCellPosition startPosition, ShipShape ship)
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
    /// <returns>True if the ship can fit, false otherwise.</returns>
    protected bool CanShipFit(GameCellPosition position, ShipShape ship)
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

                if (checkRow >= MatchState.Rows || checkCol >= MatchState.Columns)
                    return false;

                var checkPosition = new GameCellPosition(checkRow, checkCol);
                if (MatchState.GameField.GetCellStatus(checkPosition) != CellState.Unknown)
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