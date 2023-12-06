using battleships.lib.MatchPlayingStrategies;
using battleships.lib.Models;
using Microsoft.Extensions.Logging;

namespace battleships.lib.tests.MatchPlayingStrategyTests;

public class SimpleMatchPlayingStrategyTests
{
    [Fact]
    public void Should_return_the_first_grid_cell() // Dummy test
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        var playingStrategy = new SimpleMatchPlayingStrategy(loggerFactory);
        var state = new MatchState(Constants.GridRows, Constants.GridColumns)
        {
            MatchFinished = false
        };

        state.SetGridStateFromString(new string('*', 144));

        // Act
        var nextTarget = playingStrategy.GetNextTarget(state);

        // STUPID TEST
        // Assert
        Assert.True(nextTarget.Position.Row == 0);
        Assert.True(nextTarget.Position.Column == 0);
    }
}