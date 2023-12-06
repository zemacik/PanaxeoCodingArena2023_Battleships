namespace battleships.lib.Models;

/// <summary>
/// Represents the initial state of a match.
/// </summary>
public record MatchInitialState
{
    /// <summary>
    /// ID of the map, on which was called last player's move.
    /// This value will change when player beats current map.
    /// </summary>
    public int MapId { get; set; }

    /// <summary>
    /// Avenger availability after the player's move.
    /// </summary>
    public bool AvengerAvailable { get; set; }

    /// <summary>
    /// Number of valid moves which were made on the current map.
    /// Invalid moves such as firing at the same position multiple times are not included.
    /// </summary>
    public int MoveCount { get; set; }

    /// <summary>
    /// Denotes if player successfully finished currently ongoing game =>
    /// if player completed mapCount maps. Valid move after getting true in this field
    /// results in new game (or error if player has already achieved max number of tries).
    /// </summary>
    public bool Finished { get; set; }

    /// <summary>
    /// 144 chars (12x12 grid) representing updated state of map,
    /// '*' is unknown, 'X' is ship, '.' is water.
    /// </summary>
    public string Grid { get; set; } = new('*', Constants.GridColumns * Constants.GridRows);
    
    /// <summary>
    /// Fixed number of maps which are required to complete before completing one full game.
    /// </summary>
    public int MapCount { get; set; }
}