namespace battleships.lib.GameTargets.Models;

/// <summary>
/// Represents the result of Avenger's action in the game.
/// </summary>
public class AvengerResult
{
    /// <summary>
    /// Coordinates which were affected by avenger ability.
    /// </summary>
    public MapPoint MapPoint { get; set; }

    /// <summary>
    /// Denotes if coordinates specified by mapPoint have hit a ship.
    /// </summary>
    public bool Hit { get; set; }
}