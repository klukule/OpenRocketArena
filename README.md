# OpenRocketArena Backend

**Proof-of-concept** replacement backend for Rocket Arena. This project reimplements EA's backend and Rocket Arenas' Mango backend services so the game can run without the original servers.

> **Warning:** This is a PoC. Things are broken, incomplete, or held together with duct tape. Use at your own risk and only if you know what you're doing. Proper implementation coming soon™

The implementation has been tested vs. bots only although online play should be possible by exposing the server to the internet and modifying necessary configs + handling portforwarding etc.

## What works (hopefully)

- EA/Origin server emulation (EADC)
- Steam authentication (IAM)
- Player profiles, inventory, equipment, progression
- Character XP, artifact unlocks, item leveling
- Store purchases (rocket parts currency)
- Lobby/party system (create, invite, join, leave, kick, promote)
- Matchmaking with local server spawning
- Private/custom matches
- Match history and per-player stats
- Vivox voice chat token generation
- CMS data serving (store, matchmaking, progression, quests)

## What doesn't work

- Blast Pass (not implemented at all)
- Ranking formula doesn't match the original
- Quest assignment/completion
- Origin friends list
- Everything runs locally

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [CMake](https://cmake.org/) 3.20+
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (C++ workload)
- [vcpkg](https://github.com/microsoft/vcpkg) (default location `C:\vcpkg`, or set `VCPKG_ROOT`)

## Configuration

### Backend (`Server/appsettings.json`)

| Key | Description |
|---|---|
| `Steam:ApiHost` | `partner.steam-api.com` (publisher) or `api.steampowered.com` (public) |
| `Steam:ApiKey` | Your Steam Web API key |
| `Steam:AppId` | Steam App ID (has to match client) |
| `Vivox:TokenIssuer` | Vivox issuer |
| `Vivox:TokenKey` | Vivox signing key |
| `Vivox:Domain` | Vivox domain |
| `Matchmaking:ServerCommand` | Path to `Mariner.exe` for server spawning |

### Client (`Overrides.ini`)

Allows to override any string or integer values from any of the game's config files as long as they're read through `FConfigCacheIni::GetString` or `FConfigCacheIni::GetInt`

#### `[OnlineSubsystemSteamFSG]`

| Key | Description |
|---|---|
| `AppId` | Steam App ID - bootstrapper syncs this to `steam_appid.txt` automatically. Uncomment to override. (has to match server) |

#### `[MangoClient]`

| Key | Description |
|---|---|
| `IAMDedicatedServerBasicAuthToken` | Base64-encoded `clientId:clientSecret` for dedicated server auth. Must match `Iam:Clients:Server` in `appsettings.json`. |
| `DevCluster` / `CertCluster` / `ProdCluster` | Backend server `host:port` (default `localhost:5000`). Only `ProdCluster` used. |
| `BaseUrl-Mango*Service` | Service URL templates. `{Cluster}` is replaced with the cluster value above. No need to change unless you split services. |

#### `[PinTelemetry]`

| Key | Description |
|---|---|
| `Test-EndpointUrl` / `Prod-EndpointUrl` | Telemetry endpoint. Points to backend's `/pinEvents`. |

#### `[OnlineSubsystemOrigin]`

| Key | Description |
|---|---|
| `*-BaseUrl-Accounts` / `Gateway` / `Friends` / `API` | EA/Origin service URLs. All point to the backend. Change the port if your backend runs on a different port. |

#### `[RTMClient]`

| Key | Description |
|---|---|
| `Int-BaseUrl` / `Prod-BaseUrl` | RTM WebSocket URL. Points to backend's `/websocket`. |

#### `[VoiceChat.Vivox]`

optional, uncomment to enable voice chat - by default game will try to use original credentials set by FSG but those won't work because we don't have signing key

| Key | Description |
|---|---|
| `ServerUrl` | Vivox app config URL (e.g. `https://unity.vivox.com/appconfig/YOUR-APP-ID`) |
| `Domain` | Vivox domain (e.g. `mtu1xp.vivox.com`) |
| `Issuer` | Vivox token issuer (must match `Vivox:TokenIssuer` in `appsettings.json`) |

> **Note:** Vivox requires a registered Vivox/Unity account. Both the backend (`appsettings.json`) and client (`Overrides.ini`) must have matching issuer and domain values. The backend also needs the `Vivox:TokenKey` for signing tokens.

## Install

1. Download the latest release or build with `build.bat`
2. Update `Steam:ApiKey` in `Backend/appsettings.json`
3. Update `Matchmaking:ServerCommand` to point to `Mariner.exe` in your game folder
4. Copy everything to your game folder (replacing `Mariner.exe` and `Launch_RocketArena.exe`)
5. Start `LaunchBackend.bat`
6. Launch the game (Steam, `Mariner.exe`, `Launch_RocketArena.exe`, or `LaunchClient.bat`)

## Building

```
build.bat
```

Output goes to `./Output/`. Requires all prerequisites listed above.