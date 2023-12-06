using battleships.lib.MatchPlayingStrategies;
using battleships.lib.Models;

namespace battleships.lib.tests.MatchPlayingStrategyTests;

public class DummyMatchPlayingStrategyTests
{
    [Fact]
    public void Should_return_the_first_grid_cell()
    {
        // Arrange
        var playingStrategy = new DummyMatchPlayingStrategy();
        var state = new MatchState(Constants.GridRows, Constants.GridColumns)
        {
            MatchFinished = false
        };
        
        state.SetGridStateFromString(new string('*', 144));

        // Act
        var nextTarget = playingStrategy.GetNextTarget(state);

        // Assert
        Assert.True(nextTarget.Position.Row == 0);
        Assert.True(nextTarget.Position.Column == 0);
    }

    [Fact]
    public void Should_return_the_third_grid_cell()
    {
        // Arrange
        var playingStrategy = new DummyMatchPlayingStrategy();
        var state = new MatchState(Constants.GridRows, Constants.GridColumns)
        {
            MatchFinished = false
        };
        
        state.SetGridStateFromString("..*.**********************XX.**********************************.***********.***********.***********************************************.********");
        
        // Act
        var nextTarget = playingStrategy.GetNextTarget(state);

        // Assert
        Assert.True(nextTarget.Position.Column == 2);
        Assert.True(nextTarget.Position.Row == 0);
    }

    [Fact]
    public void Should_return_exception_on_already_finished_match_state()
    {
        // Arrange
        var playingStrategy = new DummyMatchPlayingStrategy();
        var state = new MatchState(Constants.GridRows, Constants.GridColumns)
        {
            MatchFinished = true
        };
        
        state.SetGridStateFromString("..*.**********************XX.**********************************.***********.***********.***********************************************.********");
        
        // Act
        var act = () => playingStrategy.GetNextTarget(state);

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Equal("Match is already finished.", exception.Message);
    }

    [Fact]
    public void Should_return_exception_if_no_fields_are_hidden()
    {
        // Arrange
        var playingStrategy = new DummyMatchPlayingStrategy();
        var state = new MatchState(Constants.GridRows, Constants.GridColumns)
        {
            MatchFinished = false
        };
        
        state.SetGridStateFromString("................................................................................................................................................");
        
        // Act
        var act = () => playingStrategy.GetNextTarget(state);

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Equal("Match is already finished.", exception.Message);
    }
}