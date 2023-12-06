using battleships.lib;
using battleships.lib.GameTargets;
using battleships.lib.MatchPlayingStrategies;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

Console.WriteLine("Welcome to Panaxeo battleships :-)");

const string panaxeoCodingArena2023DefaultAuthToken = "";

// ------------------------------------------------
// INFRASTRUCTURE
// Initialize a little piece of infrastructure
// ------------------------------------------------

var serilog = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File("log.txt")
    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Warning)
    .CreateLogger();

using var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(serilog));

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(Assembly.GetExecutingAssembly())
    .Build();

// ------------------------------------------------
// GAME TARGET
// Initialize which game target to use. Game target is something like server we are targetting
// We have 3 game targets:
// 1. ConcreteLocalGameTarget  - a local game target with a concrete map
// 2. GeneratedLocalGameTarget - a local game target which generates random maps
// 3. PanaxeoApiGameTarget     - a game target that uses Panaxeo API
// ------------------------------------------------

IGameTarget gameTarget;

// ----- PanaxeoApiGameTarget initialization sample -----
// string authorizationToken = configuration["PANAXEO_CODING_ARENA_2023_AUTH_TOKEN"] ?? panaxeoCodingArena2023DefaultAuthToken;
// var httpClient = new HttpClient();
// httpClient.BaseAddress = new Uri("https://europe-west1-ca-2023-dev.cloudfunctions.net/battleshipsApi/");
// httpClient.DefaultRequestHeaders.Authorization =
//     new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authorizationToken);
// var gameTarget = new PanaxeoApiGameTarget(httpClient, loggerFactory);

//  ----- Concrete game target initialization sample ----- 
// gameTarget = new ConcreteLocalGameTarget(new ConcreteGameTargetOptions
// {
//     Rows = 12,
//     Columns = 12,
//     MapStateGrid = new[]
//     {
//         0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
//         0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0,
//         0, 0, 0, 0, 9, 0, 0, 0, 0, 0, 2, 0,
//         0, 0, 0, 9, 9, 9, 0, 0, 0, 0, 0, 0,
//         0, 0, 0, 0, 9, 0, 0, 0, 0, 0, 0, 3,
//         0, 0, 0, 9, 9, 9, 0, 0, 4, 0, 0, 3,
//         0, 0, 0, 0, 9, 0, 0, 0, 4, 0, 0, 3,
//         0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0,
//         0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
//         6, 6, 6, 6, 6, 0, 0, 0, 0, 0, 0, 0,
//         0, 0, 0, 0, 0, 0, 0, 0, 5, 5, 5, 5,
//         0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
//     }
// }, loggerFactory);

// ----- Generated game target initialization sample ----- 
var shipPlacementGenerator = new ShipPlacementGenerator();
gameTarget = new GeneratedLocalGameTarget(
    new GeneratedLocalGameTargetOptions
    {
        Rows = Constants.GridRows,
        Columns = Constants.GridColumns,
        MapCount = 200,
    },
    shipPlacementGenerator,
    loggerFactory
);

// ------------------------------------------------
// MATCH PLAYING STRATEGY
// Initialize which game playing strategy to use.
// We use a factory method to create the strategy for each match.
// 3 match playing strategies are available:
// 1. DummyMatchPlayingStrategy       - a strategy that shoots one by one, without remembering the hits
// 2. SimpleMatchPlayingStrategy      - a strategy that shoots randomly, but remembers the hits and tries to sink the ship
// 3. ProbabilityMatchPlayingStrategy - a strategy that calculates the probability of each cell to contain a ship,
//                                      and shoots the cell with the highest probability
// ------------------------------------------------

// var matchPlayingStrategyCreator = new MatchPlayingStrategyCreator(loggerFactory, (lf)
//     => new DummyMatchPlayingStrategy());

// var matchPlayingStrategyCreator = new MatchPlayingStrategyFactory(loggerFactory, (lf)
//     => new SimpleMatchPlayingStrategy(lf));

var matchPlayingStrategyCreator = new MatchPlayingStrategyFactory(loggerFactory, (lf)
    => new ProbabilityMatchPlayingStrategy(lf, onProbabilityGridUpdated: null));

// ------------------------------------------------
// GAME
// Initialize, and play the game
// ------------------------------------------------

const bool isSimulation = true;

var game = new Game(
    gameTarget,
    matchPlayingStrategyCreator,
    loggerFactory,
    isSimulation
);

Console.WriteLine("Starting the game ...");

var gameResults = await game.PlayAsync();

// Write the game result in the console
Console.WriteLine($"Total number of moves: {gameResults.TotalMoveCount}");
Console.WriteLine("Game finished.");