using battleships.lib;
using battleships.lib.MatchPlayingStrategies.Models;

using Spectre.Console;
using Spectre.Console.Rendering;

namespace battleships.app.Helpers;

public static class UiRenderHelpers
{
    /// <summary>
    /// Pretty print the probabilities grid. 
    /// </summary>
    /// <param name="grid">The probabilities grid. Rows x Columns</param>
    /// <param name="rows">The number of rows</param>
    /// <param name="columns">The number of columns</param>
    /// <returns>The table object to be rendered</returns>
    public static Table PrettyPrintProbabilities(int[] grid, int rows, int columns)
    {
        var table = new Table().NoBorder();

        table.AddColumn("No.", c => c.RightAligned());
        for (var column = 0; column < columns; column++)
        {
            table.AddColumn(column.ToString(), c => c.RightAligned());
        }

        for (var row = 0; row < rows; row++)
        {
            var items = new List<IRenderable>();
            items.Add(new Text(row.ToString()));

            for (var column = 0; column < columns; column++)
            {
                // give two digits string
                var value = grid[row * columns + column];
                var grayColor = CalculateGrayColor(value, maxValue: 52.0);

                var strToAppend = value.ToString().PadLeft(2, ' ');
                var coloredText = new Markup($"[{grayColor}]{strToAppend}[/]");

                items.Add(coloredText);
            }

            table.AddRow(items);
        }

        return table;
    }

    /// <summary>
    /// Pretty print the game cells as grid object. 
    /// </summary>
    /// <param name="cells">The game field grid. Rows x Columns</param>
    /// <param name="rows">The number of rows</param>
    /// <param name="columns">The number of columns</param>
    /// <returns>The grid object to be rendered</returns>    
    public static Grid PrettyPrintGameCells(CellState[] cells, int rows, int columns)
    {
        var grid = new Grid();
        grid.AddColumn();

        for (var column = 0; column < columns; column++)
        {
            grid.AddColumn();
        }

        for (var row = 0; row < rows; row++)
        {
            var items = new List<IRenderable>();
            items.Add(new Markup(row.ToString()));

            for (var column = 0; column < columns; column++)
            {
                var value = cells[row * columns + column];

                var strToAppend = value switch
                {
                    CellState.Unknown => $"[gray]*[/]",
                    CellState.Water => $"[blue].[/]",
                    CellState.Ship => $"[green]X[/]",
                    _ => throw new ArgumentOutOfRangeException()
                };

                var coloredText = new Markup(strToAppend).RightJustified();

                items.Add(coloredText);
            }

            grid.AddRow(items.ToArray());
        }

        return grid;
    }

    /// <summary>
    /// Pretty print the game cells to a canvas.
    /// </summary>
    /// <param name="cells">The game field grid. Rows x Columns</param>
    /// <param name="rows">The number of rows</param>
    /// <param name="columns">The number of columns</param>
    /// <returns>The canvas object to be rendered</returns>    
    public static Canvas PrettyPrintGameCellsToCanvas(CellState[] cells, int rows, int columns)
    {
        var canvas = new Canvas(columns, rows);

        for (var column = 0; column < columns; column++)
            for (var row = 0; row < rows; row++)
            {
                var value = cells[row * columns + column];

                var color = value switch
                {
                    CellState.Unknown => Color.LightSlateGrey,
                    CellState.Water => Color.Blue,
                    CellState.Ship => Color.Green,
                    _ => throw new ArgumentOutOfRangeException()
                };

                canvas.SetPixel(column, row, color);
            }

        return canvas;
    }

    /// <summary>
    /// Renders the game information panel.
    /// </summary>
    /// <param name="args">The match state</param>
    /// <returns>The panel object to be rendered</returns>
    public static Panel PrettyPrintMatchStats(MatchStateArgs args)
    {
        var matchStatus = args.MatchFinished ? "Finished   " : "In progress";

        var table = new Table().NoBorder().HideHeaders().Expand();
        table.AddColumn("");
        table.AddColumn("");

        table.AddRow("Game target", args.GameTarget);
        table.AddRow("Playing strategy", args.PlayingStrategy);
        table.AddRow("Map ID", args.MapId.ToString());
        table.AddRow("Match move count", args.MatchMoveCount.ToString());
        table.AddRow("Total move count", args.TotalMoveCount.ToString());

        var avg = args.MapId == 0 ? 0 : Math.Round((args.TotalMoveCount - args.MatchMoveCount) / (double)args.MapId, 0);
        table.AddRow("Avg. moves per match", avg.ToString());
        table.AddRow("Status", matchStatus);
        //table.AddEmptyRow();

        table.AddRow("Map", $"{args.MapId + 1} of {args.MapCount}");

        var progressValue = (int)((args.MapId + 1) / (double)args.MapCount * 100.0);
        var progress = new BarChart()
            .AddItem("Progress (%)", progressValue, Color.SteelBlue);
        progress.MaxValue = 100;

        var rows = new Rows(table, progress).Expand();

        var panel = new Panel(rows)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("yellow"),
            Header = new PanelHeader($"[Yellow] Game information [/]")
        };

        return panel.Expand();
    }

    /// <summary>
    /// Helper method to calculate the gray color 
    /// </summary>
    /// <param name="value"> The value to calculate the gray color for</param>
    /// <param name="maxValue">The maximum possible value for the value</param> 
    /// <returns>The hex color string</returns>
    private static string CalculateGrayColor(int value, double maxValue)
    {
        // do not use complete black or white
        var minGray = 50;
        var maxGray = 225;

        var normalizedValue = value / maxValue;
        var grayValue = (int)(minGray + normalizedValue * (maxGray - minGray));
        var grayHex = grayValue.ToString("X2");

        return $"#{grayHex}{grayHex}{grayHex}";
    }
}