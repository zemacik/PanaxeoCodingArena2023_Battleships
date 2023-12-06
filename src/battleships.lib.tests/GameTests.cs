using battleships.lib.GameTargets;
using battleships.lib.MatchPlayingStrategies;
using Microsoft.Extensions.Logging;

namespace battleships.lib.tests;

public class GameTests
{
    [Fact]
    public async Task Should_finish_game_targeting_ConcreteLocalGameTarget_strategy_DummyMatchPlayingStrategy()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        var gameTarget = new ConcreteLocalGameTarget(new ConcreteGameTargetOptions
        {
            Rows = 12,
            Columns = 12,
            MapStateGrid = new[]
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0,
                0, 0, 0, 0, 9, 0, 0, 0, 0, 0, 2, 0,
                0, 0, 0, 9, 9, 9, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 9, 0, 0, 0, 0, 0, 0, 3,
                0, 0, 0, 9, 9, 9, 0, 0, 4, 0, 0, 3,
                0, 0, 0, 0, 9, 0, 0, 0, 4, 0, 0, 3,
                0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                6, 6, 6, 6, 6, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 5, 5, 5, 5,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            },
        }, loggerFactory);

        var playingStrategyCreator = new MatchPlayingStrategyFactory(loggerFactory, (lf)
            => new DummyMatchPlayingStrategy());

        var game = new Game(gameTarget, playingStrategyCreator, loggerFactory, isSimulation: true);

        // Act
        var gameResults = await game.PlayAsync();

        // Arrange
        Assert.Equal(132, gameResults.TotalMoveCount);
    }

    [Fact]
    public async Task Should_finish_game_targeting_ConcreteLocalGameTarget_strategy_SimpleMatchPlayingStrategy()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        var gameTarget = new ConcreteLocalGameTarget(new ConcreteGameTargetOptions
        {
            Rows = 12,
            Columns = 12,
            MapStateGrid = new[]
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0,
                0, 0, 0, 0, 9, 0, 0, 0, 0, 0, 2, 0,
                0, 0, 0, 9, 9, 9, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 9, 0, 0, 0, 0, 0, 0, 3,
                0, 0, 0, 9, 9, 9, 0, 0, 4, 0, 0, 3,
                0, 0, 0, 0, 9, 0, 0, 0, 4, 0, 0, 3,
                0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                6, 6, 6, 6, 6, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 5, 5, 5, 5,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            },
        }, loggerFactory);

        var playingStrategyCreator = new MatchPlayingStrategyFactory(loggerFactory, (lf)
            => new SimpleMatchPlayingStrategy(lf));
        var game = new Game(gameTarget, playingStrategyCreator, loggerFactory, isSimulation: true);

        // Act
        var gameResults = await game.PlayAsync();

        // Arrange
        Assert.Equal(1, gameResults.MatchResults.Count);
    }

    [Fact]
    public async Task Should_finish_game_targeting_ConcreteLocalGameTarget_strategy_ProbabilityMatchPlayingStrategy()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        var gameTarget = new ConcreteLocalGameTarget(new ConcreteGameTargetOptions
        {
            Rows = 12,
            Columns = 12,
            MapStateGrid = new[]
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0,
                0, 0, 0, 0, 9, 0, 0, 0, 0, 0, 2, 0,
                0, 0, 0, 9, 9, 9, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 9, 0, 0, 0, 0, 0, 0, 3,
                0, 0, 0, 9, 9, 9, 0, 0, 4, 0, 0, 3,
                0, 0, 0, 0, 9, 0, 0, 0, 4, 0, 0, 3,
                0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                6, 6, 6, 6, 6, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 5, 5, 5, 5,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            },
        }, loggerFactory);

        var playingStrategyCreator = new MatchPlayingStrategyFactory(loggerFactory, (lf)
            => new ProbabilityMatchPlayingStrategy(lf));
        var game = new Game(gameTarget, playingStrategyCreator, loggerFactory, isSimulation: true);

        // Act
        var gameResults = await game.PlayAsync();

        // Arrange
        Assert.Equal(1, gameResults.MatchResults.Count);
    }

    [Fact]
    public async Task Should_finish_game_targeting_GeneratedLocalGameTarget_strategy_DummyMatchPlayingStrategy()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        var shipPlacementGenerator = new ShipPlacementGenerator();
        var gameTarget = new GeneratedLocalGameTarget(
            new GeneratedLocalGameTargetOptions
            {
                Rows = Constants.GridRows,
                Columns = Constants.GridColumns,
                MapCount = 200,
            },
            shipPlacementGenerator,
            loggerFactory
        );

        var playingStrategyCreator = new MatchPlayingStrategyFactory(loggerFactory, (lf)
            => new DummyMatchPlayingStrategy());

        var game = new Game(gameTarget, playingStrategyCreator, loggerFactory, isSimulation: true);

        // Act
        var gameResults = await game.PlayAsync();

        // Arrange
        Assert.NotNull(gameResults);
        Assert.Equal(200, gameResults.MatchResults.Count);
    }

    [Fact]
    public async Task Should_finish_game_targeting_GeneratedLocalGameTarget_strategy_SimpleMatchPlayingStrategy()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        var shipPlacementGenerator = new ShipPlacementGenerator();
        var gameTarget = new GeneratedLocalGameTarget(
            new GeneratedLocalGameTargetOptions
            {
                Rows = Constants.GridRows,
                Columns = Constants.GridColumns,
                MapCount = 200,
            },
            shipPlacementGenerator,
            loggerFactory
        );

        var playingStrategyCreator = new MatchPlayingStrategyFactory(loggerFactory, (lf)
            => new SimpleMatchPlayingStrategy(lf));

        var game = new Game(gameTarget, playingStrategyCreator, loggerFactory, isSimulation: true);

        // Act
        var gameResults = await game.PlayAsync();

        // Arrange
        Assert.NotNull(gameResults);
        Assert.Equal(200, gameResults.MatchResults.Count);
    }

    [Fact]
    public async Task Should_finish_game_targeting_GeneratedLocalGameTarget_strategy_ProbabilityMatchPlayingStrategy()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        var shipPlacementGenerator = new ShipPlacementGenerator();
        var gameTarget = new GeneratedLocalGameTarget(
            new GeneratedLocalGameTargetOptions
            {
                Rows = Constants.GridRows,
                Columns = Constants.GridColumns,
                MapCount = 200,
            },
            shipPlacementGenerator,
            loggerFactory
        );

        var playingStrategyCreator = new MatchPlayingStrategyFactory(loggerFactory, (lf)
            => new ProbabilityMatchPlayingStrategy(lf));

        var game = new Game(gameTarget, playingStrategyCreator, loggerFactory, isSimulation: true);

        // Act
        var gameResults = await game.PlayAsync();

        // Arrange
        Assert.NotNull(gameResults);
        Assert.Equal(200, gameResults.MatchResults.Count);
    }

    [Fact]
    public async Task Should_finish_game_targeting_PanaxeoApiGameTarget_ProbabilityMatchPlayingStrategy()
    {
        throw new Exception(); // TODO - remove this line to enable this test

        const string authorizationToken = ""; // TODO - put your authorization token here

        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri("https://europe-west1-ca-2023-dev.cloudfunctions.net/battleshipsApi/");
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authorizationToken);

        var gameTarget = new PanaxeoApiGameTarget(httpClient, loggerFactory);

        var playingStrategyCreator = new MatchPlayingStrategyFactory(loggerFactory, (lf)
            => new ProbabilityMatchPlayingStrategy(lf));
        var game = new Game(gameTarget, playingStrategyCreator, loggerFactory, isSimulation: true);

        // Act
        var gameResults = await game.PlayAsync();

        // Arrange
        Assert.NotNull(gameResults);
        Assert.Equal(200, gameResults.MatchResults.Count);
    }
}