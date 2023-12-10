using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace battleships.lib.GameTargets.Models;

/// <summary>
/// Represents the response model for a fire action in the game.
/// </summary>
public record FireResponse
{
    /// <summary>
    /// 144 chars (12x12 grid) representing updated state of map,
    /// '*' is unknown, 'X' is ship, '.' is water.
    /// </summary>
    public string Grid { get; set; }

    /// <summary>
    /// Result after firing at given position ('.' or 'X').
    /// This field may be empty ('') if player calls /fire endpoint
    /// or tries to fire at already revealed position.
    /// </summary>
    public string Cell { get; set; }

    /// <summary>
    /// Denotes if fire action was valid. E.g. if player calls /fire endpoint
    /// or fire at already revealed position, this field will be false.
    /// </summary>
    public bool Result { get; set; }

    /// <summary>
    /// Avenger availability after the player's move.
    /// </summary>
    public bool AvengerAvailable { get; init; }

    /// <summary>
    /// ID of the map, on which was called last player's move.
    /// This value will change when player beats current map.
    /// </summary>
    public int MapId { get; init; }

    /// <summary>
    /// Fixed number of maps which are required to complete before completing one full game.
    /// </summary>
    public int MapCount { get; init; }

    /// <summary>
    /// Number of valid moves which were made on the current map.
    /// Invalid moves such as firing at the same position multiple times are not included.
    /// </summary>
    public int MoveCount { get; init; }

    /// <summary>
    /// Denotes if player successfully finished currently ongoing game =>
    /// if player completed mapCount maps. Valid move after getting true in this field
    /// results in new game (or error if player has already achieved max number of tries).
    /// </summary>
    [JsonPropertyName("finished")]
    [JsonProperty("finished")]
    public bool GameFinished { get; init; }
}