using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.Infrastructure.Obs;

/// <summary>
/// obs-websocket v5 client adapter for <see cref="IObsClient"/> (docs/05 §5, Phase 3). Connects
/// lazily on first use, performs the Hello/Identify handshake (with SHA256 auth when the server
/// requires it), then sends fire-and-forget requests. A background loop drains responses/events.
/// </summary>
public sealed class ObsWebSocketClient : IObsClient, IDisposable
{
    private readonly Func<ObsConnection> _connection;
    private readonly ILogger<ObsWebSocketClient> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private volatile bool _identified;

    public ObsWebSocketClient(Func<ObsConnection> connection, ILogger<ObsWebSocketClient>? logger = null)
    {
        _connection = connection;
        _logger = logger ?? NullLogger<ObsWebSocketClient>.Instance;
    }

    public void Send(ObsOp op, string? arg) => _ = SendAsync(op, arg);

    /// <summary>obs-websocket v5 auth: base64(sha256(base64(sha256(password+salt))+challenge)).</summary>
    public static string ComputeAuth(string password, string salt, string challenge)
    {
        var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
    }

    private async Task SendAsync(ObsOp op, string? arg)
    {
        try
        {
            await EnsureConnectedAsync().ConfigureAwait(false);
            if (!_identified || _ws is not { State: WebSocketState.Open })
            {
                return;
            }

            var (requestType, data) = MapRequest(op, arg);
            var request = new JsonObject
            {
                ["op"] = 6,
                ["d"] = new JsonObject
                {
                    ["requestType"] = requestType,
                    ["requestId"] = Guid.NewGuid().ToString("N"),
                    ["requestData"] = data,
                },
            };
            await SendRawAsync(request.ToJsonString()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OBS send {Op} failed", op);
            Reset();
        }
    }

    private async Task EnsureConnectedAsync()
    {
        if (_identified && _ws is { State: WebSocketState.Open })
        {
            return;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_identified && _ws is { State: WebSocketState.Open })
            {
                return;
            }

            Reset();
            var c = _connection();
            var ws = new ClientWebSocket();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await ws.ConnectAsync(new Uri($"ws://{c.Host}:{c.Port}"), cts.Token).ConfigureAwait(false);
            _ws = ws;
            _cts = new CancellationTokenSource();

            var hello = await ReceiveJsonAsync(cts.Token).ConfigureAwait(false);
            var identifyData = new JsonObject { ["rpcVersion"] = 1 };
            if (hello?["d"]?["authentication"] is JsonObject auth)
            {
                var salt = auth["salt"]?.GetValue<string>() ?? "";
                var challenge = auth["challenge"]?.GetValue<string>() ?? "";
                identifyData["authentication"] = ComputeAuth(c.Password, salt, challenge);
            }

            await SendRawAsync(new JsonObject { ["op"] = 1, ["d"] = identifyData }.ToJsonString()).ConfigureAwait(false);

            var identified = await ReceiveJsonAsync(cts.Token).ConfigureAwait(false);
            if (identified?["op"]?.GetValue<int>() == 2)
            {
                _identified = true;
                _ = DrainAsync(_cts.Token);
                _logger.LogInformation("OBS connected to {Host}:{Port}", c.Host, c.Port);
            }
            else
            {
                _logger.LogWarning("OBS identify rejected (check password / rpcVersion)");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    // Keep reading so the socket stays healthy; we don't act on responses/events.
    private async Task DrainAsync(CancellationToken ct)
    {
        try
        {
            while (_ws is { State: WebSocketState.Open })
            {
                await ReceiveJsonAsync(ct).ConfigureAwait(false);
            }
        }
        catch
        {
            // connection ended; next Send reconnects
        }
        finally
        {
            _identified = false;
        }
    }

    private async Task SendRawAsync(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws!.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None)
            .ConfigureAwait(false);
    }

    private async Task<JsonNode?> ReceiveJsonAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await _ws!.ReceiveAsync(buffer, ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            ms.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return JsonNode.Parse(Encoding.UTF8.GetString(ms.ToArray()));
    }

    private static (string RequestType, JsonObject Data) MapRequest(ObsOp op, string? arg) => op switch
    {
        ObsOp.SceneSwitch => ("SetCurrentProgramScene", new JsonObject { ["sceneName"] = arg ?? "" }),
        ObsOp.ToggleRecord => ("ToggleRecord", new JsonObject()),
        ObsOp.ToggleStream => ("ToggleStream", new JsonObject()),
        ObsOp.ToggleRecordPause => ("ToggleRecordPause", new JsonObject()),
        ObsOp.ToggleMute => ("ToggleInputMute", new JsonObject { ["inputName"] = arg ?? "" }),
        ObsOp.StartRecord => ("StartRecord", new JsonObject()),
        ObsOp.StopRecord => ("StopRecord", new JsonObject()),
        ObsOp.StartStream => ("StartStream", new JsonObject()),
        ObsOp.StopStream => ("StopStream", new JsonObject()),
        _ => ("", new JsonObject()),
    };

    private void Reset()
    {
        _identified = false;
        try { _cts?.Cancel(); } catch { /* ignore */ }
        _cts?.Dispose();
        _cts = null;
        _ws?.Dispose();
        _ws = null;
    }

    public void Dispose() => Reset();
}
