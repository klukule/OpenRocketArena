$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputDir = Join-Path $root "Output"
$overridesPath = Join-Path $outputDir "Overrides.ini"
$appsettingsPath = Join-Path (Join-Path $outputDir "Backend") "appsettings.json"

if (-not (Test-Path $overridesPath)) {
    Write-Host "ERROR: Output folder not found. Run build.bat first." -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $appsettingsPath)) {
    Write-Host "ERROR: Backend/appsettings.json not found. Run build.bat first." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host " OpenRocketArena - Configuration Wizard     " -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$appsettings = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
$overrides = Get-Content $overridesPath -Raw

# ----------------------------------------
# 1. Steam API Key
# ----------------------------------------
Write-Host "--- Steam Web API ---" -ForegroundColor Yellow
Write-Host ""
$apiType = Read-Host "Steam API key type (publisher/public) [publisher]"
if ([string]::IsNullOrWhiteSpace($apiType)) { $apiType = "publisher" }

$apiKey = Read-Host "Steam Web API key (required)"
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    Write-Host "  ERROR: Steam API key is required. Cannot continue." -ForegroundColor Red
    exit 1
}
else {
    $appsettings.Steam.ApiKey = $apiKey
    if ($apiType -eq "public") {
        $appsettings.Steam.ApiHost = "api.steampowered.com"
        Write-Host "  Set to PUBLIC API ($apiKey)" -ForegroundColor Green
    }
    else {
        $appsettings.Steam.ApiHost = "partner.steam-api.com"
        Write-Host "  Set to PUBLISHER API ($apiKey)" -ForegroundColor Green
    }
}
Write-Host ""

# ----------------------------------------
# 2. Steam App ID
# ----------------------------------------
Write-Host "--- Steam App ID ---" -ForegroundColor Yellow
Write-Host "Override the Steam App ID? If unsure, say no."
$overrideAppId = Read-Host "Override App ID? (y/n) [n]"
if ($overrideAppId -eq "y") {
    $appId = Read-Host "Steam App ID"
    if (-not [string]::IsNullOrWhiteSpace($appId)) {
        $appsettings.Steam.AppId = [int]$appId
        # Uncomment and set in Overrides.ini
        $overrides = $overrides -replace "; \[OnlineSubsystemSteamFSG\]", "[OnlineSubsystemSteamFSG]"
        $overrides = $overrides -replace ";?\s*AppId=\d*", "AppId=$appId"
        Write-Host "  App ID set to $appId" -ForegroundColor Green
    }
}
else {
    # Official Rocket Arena App ID (1233550)
    $appsettings.Steam.AppId = 1233550
    # Make sure it's commented out
    $overrides = $overrides -replace "^\[OnlineSubsystemSteamFSG\]", "; [OnlineSubsystemSteamFSG]"
    $overrides = $overrides -replace "^AppId=", "; AppId="
    Write-Host "  Using default App ID (1233550)" -ForegroundColor Green
}
Write-Host ""

