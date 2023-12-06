namespace battleships.lib.GameTargets.Models;

/// <summary>
/// Represents the response model for an Avenger fire action in the game, inheriting from FireResponse.
/// </summary>
public record AvengerFireResponse : FireResponse
{
    /// <summary>
    /// Contains the results of the Avenger's ability, with details about
    /// each affected map point and whether it hit a ship.
    /// </summary>
    public List<AvengerResult> AvengerResult { get; set; } = new();

    /// <summary>
    /// Creates a new instance of <see cref="AvengerFireResponse"/>.
    /// </summary>
    // ReSharper disable once MemberCanBePrivate.Global - required for deserialization
    public AvengerFireResponse()
    {
    }
    
    /// <summary>
    /// Creates a new instance of <see cref="AvengerFireResponse"/>.
    /// </summary>
    public AvengerFireResponse(FireResponse response)
        :this()
    {
        Grid = response.Grid;
        Cell = response.Cell;
        Result = response.Result;
        AvengerAvailable = response.AvengerAvailable;
        MapId = response.MapId;
        MapCount = response.MapCount;
        MoveCount = response.MoveCount;
        GameFinished = response.GameFinished;
    }
}