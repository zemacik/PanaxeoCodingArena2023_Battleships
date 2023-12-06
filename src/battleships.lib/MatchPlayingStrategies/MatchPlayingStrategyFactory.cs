using Microsoft.Extensions.Logging;

namespace battleships.lib.MatchPlayingStrategies;

/// <summary>
/// The factory for creating <see cref="IMatchPlayingStrategy"/> instances.
/// </summary>
public interface IMatchPlayingStrategyFactory
{
    /// <summary>
    /// Creates a new <see cref="IMatchPlayingStrategy"/> instance.
    /// </summary>
    /// <returns>The new <see cref="IMatchPlayingStrategy"/> instance.</returns>
    IMatchPlayingStrategy Create();
}

/// <summary>
///  The factory for creating <see cref="IMatchPlayingStrategy"/> instances.
/// </summary>
public class MatchPlayingStrategyFactory : IMatchPlayingStrategyFactory
{
    /// <summary>
    ///  The logger factory.
    /// </summary>
    private readonly ILoggerFactory _loggerFactory;
    
    /// <summary>
    /// The factory function to create the <see cref="IMatchPlayingStrategy"/> instance.
    /// </summary>
    private readonly Func<ILoggerFactory, IMatchPlayingStrategy> _createFunction;

    /// <summary>
    /// Creates a new <see cref="MatchPlayingStrategyFactory"/> instance.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="createFunction">The factory function to create the <see cref="IMatchPlayingStrategy"/> instance.</param>
    public MatchPlayingStrategyFactory(ILoggerFactory loggerFactory,
        Func<ILoggerFactory, IMatchPlayingStrategy> createFunction)
    {
        _loggerFactory = loggerFactory;
        _createFunction = createFunction;
    }

    /// <inheritdoc />
    public IMatchPlayingStrategy Create()
    {
        return _createFunction(_loggerFactory);
    }
}