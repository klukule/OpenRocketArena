# OpenRocketArena Backend

**Proof-of-concept** replacement backend for Rocket Arena. This project reimplements EA's backend and Rocket Arenas' Mango backend services so the game can run without the original servers.

> **Warning:** This is a PoC. Things are broken, incomplete, or held together with duct tape. Use at your own risk and only if you know what you're doing. Proper implementation coming soon™

The implementation has been tested vs. bots only although online play should be possible by exposing the server to the internet and modifying necessary configs + handling portforwarding etc.

The included CMS data is based on version 759 (the final update), which includes the *Final Sendoff* playlist. Other playlists can be enabled by modifying the CMS data in `Backend/wwwroot/cms/`.

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

## Known Issues

- When viewing certain inventory items (Totem,  Retrun Trails, Megablasts, etc.) that haven't been customized yet, the game shows *"We're sorry. The item you were viewing is no longer available."* - simply equip any customization option for that slot to prevent the message from showing again (on that slot). This is cosmetic only and doesn't prevent modifications.
- Purchasing items (e.g. totem customizations) triggers the unlock notification window twice. This happens for all purchases and is cosmetic only.
- The tutorial does not automatically trigger on first login like it would normally. It can still be played manually from the Play menu.

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

Allows to override any string or integer values from any of the game's config files as long as they're read through `FConfigCacheIni::GetString` (`GetInt`, `GetFloat`, `GetBool` etc. all internally call `GetString`)

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

### Steam API Keys

You need a Steam Web API key. There are two types:

- **Publisher key** (recommended) - Available from [Steamworks Partner site](https://partner.steamgames.com/) under Users & Permissions > Manage Groups > select group > Web API. Requires being a Steamworks partner. Uses `partner.steam-api.com`.
- **Public key** - Available from [Steam Web API Key page](https://steamcommunity.com/dev/apikey). Anyone with a Steam account can get one. Uses `api.steampowered.com`.

Both work, but the publisher key has higher rate limits and access to additional endpoints.

> **Do not share your API key.** It is a secret. Do not commit it to git, post it publicly, or include it in shared builds. The `customize.ps1` script writes it only to local config files.

## Install

1. Download the latest release or build with `build.bat`
2. Run `customize.ps1` to configure your installation (Steam API key, Vivox, server settings, etc.)
3. Copy the `Output/` folder contents to your game folder (replacing `Mariner.exe` and `Launch_RocketArena.exe`)
4. Start `LaunchBackend.bat`
5. Launch the game (Steam, `Mariner.exe`, `Launch_RocketArena.exe`, or `LaunchClient.bat`)

### Configuration Script

After building, run `customize.ps1`. It will walk you through:

1. **Steam API Key** (required) - type and key value
2. **Steam App ID** - override or use default (1233550)
3. **Vivox Voice Chat** - enable/disable and set credentials
4. **Backend URL** - custom hostname, port, and HTTP/HTTPS for online play
5. **Game Server IP** - for exposing game servers to the internet
6. **Server Executable** - path to `Mariner.exe` for matchmaking

The script updates both `Overrides.ini` and `Backend/appsettings.json` in the `Output/` folder. You can also edit these files manually.

## Building

```
build.bat
```

Output goes to `./Output/`. Requires all prerequisites listed above.