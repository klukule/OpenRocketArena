using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace OpenRocketArena.Server.Services;

public class RtmClient(WebSocket socket, string userId)
{
    public WebSocket Socket { get; } = socket;
    public string UserId { get; } = userId;
    public string Id { get; } = Guid.NewGuid().ToString("N");
}

/// <summary>
/// EA RTM service endpoints - doesn't do anything other than handling connections so client does not spam errors.
/// </summary>
public class RtmService(ILogger<RtmService> logger) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, RtmClient> _clients = new();

    public void AddClient(RtmClient client)
    {
        _clients.TryAdd(client.Id, client);
        logger.LogInformation("[RTM] {UserId} connected ({Count} total)", client.UserId, _clients.Count);
    }

    public void RemoveClient(RtmClient client)
    {
        _clients.TryRemove(client.Id, out _);
        logger.LogInformation("[RTM] {UserId} disconnected ({Count} remaining)", client.UserId, _clients.Count);
    }

    public async Task HandleConnectionAsync(RtmClient client, CancellationToken ct)
    {
        AddClient(client);
        var buffer = new byte[4096];

        try
        {
            while (client.Socket.State == WebSocketState.Open)
            {
                var result = await client.Socket.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                var data = buffer[..result.Count];
                var text = result.MessageType == WebSocketMessageType.Text ? System.Text.Encoding.UTF8.GetString(data) : $"({result.Count} bytes binary)";
                logger.LogInformation("[RTM] {UserId}: {Text}", client.UserId, text);
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            RemoveClient(client);
            if (client.Socket.State == WebSocketState.Open)
                await client.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }
    }

    public async ValueTask DisposeAsync()
    {
        logger.LogInformation("[RTM] Shutting down, closing {Count} connections", _clients.Count);
        foreach (var (_, client) in _clients)
        {
            try
            {
                if (client.Socket.State == WebSocketState.Open)
                    await client.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", CancellationToken.None);
            }
            catch { }
        }
        _clients.Clear();
    }
}