# ----------------------------------------
# 3. Vivox
# ----------------------------------------
Write-Host "--- Vivox Voice Chat ---" -ForegroundColor Yellow
Write-Host "Requires a Unity/Vivox account. Skip if you don't have one."
$setupVivox = Read-Host "Configure Vivox? (y/n) [n]"
if ($setupVivox -eq "y") {
    $vivoxServerUrl = Read-Host "Vivox Server URL (e.g. https://unity.vivox.com/appconfig/YOUR-ID)"
    $vivoxDomain = Read-Host "Vivox Domain (e.g. mtu1xp.vivox.com)"
    $vivoxIssuer = Read-Host "Vivox Token Issuer"
    $vivoxKey = Read-Host "Vivox Token Key (signing key)"

    if (-not [string]::IsNullOrWhiteSpace($vivoxIssuer)) {
        $appsettings.Vivox.TokenIssuer = $vivoxIssuer
        $appsettings.Vivox.Domain = $vivoxDomain
        $appsettings.Vivox.TokenKey = $vivoxKey

        # Uncomment Vivox section in overrides and set values
        $overrides = $overrides -replace ";\s*\[VoiceChat\.Vivox\]", "[VoiceChat.Vivox]"
        $overrides = $overrides -replace ';\s*ServerUrl="[^"]*"', "ServerUrl=`"$vivoxServerUrl`""
        $overrides = $overrides -replace ';\s*Domain=[^\r\n]*', "Domain=$vivoxDomain"
        $overrides = $overrides -replace ';\s*Issuer=[^\r\n]*', "Issuer=$vivoxIssuer"
        $overrides = $overrides -replace ';\s*bEnabled=[^\r\n]*', "bEnabled=True"
        $overrides = $overrides -replace 'bEnabled=False', "bEnabled=True"
        Write-Host "  Vivox configured and enabled" -ForegroundColor Green
    }
}
else {
    # Disable Vivox
    $overrides = $overrides -replace 'bEnabled=True', "bEnabled=False"
    Write-Host "  Vivox disabled" -ForegroundColor DarkGray
}
Write-Host ""

# ----------------------------------------
# 4. Backend URL
# ----------------------------------------
Write-Host "--- Backend URL ---" -ForegroundColor Yellow
Write-Host "By default the backend runs on localhost:5000 (HTTP)."
$customUrl = Read-Host "Set custom backend URL? (y/n) [n]"
if ($customUrl -eq "y") {
    $backendDomain = Read-Host "Backend hostname (e.g. myserver.com)"
    $backendPort = Read-Host "Backend port [5000]"
    if ([string]::IsNullOrWhiteSpace($backendPort)) { $backendPort = "5000" }

    $useHttps = Read-Host "Use HTTPS? (y/n) [n]"
    if ($useHttps -eq "y") {
        $httpScheme = "https"
        $wsScheme = "wss"
        Write-Host ""
        Write-Host "  NOTE: HTTPS requires an SSL-offloading reverse proxy (e.g. nginx, caddy)" -ForegroundColor Magenta
        Write-Host "  in front of the backend. The backend itself only serves HTTP." -ForegroundColor Magenta
    }
    else {
        $httpScheme = "http"
        $wsScheme = "ws"
    }

    $backendHost = "${backendDomain}:${backendPort}"

    if (-not [string]::IsNullOrWhiteSpace($backendDomain)) {
        $overrides = $overrides -replace "DevCluster=[^\r\n]*", "DevCluster=$backendHost"
        $overrides = $overrides -replace "CertCluster=[^\r\n]*", "CertCluster=$backendHost"
        $overrides = $overrides -replace "ProdCluster=[^\r\n]*", "ProdCluster=$backendHost"

        # Update all URL schemes and hosts
        $overrides = $overrides -replace 'http://localhost:5000', "${httpScheme}://${backendHost}"
        $overrides = $overrides -replace 'ws://localhost:5000', "${wsScheme}://${backendHost}"
        $overrides = $overrides -replace '"https://\{Cluster\}', "`"${httpScheme}://{Cluster}"
        $overrides = $overrides -replace '"http://\{Cluster\}', "`"${httpScheme}://{Cluster}"
        $overrides = $overrides -replace '"ws://\{Cluster\}', "`"${wsScheme}://{Cluster}"
        $overrides = $overrides -replace '"wss://\{Cluster\}', "`"${wsScheme}://{Cluster}"

        # Update launchSettings to bind on all interfaces
        $launchSettingsPath = Join-Path (Join-Path $outputDir "Backend") "appsettings.json"
        # We'll set Kestrel URL via appsettings
        $appsettings | Add-Member -NotePropertyName "Kestrel" -NotePropertyValue @{
            Endpoints = @{
                Http = @{
                    Url = "http://0.0.0.0:$backendPort"
                }
            }
        } -Force

        Write-Host ""
        Write-Host "  WARNING: Make sure ${backendHost} is reachable by all users!" -ForegroundColor Red
        Write-Host "  Backend will listen on 0.0.0.0:${backendPort}" -ForegroundColor Green
        Write-Host "  Client URLs set to ${httpScheme}://${backendHost}" -ForegroundColor Green
    }
}
else {
    Write-Host "  Using default (http://localhost:5000)" -ForegroundColor Green
}
Write-Host ""

# ----------------------------------------
# 5. Game Server IP
# ----------------------------------------
Write-Host "--- Game Server IP ---" -ForegroundColor Yellow
Write-Host "IP address reported to clients for connecting to game servers."
$customIp = Read-Host "Set custom game server IP? (y/n) [n]"
if ($customIp -eq "y") {
    $serverIp = Read-Host "Game server IP address"
    if (-not [string]::IsNullOrWhiteSpace($serverIp)) {
        $appsettings.Matchmaking.GameServerIp = $serverIp
        Write-Host ""
        Write-Host "  WARNING: Make sure $serverIp has correct firewall settings" -ForegroundColor Red
        Write-Host "  and ports 7777-7877 are port-forwarded to this machine!" -ForegroundColor Red
        Write-Host "  Game server IP set to $serverIp" -ForegroundColor Green
    }
}
else {
    $appsettings.Matchmaking.GameServerIp = ""
    Write-Host "  Using default (127.0.0.1)" -ForegroundColor Green
}
Write-Host ""

# ----------------------------------------
# 6. Server Command
# ----------------------------------------
Write-Host "--- Server Executable ---" -ForegroundColor Yellow
$serverCmd = Read-Host "Path to Mariner.exe (leave empty for default ..\Mariner.exe)"
if (-not [string]::IsNullOrWhiteSpace($serverCmd)) {
    $appsettings.Matchmaking.ServerCommand = $serverCmd
    Write-Host "  Server command set to $serverCmd" -ForegroundColor Green
}
else {
    $appsettings.Matchmaking.ServerCommand = "..\Mariner.exe"
    Write-Host "  Using default (..\Mariner.exe)" -ForegroundColor Green
}
Write-Host ""

# ----------------------------------------
# Save
# ----------------------------------------
Write-Host "Saving configuration..." -ForegroundColor Yellow
$appsettings | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath -Encoding UTF8
Set-Content $overridesPath $overrides -NoNewline -Encoding UTF8

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host " Configuration complete!" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Files updated:"
Write-Host "  $overridesPath"
Write-Host "  $appsettingsPath"
Write-Host ""
