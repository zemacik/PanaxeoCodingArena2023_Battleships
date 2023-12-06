using battleships.app.Helpers;
using battleships.lib;
using battleships.lib.GameTargets;
using battleships.lib.MatchPlayingStrategies;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Spectre.Console;
using Constants = battleships.lib.Constants;

const bool isSimulation = false;

// some infrastructure initialization
var serilog = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.File("log.txt")
    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Warning)
    .CreateLogger();

using var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(serilog));

// Game target initialization
// ------------------------------------------------
// Initialize which game target to use. Game target is something like server we are targetting
// We have 3 game targets:
// 1. ConcreteLocalGameTarget  - a local game target with a concrete map
// 2. GeneratedLocalGameTarget - a local game target which generates random maps
// 3. PanaxeoApiGameTarget     - a game target that uses Panaxeo API
// --
// In this case we use GeneratedLocalGameTarget which generates random maps and will play 50 matches.

// ----- PanaxeoApiGameTarget initialization -----
// var authorizationToken = "PUT_TOKEN_HERE";
// var httpClient = new HttpClient();
// httpClient.BaseAddress = new Uri("https://europe-west1-ca-2023-dev.cloudfunctions.net/battleshipsApi/");
// httpClient.DefaultRequestHeaders.Authorization =
//     new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authorizationToken);
// IGameTarget gameTarget = new PanaxeoApiGameTarget(httpClient, loggerFactory);

// ----- GeneratedLocalGameTarget initialization -----
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

// UI initialization
var layout = new Layout("Root")
    .SplitColumns(
        new Layout("Left")
            .SplitRows(
                new Layout("LeftTop"),
                new Layout("leftBottom")),
        new Layout("Right").Size(73)
            .SplitRows(
                new Layout("RightTop"),
                new Layout("RightBottom")));

var battleCaption = new FigletText("Battle").LeftJustified().Color(Color.Blue);
var shipCaption = new FigletText("ships").RightJustified().Color(Color.LightSkyBlue3);
layout["LeftTop"].Update(
    new Panel(new Rows(battleCaption, shipCaption)).Header(" Panaxeo ").NoBorder().Expand());

LiveDisplayContext? context = null;

void RenderMatchData(MatchStateArgs args, LiveDisplayContext? ctx)
{
    if (ctx is null)
    {
        return;
    }

    var table = UiRenderHelpers.PrettyPrintGameCells(args.Cells, args.Rows, args.Columns);
    var canvas = UiRenderHelpers.PrettyPrintGameCellsToCanvas(args.Cells, args.Rows, args.Columns);

    layout["RightTop"].Update(
        new Panel(new Columns(new Panel(table).NoBorder(), new Panel(canvas).NoBorder()))
            .Header("[Yellow] Battlefield [/]").Expand());

    var statsPanel = UiRenderHelpers.PrettyPrintMatchStats(args);
    layout["leftBottom"].Update(statsPanel.Expand());

    ctx.Refresh();

    // In order to slow down the simulation, and see the UI updates, we sleep for a few milliseconds
    // also the anti-flickering hack :-)
    Thread.Sleep(50);
}

// Initialize which game playing strategy to use.
// ------------------------------------------------
// We use a factory method to create the strategy for each match.
// in this case we use ProbabilityMatchPlayingStrategy which calculates the probability of each cell to contain a ship
// we also pass a callback to be called when the probabilities are updated, so we can render them to the UI.
// 3 match playing strategies are available:
// 1. DummyMatchPlayingStrategy       - a strategy that shoots one by one, without remembering the hits
// 2. SimpleMatchPlayingStrategy      - a strategy that shoots randomly, but remembers the hits and tries to sink the ship
// 3. ProbabilityMatchPlayingStrategy - a strategy that calculates the probability of each cell to contain a ship,
//                                      and shoots the cell with the highest probability
var matchPlayingStrategyCreator = new MatchPlayingStrategyFactory(loggerFactory, (lf)
    => new ProbabilityMatchPlayingStrategy(lf, args =>
    {
        if (context is null) return;
        
        // Refresh the probabilities UI
        var probabilitiesTable = UiRenderHelpers.PrettyPrintProbabilities(args.Probabilities, args.Rows, args.Columns);
        layout["RightBottom"].Update(new Panel(probabilitiesTable).Header("[Yellow] Probabilities [/]").Expand());
    }));

// Initialize, and play the game
// ------------------------------------------------
var game = new Game(
    gameTarget,
    matchPlayingStrategyCreator,
    loggerFactory,
    isSimulation: isSimulation,
    args => RenderMatchData(args, context),
    args => RenderMatchData(args, context),
    args => RenderMatchData(args, context)
);

await AnsiConsole.Live(layout)
    .AutoClear(false)
    .StartAsync(async ctx =>
    {
        context = ctx;

        // 🤡 I WANT TO PLAY A GAME. Shall we?
        await game.PlayAsync();
    });

Console.ReadKey();