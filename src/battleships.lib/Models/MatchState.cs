using battleships.lib.Helpers;
using battleships.lib.MatchPlayingStrategies.Models;

namespace battleships.lib.Models;

public class MatchState
{
    public MatchState(int rows, int columns)
    {
        Rows = rows;
        Columns = columns;
        GameField = new GameField<CellState>(rows, columns);
    }

    /// <summary>
    /// Total number of rows in the map.
    /// </summary>
    public int Rows { get; }

    /// <summary>
    /// Total number of columns in the map.
    /// </summary>
    public int Columns { get; }

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
    /// The result of the last avenger fire.
    /// </summary>
    public Dictionary<GameCellPosition, bool> AvengerFireResult { get; } = new();

    /// <summary>
    /// Number of valid moves which were made on the current map.
    /// Invalid moves such as firing at the same position multiple times are not included.
    /// </summary>
    public int MoveCount { get; set; }

    /// <summary>
    /// Indicates if the match is finished.
    /// </summary>
    public bool MatchFinished { get; set; }

    /// <summary>
    /// Denotes if player successfully finished currently ongoing game =>
    /// if player completed mapCount maps. Valid move after getting true in this field
    /// results in new game (or error if player has already achieved max number of tries).
    /// </summary>
    public bool GameFinished { get; set; }
    
    /// <summary>
    /// 144 chars (12x12 grid) representing updated state of map,
    /// '*' is unknown, 'X' is ship, '.' is water.
    /// </summary>
    public GameField<CellState> GameField { get; }

    /// <summary>
    /// Fixed number of maps which are required to complete before completing one full game.
    /// </summary>
    public int MapCount { get; set; }

    /// <summary>
    /// Weather the last move was hit.
    /// </summary>
    public bool LastMoveWasHit { get; set; }

    /// <summary>
    /// Weather the avenger was already used.
    /// </summary>
    public bool AvengerUsed { get; set; }

    /// <summary>
    /// Maps state from initial state.
    /// </summary>
    /// <param name="initialState">The initial state of the match.</param>
    public void MapFromInitialState(MatchInitialState initialState)
    {
        MapId = initialState.MapId;
        AvengerAvailable = initialState.AvengerAvailable;
        MoveCount = initialState.MoveCount;
        MapCount = initialState.MapCount;
        MatchFinished = initialState.Finished;

        SetGridStateFromString(initialState.Grid);
    }

    /// <summary>
    /// Updates the grid with the given cell state.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="cellState"></param>
    /// <exception cref="NotImplementedException"></exception>
    public void UpdateGrid(GameCellPosition position, CellState cellState)
    {
        GameField.SetCellStatus(position, cellState);
    }

    /// <summary>
    /// Sets the grid state from the given string.
    /// </summary>
    /// <param name="gridState">The grid state to set.</param>
    public void SetGridStateFromString(string gridState)
    {
        var newdata = gridState.Select(c => c switch
        {
            Constants.GridCellUnknown => CellState.Unknown,
            Constants.GridCellShip => CellState.Ship,
            Constants.GridCellWater => CellState.Water,
            _ => throw new ArgumentException($"Unknown character '{c}' in grid state string.")
        }).ToArray();

        if (!GameField.IsInitialized)
        {
            GameField.Initialize(newdata);
        }
        else
        {
            GameField.Update(newdata, (oldValue, newValue) =>
                newValue == CellState.Water || newValue == CellState.Ship ? newValue : oldValue);
        }
    }

    public override string ToString()
    {
        return
            $"Rows: {Rows}, Columns: {Columns}, MapId: {MapId}, AvengerAvailable: {AvengerAvailable}, MoveCount: {MoveCount}, Finished: {MatchFinished}, MapCount: {MapCount}, Grid: {GridHelper.PrettyPrint(GameField)}";
    }
}