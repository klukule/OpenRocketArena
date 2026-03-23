using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using OpenRocketArena.Server.Models;

namespace OpenRocketArena.Server.Services;

public class LobbyClient(WebSocket socket, string displayName, string playerId, string platformName, string platformUserId)
{
    public WebSocket Socket { get; } = socket;
    public string DisplayName { get; } = displayName;
    public string PlayerId { get; } = playerId;
    public string PlatformName { get; } = platformName;
    public string PlatformUserId { get; } = platformUserId;
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? PartyId { get; set; }
    public string SessionId { get; } = Guid.NewGuid().ToString("N");
}

public class Party
{
    public string PartyId { get; } = Guid.NewGuid().ToString("N");
    public string LeaderId { get; set; } = string.Empty;
    public List<string> Members { get; set; } = [];
    public List<string> InvitedPlayers { get; set; } = [];
    public string Data { get; set; } = "{}";
    public string Token { get; set; } = string.Empty;
    public ConcurrentDictionary<string, string> UserData { get; } = new();
}

public class LobbyService(ILogger<LobbyService> logger) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, LobbyClient> _clients = new();

    // playerId -> LobbyClient for quick lookup
    private readonly ConcurrentDictionary<string, LobbyClient> _playerClients = new();
    private readonly ConcurrentDictionary<string, Party> _parties = new();
    private readonly ConcurrentDictionary<string, string> _playerPartyMap = new();

    public void AddClient(LobbyClient client)
    {
        _clients.TryAdd(client.Id, client);
        _playerClients[client.PlayerId] = client;
        logger.LogInformation("[Lobby] {DisplayName} ({PlayerId}) connected ({Count} total)", client.DisplayName, client.PlayerId, _clients.Count);
    }

    public void RemoveClient(LobbyClient client)
    {
        _clients.TryRemove(client.Id, out _);
        _playerClients.TryRemove(client.PlayerId, out _);

        if (client.PartyId != null && _parties.TryGetValue(client.PartyId, out var party))
        {
            party.Members.Remove(client.PlayerId);
            party.UserData.TryRemove(client.PlayerId, out _);
            _playerPartyMap.TryRemove(client.PlayerId, out _);

            // Notify remaining members
            _ = BroadcastToPartyAsync(party, client.PlayerId, new LobbyLeavePartyAnnouncement
            {
                MsgType = "/leave_notice",
                PlayerId = client.PlayerId,
                PartyId = party.PartyId
            });

            // Promote new leader if leader left
            if (party.LeaderId == client.PlayerId && party.Members.Count > 0)
            {
                party.LeaderId = party.Members[0];
                _ = BroadcastToPartyAsync(party, null, new LobbyPromotePartyAnnouncement
                {
                    MsgType = "/promote_notice",
                    PlayerId = party.LeaderId,
                    PartyId = party.PartyId
                });
            }

            if (party.Members.Count == 0)
                _parties.TryRemove(client.PartyId, out _);
        }

        logger.LogInformation("[Lobby] {DisplayName} disconnected ({Count} remaining)", client.DisplayName, _clients.Count);
    }

    public async Task HandleConnectionAsync(LobbyClient client, CancellationToken ct)
    {
        AddClient(client);
        await SendInfoAsync(client);

        var buffer = new byte[8192];
        try
        {
            while (client.Socket.State == WebSocketState.Open)
            {
                var result = await client.Socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                logger.LogInformation("[Lobby] {DisplayName}: {Text}", client.DisplayName, text);
                await HandleMessageAsync(client, text);
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

    private async Task HandleMessageAsync(LobbyClient client, string text)
    {
        var msg = JsonSerializer.Deserialize<LobbyMessage>(text);
        if (msg == null || string.IsNullOrEmpty(msg.MsgType)) return;

        switch (msg.MsgType)
        {
            case "/info":
                await SendInfoAsync(client);
                break;
            case "/create":
                await HandleCreateAsync(client, text);
                break;
            case "/invite":
                await HandleInviteAsync(client, text);
                break;
            case "/join":
            case "/accept":
                await HandleJoinAsync(client, text, msg.MsgType);
                break;
            case "/reject":
                await HandleRejectAsync(client, text);
                break;
            case "/leave":
                await HandleLeaveAsync(client);
                break;
            case "/kick":
                await HandleKickAsync(client, text);
                break;
            case "/promote":
                await HandlePromoteAsync(client, text);
                break;
            case "/setdata":
                await HandleSetDataAsync(client, text);
                break;
            case "/setuserdata":
                await HandleSetUserDataAsync(client, text);
                break;
            default:
                logger.LogWarning("[Lobby] Unknown message type: {MsgType}\n   Body: {Body}", msg.MsgType, text);
                break;
        }
    }

    // --- /info ---

    private async Task SendInfoAsync(LobbyClient client)
    {
        if (_playerPartyMap.TryGetValue(client.PlayerId, out var partyId) && _parties.TryGetValue(partyId, out var party))
        {
            await SendAsync(client, new LobbyInfoResponse
            {
                MsgType = "/info",
                Message = "Party info acquired",
                PartyId = party.PartyId,
                LeaderId = party.LeaderId,
                PartyMembers = party.Members.ToList(),
                InvitedPlayers = party.InvitedPlayers.ToList(),
                Data = party.Data,
                UserData = party.UserData.ToDictionary(kv => kv.Key, kv => kv.Value),
                Token = party.Token,
                Result = true,
                Code = 200,
                SessionId = client.SessionId
            });
        }
        else
        {
            await SendAsync(client, new LobbyInfoResponse
            {
                MsgType = "/info",
                Message = "Party info acquired (no party)",
                Result = true,
                Code = 200,
                SessionId = client.SessionId
            });
        }
    }

    // --- /create ---

    private async Task HandleCreateAsync(LobbyClient client, string text)
    {
        var request = JsonSerializer.Deserialize<LobbyCreateRequest>(text);
        var token = request?.Token ?? string.Empty;

        // Leave existing party if in one
        await LeaveCurrentParty(client);

        var party = new Party
        {
            LeaderId = client.PlayerId,
            Members = [client.PlayerId],
            Token = token
        };

        var initialUserData = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["displayName"] = client.DisplayName,
            ["platformName"] = client.PlatformName,
            ["platformUserId"] = client.PlatformUserId
        });
        party.UserData[client.PlayerId] = initialUserData;

        _parties[party.PartyId] = party;
        _playerPartyMap[client.PlayerId] = party.PartyId;
        client.PartyId = party.PartyId;

        logger.LogInformation("[Lobby] Party {PartyId} created by {PlayerId}", party.PartyId, client.PlayerId);

        await SendAsync(client, new LobbyCreateResponse
        {
            MsgType = "/create",
            PartyId = party.PartyId,
            LeaderId = party.LeaderId,
            PartyMembers = party.Members.ToList(),
            InvitedPlayers = [],
            Data = party.Data,
            UserData = party.UserData.ToDictionary(kv => kv.Key, kv => kv.Value),
            Token = token,
            Message = "Party created",
            Result = true,
            Code = 200
        });
    }

    // --- /invite ---

    private async Task HandleInviteAsync(LobbyClient client, string text)
    {
        var request = JsonSerializer.Deserialize<LobbyInviteRequest>(text);
        var friendId = request?.FriendId ?? "";

        if (client.PartyId == null || !_parties.TryGetValue(client.PartyId, out var party))
        {
            await SendAsync(client, new LobbyInviteResponse
            {
                MsgType = "/invite",
                FriendId = friendId,
                Message = "Not in a party",
                Result = false,
                Code = 400
            });
            return;
        }

        if (!party.InvitedPlayers.Contains(friendId))
            party.InvitedPlayers.Add(friendId);

        logger.LogInformation("[Lobby] {PlayerId} invited {FriendId} to party {PartyId}", client.PlayerId, friendId, party.PartyId);

        // Send response to inviter
        await SendAsync(client, new LobbyInviteResponse
        {
            MsgType = "/invite",
            FriendId = friendId,
            Message = "Invite sent",
            Result = true,
            Code = 200
        });

        // Send invite_notice to the invited friend if online
        if (_playerClients.TryGetValue(friendId, out var friendClient))
        {
            await SendAsync(friendClient, new LobbyPartyInvitationAnnouncement
            {
                MsgType = "/invite_notice",
                PlayerId = client.PlayerId,
                FriendId = friendId,
                PartyId = party.PartyId
            });
        }
    }

    // --- /join and /accept (both use JoinResponse) ---

    private async Task HandleJoinAsync(LobbyClient client, string text, string msgType)
    {
        var request = JsonSerializer.Deserialize<LobbyJoinRequest>(text);
        var token = request?.Token ?? "";

        // Find the party this player was invited to
        Party? targetParty = null;
        string inviterId = "";

        foreach (var (_, party) in _parties)
        {
            if (party.InvitedPlayers.Contains(client.PlayerId) || party.Token == token)
            {
                targetParty = party;
                inviterId = party.LeaderId;
                break;
            }
        }

        if (targetParty == null)
        {
            await SendAsync(client, new LobbyJoinResponse
            {
                MsgType = msgType,
                Message = "No party to join",
                Result = false,
                Code = 404
            });
            return;
        }

        // Leave existing party
        await LeaveCurrentParty(client);

        // Join the target party
        targetParty.Members.Add(client.PlayerId);
        targetParty.InvitedPlayers.Remove(client.PlayerId);
        _playerPartyMap[client.PlayerId] = targetParty.PartyId;
        client.PartyId = targetParty.PartyId;

        // Set initial user data
        var userData = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["displayName"] = client.DisplayName,
            ["platformName"] = client.PlatformName,
            ["platformUserId"] = client.PlatformUserId
        });
        targetParty.UserData[client.PlayerId] = userData;

        logger.LogInformation("[Lobby] {PlayerId} joined party {PartyId}", client.PlayerId, targetParty.PartyId);

        // Send response to joiner
        await SendAsync(client, new LobbyJoinResponse
        {
            MsgType = msgType,
            PartyId = targetParty.PartyId,
            LeaderId = targetParty.LeaderId,
            PartyMembers = targetParty.Members.ToList(),
            InvitedPlayers = targetParty.InvitedPlayers.ToList(),
            Data = targetParty.Data,
            UserData = targetParty.UserData.ToDictionary(kv => kv.Key, kv => kv.Value),
            Token = targetParty.Token,
            InviterId = inviterId,
            Message = "Party joined",
            Result = true,
            Code = 200
        });

        // Notify party members
        var playerPresence = JsonSerializer.Deserialize<Dictionary<string, string>>(userData) ?? new();
        if (msgType == "/accept")
        {
            await BroadcastToPartyAsync(targetParty, client.PlayerId, new LobbyAcceptPartyInvitationAnnouncement
            {
                MsgType = "/accept_notice",
                PartyId = targetParty.PartyId,
                PlayerId = client.PlayerId,
                PlayerPresence = playerPresence
            });
        }
        else
        {
            await BroadcastToPartyAsync(targetParty, client.PlayerId, new LobbyJoinPartyAnnouncement
            {
                MsgType = "/join_notice",
                PartyId = targetParty.PartyId,
                PlayerId = client.PlayerId,
                PlayerPresence = playerPresence
            });
        }
    }

    // --- /reject ---

    private async Task HandleRejectAsync(LobbyClient client, string text)
    {
        // Find and remove from invited lists
        foreach (var (_, party) in _parties)
        {
            if (party.InvitedPlayers.Remove(client.PlayerId))
            {
                // Notify party of decline
                await BroadcastToPartyAsync(party, client.PlayerId, new LobbyLeavePartyAnnouncement
                {
                    MsgType = "/decline_notice",
                    PlayerId = client.PlayerId,
                    PartyId = party.PartyId
                });
                break;
            }
        }

        // Reject uses JoinResponse
        await SendAsync(client, new LobbyJoinResponse
        {
            MsgType = "/reject",
            Message = "Invitation rejected",
            Result = true,
            Code = 200
        });
    }

    // --- /leave ---

    private async Task HandleLeaveAsync(LobbyClient client)
    {
        await LeaveCurrentParty(client);

        await SendAsync(client, new LobbyLeaveResponse
        {
            MsgType = "/leave",
            Message = "Party left",
            Result = true,
            Code = 200
        });
    }

    // --- /kick ---

    private async Task HandleKickAsync(LobbyClient client, string text)
    {
        var request = JsonSerializer.Deserialize<LobbyKickRequest>(text);
        var friendId = request?.FriendId ?? "";

        if (client.PartyId == null || !_parties.TryGetValue(client.PartyId, out var party))
        {
            await SendAsync(client, new LobbyKickResponse
            {
                MsgType = "/kick",
                FriendId = friendId,
                Message = "Not in a party",
                Result = false,
                Code = 400
            });
            return;
        }

        if (party.LeaderId != client.PlayerId)
        {
            await SendAsync(client, new LobbyKickResponse
            {
                MsgType = "/kick",
                FriendId = friendId,
                Message = "Not party leader",
                Result = false,
                Code = 403
            });
            return;
        }

        // Remove the kicked player
        party.Members.Remove(friendId);
        party.UserData.TryRemove(friendId, out _);
        _playerPartyMap.TryRemove(friendId, out _);

        if (_playerClients.TryGetValue(friendId, out var kickedClient))
            kickedClient.PartyId = null;

        logger.LogInformation("[Lobby] {PlayerId} kicked {FriendId} from party {PartyId}", client.PlayerId, friendId, party.PartyId);

        // Respond to kicker
        await SendAsync(client, new LobbyKickResponse
        {
            MsgType = "/kick",
            FriendId = friendId,
            Message = "Player kicked",
            Result = true,
            Code = 200
        });

        // Notify all party members including kicked player
        var announcement = new LobbyKickPartyAnnouncement
        {
            MsgType = "/kick_notice",
            PlayerId = client.PlayerId,
            FriendId = friendId,
            PartyId = party.PartyId
        };

        await BroadcastToPartyAsync(party, null, announcement);
        if (kickedClient != null)
            await SendAsync(kickedClient, announcement);
    }

    // --- /promote ---

    private async Task HandlePromoteAsync(LobbyClient client, string text)
    {
        var request = JsonSerializer.Deserialize<LobbyPromoteRequest>(text);
        var friendId = request?.FriendId ?? "";

        if (client.PartyId == null || !_parties.TryGetValue(client.PartyId, out var party))
        {
            await SendAsync(client, new LobbyPromoteResponse
            {
                MsgType = "/promote",
                PlayerId = client.PlayerId,
                FriendId = friendId,
                Message = "Not in a party",
                Result = false,
                Code = 400
            });
            return;
        }

        if (party.LeaderId != client.PlayerId)
        {
            await SendAsync(client, new LobbyPromoteResponse
            {
                MsgType = "/promote",
                PlayerId = client.PlayerId,
                FriendId = friendId,
                Message = "Not party leader",
                Result = false,
                Code = 403
            });
            return;
        }

        if (!party.Members.Contains(friendId))
        {
            await SendAsync(client, new LobbyPromoteResponse
            {
                MsgType = "/promote",
                PlayerId = client.PlayerId,
                FriendId = friendId,
                Message = "Player not in party",
                Result = false,
                Code = 404
            });
            return;
        }

        party.LeaderId = friendId;
        logger.LogInformation("[Lobby] {FriendId} promoted to leader in party {PartyId}", friendId, party.PartyId);

        await SendAsync(client, new LobbyPromoteResponse
        {
            MsgType = "/promote",
            PlayerId = client.PlayerId,
            FriendId = friendId,
            Message = "Player promoted",
            Result = true,
            Code = 200
        });

        await BroadcastToPartyAsync(party, client.PlayerId, new LobbyPromotePartyAnnouncement
        {
            MsgType = "/promote_notice",
            PlayerId = friendId,
            PartyId = party.PartyId
        });
    }

    // --- /setdata ---

    private async Task HandleSetDataAsync(LobbyClient client, string text)
    {
        var request = JsonSerializer.Deserialize<LobbySetDataRequest>(text);
        var data = request?.Data ?? "{}";

        if (client.PartyId == null || !_parties.TryGetValue(client.PartyId, out var party))
        {
            await SendAsync(client, new LobbySetDataResponse
            {
                MsgType = "/setdata",
                Message = "Not in a party",
                Result = false,
                Code = 400
            });
            return;
        }

        party.Data = data;

        await SendAsync(client, new LobbySetDataResponse
        {
            MsgType = "/setdata",
            Data = data,
            Message = "Party data set.",
            Result = true,
            Code = 200
        });

        await BroadcastToPartyAsync(party, client.PlayerId, new LobbyDataPartyAnnouncement
        {
            MsgType = "/data_notice",
            Data = data,
            PartyId = party.PartyId,
            PlayerId = client.PlayerId
        });
    }

    // --- /setuserdata ---

    private async Task HandleSetUserDataAsync(LobbyClient client, string text)
    {
        var request = JsonSerializer.Deserialize<LobbySetUserDataRequest>(text);
        var incomingData = request?.Data ?? "{}";

        if (client.PartyId == null || !_parties.TryGetValue(client.PartyId, out var party))
        {
            await SendAsync(client, new LobbySetUserDataResponse
            {
                MsgType = "/setuserdata",
                Message = "Not in a party",
                Result = false,
                Code = 400
            });
            return;
        }

        var existing = party.UserData.GetValueOrDefault(client.PlayerId, "{}");
        var existingDict = JsonSerializer.Deserialize<Dictionary<string, string>>(existing) ?? [];
        var incomingDict = JsonSerializer.Deserialize<Dictionary<string, string>>(incomingData) ?? [];

        foreach (var (k, v) in incomingDict)
            existingDict[k] = v;

        var merged = JsonSerializer.Serialize(existingDict);
        party.UserData[client.PlayerId] = merged;

        await SendAsync(client, new LobbySetUserDataResponse
        {
            MsgType = "/setuserdata",
            Data = merged,
            Message = "Party user data set.",
            Result = true,
            Code = 200
        });

        await BroadcastToPartyAsync(party, client.PlayerId, new LobbyUserDataAnnouncement
        {
            MsgType = "/user_data_notice",
            Data = merged,
            PartyId = party.PartyId,
            PlayerId = client.PlayerId
        });
    }

    // --- Helpers ---

    private async Task LeaveCurrentParty(LobbyClient client)
    {
        if (client.PartyId == null || !_parties.TryGetValue(client.PartyId, out var party))
            return;

        party.Members.Remove(client.PlayerId);
        party.UserData.TryRemove(client.PlayerId, out _);
        _playerPartyMap.TryRemove(client.PlayerId, out _);

        await BroadcastToPartyAsync(party, client.PlayerId, new LobbyLeavePartyAnnouncement
        {
            MsgType = "/leave_notice",
            PlayerId = client.PlayerId,
            PartyId = party.PartyId
        });

        // Promote new leader if needed
        if (party.LeaderId == client.PlayerId && party.Members.Count > 0)
        {
            party.LeaderId = party.Members[0];
            await BroadcastToPartyAsync(party, null, new LobbyPromotePartyAnnouncement
            {
                MsgType = "/promote_notice",
                PlayerId = party.LeaderId,
                PartyId = party.PartyId
            });
        }

        if (party.Members.Count == 0)
            _parties.TryRemove(client.PartyId, out _);

        client.PartyId = null;
    }

    private async Task BroadcastToPartyAsync<T>(Party party, string? excludePlayerId, T message) where T : LobbyMessage
    {
        foreach (var memberId in party.Members)
        {
            if (memberId == excludePlayerId) continue;
            if (_playerClients.TryGetValue(memberId, out var memberClient))
            {
                try
                {
                    await SendAsync(memberClient, message);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[Lobby] Failed to send to {PlayerId}", memberId);
                }
            }
        }
    }

    // --- Public query methods for HTTP bulk endpoints ---

    public Dictionary<string, string>? GetPlayerUserData(string playerId)
    {
        if (!_playerPartyMap.TryGetValue(playerId, out var partyId)) return null;
        if (!_parties.TryGetValue(partyId, out var party)) return null;
        var json = party.UserData.GetValueOrDefault(playerId);
        if (json == null) return null;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
    }

    public LobbyInfoResponse GetPlayerPartyInfo(string playerId)
    {
        if (_playerPartyMap.TryGetValue(playerId, out var partyId) && _parties.TryGetValue(partyId, out var party))
        {
            return new LobbyInfoResponse
            {
                MsgType = "/info",
                Message = "Party info acquired",
                PartyId = party.PartyId,
                LeaderId = party.LeaderId,
                PartyMembers = party.Members.ToList(),
                InvitedPlayers = party.InvitedPlayers.ToList(),
                Data = party.Data,
                UserData = party.UserData.ToDictionary(kv => kv.Key, kv => kv.Value),
                Token = party.Token,
                Result = true,
                Code = 200
            };
        }

        return new LobbyInfoResponse
        {
            MsgType = "/info",
            Message = "Party info acquired (no party)",
            Result = true,
            Code = 200
        };
    }

    private static async Task SendAsync<T>(LobbyClient client, T message) where T : LobbyMessage
    {
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        await client.Socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        logger.LogInformation("[Lobby] Shutting down, closing {Count} connections", _clients.Count);
        foreach (var (_, client) in _clients)
        {
            try
            {
                // TODO: Replace this - client does not reconnect on normal closure so we have to unexpectedly close the connection
                if (client.Socket.State == WebSocketState.Open)
                    await client.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", CancellationToken.None);
            }
            catch
            {
            }
        }

        _clients.Clear();
    }
}