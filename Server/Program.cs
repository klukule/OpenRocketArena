using System.Net.WebSockets;
using Microsoft.EntityFrameworkCore;
using OpenRocketArena.Server.Data;
using OpenRocketArena.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Database
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=openrocketarena.db"));

// Steam
builder.Services.AddHttpClient<SteamAuthService>();

// Lobby
builder.Services.AddSingleton<LobbyService>();

// IAM token store
builder.Services.AddSingleton<IamTokenStore>();

// Matchmaking
builder.Services.AddSingleton<MatchmakingService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MatchmakingService>());

// RTM
builder.Services.AddSingleton<RtmService>();

// CMS data
builder.Services.AddSingleton<CmsStoreService>();
builder.Services.AddSingleton<CmsMatchmakingData>();
builder.Services.AddSingleton<CmsProgressionData>();

var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Log all incoming requests (for easier implementation of missing endpoints)
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RequestLog");

    context.Request.EnableBuffering();
    var body = "";
    if (context.Request.ContentLength > 0)
    {
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;
    }

    var headers = string.Join(", ", context.Request.Headers.Select(h => $"{h.Key}: {h.Value}"));
    if (context.Request.Method == "GET")
        logger.LogInformation(">> {Method} {Path}{Query}\n   Headers: {Headers}", context.Request.Method, context.Request.Path, context.Request.QueryString, headers);
    else
        logger.LogInformation(">> {Method} {Path}{Query}\n   Headers: {Headers}\n   Body: {Body}", context.Request.Method, context.Request.Path, context.Request.QueryString, headers, body);

    await next();

    logger.LogInformation("<< {Method} {Path} => {StatusCode}", context.Request.Method, context.Request.Path, context.Response.StatusCode);
});

app.UseHttpsRedirection();
app.UseWebSockets();
app.MapControllers();

// Lobby WebSocket endpoint
app.Map("/lobby/connect", async (HttpContext context, LobbyService lobby, IamTokenStore tokenStore) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var displayName = context.Request.Headers["X-FSG-DisplayName"].FirstOrDefault() ?? "Unknown";
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault() ?? "";
    var bearerToken = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authHeader["Bearer ".Length..] : "";

    // Resolve player info from IAM token
    var tokenInfo = !string.IsNullOrEmpty(bearerToken) ? tokenStore.Resolve(bearerToken) : null;
    var playerId = tokenInfo?.PlayerId ?? "0:0:1";

    // Look up account for platform info
    string platformName = "steam";
    string platformUserId = "";
    if (tokenInfo != null)
    {
        using var scope = context.RequestServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpenRocketArena.Server.Data.AppDbContext>();
        var account = await db.Accounts.FindAsync(tokenInfo.AccountId);
        if (account != null)
            platformUserId = account.SteamId;
    }

    var ws = await context.WebSockets.AcceptWebSocketAsync("wss");
    var client = new LobbyClient(ws, displayName, playerId, platformName, platformUserId);
    await lobby.HandleConnectionAsync(client, context.RequestAborted);
});

// RTM WebSocket endpoint
app.Map("/websocket", async (HttpContext context, RtmService rtm) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var authHeader = context.Request.Headers.Authorization.FirstOrDefault() ?? "";
    var userId = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authHeader["Bearer ".Length..] : "anonymous";

    var ws = await context.WebSockets.AcceptWebSocketAsync();
    var client = new RtmClient(ws, userId);
    await rtm.HandleConnectionAsync(client, context.RequestAborted);
});

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    app.Services.GetRequiredService<LobbyService>().DisposeAsync().AsTask().Wait();
    app.Services.GetRequiredService<RtmService>().DisposeAsync().AsTask().Wait();
});

app.Run();
