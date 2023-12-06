using battleships.lib.GameTargets;
using battleships.lib.GameTargets.Models;
using battleships.lib.MatchPlayingStrategies;
using battleships.lib.MatchPlayingStrategies.Models;
using battleships.lib.Models;
using Microsoft.Extensions.Logging;

namespace battleships.lib;

public record MatchStateArgs
{
    public CellState[] Cells { get; init; } = Array.Empty<CellState>();
    public int Rows { get; init; }
    public int Columns { get; init; }
    public int MapId { get; init; }
    public bool MatchFinished { get; init; }
    public bool GameFinished { get; init; }
    public int MapCount { get; init; }
    public int MatchMoveCount { get; init; }
    public string GameTarget { get; init; } = string.Empty;
    public string PlayingStrategy { get; init; } = string.Empty;
}

/// <summary>
/// Represents a one match in the game.
/// </summary>
internal class Match
{
    /// <summary>
    /// The game target to play with.
    /// </summary>
    private readonly IGameTarget _gameTarget;

    /// <summary>
    /// The strategy to use for playing matches.
    /// </summary>
    private readonly IMatchPlayingStrategy _matchPlayingStrategy;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Weather the game is a simulation or not.
    /// </summary>
    private readonly bool _isSimulation;

    /// <summary>
    /// The callback to call when a match is started.
    /// </summary>
    private readonly Action<MatchStateArgs>? _onMatchStarted;

    /// <summary>
    /// The callback to call after a match is fired.
    /// </summary>
    private readonly Action<MatchStateArgs>? _onMatchAfterFire;

    /// <summary>
    /// The callback to call when a match is finished.
    /// </summary>
    private readonly Action<MatchStateArgs>? _onMatchFinished;

    /// <summary>
    /// Flag indicating whether the match is already initialized or not. 
    /// </summary>
    private bool _initialized;

    /// <summary>
    /// The current state of the match.
    /// </summary>
    private readonly MatchState _state;

    /// <summary>
    /// Initialize a new instance of Match.
    /// </summary>
    /// <param name="gameTarget">The game target to play with.</param>
    /// <param name="matchPlayingStrategy">The strategy to use for playing matches.</param>
    /// <param name="loggerFactory">The logger factory to use.</param>
    /// <param name="isSimulation">Whether the game is a simulation or not.</param>
    /// <param name="onMatchStarted">The callback to call when a match is started.</param>
    /// <param name="onMatchAfterFire">The callback to call after a match is fired.</param>
    /// <param name="onMatchFinished">The callback to call when a match is finished.</param>
    public Match(IGameTarget gameTarget,
        IMatchPlayingStrategy matchPlayingStrategy,
        ILoggerFactory loggerFactory,
        bool isSimulation,
        Action<MatchStateArgs>? onMatchStarted = null,
        Action<MatchStateArgs>? onMatchAfterFire = null,
        Action<MatchStateArgs>? onMatchFinished = null)
    {
        _logger = loggerFactory.CreateLogger<Match>();
        _gameTarget = gameTarget;
        _matchPlayingStrategy = matchPlayingStrategy;
        _isSimulation = isSimulation;
        _state = new MatchState(Constants.GridRows, Constants.GridColumns);
        _onMatchStarted = onMatchStarted;
        _onMatchAfterFire = onMatchAfterFire;
        _onMatchFinished = onMatchFinished;
    }

    /// <summary>
    /// Initializes the match.
    /// </summary>
    /// <param name="initialState">The initial state of the match.</param>
    public void Initialize(MatchInitialState initialState)
    {
        if (_initialized)
        {
            throw new InvalidOperationException("Match is already initialized.");
        }

        _state.MapFromInitialState(initialState);

        _onMatchStarted?.Invoke(MapStateArgs());

        _initialized = true;
    }

