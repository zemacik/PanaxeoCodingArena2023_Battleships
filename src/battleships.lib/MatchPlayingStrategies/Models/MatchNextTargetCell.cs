namespace battleships.lib.MatchPlayingStrategies.Models;

/// <summary>
/// Represents the next target the gameTarget should target on.
/// </summary>
public record MatchNextTargetCell
{
    public GameCellPosition Position { get; }

    public bool FireWithAvenger { get; init; }

    public Avenger Avenger { get; init; } = Avenger.Undefined;

    public MatchNextTargetCell(GameCellPosition position)
    {
        Position = position;
    }
}