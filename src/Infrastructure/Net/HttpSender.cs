using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiToEverything.Core.Application.Ports;

namespace MidiToEverything.Infrastructure.Net;

/// <summary>HTTP / webhook sender (docs/05 §5, Phase 3). Fire-and-forget; never blocks the caller.</summary>
public sealed class HttpSender : IHttpSender
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly ILogger<HttpSender> _logger;

    public HttpSender(ILogger<HttpSender>? logger = null)
        => _logger = logger ?? NullLogger<HttpSender>.Instance;

    public void Send(string url, string method, string? body)
    {
        _ = SendAsync(url, method, body);
    }

    private async Task SendAsync(string url, string method, string? body)
    {
        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(string.IsNullOrWhiteSpace(method) ? "GET" : method.ToUpperInvariant()), url);
            if (!string.IsNullOrEmpty(body))
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            using var response = await Client.SendAsync(request).ConfigureAwait(false);
            _logger.LogDebug("HTTP {Method} {Url} -> {Status}", method, url, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HTTP {Method} {Url} failed", method, url);
        }
    }
}
