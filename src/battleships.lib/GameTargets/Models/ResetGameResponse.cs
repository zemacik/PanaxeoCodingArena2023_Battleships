namespace battleships.lib.GameTargets.Models;

/// <summary>
/// Represents a result object when resetting a game.
/// </summary>
public class ResetGameResponse
{
    /// <summary>
    /// The number of available tries.
    /// </summary>
    public int AvailableTries { get; set; }
}