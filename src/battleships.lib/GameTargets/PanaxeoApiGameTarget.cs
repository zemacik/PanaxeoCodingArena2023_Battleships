using battleships.lib.GameTargets.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace battleships.lib.GameTargets;

/// <summary>
/// Implementation of <see cref="IGameTarget"/> that uses the Panaxeo API.
/// </summary>
public class PanaxeoApiGameTarget : IGameTarget
{
    /// <summary>
    /// The configured HttpClient to use for requests.
    /// </summary>
    private readonly HttpClient _httpClient;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Create a new instance of PanaxeoApiGameTarget.
    /// </summary>
    /// <param name="httpClient">The configured HttpClient for requests panaxeo API</param>
    /// <param name="loggerFactory">The logger factory to use.</param>
    public PanaxeoApiGameTarget(HttpClient httpClient, ILoggerFactory loggerFactory)
    {
        _httpClient = httpClient;
        _logger = loggerFactory.CreateLogger<PanaxeoApiGameTarget>();
    }

    /// <inheritdoc />
    public string Name => "Panaxeo API";

    /// <inheritdoc />
    public async Task<FireResponse> GetFireStatusAsync(bool test = false)
    {
        return await SendRequestAsync<FireResponse>($"fire" + (test ? "?test=yes" : ""));
    }

    /// <inheritdoc />
    public async Task<FireResponse> FireAtPositionAsync(int row, int column, bool test = false)
    {
        return await SendRequestAsync<FireResponse>($"fire/{row}/{column}" + (test ? "?test=yes" : ""));
    }

    /// <inheritdoc />
    public async Task<AvengerFireResponse> FireWithAvengerAsync(int row, int column, string avenger, bool test = false)
    {
        return await SendRequestAsync<AvengerFireResponse>($"fire/{row}/{column}/avenger/{avenger}" +
                                                           (test ? "?test=yes" : ""));
    }

    /// <inheritdoc />
    public async Task<ResetGameResponse> ResetGameAsync(bool test = false)
    {
        return await SendRequestAsync<ResetGameResponse>($"reset" + (test ? "?test=yes" : ""));
    }

    /// <summary>
    /// Generic method for sending requests.
    /// </summary>
    /// <param name="uri">The URI to send the request to.</param>
    private async Task<T> SendRequestAsync<T>(string uri) where T : class
    {
        _logger.LogTrace($"Sending request to {uri}");
        var response = await _httpClient.GetAsync(uri);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            
            _logger.LogTrace($"Received response: {content}");
            
            return JsonConvert.DeserializeObject<T>(content)!;
        }

        _logger.LogWarning($"Received error response: {response.StatusCode}");

        try
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning($"Received error response content: {errorContent}");
        }
        catch (Exception e)
        {
            _logger.LogTrace($"Error while reading error response content: {e.Message}");
        }

        // Handle error response here
        throw new HttpRequestException($"Error: {response.StatusCode}");
    }
}