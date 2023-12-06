
namespace battleships.lib.tests.Models;

using battleships.lib.MatchPlayingStrategies.Models;
using Xunit;

public class GameFieldTests
{
    [Fact]
    public void TestGetFieldStatus()
    {
        var gameField = new GameField<ServerCellValue>(3, 3);
        var position = new GameCellPosition(1, 1);
        
        gameField.SetCellStatus(position, new ServerCellValue(CellState.Ship, 1));

        Assert.Equal(CellState.Ship, gameField.GetCellStatus(position).CellState);
    }

    [Fact]
    public void TestSetFieldStatus()
    {
        var gameField = new GameField<ServerCellValue>(3, 3);
        var position = new GameCellPosition(1, 1);
        
        gameField.SetCellStatus(position, new ServerCellValue(CellState.Ship, 1));

        Assert.Equal(CellState.Ship, gameField.GetCellStatus(position).CellState);
    }

    [Fact]
    public void TestGetFieldPosition()
    {
        var gameField = new GameField<ServerCellValue>(3, 3);
        var position = gameField.GetCellPosition(4);
        
        Assert.Equal(1, position.Row);
        Assert.Equal(1, position.Column);
    }

    [Fact]
    public void TestHasPreviousField()
    {
        var gameField = new GameField<ServerCellValue>(3, 3);
        var position = new GameCellPosition(1, 1);
        
        Assert.True(gameField.HasPreviousCell(position));
    }

    [Fact]
    public void TestHasNextField()
    {
        var gameField = new GameField<ServerCellValue>(3, 3);
        var position = new GameCellPosition(1, 1);
        Assert.True(gameField.HasNextCell(position));
    }

    [Fact]
    public void TestHasAboveField()
    {
        var gameField = new GameField<ServerCellValue>(3, 3);
        var position = new GameCellPosition(1, 1);
        Assert.True(gameField.HasAboveCell(position));
    }

    [Fact]
    public void TestHasBelowField()
    {
        var gameField = new GameField<ServerCellValue>(3, 3);
        var position = new GameCellPosition(1, 1);
        Assert.True(gameField.HasBelowCell(position));
    }
    
    [Fact]
    public void TestHasNotPreviousField()
    {
        var gameField = new GameField<ServerCellValue>(3, 3);
        var position = new GameCellPosition(2, 0);
        Assert.False(gameField.HasPreviousCell(position));
    }
    
    [Fact]
    public void TestHasNotNextField()
    {
        var gameField = new GameField<ServerCellValue>(3, 3);
        var position = new GameCellPosition(2, 2);
        Assert.False(gameField.HasNextCell(position));
    }
    
    [Fact]
    public void TestHasNotAboveField()
    {
        var gameField = new GameField<ServerCellValue>(3, 3);
        var position = new GameCellPosition(0, 1);
        Assert.False(gameField.HasAboveCell(position));
    }
    
    [Fact]
    public void TestHasNotBelowField()
    {
        var gameField = new GameField<ServerCellValue>(3, 3);
        var position = new GameCellPosition(2, 1);
        Assert.False(gameField.HasBelowCell(position));
    }
}