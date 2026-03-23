using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenRocketArena.Server.Models;

namespace OpenRocketArena.Server.Services;

public enum MatchmakingStatus
{
    Pending,
    Queued,
    Searching,
    RequiresAcceptance,
    Placing,
    Completed,
    Failed,
    TimedOut,
    Cancelled
}

public class MatchmakingTicket
{
    public string TicketId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public string Playlist { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public MatchmakingStatus Status { get; set; } = MatchmakingStatus.Searching;
    public string StatusMessage { get; set; } = "Searching for players";
    public string StatusReason { get; set; } = string.Empty;
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public Dictionary<string, int> RegionPings { get; set; } = new();
    public List<MatchmakingTicket> MatchedTickets { get; set; } = [];

    // Connection info filled when COMPLETED
    public string GameSessionArn { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public string PlayerSessionId { get; set; } = string.Empty;
}

public class GameServerInstance
{
    public string SessionId { get; set; } = string.Empty;
    public Process? Process { get; set; }
    public int Port { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Minimal matchmaking service with "local" fleet manager implementation - spawns a local server process for each match. 
/// This is not meant to be a full production-ready solution, just a simple implementation to get matchmaking working.
/// </summary>
public class MatchmakingService : IHostedService, IDisposable
{
    private readonly ConcurrentDictionary<string, MatchmakingTicket> _tickets = new();
    private readonly ConcurrentDictionary<string, GameServerInstance> _servers = new();
    private readonly ILogger<MatchmakingService> _logger;
    private readonly CmsMatchmakingData _cmsData;
    private Timer? _ticker;

    // If we don't find full group within this time we fill with bots (if playlist allows)
    private const int BotFillTimeoutSeconds = 15;

    // Cached config
    private readonly string _serverCommand;
    private readonly string _serverArguments;
    private readonly string _gameServerIp;
    private readonly int _portRangeStart;
    private readonly int _portRangeEnd;
    private readonly string _cmsEnv;
    private readonly string _cmsVersion;

    public MatchmakingService(ILogger<MatchmakingService> logger, IConfiguration config, CmsMatchmakingData cmsData)
    {
        _logger = logger;
        _cmsData = cmsData;

        _serverCommand = config["Matchmaking:ServerCommand"] is { Length: > 0 } cmd ? cmd : "";
        _serverArguments = config["Matchmaking:ServerArguments"] ?? "";
        _gameServerIp = config["Matchmaking:GameServerIp"] is { Length: > 0 } ip ? ip : "127.0.0.1";
        _portRangeStart = config.GetValue("Matchmaking:PortRangeStart", 7777);
        _portRangeEnd = config.GetValue("Matchmaking:PortRangeEnd", 7877);
        _cmsEnv = config["Cms:Environment"] ?? "stage";
        _cmsVersion = config["Cms:Versions:stage"] ?? "1.default";
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ticker = new Timer(Tick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        _logger.LogInformation("Matchmaking service started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _ticker?.Change(Timeout.Infinite, 0);

        // Kill all running server processes
        foreach (var (_, server) in _servers)
        {
            try
            {
                if (server.Process is { HasExited: false })
                {
                    _logger.LogInformation("Killing server process on port {Port}", server.Port);
                    server.Process.Kill(true);
                }
            }
            catch { }
        }

        _logger.LogInformation("Matchmaking service stopped");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _ticker?.Dispose();
    }

    public MatchmakingTicket StartMatchmaking(string ticketId, string playerId, string playlist, string region, Dictionary<string, int> regionPings)
    {
        var ticket = new MatchmakingTicket
        {
            TicketId = ticketId,
            PlayerId = playerId,
            Playlist = playlist,
            Region = region,
            RegionPings = regionPings,
            Status = MatchmakingStatus.Searching
        };

        _tickets[ticketId] = ticket;
        _logger.LogInformation("Matchmaking ticket {TicketId} created for {PlayerId} in playlist {Playlist} region {Region}", ticketId, playerId, playlist, region);

        return ticket;
    }

    public MatchmakingTicket StartCustomGame(string ticketId, string playerId, GameSessionData sessionData, Dictionary<string, int> regionPings)
    {
        var port = FindAvailablePort();
        var sessionId = Guid.NewGuid().ToString();
        var sessionArn = $"arn:aws:gamelift:localhost::gamesession/fleet-00000000-0000-0000-0000-000000000000/{sessionId}";

        var ticket = new MatchmakingTicket
        {
            TicketId = ticketId,
            PlayerId = playerId,
            Playlist = "custom",
            Region = regionPings.Keys.FirstOrDefault() ?? "",
            RegionPings = regionPings,
            Status = MatchmakingStatus.Placing,
            StatusMessage = "Starting custom game server"
        };
        _tickets[ticketId] = ticket;

        if (port == 0)
        {
            ticket.Status = MatchmakingStatus.Failed;
            ticket.StatusMessage = "No available server ports";
            ticket.EndTime = DateTime.UtcNow;
            return ticket;
        }

        var server = SpawnServer(sessionId, port, sessionData, null);
        if (server == null)
        {
            ticket.Status = MatchmakingStatus.Failed;
            ticket.StatusMessage = "Failed to start game server";
            ticket.EndTime = DateTime.UtcNow;
            return ticket;
        }

        ticket.Status = MatchmakingStatus.Completed;
        ticket.StatusMessage = "Custom game ready";
        ticket.EndTime = DateTime.UtcNow;
        ticket.GameSessionArn = sessionArn;
        ticket.IpAddress = _gameServerIp;
        ticket.Port = port;
        ticket.PlayerSessionId = $"psess-{Guid.NewGuid():N}";
        ticket.MatchedTickets = [ticket];

        _logger.LogInformation("Custom game started: session={SessionArn} port={Port} player={PlayerId}", sessionArn, port, playerId);

        return ticket;
    }

    public MatchmakingTicket? GetTicket(string ticketId) => _tickets.GetValueOrDefault(ticketId);

    public bool CancelTicket(string ticketId)
    {
        if (!_tickets.TryGetValue(ticketId, out var ticket))
            return false;

        if (ticket.Status is MatchmakingStatus.Completed or MatchmakingStatus.Cancelled or MatchmakingStatus.Failed)
            return false;

        ticket.Status = MatchmakingStatus.Cancelled;
        ticket.StatusMessage = "Matchmaking cancelled";
        ticket.EndTime = DateTime.UtcNow;

        _logger.LogInformation("Matchmaking ticket {TicketId} cancelled", ticketId);
        return true;
    }

    private void Tick(object? state)
    {
        try
        {
            ProcessSearching();
            CleanupExpired();
            CleanupDeadServers();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in matchmaking tick");
        }
    }

    private void ProcessSearching()
    {
        var searchingTickets = _tickets.Values.Where(t => t.Status == MatchmakingStatus.Searching).GroupBy(t => t.Playlist).ToList();

        foreach (var group in searchingTickets)
        {
            var playlistId = group.Key;
            var playlist = _cmsData.GetPlaylist(playlistId);
            var targetCount = playlist?.TargetPlayerCount ?? 6;

            var tickets = group.OrderBy(t => t.StartTime).ToList();

            // Try to form full matches
            while (tickets.Count >= targetCount)
            {
                var matchGroup = tickets.Take(targetCount).ToList();
                tickets = tickets.Skip(targetCount).ToList();
                CompleteMatch(matchGroup, playlistId, 0);
            }

            // Fill with bots after timeout
            if (playlist?.FillWithBots == true)
            {
                foreach (var ticket in tickets)
                {
                    var waitTime = (DateTime.UtcNow - ticket.StartTime).TotalSeconds;
                    if (waitTime >= BotFillTimeoutSeconds)
                    {
                        var botsNeeded = targetCount - 1; // just this player + bots
                        CompleteMatch([ticket], playlistId, botsNeeded);
                    }
                }
            }
        }
    }

    private void CompleteMatch(List<MatchmakingTicket> tickets, string playlistId, int botCount)
    {

        // Transition to PLACING while we spawn the server
        foreach (var t in tickets)
        {
            t.Status = MatchmakingStatus.Placing;
            t.StatusMessage = "Starting game server";
        }

        var port = FindAvailablePort();
        if (port == 0)
        {
            _logger.LogError("No available ports for game server");
            foreach (var t in tickets)
            {
                t.Status = MatchmakingStatus.Failed;
                t.StatusMessage = "No available server ports";
                t.EndTime = DateTime.UtcNow;
            }
            return;
        }

        var playlist = _cmsData.GetPlaylist(playlistId);
        var gameliftId = playlist?.GameliftId ?? "unknown";
        var sessionId = Guid.NewGuid().ToString();
        var matchId = Guid.NewGuid().ToString();
        var sessionArn = $"arn:aws:gamelift:localhost::gamesession/fleet-00000000-0000-0000-0000-000000000000/{sessionId}";
        var configArn = $"arn:aws:gamelift:localhost::matchmakingconfiguration/{_cmsEnv}-{gameliftId}-{_cmsVersion}";

        // Build session data - map/mode left empty, server resolves from ARN
        var sessionData = new GameSessionData
        {
            IsPrivateMatchSession = false,
            IsMatchmakingSession = true
        };

        // Build matchmaker data
        var matchmakerData = new MatchmakerData
        {
            MatchId = matchId,
            MatchmakingConfigurationArn = configArn,
            Teams =
            [
                new MatchmakerTeam
                {
                    Name = "team1",
                    Players = tickets.Select(t => new MatchmakerPlayer { PlayerId = t.PlayerId }).ToList()
                }
            ]
        };

        // Spawn server process
        var server = SpawnServer(sessionId, port, sessionData, matchmakerData);

        if (server == null)
        {
            foreach (var t in tickets)
            {
                t.Status = MatchmakingStatus.Failed;
                t.StatusMessage = "Failed to start game server";
                t.EndTime = DateTime.UtcNow;
            }
            return;
        }

        // Complete all tickets
        foreach (var ticket in tickets)
        {
            ticket.Status = MatchmakingStatus.Completed;
            ticket.StatusMessage = "Match found";
            ticket.EndTime = DateTime.UtcNow;
            ticket.GameSessionArn = sessionArn;
            ticket.IpAddress = _gameServerIp;
            ticket.Port = port;
            ticket.PlayerSessionId = $"psess-{Guid.NewGuid():N}";
            ticket.MatchedTickets = tickets;
        }

        _logger.LogInformation("Match formed: session={SessionArn} port={Port} playlist={Playlist} players={PlayerCount} bots={BotCount}", sessionArn, port, gameliftId, tickets.Count, botCount);
    }

    private GameServerInstance? SpawnServer(string sessionId, int port, GameSessionData sessionData, MatchmakerData? matchmakerData)
    {
        if (string.IsNullOrEmpty(_serverCommand))
        {
            _logger.LogError("Matchmaking:ServerCommand not configured");
            return null;
        }

        try
        {
            var sessionJson = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sessionData)));
            var args = $"{_serverArguments} -port={port} -sessiondata=\"{sessionJson}\"".TrimStart();

            if (matchmakerData != null)
            {
                var matchmakerJson = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(matchmakerData)));
                args += $" -matchmakerdata=\"{matchmakerJson}\"";
            }

            _logger.LogInformation("Spawning server: {Command} {Args}", _serverCommand, args);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _serverCommand,
                    Arguments = args,
                    UseShellExecute = true,
                    CreateNoWindow = false
                },
                EnableRaisingEvents = true
            };

            process.Exited += (_, _) =>
            {
                _logger.LogInformation("Server process exited: session={SessionId} port={Port}", sessionId, port);
                _servers.TryRemove(sessionId, out _);
            };

            process.Start();

            var server = new GameServerInstance
            {
                SessionId = sessionId,
                Process = process,
                Port = port
            };

            _servers[sessionId] = server;
            return server;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to spawn server process");
            return null;
        }
    }

    private int FindAvailablePort()
    {
        var usedPorts = _servers.Values.Select(s => s.Port).ToHashSet();
        var start = _portRangeStart;
        var end = _portRangeEnd;

        for (var port = start; port <= end; port++)
        {
            if (usedPorts.Contains(port)) continue;

            // Also check if port is actually free on the system
            try
            {
                using var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return port;
            }
            catch { }
        }

        return 0;
    }

    private void CleanupExpired()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-5);
        var expired = _tickets.Values
            .Where(t => t.EndTime.HasValue && t.EndTime.Value < cutoff)
            .Select(t => t.TicketId)
            .ToList();

        foreach (var id in expired)
            _tickets.TryRemove(id, out _);
    }

    private void CleanupDeadServers()
    {
        var dead = _servers.Values
            .Where(s => s.Process is { HasExited: true })
            .Select(s => s.SessionId)
            .ToList();

        foreach (var id in dead)
            _servers.TryRemove(id, out _);
    }

    public MatchmakingTicketDto ToDto(MatchmakingTicket ticket)
    {
        var allPlayers = ticket.Status == MatchmakingStatus.Completed
            ? ticket.MatchedTickets.Select(t => new MatchmakePlayerDto
            {
                PlayerId = t.PlayerId,
                LatencyInMs = t.RegionPings.GetValueOrDefault(ticket.Region.ToLower(), 0),
                Team = ""
            }).ToList()
            : [new MatchmakePlayerDto { PlayerId = ticket.PlayerId, LatencyInMs = 0, Team = "" }];

        var connectionInfo = new GameSessionConnectionInfoDto
        {
            GameSessionArn = ticket.GameSessionArn,
            IpAddress = ticket.IpAddress,
            Port = ticket.Port,
            MatchedPlayerSessions = ticket.Status == MatchmakingStatus.Completed
                ? ticket.MatchedTickets.Select(t => new PlayerSessionDto
                {
                    PlayerId = t.PlayerId,
                    PlayerSessionId = t.PlayerSessionId
                }).ToList()
                : []
        };

        return new MatchmakingTicketDto
        {
            ConfigurationName = ticket.Playlist,
            StartTime = ticket.StartTime.ToString("O"),
            EndTime = ticket.EndTime?.ToString("O") ?? "",
            Status = StatusToString(ticket.Status),
            StatusMessage = ticket.StatusMessage,
            StatusReason = ticket.StatusReason,
            TicketId = ticket.TicketId,
            Players = allPlayers,
            GameSessionConnectionInfo = connectionInfo
        };
    }

    private static string StatusToString(MatchmakingStatus status) => status switch
    {
        MatchmakingStatus.Pending => "PENDING",
        MatchmakingStatus.Queued => "QUEUED",
        MatchmakingStatus.Searching => "SEARCHING",
        MatchmakingStatus.RequiresAcceptance => "REQUIRES_ACCEPTANCE",
        MatchmakingStatus.Placing => "PLACING",
        MatchmakingStatus.Completed => "COMPLETED",
        MatchmakingStatus.Failed => "FAILED",
        MatchmakingStatus.TimedOut => "TIMED_OUT",
        MatchmakingStatus.Cancelled => "CANCELLED",
        _ => "UNKNOWN"
    };
}

