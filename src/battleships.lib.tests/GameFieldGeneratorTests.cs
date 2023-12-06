using battleships.lib.GameTargets;

namespace battleships.lib.tests;

using Xunit;

public class GameFieldGeneratorTests
{
    [Fact]
    public void TestGenerate()
    {
        var shipGenerator = new ShipPlacementGenerator();
        var gameField = shipGenerator.Generate(12, 12);
        Assert.NotNull(gameField);
        Assert.Equal(12, gameField.GetLength(0));
        Assert.Equal(12, gameField.GetLength(1));
    }

    [Fact]
    public void TestShipPlacement()
    {
        var shipGenerator = new ShipPlacementGenerator();
        var gameField = shipGenerator.Generate(12, 12);

        var rows = gameField.GetLength(0);
        var cols = gameField.GetLength(1);

        // A dictionary to keep track of which ship parts we've already seen
        var shipPartsSeen = new Dictionary<int, bool>();

        // Check that there are no overlapping ships
        for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
        {
            var currentCell = gameField[r, c];
            if (currentCell != 0)
            {
                // If we haven't encountered this part of the ship before
                if (!shipPartsSeen.ContainsKey(currentCell))
                {
                    shipPartsSeen[currentCell] = true;
                    CheckShipPlacement(gameField, r, c, rows, cols, currentCell);
                }
            }
        }
    }

    [Fact]
    public void TestShipCount()
    {
        var shipGenerator = new ShipPlacementGenerator();
        var gameField = shipGenerator.Generate(12, 12);

        // Count the number of ship parts
        var count = 0;
        for (var i = 0; i < gameField.GetLength(0); i++)
        for (var j = 0; j < gameField.GetLength(1); j++)
        {
            if (gameField[i, j] != 0)
            {
                count++;
            }
        }

        // Check that the total number of ship parts matches the sum of the sizes of all ships
        Assert.Equal(5 + 4 + 3 + 3 + 2 + 9, count);
    }

    private void CheckShipPlacement(int[,] gameField, int startRow, int startCol, int totalRows, int totalCols,
        int shipType)
    {
        // Iterate through each cell in the game field
        for (var r = 0; r < totalRows; r++)
        for (var c = 0; c < totalCols; c++)
        {
            // Check only the cells that are part of the current ship
            if (gameField[r, c] == shipType)
            {
                // Check all adjacent cells
                for (var y = -1; y <= 1; y++)
                for (var x = -1; x <= 1; x++)
                {
                    var checkRow = r + y;
                    var checkCol = c + x;

                    // Boundary check for adjacent cells
                    if (checkRow >= 0 && checkRow < totalRows && checkCol >= 0 && checkCol < totalCols)
                    {
                        // If it's an adjacent cell and not part of the same ship, it should be water (0)
                        if (!(checkRow == r && checkCol == c) && gameField[checkRow, checkCol] != 0 &&
                            gameField[checkRow, checkCol] != shipType)
                        {
                            Assert.Fail($"Ship placement violation at [{checkRow},{checkCol}].");
                        }
                    }
                }
            }
        }
    }
}