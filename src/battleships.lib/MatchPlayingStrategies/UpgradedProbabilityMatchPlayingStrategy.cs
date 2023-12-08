using battleships.lib.MatchPlayingStrategies.Models;
using Microsoft.Extensions.Logging;

namespace battleships.lib.MatchPlayingStrategies;

/// <summary>
/// Upgraded probability match playing strategy.
/// </summary>
public class UpgradedProbabilityMatchPlayingStrategy : ProbabilityMatchPlayingStrategy
{
    /// <summary>
    /// Initialises a new instance of the <see cref="UpgradedProbabilityMatchPlayingStrategy"/> class.
    /// </summary>
    /// <param name="loggerFactory"></param>
    /// <param name="onProbabilityGridUpdated"></param>
    public UpgradedProbabilityMatchPlayingStrategy(
        ILoggerFactory loggerFactory,
        Action<ProbabilitiesUpdatedArgs>? onProbabilityGridUpdated = null)
        : base(loggerFactory, onProbabilityGridUpdated)
    {
        Logger = loggerFactory.CreateLogger<UpgradedProbabilityMatchPlayingStrategy>();
    }

    /// <inheritdoc />
    public override string Name => "Upgraded Probability Match Playing Strategy";

    /// <inheritdoc />
    protected override bool DetectAndProcessSunkenShip()
    {
        if (TargetingSessionHitPositions.Count <= 1)
            return false;

        const int theAvengersHelicarrierCells = 9;

        var numberOfHits = TargetingSessionHitPositions.Count;
        var allHitsInLine = AreHitsInLine();

        var direction = Direction.Unknown;
        if (allHitsInLine)
        {
            direction = AreHitsInHorizontalLine() ? Direction.Horizontal : Direction.Vertical;
        }

        var allAdjacentCellsKnown = TargetingSessionHitPositions.All(AreAllAdjacentCellsKnown);
        var existAvengersHelicarrier = RemainingShipSizes.Contains(theAvengersHelicarrierCells);
        var isAvengersHelicarrier = existAvengersHelicarrier &&
                                    (numberOfHits == theAvengersHelicarrierCells || direction == Direction.Unknown);
        var isWaterNextToFirstAndLastHit = IsWaterAtBothEnds();

        var canBeAvengersHelicarrier = existAvengersHelicarrier
                                       && (isAvengersHelicarrier
                                           || numberOfHits == 3
                                           || numberOfHits == 5
                                           || (allHitsInLine &&
                                               (numberOfHits == 2 || numberOfHits == 4) &&
                                               !isWaterNextToFirstAndLastHit));


        // if the ship can not be an avengers helicarrier and the number of hits is the max ship size we have to sink,
        // or there is water at both ends we know we have a ship to sink
        var maxShip = RemainingShipSizes.Max();
        if (!canBeAvengersHelicarrier && (numberOfHits == maxShip || isWaterNextToFirstAndLastHit))
        {
            SinkShip();
            return true;
        }

        // we definitely have an avengers helicarrier
        if (isAvengersHelicarrier && numberOfHits == theAvengersHelicarrierCells)
        {
            SinkShip();
            return true;
        }

        // if the ship can be an avengers helicarrier and the number of hits is 5 (we have the main line)
        // we will check if any of the "wings" is water. If so, than we know it's not an avengers helicarrier.
        if (canBeAvengersHelicarrier && numberOfHits == 5 && isWaterNextToFirstAndLastHit)
        {
            var gf = MatchState.GameField;
            var orderedPositions = GetOrderedTargetingSessionHitPositionsInLine().ToArray();
            var secondCell = orderedPositions[1];
            var fourthCell = orderedPositions[3];

            var isHorizontal = AreHitsInHorizontalLine();

            if ((isHorizontal && (
                        gf.GetTopBottomAdjacentCellPositions(secondCell)
                            .Any(x => gf.GetCellStatus(x) == CellState.Water)
                        || gf.GetTopBottomAdjacentCellPositions(fourthCell)
                            .Any(x => gf.GetCellStatus(x) == CellState.Water))
                ) || (!isHorizontal && (
                    gf.GetLeftRightAdjacentCellPositions(secondCell).Any(x => gf.GetCellStatus(x) == CellState.Water)
                    || gf.GetLeftRightAdjacentCellPositions(fourthCell)
                        .Any(x => gf.GetCellStatus(x) == CellState.Water))))
            {
                SinkShip();
                return true;
            }
        }

        // if the ship can be an avengers helicarrier and the number of hits is 3 (we may have the cross line through the ship)
        // we will check if any of the cell around the middle cell is water. If so, than we know it's not an avengers helicarrier.        
        if (canBeAvengersHelicarrier && numberOfHits == 3 && isWaterNextToFirstAndLastHit)
        {
            var gf = MatchState.GameField;
            var orderedPositions = GetOrderedTargetingSessionHitPositionsInLine().ToArray();
            var middleCell = orderedPositions[1];

            var isHorizontal = AreHitsInHorizontalLine();

            if ((isHorizontal && gf.GetTopBottomAdjacentCellPositions(middleCell)
                    .Any(x => gf.GetCellStatus(x) == CellState.Water))
                || (!isHorizontal && gf.GetLeftRightAdjacentCellPositions(middleCell)
                    .Any(x => gf.GetCellStatus(x) == CellState.Water))
               )
            {
                SinkShip();
                return true;
            }
        }

        // just to be sure
        if (allAdjacentCellsKnown)
        {
            SinkShip();
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    protected override MatchNextTargetCell GetNextCellByTargeting()
    {
        // If the direction of the ship is known, continue along that direction
        if (TargetingSessionHitPositions.Count > 1 &&
            TargetingSessionHitPositions.Count <= Math.Min(5, RemainingShipSizes.Max()))
        {
            var nextTarget = GetNextTargetInLine();
            if (nextTarget != null)
            {
                return new MatchNextTargetCell(nextTarget);
            }
        }

        var suggestedPosition = GetNextCellFromPossibleTargets();
        if (suggestedPosition != null)
        {
            return suggestedPosition;
        }

        // Decide whether to use an Avenger power
        var (useAvenger, avenger) = ShouldUseAvengerPower();

        // detecting helicarrier
        if (RemainingShipSizes.Contains(9))
        {
            if (TargetingSessionHitPositions.Count == 3 && AreHitsInLine())
            {
                var line = GetOrderedTargetingSessionHitPositionsInLine().ToArray();

                // get middle cell
                var middleCell = line[1];

                // get adjacent cells
                foreach (var cell in GetUnknownAdjacentTargets(middleCell).ToArray())
                {
                    if (!PossibleTargets.Contains(cell))
                        PossibleTargets.Enqueue(cell);
                }

                if (PossibleTargets.Count > 0)
                    return new MatchNextTargetCell(PossibleTargets.Dequeue())
                        { FireWithAvenger = useAvenger, Avenger = avenger };
            }
            else if (TargetingSessionHitPositions.Count == 5 && AreHitsInLine())
            {
                var line = GetOrderedTargetingSessionHitPositionsInLine().ToArray();

                var secondCell = line[1];
                var fourthCell = line[3];

                var adjacentCells1 = GetUnknownAdjacentTargets(secondCell).ToArray();
                var adjacentCells2 = GetUnknownAdjacentTargets(fourthCell).ToArray();

                foreach (var cell in adjacentCells1.Concat(adjacentCells2))
                {
                    if (!PossibleTargets.Contains(cell))
                        PossibleTargets.Enqueue(cell);
                }

                if (PossibleTargets.Count > 0)
                    return new MatchNextTargetCell(PossibleTargets.Dequeue())
                        { FireWithAvenger = useAvenger, Avenger = avenger };
            }
        }

        // If the direction is not known, check adjacent cells
        var adjacentTargets = GetUnknownAdjacentTargets(TargetingSessionHitPositions.Last());
        foreach (var target in adjacentTargets)
        {
            return new MatchNextTargetCell(target) { FireWithAvenger = useAvenger, Avenger = avenger };
        }

        foreach (var hitPosition in TargetingSessionHitPositions)
        {
            if (LastTarget!.Position == hitPosition)
                continue;

            var adjacentTargetsFromHitPosition = GetUnknownAdjacentTargets(hitPosition);
            foreach (var target in adjacentTargetsFromHitPosition)
            {
                return new MatchNextTargetCell(target) { FireWithAvenger = useAvenger, Avenger = avenger };
            }
        }

        throw new InvalidOperationException("No suitable targeting cell found.");
    }

    /// <summary>
    /// Sinks the ship.
    /// </summary>
    private void SinkShip()
    {
        RemoveSunkShipFromList();
        MarkAllAroundAdjacentCellsAsWater();
        StoreSunkShipPositions();

        TargetingSessionHitPositions.Clear(); // Clear the targeting session as you might have sunk the ship
    }

    /// <summary>
    /// Checks whether there is water at both ends of the hits in line.
    /// </summary>
    /// <returns></returns>
    private bool IsWaterAtBothEnds()
    {
        if (!AreHitsInLine())
            return false;

        var isHorizontal = AreHitsInHorizontalLine();

        var field = MatchState.GameField;

        GameCellPosition theCellBeforeTheFirstHit;
        GameCellPosition theCellAfterTheLastHit;

        if (isHorizontal)
        {
            var orderedHits = TargetingSessionHitPositions.OrderBy(x => x.Column).ToList();
            var firstHit = orderedHits.First();
            var lastHit = orderedHits.Last();

            theCellBeforeTheFirstHit = firstHit with { Column = firstHit.Column - 1 };
            theCellAfterTheLastHit = lastHit with { Column = lastHit.Column + 1 };
        }
        else
        {
            var orderedHits = TargetingSessionHitPositions.OrderBy(x => x.Row).ToList();
            var firstHit = orderedHits.First();
            var lastHit = orderedHits.Last();

            theCellBeforeTheFirstHit = firstHit with { Row = firstHit.Row - 1 };
            theCellAfterTheLastHit = lastHit with { Row = lastHit.Row + 1 };
        }

        return (!IsWithinGrid(theCellBeforeTheFirstHit) ||
                field.GetCellStatus(theCellBeforeTheFirstHit) == CellState.Water)
               && (!IsWithinGrid(theCellAfterTheLastHit) ||
                   field.GetCellStatus(theCellAfterTheLastHit) == CellState.Water);
    }
}