// --- Server launch data models ---

public class GameSessionData
{
    [JsonPropertyName("bIsPrivateMatchSession")]
    public bool IsPrivateMatchSession { get; set; }

    [JsonPropertyName("bIsMatchmakingSession")]
    public bool IsMatchmakingSession { get; set; }

    [JsonPropertyName("SelectedModeId")]
    public string SelectedModeId { get; set; } = string.Empty;

    [JsonPropertyName("SelectedMapId")]
    public string SelectedMapId { get; set; } = string.Empty;

    [JsonPropertyName("Team1")]
    public List<PrivateMatchLobbySlot> Team1 { get; set; } = [];

    [JsonPropertyName("Team2")]
    public List<PrivateMatchLobbySlot> Team2 { get; set; } = [];
}

public class PrivateMatchLobbySlot
{
    [JsonPropertyName("SlotType")]
    public int SlotType { get; set; }

    [JsonPropertyName("PlayerId")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("BotDifficulty")]
    public int BotDifficulty { get; set; }
}

public class MatchmakerData
{
    [JsonPropertyName("MatchId")]
    public string MatchId { get; set; } = string.Empty;

    [JsonPropertyName("MatchmakingConfigurationArn")]
    public string MatchmakingConfigurationArn { get; set; } = string.Empty;

    [JsonPropertyName("Teams")]
    public List<MatchmakerTeam> Teams { get; set; } = [];
}

public class MatchmakerTeam
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Players")]
    public List<MatchmakerPlayer> Players { get; set; } = [];
}

public class MatchmakerPlayer
{
    [JsonPropertyName("PlayerId")]
    public string PlayerId { get; set; } = string.Empty;
}