    /// <summary>
    /// Plays the match.
    /// </summary>
    /// <returns>Result of the match.</returns>
    public async Task<MatchResult> PlayAsync()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Match is not initialized.");
        }

        _logger.LogTrace("Starting the match.");

        do
        {
            await PlayTurn();
        } while (_state is { MatchFinished: false, GameFinished: false });

        _onMatchFinished?.Invoke(MapStateArgs());

        return new MatchResult
        {
            MoveCount = _state.MoveCount,
            MapId = _state.MapId
        };
    }

    /// <summary>
    /// Plays a turn in the match.
    /// </summary>
    private async Task PlayTurn()
    {
        _logger.LogTrace("Playing turn {MoveCount}.", _state.MoveCount + 1);

        var nextTarget = _matchPlayingStrategy.GetNextTarget(_state);

        if (nextTarget.FireWithAvenger)
        {
            _logger.LogTrace("Firing with '{avenger}' the avenger at position Row: {Row}, Column: {Column}.",
                nextTarget.Avenger,
                nextTarget.Position.Row,
                nextTarget.Position.Column);
        }
        else
        {
            _logger.LogTrace("Firing at position Row: {Row}, Column: {Column}.", nextTarget.Position.Row,
                nextTarget.Position.Column);
        }

        var fireResponse = !nextTarget.FireWithAvenger
            ? await _gameTarget.FireAtPositionAsync(nextTarget.Position.Row, nextTarget.Position.Column, _isSimulation)
            : await _gameTarget.FireWithAvengerAsync(nextTarget.Position.Row, nextTarget.Position.Column,
                nextTarget.Avenger.ToString().ToLowerInvariant(), _isSimulation);

        if (!fireResponse.Result)
        {
            _logger.LogError("The last move was not valid. Did you fire at already revealed position?");

            throw new InvalidOperationException(
                "The last move was not valid. Did you fire at already revealed position?");
        }

        UpdateCurrentState(nextTarget, fireResponse);

        _onMatchAfterFire?.Invoke(MapStateArgs());
    }

    /// <summary>
    /// Updates the current state of the match.
    /// </summary>
    /// <param name="nextTarget">The target position we fired at.</param>
    /// <param name="fireResponse">The response of the fire action.</param>
    //  TODO: Refactor this method, because is is handling responses from two calls. and fireResponse also can be of type  AvengerFireResponse.
    private void UpdateCurrentState(MatchNextTargetCell nextTarget, FireResponse fireResponse)
    {
        _logger.LogTrace("Updating current state. State before update: {@CurrentState}", _state.ToString());

        // check if match is finished        
        if (_state.MapId != fireResponse.MapId || fireResponse.GameFinished)
        {
            _logger.LogTrace("Match finished. MapId: {MapId}, MoveCount: {MoveCount}", fireResponse.MapId,
                fireResponse.MoveCount);

            _state.MatchFinished = true;
        }
        
        _state.MoveCount = fireResponse.MoveCount;
        _state.AvengerAvailable = fireResponse.AvengerAvailable;
        _state.GameFinished = fireResponse.GameFinished;
        _state.MapId = fireResponse.MapId;

        _state.SetGridStateFromString(fireResponse.Grid);

        // field may be empty ('') if player calls/fire endpoint or tries to fire at already revealed position.
        if (string.IsNullOrWhiteSpace(fireResponse.Cell))
        {
            return;
        }

        var newCellState = fireResponse.Cell.ToUpperInvariant() == Constants.GridCellShip.ToString()
            ? CellState.Ship
            : CellState.Water;

        _state.UpdateGrid(new GameCellPosition(nextTarget.Position.Row, nextTarget.Position.Column), newCellState);

        _state.LastMoveWasHit = newCellState == CellState.Ship;

        if (fireResponse is AvengerFireResponse avengerFireResponse)
        {
            _state.AvengerUsed = true;

            avengerFireResponse.AvengerResult.ForEach(cell =>
            {
                _state.AvengerFireResult.Add(new GameCellPosition(cell.MapPoint.Y, cell.MapPoint.X), cell.Hit);
            });
        }

        _logger.LogTrace("Updated current state. State after update: {@CurrentState}", _state.ToString());
    }

    /// <summary>
    /// Maps the current state of the match to a MatchStateArgs object.
    /// </summary>
    /// <returns>The current state of the match as MatchStateArgs object.</returns>
    private MatchStateArgs MapStateArgs() => new()
    {
        Cells = _state.GameField.Cells,
        Rows = _state.Rows,
        Columns = _state.Columns,
        MapId = _state.MapId,
        MapCount = _state.MapCount,
        MatchMoveCount = _state.MoveCount,
        MatchFinished = _state.MatchFinished,
        GameFinished = _state.GameFinished,
        GameTarget = _gameTarget.Name,
        PlayingStrategy = _matchPlayingStrategy.Name
    };
}