using battleships.lib.GameTargets;
using battleships.lib.MatchPlayingStrategies;
using battleships.lib.Models;
using Microsoft.Extensions.Logging;

namespace battleships.lib;

/// <summary>
/// Represents the whole game.
/// Game is a collection of matches.
/// </summary>
public class Game
{
    /// <summary>
    /// The game target to play with.
    /// </summary>
    private readonly IGameTarget _gameTarget;

    /// <summary>
    /// The strategy creator to use for playing matches.
    /// </summary>
    private readonly IMatchPlayingStrategyFactory _matchPlayingStrategyFactory;

    /// <summary>
    /// The logger factory.
    /// </summary>
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Whether the game is a simulation or not.
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
    /// Initialize a new instance of Game.
    /// </summary>
    /// <param name="gameTarget">The game target to play with.</param>
    /// <param name="matchPlayingStrategyFactory">The strategy creator to use for playing matches.</param>
    /// <param name="loggerFactory">The logger factory to use.</param>
    /// <param name="isSimulation">Whether the game is a simulation or not.</param>
    /// <param name="onMatchStarted">The callback to call when a match is started.</param>
    /// <param name="onMatchAfterFire">The callback to call after a match is fired.</param>
    /// <param name="onMatchFinished">The callback to call when a match is finished.</param>
    public Game(IGameTarget gameTarget,
        IMatchPlayingStrategyFactory matchPlayingStrategyFactory,
        ILoggerFactory loggerFactory,
        bool isSimulation = false,
        Action<MatchStateArgs>? onMatchStarted = null,
        Action<MatchStateArgs>? onMatchAfterFire = null,
        Action<MatchStateArgs>? onMatchFinished = null)
    {
        _logger = loggerFactory.CreateLogger<Game>();
        _loggerFactory = loggerFactory;
        _gameTarget = gameTarget;
        _matchPlayingStrategyFactory = matchPlayingStrategyFactory;
        _isSimulation = isSimulation;
        _onMatchStarted = onMatchStarted;
        _onMatchAfterFire = onMatchAfterFire;
        _onMatchFinished = onMatchFinished;
    }

    /// <summary>
    /// Plays the game.
    /// </summary>
    /// <returns>Result of the game.</returns>
    public async Task<GameResult> PlayAsync()
    {
        _logger.LogInformation("Starting the game.");

        if (_isSimulation)
        {
            _logger.LogInformation("Simulation mode is enabled.");
        }

        // Get the state of the game.
        var matchState = await _gameTarget.GetFireStatusAsync(_isSimulation);
        _logger.LogTrace("State of game or last match: {@matchState}", matchState);

        var isGameOver = matchState.GameFinished && matchState.MapId == matchState.MapCount;

        // If the game is finished at the beginning, return the total move count.
        // we don't need to play the matches. (But also we will not get the match results.)
        // (API/GameTarget is not returning the match results if the game is finished.)
        if (isGameOver)
        {
            return new GameResult
            {
                TotalMoveCount = matchState.MoveCount
            };
        }

        // Play the matches.
        var totalMatchesToPlay = matchState.MapCount;
        var matchesPlayed = matchState.MapId;
        var matchResults = new List<MatchResult>();
        var isFirstIteration = true;
        var newMapId = matchState.MapId;

        while (matchesPlayed < totalMatchesToPlay)
        {
            var match = new Match(
                _gameTarget,
                _matchPlayingStrategyFactory.Create(),
                _loggerFactory,
                _isSimulation,
                _onMatchStarted,
                _onMatchAfterFire,
                _onMatchFinished
            );

            var matchInitialState = new MatchInitialState
            {
                MapCount = totalMatchesToPlay,
                MapId = newMapId,
                MoveCount = 0
            };

            // initialize the first match after execution with the initial state (we might have an ongoing game).
            if (isFirstIteration && !matchState.GameFinished)
            {
                matchInitialState = matchInitialState with
                {
                    MoveCount = matchState.MoveCount,
                    MapId = matchState.MapId,
                    AvengerAvailable = matchState.AvengerAvailable,
                    Grid = matchState.Grid
                };
            }

            match.Initialize(matchInitialState);
            _logger.LogTrace("Initialized the first match with initial state: {@matchState}", matchState);

            var matchResult = await match.PlayAsync();

            matchResults.Add(matchResult);

            newMapId = matchResult.MapId;
            matchesPlayed++;
            isFirstIteration = false;
        }

        _logger.LogInformation("Game finished with total move count: {totalMoveCount}",
            matchResults.Sum(m => m.MoveCount));

        return new GameResult
        {
            MatchResults = matchResults,
            TotalMoveCount = matchResults.Sum(m => m.MoveCount)
        };
    }
}