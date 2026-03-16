# Table of Contents
- [jf -- Jellyfin CLI Design Document](#jf----jellyfin-cli-design-document)
  - [1. Command Tree](#1-command-tree)
    - [Grouping rationale](#grouping-rationale)
  - [2. Global Flags](#2-global-flags)
  - [3. Auth Model and Commands](#3-auth-model-and-commands)
    - [Authentication mechanisms](#authentication-mechanisms)
    - [Auth commands](#auth-commands)
    - [Auth flow: `jf auth login`](#auth-flow-jf-auth-login)
    - [Auth failure behavior](#auth-failure-behavior)
    - [Token storage](#token-storage)
    - [`jf auth set-token`](#jf-auth-set-token)
  - [4. Host / Profile / Config Resolution](#4-host--profile--config-resolution)
    - [Host resolution](#host-resolution)
    - [Profile resolution](#profile-resolution)
    - [Config file](#config-file)
    - [Config schema](#config-schema)
  - [5. Environment Variables](#5-environment-variables)
  - [6. Reserved Flags](#6-reserved-flags)
  - [7. Output Modes](#7-output-modes)
    - [Human output (default)](#human-output-default)
    - [Machine output (`--json`)](#machine-output---json)
    - [Quiet mode (`--quiet`)](#quiet-mode---quiet)
    - [Dry-run output](#dry-run-output)
  - [8. Exit Codes](#8-exit-codes)
  - [9. Confirmation Rules for Destructive Commands](#9-confirmation-rules-for-destructive-commands)
    - [Commands that require confirmation](#commands-that-require-confirmation)
    - [Flag interaction matrix](#flag-interaction-matrix)
    - [Confirmation prompt format](#confirmation-prompt-format)
    - [Implementation](#implementation)
  - [10. Five Realistic Copy-Paste Examples](#10-five-realistic-copy-paste-examples)
    - [Example 1: First-time setup and browsing](#example-1-first-time-setup-and-browsing)
    - [Example 2: CI/automation -- export all users as JSON](#example-2-ciautomation----export-all-users-as-json)
    - [Example 3: Library maintenance with dry-run safety](#example-3-library-maintenance-with-dry-run-safety)
    - [Example 4: Destructive operation with confirmation](#example-4-destructive-operation-with-confirmation)
    - [Example 5: Managing playlists and using the raw escape hatch](#example-5-managing-playlists-and-using-the-raw-escape-hatch)
  - [Appendix A: Project Structure (Spectre.Console.Cli)](#appendix-a-project-structure-spectreconsolecli)
  - [Appendix B: Program.cs Tree Registration Sketch](#appendix-b-programcs-tree-registration-sketch)
  - [Appendix C: Design Decision Log](#appendix-c-design-decision-log)


# jf -- Jellyfin CLI Design Document

Target: C# / .NET 8+ / Spectre.Console.Cli
API baseline: Jellyfin 10.11.3 OpenAPI spec (389 endpoints across 62 API tags)

Designed by applying the ergonomic CLI skill principles to the actual Jellyfin API surface.
Organized by user-facing domain, not by API controller tags.

This worked example explicitly chooses:

- **CLI class:** service-native
- **Target identity mode:** hostname key (lowercased hostname)
- **Machine contract style:** envelope JSON on stdout when `--json` is used, with `meta.schemaVersion = 1`
- **Secret storage:** separate secret store (OS credential store / keyring) preferred
- **Confirmation refusal:** exit `2` when interaction is required but forbidden (`--quiet` / non-TTY)

---

## 1. Command Tree

The tree is organized by **user-facing domain**, not by Jellyfin API tags. API tags like
`UserLibrary`, `ItemLookup`, `ItemUpdate`, `ItemRefresh`, `Filter`, and `Image` all collapse
into the `items` branch because a user thinks "I want to do something with an item," not "I
want to call the ItemRefresh controller."

Verbs at the leaves are drawn from a small, repeated set:
`list`, `get`, `create`, `update`, `delete`, `add`, `remove`, `set`, `show`, `search`, `refresh`, `test`.

```
jf
 |
 |-- auth                          Authentication, identity, and profiles
 |    |-- login                    Authenticate by username/password (stores credential)
 |    |-- login --quick-connect    Authenticate via Quick Connect flow
 |    |-- logout                   Best-effort revoke and remove stored credential
 |    |-- status                   Show current auth state (user, server, token expiry)
 |    |-- whoami                   Show the authenticated user (GET /Users/Me)
 |    |-- set-token                Store a pre-existing token (supports --stdin)
 |    |-- test                     Validate stored credentials against the server
 |    |-- profiles
 |    |    |-- list                List all saved profiles
 |    |    |-- use <name>          Set the default profile for the host
 |    |    |-- show [<name>]       Show details of a profile (or the resolved profile)
 |    |    |-- rename <old> <new>  Rename a profile
 |    |    |-- delete <name>       Delete a saved profile
 |    |-- host
 |    |    |-- list                List configured hosts
 |    |    |-- use <hostname>      Set the default host
 |    |    |-- rename <old> <new>  Rename a hostname key
 |    |    |-- delete <hostname>   Remove a host and its profiles  [confirm]
 |    |    |-- alias
 |    |    |    |-- add <hostname> <alias>    Add an alias
 |    |    |    |-- remove <hostname> <alias> Remove an alias
 |    |    |    |-- list [<hostname>]         List aliases
 |    |-- api-keys                 [admin] Manage server-level API keys
 |    |    |-- list                List all API keys (GET /Auth/Keys)
 |    |    |-- create <name>       Create a new API key (POST /Auth/Keys)
 |    |    |-- delete <key>        Revoke an API key (DELETE /Auth/Keys/{key})  [confirm]
 |
 |-- server                        Server information and administration
 |    |-- info                     Show server info (GET /System/Info)
 |    |-- info --public            Show public server info (GET /System/Info/Public)
 |    |-- storage                  Show storage info (GET /System/Info/Storage)
 |    |-- ping                     Ping the server (GET /System/Ping)
 |    |-- restart                  [admin] Restart the server (POST /System/Restart)  [confirm]
 |    |-- shutdown                 [admin] Shut down the server (POST /System/Shutdown)  [confirm]
 |    |-- logs
 |    |    |-- list                List available log files (GET /System/Logs)
 |    |    |-- get <name>          Download/display a log file (GET /System/Logs/Log)
 |    |-- activity
 |    |    |-- list                List activity log entries (GET /System/ActivityLog/Entries)
 |    |-- config
 |    |    |-- get [key]           Get server config (GET /System/Configuration[/{key}])
 |    |    |-- set <key>           Update server config (POST /System/Configuration/{key})  [confirm]
 |    |-- branding
 |    |    |-- get                 Get branding config (GET /Branding/Configuration)
 |    |    |-- css                 Get branding CSS (GET /Branding/Css)
 |    |    |-- splashscreen
 |    |    |    |-- get            Get splashscreen (GET /Branding/Splashscreen)
 |    |    |    |-- set <file>     Upload splashscreen (POST /Branding/Splashscreen)
 |    |    |    |-- delete         Delete splashscreen (DELETE /Branding/Splashscreen)  [confirm]
 |    |-- backup
 |    |    |-- list                List backups (GET /Backup)
 |    |    |-- create              Create a backup (POST /Backup/Create)
 |    |    |-- show <id>           Show backup manifest (GET /Backup/Manifest)
 |    |    |-- restore <id>        [admin] Restore from backup (POST /Backup/Restore)  [confirm]
 |
 |-- users                         User management
 |    |-- list                     List users (GET /Users)
 |    |-- list --public            List publicly visible users (GET /Users/Public)
 |    |-- get <id>                 Get user details (GET /Users/{userId})
 |    |-- create                   [admin] Create a user (POST /Users/New)
 |    |-- update <id>              Update a user (POST /Users)
 |    |-- delete <id>              [admin] Delete a user (DELETE /Users/{userId})  [confirm]
 |    |-- set-password             Change user password (POST /Users/Password)
 |    |-- set-policy <id>          [admin] Update user policy (POST /Users/{userId}/Policy)
 |    |-- set-config               Update user display config (POST /Users/Configuration)
 |    |-- image
 |    |    |-- get                 Get user profile image (GET /UserImage)
 |    |    |-- set <file>          Upload user profile image (POST /UserImage)
 |    |    |-- delete              Delete user profile image (DELETE /UserImage)
 |
 |-- items                         Browse, search, and manage library items
 |    |-- list                     Query items with filters (GET /Items)
 |    |-- get <id>                 Get a single item (GET /Items/{itemId})
 |    |-- search <term>            Search for items (GET /Search/Hints)
 |    |-- update <id>              Update item metadata (POST /Items/{itemId})
 |    |-- delete <id>              Delete item from library+filesystem (DELETE /Items/{itemId})  [confirm]
 |    |-- delete --ids <...>       Bulk delete items (DELETE /Items)  [confirm]
 |    |-- refresh <id>             Refresh item metadata (POST /Items/{itemId}/Refresh)
 |    |-- download <id>            Download item media (GET /Items/{itemId}/Download)
 |    |-- similar <id>             Get similar items (GET /Items/{itemId}/Similar)
 |    |-- latest                   Get latest additions (GET /Items/Latest)
 |    |-- resume                   Get resume-able items (GET /UserItems/Resume)
 |    |-- suggestions              Get item suggestions (GET /Items/Suggestions)
 |    |-- counts                   Get item counts (GET /Items/Counts)
 |    |-- ancestors <id>           Get item parents (GET /Items/{itemId}/Ancestors)
 |    |-- favorite
 |    |    |-- add <id>            Mark item as favorite (POST /UserFavoriteItems/{itemId})
 |    |    |-- remove <id>         Unmark item as favorite (DELETE /UserFavoriteItems/{itemId})
 |    |-- rating
 |    |    |-- set <id> <score>    Rate an item (POST /UserItems/{itemId}/Rating)
 |    |    |-- delete <id>         Delete a rating (DELETE /UserItems/{itemId}/Rating)
 |    |-- played
 |    |    |-- mark <id>           Mark as played (POST /UserPlayedItems/{itemId})
 |    |    |-- unmark <id>         Mark as unplayed (DELETE /UserPlayedItems/{itemId})
 |    |-- images
 |    |    |-- list <id>           List item images (GET /Items/{itemId}/Images)
 |    |    |-- get <id>            Get an item image (GET /Items/{itemId}/Images/{type})
 |    |    |-- set <id>            Upload an item image (POST /Items/{itemId}/Images/{type})
 |    |    |-- delete <id>         Delete an item image (DELETE /Items/{itemId}/Images/{type})  [confirm]
 |    |    |-- reorder <id>        Reorder image index (POST .../Index)
 |    |-- remote-images
 |    |    |-- list <id>           List available remote images (GET .../RemoteImages)
 |    |    |-- providers <id>      List remote image providers (GET .../RemoteImages/Providers)
 |    |    |-- download <id>       Download a remote image (POST .../RemoteImages/Download)
 |    |-- subtitles
 |    |    |-- search <id> <lang>  Search remote subtitles (GET .../RemoteSearch/Subtitles/{lang})
 |    |    |-- download <id> <sub> Download remote subtitle (POST .../RemoteSearch/Subtitles/{subId})
 |    |    |-- upload <id> <file>  Upload a subtitle file (POST /Videos/{itemId}/Subtitles)
 |    |    |-- delete <id> <index> Delete a subtitle file (DELETE .../Subtitles/{index})  [confirm]
 |    |-- lyrics
 |    |    |-- get <id>            Get item lyrics (GET /Audio/{itemId}/Lyrics)
 |    |    |-- search <id>         Search remote lyrics (GET .../RemoteSearch/Lyrics)
 |    |    |-- download <id> <lid> Download remote lyrics (POST .../RemoteSearch/Lyrics/{lyricId})
 |    |    |-- upload <id> <file>  Upload a lyric file (POST /Audio/{itemId}/Lyrics)
 |    |    |-- delete <id>         Delete lyric file (DELETE /Audio/{itemId}/Lyrics)  [confirm]
 |    |-- lookup
 |    |    |-- movie <term>        Remote search for movie metadata (POST .../RemoteSearch/Movie)
 |    |    |-- series <term>       Remote search for series metadata (POST .../RemoteSearch/Series)
 |    |    |-- person <term>       Remote search for person (POST .../RemoteSearch/Person)
 |    |    |-- album <term>        Remote search for album (POST .../RemoteSearch/MusicAlbum)
 |    |    |-- artist <term>       Remote search for artist (POST .../RemoteSearch/MusicArtist)
 |    |    |-- apply <id>          Apply lookup result to item (POST .../RemoteSearch/Apply/{itemId})
 |
 |-- shows                         TV show-specific views
 |    |-- episodes <seriesId>      Get episodes for a series (GET /Shows/{seriesId}/Episodes)
 |    |-- seasons <seriesId>       Get seasons for a series (GET /Shows/{seriesId}/Seasons)
 |    |-- next-up                  Get next-up episodes (GET /Shows/NextUp)
 |    |-- upcoming                 Get upcoming episodes (GET /Shows/Upcoming)
 |
 |-- movies                        Movie-specific views
 |    |-- recommendations          Get movie recommendations (GET /Movies/Recommendations)
 |
 |-- playlists                     Playlist management
 |    |-- list                     List playlists (via GET /Items with type filter)
 |    |-- get <id>                 Get a playlist (GET /Playlists/{playlistId})
 |    |-- create <name>            Create a playlist (POST /Playlists)
 |    |-- update <id>              Update a playlist (POST /Playlists/{playlistId})
 |    |-- delete <id>              Delete a playlist (DELETE /Items/{itemId})  [confirm]
 |    |-- items
 |    |    |-- list <id>           List playlist items (GET /Playlists/{playlistId}/Items)
 |    |    |-- add <id> <itemIds>  Add items to playlist (POST /Playlists/{playlistId}/Items)
 |    |    |-- remove <id> <ids>   Remove items from playlist (DELETE .../Items)
 |    |    |-- move <id> <item> <i> Move item in playlist (POST .../Items/{itemId}/Move/{newIndex})
 |    |-- users
 |    |    |-- list <id>           List playlist users (GET /Playlists/{playlistId}/Users)
 |    |    |-- get <id> <userId>   Get a playlist user (GET .../Users/{userId})
 |    |    |-- update <id> <uid>   Update playlist user perms (POST .../Users/{userId})
 |    |    |-- remove <id> <uid>   Remove user from playlist (DELETE .../Users/{userId})
 |
 |-- collections                   Collection (box set) management
 |    |-- create <name>            Create a collection (POST /Collections)
 |    |-- add <id> <itemIds>       Add items to a collection (POST /Collections/{id}/Items)
 |    |-- remove <id> <itemIds>    Remove items from a collection (DELETE /Collections/{id}/Items)
 |
 |-- libraries                     Library and virtual folder management
 |    |-- list                     List virtual folders / libraries (GET /Library/VirtualFolders)
 |    |-- create                   [admin] Add a virtual folder (POST /Library/VirtualFolders)
 |    |-- delete <name>            [admin] Remove a virtual folder (DELETE /Library/VirtualFolders)  [confirm]
 |    |-- update                   [admin] Update library options (POST .../LibraryOptions)
 |    |-- rename <old> <new>       [admin] Rename a virtual folder (POST .../Name)
 |    |-- scan                     Trigger a full library scan (POST /Library/Refresh)
 |    |-- media-folders            List user media folders (GET /Library/MediaFolders)
 |    |-- paths
 |    |    |-- list                List physical paths (GET /Library/PhysicalPaths)
 |    |    |-- add                 [admin] Add a media path (POST .../Paths)
 |    |    |-- update              [admin] Update a media path (POST .../Paths/Update)
 |    |    |-- remove              [admin] Remove a media path (DELETE .../Paths)  [confirm]
 |
 |-- sessions                      Active session management
 |    |-- list                     List active sessions (GET /Sessions)
 |    |-- message <sid> <text>     Send message to a session (POST /Sessions/{sid}/Message)
 |    |-- play <sid> <itemIds>     Instruct session to play items (POST /Sessions/{sid}/Playing)
 |    |-- command <sid> <cmd>      Send playback command (POST /Sessions/{sid}/Playing/{cmd})
 |    |-- system <sid> <cmd>       Send system command (POST /Sessions/{sid}/System/{cmd})
 |
 |-- devices                       Device management
 |    |-- list                     List devices (GET /Devices)
 |    |-- get <id>                 Get device info (GET /Devices/Info)
 |    |-- options <id>             Get device options (GET /Devices/Options)
 |    |-- update <id>              Update device options (POST /Devices/Options)
 |    |-- delete <id>              [admin] Delete a device (DELETE /Devices)  [confirm]
 |
 |-- plugins                       Plugin management
 |    |-- list                     List installed plugins (GET /Plugins)
 |    |-- get <id>                 Get plugin config (GET /Plugins/{id}/Configuration)
 |    |-- update <id>              Update plugin config (POST /Plugins/{id}/Configuration)
 |    |-- enable <id> <version>    Enable a plugin (POST /Plugins/{id}/{version}/Enable)
 |    |-- disable <id> <version>   Disable a plugin (POST /Plugins/{id}/{version}/Disable)
 |    |-- uninstall <id>           [admin] Uninstall a plugin (DELETE /Plugins/{id})  [confirm]
 |
 |-- packages                      Package repository and installation
 |    |-- list                     List available packages (GET /Packages)
 |    |-- get <name>               Get package info (GET /Packages/{name})
 |    |-- install <name>           Install a package (POST /Packages/Installed/{name})
 |    |-- cancel <id>              Cancel a pending installation (DELETE /Packages/Installing/{id})
 |    |-- repos
 |    |    |-- list                List repositories (GET /Repositories)
 |    |    |-- set                 Set repositories (POST /Repositories)
 |
 |-- tasks                         Scheduled task management
 |    |-- list                     List tasks (GET /ScheduledTasks)
 |    |-- get <id>                 Get task details (GET /ScheduledTasks/{taskId})
 |    |-- start <id>               Start a task (POST /ScheduledTasks/Running/{taskId})
 |    |-- stop <id>                Stop a running task (DELETE /ScheduledTasks/Running/{taskId})
 |    |-- set-triggers <id>        Update task triggers (POST /ScheduledTasks/{taskId}/Triggers)
 |
 |-- live-tv                       Live TV management
 |    |-- info                     Get live TV info (GET /LiveTv/Info)
 |    |-- channels
 |    |    |-- list                List channels (GET /LiveTv/Channels)
 |    |    |-- get <id>            Get a channel (GET /LiveTv/Channels/{channelId})
 |    |-- programs
 |    |    |-- list                List programs / EPG (GET /LiveTv/Programs)
 |    |    |-- get <id>            Get a program (GET /LiveTv/Programs/{programId})
 |    |    |-- recommended         Recommended programs (GET /LiveTv/Programs/Recommended)
 |    |-- recordings
 |    |    |-- list                List recordings (GET /LiveTv/Recordings)
 |    |    |-- get <id>            Get a recording (GET /LiveTv/Recordings/{recordingId})
 |    |    |-- delete <id>         Delete a recording (DELETE .../Recordings/{id})  [confirm]
 |    |-- timers
 |    |    |-- list                List timers (GET /LiveTv/Timers)
 |    |    |-- get <id>            Get a timer (GET /LiveTv/Timers/{timerId})
 |    |    |-- create              Create a timer (POST /LiveTv/Timers)
 |    |    |-- update <id>         Update a timer (POST /LiveTv/Timers/{timerId})
 |    |    |-- cancel <id>         Cancel a timer (DELETE /LiveTv/Timers/{timerId})  [confirm]
 |    |-- series-timers
 |    |    |-- list                List series timers (GET /LiveTv/SeriesTimers)
 |    |    |-- get <id>            Get a series timer (GET /LiveTv/SeriesTimers/{timerId})
 |    |    |-- create              Create a series timer (POST /LiveTv/SeriesTimers)
 |    |    |-- update <id>         Update a series timer (POST /LiveTv/SeriesTimers/{timerId})
 |    |    |-- cancel <id>         Cancel a series timer (DELETE .../SeriesTimers/{id})  [confirm]
 |    |-- tuners
 |    |    |-- list                Get tuner host types (GET /LiveTv/TunerHosts/Types)
 |    |    |-- discover            Discover tuners (GET /LiveTv/Tuners/Discover)
 |    |    |-- add                 Add a tuner host (POST /LiveTv/TunerHosts)
 |    |    |-- delete              Delete a tuner host (DELETE /LiveTv/TunerHosts)  [confirm]
 |    |    |-- reset <id>          Reset a tuner (POST /LiveTv/Tuners/{tunerId}/Reset)  [confirm]
 |    |-- listings
 |    |    |-- list                Get listing providers (GET /LiveTv/ListingProviders/Default)
 |    |    |-- lineups             Get available lineups (GET .../Lineups)
 |    |    |-- add                 Add a listing provider (POST /LiveTv/ListingProviders)
 |    |    |-- delete              Delete a listing provider (DELETE /LiveTv/ListingProviders)  [confirm]
 |    |-- guide-info               Get guide info (GET /LiveTv/GuideInfo)
 |    |-- mappings
 |    |    |-- options             Get channel mapping options (GET .../ChannelMappingOptions)
 |    |    |-- set                 Set channel mappings (POST /LiveTv/ChannelMappings)
 |
 |-- sync-play                     SyncPlay group playback
 |    |-- list                     List SyncPlay groups (GET /SyncPlay/List)
 |    |-- get <id>                 Get a SyncPlay group (GET /SyncPlay/{id})
 |    |-- create                   Create a new group (POST /SyncPlay/New)
 |    |-- join <id>                Join a group (POST /SyncPlay/Join)
 |    |-- leave                    Leave a group (POST /SyncPlay/Leave)
 |    |-- play                     Unpause playback (POST /SyncPlay/Unpause)
 |    |-- pause                    Pause playback (POST /SyncPlay/Pause)
 |    |-- stop                     Stop playback (POST /SyncPlay/Stop)
 |    |-- seek <ticks>             Seek to position (POST /SyncPlay/Seek)
 |    |-- next                     Next item (POST /SyncPlay/NextItem)
 |    |-- previous                 Previous item (POST /SyncPlay/PreviousItem)
 |    |-- queue <itemIds>          Queue items (POST /SyncPlay/Queue)
 |    |-- set-queue <itemIds>      Replace queue (POST /SyncPlay/SetNewQueue)
 |    |-- remove <ids>             Remove from playlist (POST /SyncPlay/RemoveFromPlaylist)
 |    |-- set-repeat <mode>        Set repeat mode (POST /SyncPlay/SetRepeatMode)
 |    |-- set-shuffle <mode>       Set shuffle mode (POST /SyncPlay/SetShuffleMode)
 |
 |-- artists                       Browse artists
 |    |-- list                     List all artists (GET /Artists)
 |    |-- get <name>               Get an artist by name (GET /Artists/{name})
 |    |-- album-artists            List album artists (GET /Artists/AlbumArtists)
 |
 |-- genres                        Browse genres
 |    |-- list                     List genres (GET /Genres)
 |    |-- get <name>               Get a genre by name (GET /Genres/{genreName})
 |    |-- music
 |    |    |-- list                List music genres (GET /MusicGenres)
 |    |    |-- get <name>          Get a music genre (GET /MusicGenres/{genreName})
 |
 |-- config                        Local CLI configuration (not server config)
 |    |-- path                     Print the config file location
 |    |-- get [key]                Get a config value
 |    |-- set <key> <value>        Set a config value
 |
 |-- raw <METHOD> <path>           Escape hatch: call any Jellyfin API endpoint directly
 |    |   --body <json>             Optional request body
 |    |   --body-file <path>        Read body from file
```

### Grouping rationale

| Branch | Why it exists |
|---|---|
| `auth` | All identity, credential, and profile management in one place. A user knows to look here first. |
| `server` | Server-level admin: info, restart, shutdown, logs, config, backups. Matches the mental model of "the server itself." |
| `users` | User CRUD and policy -- distinct from items because the audience is admins managing accounts. |
| `items` | The core domain. Browse, search, modify, rate, subtitle, image -- everything about a media item. Subsumes the API tags `UserLibrary`, `Items`, `ItemUpdate`, `ItemRefresh`, `ItemLookup`, `Image`, `Subtitle`, `Lyrics`, `RemoteImage`, `Filter`. |
| `shows`, `movies` | Thin convenience branches for TV- and movie-specific queries. These could live under `items` but they map to distinct mental tasks ("what's next up?"). |
| `playlists`, `collections` | User-managed groupings. Separate branches because the verbs differ (add/remove items, manage users on playlists). |
| `libraries` | Virtual folder and path management -- the admin concern of "where are my media files?" |
| `sessions` | Controlling active playback sessions, sending commands. |
| `devices` | Device registration and management. |
| `plugins`, `packages` | Plugin lifecycle and package installation. |
| `tasks` | Scheduled task management. |
| `live-tv` | All live TV: channels, programs, recordings, timers, tuners, listings. Deep sub-branches because Live TV is a complex feature area with 41 API endpoints. |
| `sync-play` | Group watch functionality. |
| `artists`, `genres` | Browse-oriented branches for music and genre discovery. |
| `config` | Local CLI configuration, distinct from `server config` which manages the Jellyfin server. |
| `raw` | Escape hatch for any endpoint not yet wrapped. |

---

## 2. Global Flags

These flags are available on every command via `GlobalSettings`:

| Flag | Short | Type | Description |
|---|---|---|---|
| `--server <value>` | `-S` | string | Hostname, alias, scheme-less `host:port`, or full URL of the target Jellyfin server |
| `--profile <NAME>` | | string | Use a named profile on the resolved host (host-scoped; not globally unique) |
| `--config <path>` | | string | Path to config file (overrides default location) |
| `--json` | | bool | Output stable machine-readable JSON to stdout |
| `--no-color` | | bool | Disable ANSI color/formatting (also respects `NO_COLOR` env var) |
| `--quiet` | `-q` | bool | Suppress banners, progress, prompts. Fail if confirmation required. |
| `--dry-run` | | bool | Preview a mutating operation without executing it |
| `--yes` | `-y` | bool | Skip confirmation prompts for destructive commands |
| `--verbose` | `-v` | bool | Increase output detail |
| `--help` | `-h` | bool | Show help (Spectre built-in) |
| `--version` | `-V` | bool | Show version (Spectre built-in) |

**Note**: The generic pattern in `references/service-cli-patterns.md` uses `--host`. This CLI uses `--server` because the value can be a full URL, a scheme-less `host:port`, a bare hostname, or an alias — not strictly a hostname.

**Note**: This CLI intentionally avoids a global “force” flag. Use `--dry-run` to preview and `--yes` to bypass destructive confirmations.

```csharp
public class GlobalSettings : CommandSettings
{
    [CommandOption("-S|--server <VALUE>")]
    [Description("Jellyfin server (hostname, alias, scheme-less host:port, or full URL)")]
    public string? Server { get; set; }

    [CommandOption("--profile <NAME>")]
    [Description("Use a named profile")]
    public string? Profile { get; set; }

    [CommandOption("--config <PATH>")]
    [Description("Override config file path")]
    public string? Config { get; set; }

    [CommandOption("--json")]
    [Description("Output machine-readable JSON")]
    public bool Json { get; set; }

    [CommandOption("--no-color")]
    [Description("Disable ANSI color output")]
    public bool NoColor { get; set; }

    [CommandOption("-q|--quiet")]
    [Description("Suppress non-essential output")]
    public bool Quiet { get; set; }

    [CommandOption("--dry-run")]
    [Description("Preview the operation without executing")]
    public bool DryRun { get; set; }

    [CommandOption("-y|--yes")]
    [Description("Skip confirmation prompts")]
    public bool Yes { get; set; }

    [CommandOption("-v|--verbose")]
    [Description("Increase output detail")]
    public bool Verbose { get; set; }
}
```

---

## 3. Auth Model and Commands

### Authentication mechanisms

Jellyfin supports three authentication paths:

1. **Username/Password** (POST /Users/AuthenticateByName) -- returns an `AccessToken`
2. **Quick Connect** (POST /QuickConnect/Initiate + poll GET /QuickConnect/Connect) -- device-flow style, user approves on another device
3. **API Key** (server-level key, managed via POST /Auth/Keys) -- for automation/service accounts

### Auth commands

```
jf auth login [--server <VALUE>] [--profile <NAME>] [--username <USER>] [--password-stdin] [--quick-connect]
jf auth logout [--server <VALUE>] [--profile <NAME>]
jf auth status
jf auth whoami
jf auth set-token <TOKEN> [--stdin] [--no-validate]
jf auth test
jf auth profiles list
jf auth profiles use <name>
jf auth profiles show [<name>]
jf auth profiles rename <old> <new>
jf auth profiles delete <name>
jf auth host list
jf auth host use <hostname>
jf auth host rename <old> <new>
jf auth host delete <hostname> [--yes] [--dry-run]
jf auth host alias add <hostname> <alias>
jf auth host alias remove <hostname> <alias>
jf auth host alias list [<hostname>]
jf auth api-keys list
jf auth api-keys create <name>
jf auth api-keys delete <key>
```

### Auth flow: `jf auth login`

1. Resolve host from `--server` flag, `JF_SERVER` env var, config default, or single-entry inference; otherwise fail.
2. If `--quick-connect`:
    a. In machine output modes (`--json`), refuse (exit `2`): Quick Connect is interactive and requires user-visible prompts/codes.
    b. Check GET /QuickConnect/Enabled. If disabled, fail with message.
    c. POST /QuickConnect/Initiate to get a code.
    d. Display code to user on stderr.
    e. Poll GET /QuickConnect/Connect until authorized or timeout.
    f. POST /Users/AuthenticateWithQuickConnect with the secret.
    g. Store the returned AccessToken.
3. If username/password:
    a. In `--json` mode, prompts are disabled: require `--username` and `--password-stdin` (or refuse with exit `2`).
    b. If `--username` not provided and TTY present, prompt on stderr.
    c. If `--password-stdin`, read password from stdin (one line, no echo).
    d. If TTY present and no `--password-stdin`, prompt for password on stderr with no-echo.
    e. If no TTY and no `--password-stdin`, fail: `Password required. Use --password-stdin or run interactively.`
    f. POST /Users/AuthenticateByName with username and password.
    g. Store the returned credential in the secret store under `jf:cred:{hostnameKey}:{profile}:{credentialKind}` (hostname key + profile name + credential kind).
4. Save or update a profile entry in config.
5. In human output modes, print the username and server on stderr to confirm. In `--json` mode, include these as structured metadata in the JSON envelope (avoid ad hoc stderr noise).

### Auth failure behavior

Any command that requires authentication checks for a valid token before making the API call.
If no token is found or the token is invalid (401):

```
Error: Not authenticated for https://jf.example.com
Run: jf auth login --server https://jf.example.com
```

Exit code: 3.

The CLI never opens a login prompt from a non-auth command. Ever.

### Token storage

- Non-secret profile metadata lives in `config.json`.
- Credentials (tokens, API keys) are stored in a separate secret store (OS credential store / keyring / external helper), keyed by:
  - hostname key (lowercased hostname)
  - profile name (within that host)
  - credential kind (`token` or `apiKey`)
- Fallback (allowed but discouraged): a separate secrets file with strict permissions and explicit redaction rules. Do not inline secrets in `config.json`.
- File permissions: `config.json` is still user-only (600 on Unix; user-restricted ACL on Windows where possible).
- If a legacy `credentials.json` exists and `config.json` does not, the CLI performs an automatic one-time migration on first run:
  - moves the credential into the secret store
  - writes the new `config.json` (creates `hosts[hostnameKey]` and a `"default"` profile, and sets `defaultHost` / `defaultProfile`)
  - backs up `credentials.json` to `credentials.json.bak`
  - emits a brief stderr note in human mode; in machine output mode, represent it as a `meta.warnings` item (no ad hoc stderr noise)

### `jf auth set-token`

For automation scenarios where the user already has a token:

```
jf auth set-token eyJhbGci... --server https://jf.example.com
echo "eyJhbGci..." | jf auth set-token --stdin --server https://jf.example.com
```

By default, validates the token by calling GET /Users/Me. Skip with `--no-validate`.

---

## 4. Host / Profile / Config Resolution

Resolution is a two-step process: resolve the host, then resolve the profile within that host. For the full resolution algorithm, alias semantics, hostname extraction, validation, migration, and edge cases, see [`jf-cli-profile-system.md`](jf-cli-profile-system.md).

### Host resolution

| Priority | Source | Behavior |
|----------|--------|----------|
| 1 | `--server <value>` flag | Full URL (or scheme-less `host:port`): extract hostname for lookup; use the URL as a runtime base URL override. Bare hostname or alias: lookup below. |
| 2 | `JF_SERVER` env var | Same rules as `--server`. |
| 3 | `defaultHost` in config | Used as-is. |
| 4 | Single host | If `hosts` contains exactly one entry, use it implicitly. |
| 5 | *(none)* | Error: `No server specified. Use --server or set a default host with: jf auth host use <hostname>` |

Bare value lookup order: exact match against `hosts` keys first, then alias scan. Multiple alias matches → warn and use first (config file order).

Scheme-less `host:port` inputs (e.g. `nas.local:8096`) are treated like a full URL by first adding a default scheme (e.g. `http://nas.local:8096`) before extracting the hostname.

### Profile resolution

Given a resolved host:

| Priority | Source | Behavior |
|----------|--------|----------|
| 1 | `--profile <name>` flag | For most commands: must exist under the resolved host. For `jf auth login`: may create the profile if it does not exist yet. |
| 2 | `JF_PROFILE` env var | For most commands: must exist under the resolved host. For `jf auth login`: may create the profile if it does not exist yet. |
| 3 | `defaultProfile` for the host | Used as-is. |
| 4 | Single profile | If the host has exactly one profile, use it implicitly. |
| 5 | *(none)* | Error: `No profile specified for host "<hostname>". Use --profile or set a default with: jf auth profiles use <name>` |

### Config file

Single file: `config.json`. Location:

| Platform | Default path |
|----------|------|
| Windows  | `%APPDATA%/jf/config.json` |
| macOS    | `~/Library/Application Support/jf/config.json` |
| Linux    | `$XDG_CONFIG_HOME/jf/config.json` (default `~/.config/jf/config.json`) |

Override with `--config <path>` or `JF_CONFIG` env var.

### Config schema

```jsonc
{
  "defaultHost": "jf.home.example.com",
  "hosts": {
    "jf.home.example.com": {
      "baseUrl": "https://jf.home.example.com",
      "aliases": ["home", "jf"],
      "defaultProfile": "main",
      "profiles": {
        "main": {
          "user": "jonas",
          "userId": "f692a3c1-...",
          "authKind": "token"
        },
        "admin": {
          "user": "admin",
          "baseUrl": "https://jf.home.example.com/jellyfin",
          "authKind": "apiKey"
        }
      }
    },
    "nas.local": {
      "baseUrl": "http://nas.local:8096/jellyfin",
      "aliases": ["nas"],
      "defaultProfile": "admin",
      "profiles": {
        "admin": {
          "user": "admin",
          "authKind": "token"
        }
      }
    }
  }
}
```

Hosts are keyed by network hostname (lowercased). Profiles are nested under their host and may optionally override the host `baseUrl`. Credentials live in the secret store; `authKind` declares what is stored for the profile.

---

## 5. Environment Variables

| Variable | Maps to flag | Description |
|---|---|---|
| `JF_SERVER` | `--server` | Jellyfin server (hostname, alias, scheme-less `host:port`, or full URL) |
| `JF_TOKEN` | (auth override) | Access token override, bypasses the secret store |
| `JF_PROFILE` | `--profile` | Profile name override for the resolved host |
| `JF_CONFIG` | `--config` | Override config file path |
| `JF_OUTPUT` | `--json` | Set to `json` for machine output |
| `NO_COLOR` | `--no-color` | Disable ANSI formatting (standard, see no-color.org) |

**Precedence**: CLI flag > environment variable > config file > hardcoded default.

`JF_TOKEN` is specifically for CI/automation pipelines where storing a profile is undesirable.
When set, it takes precedence over any stored credential for the resolved host.
If `JF_TOKEN` is set and the **selected server input** is a full URL (or scheme-less `host:port`) (`--server` or `JF_SERVER`), the CLI may allow a config-less ephemeral context for that invocation when no configured host/alias matches the hostname (no config writes, no secret-store access).

---

## 6. Reserved Flags

These flags are reserved across the entire CLI and must not be repurposed by individual commands:

| Flag | Meaning |
|---|---|
| `-h`, `--help` | Show help |
| `-V`, `--version` | Show version |
| `--dry-run` | Preview a mutating operation without side effects |
| `-y`, `--yes` | Skip confirmation prompt |
| `--json` | Machine-readable JSON output |
| `-v`, `--verbose` | Increase verbosity |
| `-q`, `--quiet` | Suppress banners and prompts; fail if confirmation needed |
| `--no-color` | Disable ANSI formatting |
| `-S`, `--server` | Target server (hostname, alias, scheme-less `host:port`, or full URL) |
| `--profile` | Profile name on the resolved host |
| `--config` | Config file path |

Individual commands may add their own flags (e.g., `--recursive`, `--limit`, `--type`,
`--sort-by`), but they must never shadow or redefine a reserved flag.

---

## 7. Output Modes

### Human output (default)

- Tables via `Spectre.Console` for list commands (e.g., `jf items list`, `jf users list`).
- Key-value panels for single-entity commands (e.g., `jf server info`, `jf users get`).
- Progress spinners for long operations (e.g., `jf libraries scan`, `jf items refresh`).
- Prompts (human mode) and human-formatted warnings on **stderr**, never stdout.
- Secrets are redacted in human output (tokens shown as `eyJ...xxxxx`).

### Machine output (`--json`)

- Stable JSON on **stdout**.
- One JSON envelope per command invocation (versioned).
- No ANSI codes, no progress bars, no banners mixed in.
- `jf raw ... --json` still returns the envelope; `data` contains the parsed JSON response from the server for that call.
- In `--json` mode, avoid stderr noise; surface diagnostic paths and structured errors in the envelope `meta`/`error`. Use stderr only when the envelope cannot be emitted.
- Warnings in `--json` mode are represented as structured items in `meta.warnings` (no ad hoc stderr noise).
- Errors are still JSON when `--json` is set:

```json
{
  "ok": false,
  "data": null,
  "error": {
    "kind": "not_authenticated",
    "message": "Not authenticated for https://jf.example.com",
    "recovery": "jf auth login --server https://jf.example.com"
  },
  "meta": {
    "schemaVersion": 1
  }
}
```

### Quiet mode (`--quiet`)

- No banners, no progress spinners, no prompts.
- If a confirmation prompt would fire, fail with exit code 2 and the message:
  ```
  Error: Confirmation required. Use --yes to confirm or --dry-run to preview.
  ```
- Combined with `--json`, still emits the normal JSON envelope on stdout; `--quiet` only suppresses human-facing stderr chatter.

### Dry-run output

Mutating commands with `--dry-run` print a preview and exit 0 without executing:

```
[dry-run] Would delete item: "My Movie" (id: abc123)
  Type: Movie
  Path: /media/movies/My Movie (2024)
No changes made.
```

With `--json --dry-run`:

```json
{
  "ok": true,
  "data": {
    "dryRun": true,
    "action": "delete_item",
    "item": {
      "id": "abc123",
      "name": "My Movie",
      "type": "Movie",
      "path": "/media/movies/My Movie (2024)"
    }
  },
  "error": null,
  "meta": {
    "schemaVersion": 1
  }
}
```

---

## 8. Exit Codes

| Code | Meaning | When |
|---|---|---|
| 0 | Success | Command completed normally |
| 1 | General error | Unclassified failures, unexpected exceptions |
| 2 | Usage / validation error | Missing arguments, invalid flags, confirmation required in quiet mode |
| 3 | Not authenticated | No token, expired token, or login required |
| 4 | Authorization failed | Token valid but insufficient permissions (403) |
| 5 | Not found | Item, user, or resource does not exist (404) |
| 6 | Conflict | Resource conflict (409), e.g., duplicate username |
| 7 | Rate limited | Server returned 429 |
| 8 | Network / timeout | Connection refused, DNS failure, timeout |
| 10 | Cancelled | User cancelled a confirmation prompt, or Ctrl+C |

Implementation: a central `ExceptionHandler` maps domain exceptions to exit codes.

```csharp
app.SetExceptionHandler((ex, _) => ex switch
{
    NotAuthenticatedException => 3,
    ForbiddenException => 4,
    NotFoundException => 5,
    ConflictException => 6,
    RateLimitException => 7,
    HttpRequestException or TaskCanceledException => 8,
    OperationCanceledException => 10,
    ValidationException => 2,
    _ => 1
});
```

---

## 9. Confirmation Rules for Destructive Commands

### Commands that require confirmation

Every command marked `[confirm]` in the command tree above requires user confirmation before
executing. These include:

**High severity (data loss / service disruption)**:
- `jf items delete` -- deletes from library AND filesystem
- `jf users delete` -- permanently removes a user account
- `jf server restart` -- restarts the Jellyfin server
- `jf server shutdown` -- shuts down the Jellyfin server
- `jf server backup restore` -- restores a backup and restarts
- `jf libraries delete` -- removes a virtual folder
- `jf libraries paths remove` -- removes a media path
- `jf plugins uninstall` -- removes a plugin

**Medium severity (destructive but recoverable)**:
- `jf auth api-keys delete` -- revokes an API key
- `jf items images delete` -- deletes an item image
- `jf items subtitles delete` -- deletes a subtitle file
- `jf items lyrics delete` -- deletes a lyric file
- `jf playlists delete` -- deletes a playlist
- `jf devices delete` -- removes a device registration
- `jf live-tv recordings delete` -- deletes a recording
- `jf live-tv timers cancel` -- cancels a timer
- `jf live-tv series-timers cancel` -- cancels a series timer
- `jf live-tv tuners delete` -- removes a tuner host
- `jf live-tv tuners reset` -- resets a tuner
- `jf live-tv listings delete` -- removes a listing provider
- `jf server branding splashscreen delete` -- removes the custom splashscreen
- `jf server config set` -- modifies server configuration

### Flag interaction matrix

| Flags passed | Behavior |
|---|---|
| (none) | Human mode: prompt for confirmation on stderr if TTY present. If no TTY, fail with exit 2. Machine output mode (`--json`): never prompt; refuse (exit 2) unless `--yes` or `--dry-run` is present. |
| `--dry-run` | Print a preview and exit 0. Never prompt, never mutate. |
| `--yes` | Skip prompt, execute immediately. |
| `--dry-run --yes` | `--dry-run` wins. Preview only, exit 0. |
| `--quiet` | Fail with exit 2: "Confirmation required. Use --yes to confirm or --dry-run to preview." |
| `--quiet --yes` | Execute silently. In human mode: no output on success. In `--json` mode: still emits the JSON envelope on stdout. |
| `--quiet --dry-run` | Print preview, exit 0. No prompt. |

### Confirmation prompt format

Human mode only (machine output mode refuses instead of prompting):

```
Are you sure you want to delete "My Movie" (abc123)?
This will remove the item from the library and delete files from disk.
Type 'yes' to confirm:
```

For high-severity operations, require the user to type `yes`, not just press Enter.

### Implementation

A shared `ConfirmationHelper` service encapsulates this logic:

```csharp
public enum ConfirmResult { Confirmed, DryRun, Cancelled }

public class ConfirmationHelper
{
    public ConfirmResult Confirm(GlobalSettings settings, string message, string detail);
}
```

Every destructive command calls this helper before executing. The helper checks `DryRun`, `Yes`,
`Quiet`, and TTY state, and returns the appropriate result. Commands never implement their own
prompt logic.

---

## 10. Five Realistic Copy-Paste Examples

### Example 1: First-time setup and browsing

```bash
# Log in to your home server
jf auth login --server https://jf.home.example.com --username jonas

# Check connection
jf server ping

# See what's in the library
jf items list --type Movie --limit 20 --sort-by DateCreated --sort-order Descending

# Get details for a specific movie
jf items get a1b2c3d4
```

### Example 2: CI/automation -- export all users as JSON

```bash
export JF_SERVER="https://jf.company.com"
export JF_TOKEN="eyJhbGciOiJIUzI1NiJ9..."

# List all users as JSON for processing
jf users list --json | jq '.data[] | {name: .Name, id: .Id, lastActive: .LastActivityDate}'

# Check a specific user's policy
jf users get abc123 --json | jq '.data.Policy'
```

### Example 3: Library maintenance with dry-run safety

```bash
# Preview what a library scan will do
jf libraries scan --dry-run

# Actually trigger the scan
jf libraries scan

# Refresh metadata for a specific item, preview first
jf items refresh a1b2c3d4 --dry-run

# Execute the refresh
jf items refresh a1b2c3d4
```

### Example 4: Destructive operation with confirmation

```bash
# Preview deleting a movie
jf items delete a1b2c3d4 --dry-run
# Output:
#   [dry-run] Would delete item: "Bad Movie" (id: a1b2c3d4)
#     Type: Movie
#     Path: /media/movies/Bad Movie (2023)
#   No changes made.

# Actually delete it (will prompt for confirmation in human mode)
jf items delete a1b2c3d4
# Output:
#   Are you sure you want to delete "Bad Movie" (a1b2c3d4)?
#   This will remove the item from the library and delete files from disk.
#   Type 'yes' to confirm: yes
#   Deleted: "Bad Movie" (a1b2c3d4)

# Delete in a script without prompts
jf items delete a1b2c3d4 --yes --quiet
```

### Example 5: Managing playlists and using the raw escape hatch

```bash
# Create a playlist
jf playlists create "Weekend Watchlist" --type Video

# Add items to it
jf playlists items add abc123 --items d4e5f6,g7h8i9

# List what's in the playlist
jf playlists items list abc123

# Call an endpoint not yet wrapped by the CLI
jf raw GET "/Items/Filters2?parentId=xyz789" --json | jq '.data'

# POST with a body
jf raw POST "/Library/Media/Updated" --body '{"Updates":[]}'
```

---

## Appendix A: Project Structure (Spectre.Console.Cli)

```
src/Jf.Cli/
  Program.cs                           # Command tree registration, DI bootstrap
  Common/
    GlobalSettings.cs                  # Shared base settings
    TypeRegistrar.cs                   # Spectre-to-DI bridge
    ConfirmationHelper.cs              # --dry-run / --yes / --quiet logic
    OutputHelper.cs                    # JSON vs human output switching
    ExitCodes.cs                       # Exit code constants
  Commands/
    Auth/
      LoginCommand.cs                  # jf auth login
      LogoutCommand.cs                 # jf auth logout
      StatusCommand.cs                 # jf auth status
      WhoAmICommand.cs                 # jf auth whoami
      SetTokenCommand.cs              # jf auth set-token
      TestCommand.cs                   # jf auth test
      Profiles/
        ProfilesListCommand.cs
        ProfilesUseCommand.cs
        ProfilesShowCommand.cs
        ProfilesRenameCommand.cs
        ProfilesDeleteCommand.cs
      Host/
        HostListCommand.cs
        HostUseCommand.cs
        HostRenameCommand.cs
        HostDeleteCommand.cs
        Alias/
          AliasAddCommand.cs
          AliasRemoveCommand.cs
          AliasListCommand.cs
      ApiKeys/
        ApiKeysListCommand.cs
        ApiKeysCreateCommand.cs
        ApiKeysDeleteCommand.cs
      AuthService.cs                   # Login flows, token validation
      ConfigStore.cs                   # Unified config with hostname-keyed profiles
    Server/
      InfoCommand.cs
      PingCommand.cs
      RestartCommand.cs
      ShutdownCommand.cs
      Logs/
        LogsListCommand.cs
        LogsGetCommand.cs
      Activity/
        ActivityListCommand.cs
      Config/
        ServerConfigGetCommand.cs
        ServerConfigSetCommand.cs
      Backup/
        BackupListCommand.cs
        BackupCreateCommand.cs
        BackupShowCommand.cs
        BackupRestoreCommand.cs
      ServerClient.cs
    Users/
      UsersListCommand.cs
      UsersGetCommand.cs
      UsersCreateCommand.cs
      UsersUpdateCommand.cs
      UsersDeleteCommand.cs
      SetPasswordCommand.cs
      SetPolicyCommand.cs
      UsersClient.cs
    Items/
      ItemsListCommand.cs
      ItemsGetCommand.cs
      ItemsSearchCommand.cs
      ItemsUpdateCommand.cs
      ItemsDeleteCommand.cs
      ItemsRefreshCommand.cs
      ItemsDownloadCommand.cs
      LatestCommand.cs
      ResumeCommand.cs
      Favorites/
        FavoriteAddCommand.cs
        FavoriteRemoveCommand.cs
      Rating/
        RatingSetCommand.cs
        RatingDeleteCommand.cs
      Played/
        PlayedMarkCommand.cs
        PlayedUnmarkCommand.cs
      Images/
        ItemImagesListCommand.cs
        ItemImagesSetCommand.cs
        ItemImagesDeleteCommand.cs
      Subtitles/
        SubtitlesSearchCommand.cs
        SubtitlesDownloadCommand.cs
        SubtitlesUploadCommand.cs
        SubtitlesDeleteCommand.cs
      Lyrics/
        LyricsGetCommand.cs
        LyricsSearchCommand.cs
        LyricsDownloadCommand.cs
        LyricsUploadCommand.cs
        LyricsDeleteCommand.cs
      Lookup/
        LookupMovieCommand.cs
        LookupSeriesCommand.cs
        LookupApplyCommand.cs
      ItemsClient.cs
    Shows/
      EpisodesCommand.cs
      SeasonsCommand.cs
      NextUpCommand.cs
      UpcomingCommand.cs
      ShowsClient.cs
    Playlists/
      PlaylistsListCommand.cs
      PlaylistsGetCommand.cs
      PlaylistsCreateCommand.cs
      PlaylistsUpdateCommand.cs
      PlaylistsDeleteCommand.cs
      Items/
        PlaylistItemsListCommand.cs
        PlaylistItemsAddCommand.cs
        PlaylistItemsRemoveCommand.cs
        PlaylistItemsMoveCommand.cs
      Users/
        PlaylistUsersListCommand.cs
        PlaylistUsersUpdateCommand.cs
        PlaylistUsersRemoveCommand.cs
      PlaylistsClient.cs
    Collections/
      CollectionsCreateCommand.cs
      CollectionsAddCommand.cs
      CollectionsRemoveCommand.cs
      CollectionsClient.cs
    Libraries/
      LibrariesListCommand.cs
      LibrariesCreateCommand.cs
      LibrariesDeleteCommand.cs
      LibrariesScanCommand.cs
      Paths/
        PathsListCommand.cs
        PathsAddCommand.cs
        PathsUpdateCommand.cs
        PathsRemoveCommand.cs
      LibrariesClient.cs
    Sessions/
      SessionsListCommand.cs
      SessionsMessageCommand.cs
      SessionsPlayCommand.cs
      SessionsCommandCommand.cs
      SessionsClient.cs
    Devices/
      DevicesListCommand.cs
      DevicesGetCommand.cs
      DevicesDeleteCommand.cs
      DevicesClient.cs
    Plugins/
      PluginsListCommand.cs
      PluginsGetCommand.cs
      PluginsEnableCommand.cs
      PluginsDisableCommand.cs
      PluginsUninstallCommand.cs
      PluginsClient.cs
    Packages/
      PackagesListCommand.cs
      PackagesGetCommand.cs
      PackagesInstallCommand.cs
      PackagesCancelCommand.cs
      Repos/
        ReposListCommand.cs
        ReposSetCommand.cs
      PackagesClient.cs
    Tasks/
      TasksListCommand.cs
      TasksGetCommand.cs
      TasksStartCommand.cs
      TasksStopCommand.cs
      TasksSetTriggersCommand.cs
      TasksClient.cs
    LiveTv/
      LiveTvInfoCommand.cs
      Channels/
        ChannelsListCommand.cs
        ChannelsGetCommand.cs
      Programs/
        ProgramsListCommand.cs
        ProgramsGetCommand.cs
        ProgramsRecommendedCommand.cs
      Recordings/
        RecordingsListCommand.cs
        RecordingsGetCommand.cs
        RecordingsDeleteCommand.cs
      Timers/
        TimersListCommand.cs
        TimersGetCommand.cs
        TimersCreateCommand.cs
        TimersCancelCommand.cs
      SeriesTimers/
        SeriesTimersListCommand.cs
        SeriesTimersGetCommand.cs
        SeriesTimersCreateCommand.cs
        SeriesTimersCancelCommand.cs
      Tuners/
        TunersListCommand.cs
        TunersDiscoverCommand.cs
        TunersAddCommand.cs
        TunersDeleteCommand.cs
        TunersResetCommand.cs
      Listings/
        ListingsListCommand.cs
        ListingsAddCommand.cs
        ListingsDeleteCommand.cs
      LiveTvClient.cs
    SyncPlay/
      SyncPlayListCommand.cs
      SyncPlayGetCommand.cs
      SyncPlayCreateCommand.cs
      SyncPlayJoinCommand.cs
      SyncPlayLeaveCommand.cs
      SyncPlayPauseCommand.cs
      SyncPlayStopCommand.cs
      SyncPlayQueueCommand.cs
      SyncPlayClient.cs
    Artists/
      ArtistsListCommand.cs
      ArtistsGetCommand.cs
      ArtistsClient.cs
    Genres/
      GenresListCommand.cs
      GenresGetCommand.cs
      GenresClient.cs
    Config/
      ConfigPathCommand.cs
      ConfigGetCommand.cs
      ConfigSetCommand.cs
      ConfigStore.cs
    Raw/
      RawCommand.cs
  Infrastructure/
    TargetResolver.cs                  # Host + profile + env var resolution
    JellyfinHttpClient.cs             # Typed HTTP client with auth header injection
    JsonOutputSerializer.cs           # Stable JSON serializer for --json mode
```

---

## Appendix B: Program.cs Tree Registration Sketch

```csharp
var app = new CommandApp(registrar);
app.SetApplicationName("jf");

app.Configure(config =>
{
    config.SetApplicationVersion(version);

    config.AddBranch("auth", auth =>
    {
        auth.SetDescription("Authentication, identity, and profiles");
        auth.AddCommand<LoginCommand>("login");
        auth.AddCommand<LogoutCommand>("logout");
        auth.AddCommand<StatusCommand>("status");
        auth.AddCommand<WhoAmICommand>("whoami");
        auth.AddCommand<SetTokenCommand>("set-token");
        auth.AddCommand<TestCommand>("test");

        auth.AddBranch("profiles", profiles =>
        {
            profiles.SetDescription("Manage saved connection profiles");
            profiles.AddCommand<ProfilesListCommand>("list");
            profiles.AddCommand<ProfilesUseCommand>("use");
            profiles.AddCommand<ProfilesShowCommand>("show");
            profiles.AddCommand<ProfilesRenameCommand>("rename");
            profiles.AddCommand<ProfilesDeleteCommand>("delete");
        });

        auth.AddBranch("host", host =>
        {
            host.SetDescription("Manage configured hosts");
            host.AddCommand<HostListCommand>("list");
            host.AddCommand<HostUseCommand>("use");
            host.AddCommand<HostRenameCommand>("rename");
            host.AddCommand<HostDeleteCommand>("delete");

            host.AddBranch("alias", alias =>
            {
                alias.SetDescription("Manage host aliases");
                alias.AddCommand<AliasAddCommand>("add");
                alias.AddCommand<AliasRemoveCommand>("remove");
                alias.AddCommand<AliasListCommand>("list");
            });
        });

        auth.AddBranch("api-keys", keys =>
        {
            keys.SetDescription("[admin] Manage server API keys");
            keys.AddCommand<ApiKeysListCommand>("list");
            keys.AddCommand<ApiKeysCreateCommand>("create");
            keys.AddCommand<ApiKeysDeleteCommand>("delete");
        });
    });

    config.AddBranch("server", server =>
    {
        server.SetDescription("Server information and administration");
        server.AddCommand<InfoCommand>("info");
        server.AddCommand<PingCommand>("ping");
        server.AddCommand<RestartCommand>("restart");
        server.AddCommand<ShutdownCommand>("shutdown");
        server.AddCommand<StorageCommand>("storage");

        server.AddBranch("logs", logs =>
        {
            logs.AddCommand<LogsListCommand>("list");
            logs.AddCommand<LogsGetCommand>("get");
        });

        server.AddBranch("activity", activity =>
        {
            activity.AddCommand<ActivityListCommand>("list");
        });

        server.AddBranch("config", cfg =>
        {
            cfg.SetDescription("Server configuration (not local CLI config)");
            cfg.AddCommand<ServerConfigGetCommand>("get");
            cfg.AddCommand<ServerConfigSetCommand>("set");
        });

        server.AddBranch("backup", backup =>
        {
            backup.AddCommand<BackupListCommand>("list");
            backup.AddCommand<BackupCreateCommand>("create");
            backup.AddCommand<BackupShowCommand>("show");
            backup.AddCommand<BackupRestoreCommand>("restore");
        });

        server.AddBranch("branding", branding =>
        {
            branding.AddCommand<BrandingGetCommand>("get");
            branding.AddCommand<BrandingCssCommand>("css");

            branding.AddBranch("splashscreen", splash =>
            {
                splash.AddCommand<SplashGetCommand>("get");
                splash.AddCommand<SplashSetCommand>("set");
                splash.AddCommand<SplashDeleteCommand>("delete");
            });
        });
    });

    config.AddBranch("users", users =>
    {
        users.SetDescription("User management");
        users.AddCommand<UsersListCommand>("list");
        users.AddCommand<UsersGetCommand>("get");
        users.AddCommand<UsersCreateCommand>("create");
        users.AddCommand<UsersUpdateCommand>("update");
        users.AddCommand<UsersDeleteCommand>("delete");
        users.AddCommand<SetPasswordCommand>("set-password");
        users.AddCommand<SetPolicyCommand>("set-policy");
        users.AddCommand<SetConfigCommand>("set-config");
    });

    config.AddBranch("items", items =>
    {
        items.SetDescription("Browse, search, and manage library items");
        items.AddCommand<ItemsListCommand>("list");
        items.AddCommand<ItemsGetCommand>("get");
        items.AddCommand<ItemsSearchCommand>("search");
        items.AddCommand<ItemsUpdateCommand>("update");
        items.AddCommand<ItemsDeleteCommand>("delete");
        items.AddCommand<ItemsRefreshCommand>("refresh");
        items.AddCommand<ItemsDownloadCommand>("download");
        items.AddCommand<SimilarCommand>("similar");
        items.AddCommand<LatestCommand>("latest");
        items.AddCommand<ResumeCommand>("resume");
        items.AddCommand<SuggestionsCommand>("suggestions");
        items.AddCommand<CountsCommand>("counts");
        items.AddCommand<AncestorsCommand>("ancestors");

        items.AddBranch("favorite", fav =>
        {
            fav.AddCommand<FavoriteAddCommand>("add");
            fav.AddCommand<FavoriteRemoveCommand>("remove");
        });

        items.AddBranch("rating", rating =>
        {
            rating.AddCommand<RatingSetCommand>("set");
            rating.AddCommand<RatingDeleteCommand>("delete");
        });

        items.AddBranch("played", played =>
        {
            played.AddCommand<PlayedMarkCommand>("mark");
            played.AddCommand<PlayedUnmarkCommand>("unmark");
        });

        items.AddBranch("images", images =>
        {
            images.AddCommand<ItemImagesListCommand>("list");
            images.AddCommand<ItemImagesGetCommand>("get");
            images.AddCommand<ItemImagesSetCommand>("set");
            images.AddCommand<ItemImagesDeleteCommand>("delete");
            images.AddCommand<ItemImagesReorderCommand>("reorder");
        });

        items.AddBranch("remote-images", remoteImages =>
        {
            remoteImages.AddCommand<RemoteImagesListCommand>("list");
            remoteImages.AddCommand<RemoteImagesProvidersCommand>("providers");
            remoteImages.AddCommand<RemoteImagesDownloadCommand>("download");
        });

        items.AddBranch("subtitles", subs =>
        {
            subs.AddCommand<SubtitlesSearchCommand>("search");
            subs.AddCommand<SubtitlesDownloadCommand>("download");
            subs.AddCommand<SubtitlesUploadCommand>("upload");
            subs.AddCommand<SubtitlesDeleteCommand>("delete");
        });

        items.AddBranch("lyrics", lyrics =>
        {
            lyrics.AddCommand<LyricsGetCommand>("get");
            lyrics.AddCommand<LyricsSearchCommand>("search");
            lyrics.AddCommand<LyricsDownloadCommand>("download");
            lyrics.AddCommand<LyricsUploadCommand>("upload");
            lyrics.AddCommand<LyricsDeleteCommand>("delete");
        });

        items.AddBranch("lookup", lookup =>
        {
            lookup.SetDescription("Remote metadata search");
            lookup.AddCommand<LookupMovieCommand>("movie");
            lookup.AddCommand<LookupSeriesCommand>("series");
            lookup.AddCommand<LookupPersonCommand>("person");
            lookup.AddCommand<LookupAlbumCommand>("album");
            lookup.AddCommand<LookupArtistCommand>("artist");
            lookup.AddCommand<LookupApplyCommand>("apply");
        });
    });

    config.AddBranch("shows", shows =>
    {
        shows.SetDescription("TV show-specific views");
        shows.AddCommand<EpisodesCommand>("episodes");
        shows.AddCommand<SeasonsCommand>("seasons");
        shows.AddCommand<NextUpCommand>("next-up");
        shows.AddCommand<UpcomingCommand>("upcoming");
    });

    config.AddBranch("movies", movies =>
    {
        movies.SetDescription("Movie-specific views");
        movies.AddCommand<RecommendationsCommand>("recommendations");
    });

    config.AddBranch("playlists", playlists =>
    {
        playlists.SetDescription("Playlist management");
        playlists.AddCommand<PlaylistsListCommand>("list");
        playlists.AddCommand<PlaylistsGetCommand>("get");
        playlists.AddCommand<PlaylistsCreateCommand>("create");
        playlists.AddCommand<PlaylistsUpdateCommand>("update");
        playlists.AddCommand<PlaylistsDeleteCommand>("delete");

        playlists.AddBranch("items", pi =>
        {
            pi.AddCommand<PlaylistItemsListCommand>("list");
            pi.AddCommand<PlaylistItemsAddCommand>("add");
            pi.AddCommand<PlaylistItemsRemoveCommand>("remove");
            pi.AddCommand<PlaylistItemsMoveCommand>("move");
        });

        playlists.AddBranch("users", pu =>
        {
            pu.AddCommand<PlaylistUsersListCommand>("list");
            pu.AddCommand<PlaylistUsersGetCommand>("get");
            pu.AddCommand<PlaylistUsersUpdateCommand>("update");
            pu.AddCommand<PlaylistUsersRemoveCommand>("remove");
        });
    });

    config.AddBranch("collections", collections =>
    {
        collections.SetDescription("Collection (box set) management");
        collections.AddCommand<CollectionsCreateCommand>("create");
        collections.AddCommand<CollectionsAddCommand>("add");
        collections.AddCommand<CollectionsRemoveCommand>("remove");
    });

    config.AddBranch("libraries", libraries =>
    {
        libraries.SetDescription("Library and virtual folder management");
        libraries.AddCommand<LibrariesListCommand>("list");
        libraries.AddCommand<LibrariesCreateCommand>("create");
        libraries.AddCommand<LibrariesDeleteCommand>("delete");
        libraries.AddCommand<LibrariesUpdateCommand>("update");
        libraries.AddCommand<LibrariesRenameCommand>("rename");
        libraries.AddCommand<LibrariesScanCommand>("scan");
        libraries.AddCommand<MediaFoldersCommand>("media-folders");

        libraries.AddBranch("paths", paths =>
        {
            paths.AddCommand<PathsListCommand>("list");
            paths.AddCommand<PathsAddCommand>("add");
            paths.AddCommand<PathsUpdateCommand>("update");
            paths.AddCommand<PathsRemoveCommand>("remove");
        });
    });

    config.AddBranch("sessions", sessions =>
    {
        sessions.SetDescription("Active session management");
        sessions.AddCommand<SessionsListCommand>("list");
        sessions.AddCommand<SessionsMessageCommand>("message");
        sessions.AddCommand<SessionsPlayCommand>("play");
        sessions.AddCommand<SessionsCommandCommand>("command");
        sessions.AddCommand<SessionsSystemCommand>("system");
    });

    config.AddBranch("devices", devices =>
    {
        devices.SetDescription("Device management");
        devices.AddCommand<DevicesListCommand>("list");
        devices.AddCommand<DevicesGetCommand>("get");
        devices.AddCommand<DevicesOptionsCommand>("options");
        devices.AddCommand<DevicesUpdateCommand>("update");
        devices.AddCommand<DevicesDeleteCommand>("delete");
    });

    config.AddBranch("plugins", plugins =>
    {
        plugins.SetDescription("Plugin management");
        plugins.AddCommand<PluginsListCommand>("list");
        plugins.AddCommand<PluginsGetCommand>("get");
        plugins.AddCommand<PluginsUpdateCommand>("update");
        plugins.AddCommand<PluginsEnableCommand>("enable");
        plugins.AddCommand<PluginsDisableCommand>("disable");
        plugins.AddCommand<PluginsUninstallCommand>("uninstall");
    });

    config.AddBranch("packages", packages =>
    {
        packages.SetDescription("Package repository and installation");
        packages.AddCommand<PackagesListCommand>("list");
        packages.AddCommand<PackagesGetCommand>("get");
        packages.AddCommand<PackagesInstallCommand>("install");
        packages.AddCommand<PackagesCancelCommand>("cancel");

        packages.AddBranch("repos", repos =>
        {
            repos.AddCommand<ReposListCommand>("list");
            repos.AddCommand<ReposSetCommand>("set");
        });
    });

    config.AddBranch("tasks", tasks =>
    {
        tasks.SetDescription("Scheduled task management");
        tasks.AddCommand<TasksListCommand>("list");
        tasks.AddCommand<TasksGetCommand>("get");
        tasks.AddCommand<TasksStartCommand>("start");
        tasks.AddCommand<TasksStopCommand>("stop");
        tasks.AddCommand<TasksSetTriggersCommand>("set-triggers");
    });

    config.AddBranch("live-tv", livetv =>
    {
        livetv.SetDescription("Live TV management");
        livetv.AddCommand<LiveTvInfoCommand>("info");
        livetv.AddCommand<GuideInfoCommand>("guide-info");

        livetv.AddBranch("channels", ch =>
        {
            ch.AddCommand<ChannelsListCommand>("list");
            ch.AddCommand<ChannelsGetCommand>("get");
        });

        livetv.AddBranch("programs", prg =>
        {
            prg.AddCommand<ProgramsListCommand>("list");
            prg.AddCommand<ProgramsGetCommand>("get");
            prg.AddCommand<ProgramsRecommendedCommand>("recommended");
        });

        livetv.AddBranch("recordings", rec =>
        {
            rec.AddCommand<RecordingsListCommand>("list");
            rec.AddCommand<RecordingsGetCommand>("get");
            rec.AddCommand<RecordingsDeleteCommand>("delete");
        });

        livetv.AddBranch("timers", tmr =>
        {
            tmr.AddCommand<TimersListCommand>("list");
            tmr.AddCommand<TimersGetCommand>("get");
            tmr.AddCommand<TimersCreateCommand>("create");
            tmr.AddCommand<TimersUpdateCommand>("update");
            tmr.AddCommand<TimersCancelCommand>("cancel");
        });

        livetv.AddBranch("series-timers", stmr =>
        {
            stmr.AddCommand<SeriesTimersListCommand>("list");
            stmr.AddCommand<SeriesTimersGetCommand>("get");
            stmr.AddCommand<SeriesTimersCreateCommand>("create");
            stmr.AddCommand<SeriesTimersUpdateCommand>("update");
            stmr.AddCommand<SeriesTimersCancelCommand>("cancel");
        });

        livetv.AddBranch("tuners", tnr =>
        {
            tnr.AddCommand<TunersListCommand>("list");
            tnr.AddCommand<TunersDiscoverCommand>("discover");
            tnr.AddCommand<TunersAddCommand>("add");
            tnr.AddCommand<TunersDeleteCommand>("delete");
            tnr.AddCommand<TunersResetCommand>("reset");
        });

        livetv.AddBranch("listings", lst =>
        {
            lst.AddCommand<ListingsListCommand>("list");
            lst.AddCommand<ListingsLineupsCommand>("lineups");
            lst.AddCommand<ListingsAddCommand>("add");
            lst.AddCommand<ListingsDeleteCommand>("delete");
        });

        livetv.AddBranch("mappings", map =>
        {
            map.AddCommand<MappingsOptionsCommand>("options");
            map.AddCommand<MappingsSetCommand>("set");
        });
    });

    config.AddBranch("sync-play", syncplay =>
    {
        syncplay.SetDescription("SyncPlay group playback");
        syncplay.AddCommand<SyncPlayListCommand>("list");
        syncplay.AddCommand<SyncPlayGetCommand>("get");
        syncplay.AddCommand<SyncPlayCreateCommand>("create");
        syncplay.AddCommand<SyncPlayJoinCommand>("join");
        syncplay.AddCommand<SyncPlayLeaveCommand>("leave");
        syncplay.AddCommand<SyncPlayPlayCommand>("play");
        syncplay.AddCommand<SyncPlayPauseCommand>("pause");
        syncplay.AddCommand<SyncPlayStopCommand>("stop");
        syncplay.AddCommand<SyncPlaySeekCommand>("seek");
        syncplay.AddCommand<SyncPlayNextCommand>("next");
        syncplay.AddCommand<SyncPlayPreviousCommand>("previous");
        syncplay.AddCommand<SyncPlayQueueCommand>("queue");
        syncplay.AddCommand<SyncPlaySetQueueCommand>("set-queue");
        syncplay.AddCommand<SyncPlayRemoveCommand>("remove");
        syncplay.AddCommand<SyncPlaySetRepeatCommand>("set-repeat");
        syncplay.AddCommand<SyncPlaySetShuffleCommand>("set-shuffle");
    });

    config.AddBranch("artists", artists =>
    {
        artists.SetDescription("Browse artists");
        artists.AddCommand<ArtistsListCommand>("list");
        artists.AddCommand<ArtistsGetCommand>("get");
        artists.AddCommand<AlbumArtistsCommand>("album-artists");
    });

    config.AddBranch("genres", genres =>
    {
        genres.SetDescription("Browse genres");
        genres.AddCommand<GenresListCommand>("list");
        genres.AddCommand<GenresGetCommand>("get");

        genres.AddBranch("music", music =>
        {
            music.AddCommand<MusicGenresListCommand>("list");
            music.AddCommand<MusicGenresGetCommand>("get");
        });
    });

    config.AddBranch("config", cfg =>
    {
        cfg.SetDescription("Local CLI configuration");
        cfg.AddCommand<ConfigPathCommand>("path");
        cfg.AddCommand<ConfigGetCommand>("get");
        cfg.AddCommand<ConfigSetCommand>("set");
    });

    config.AddCommand<RawCommand>("raw");
});

return app.RunAsync(args);
```

---

## Appendix C: Design Decision Log

**Why no `jf play` top-level command?**
Jellyfin's playback is session-directed (you tell a *session/device* to play something). There is
no local playback. `jf sessions play <sessionId> <itemIds>` accurately reflects the mental model.

**Why `items` subsumes so many API tags?**
A user does not think "I need the ItemLookup controller." They think "I want to find metadata for
this movie." Grouping by item-centric tasks keeps the tree navigable. The alternative -- a
separate `images`, `subtitles`, `lyrics`, `lookup` top-level branch for each -- would scatter
related operations and make the CLI feel like an API dump.

**Why `live-tv` has deep sub-branches?**
Live TV is a complex feature area with 41 endpoints across channels, programs, recordings, timers,
series timers, tuners, and listings. Flattening these into `live-tv list-channels`,
`live-tv list-recordings`, etc. would produce an overwhelming flat list. Sub-branches let users
navigate incrementally.

**Why `raw` instead of `api`?**
`raw` signals "you are leaving the designed CLI surface." `api` could be mistaken for a
first-class branch. The name discourages casual use while remaining discoverable for power users.

**Why `--quick-connect` is a flag on `login`, not a separate command?**
The outcome is the same (authenticate and get a token). The method differs. A flag keeps the auth
branch surface minimal while making both paths discoverable from `jf auth login --help`.

**Why `jf movies` and `jf shows` exist as separate branches?**
These are thin convenience branches that map to distinct user tasks: "what's next up for my TV
shows?" and "give me movie recommendations." They could live under `items` but the cognitive
overhead of `jf items next-up --type Series` is higher than `jf shows next-up` for everyday use.

**Why streaming endpoints (DynamicHls, HlsSegment, Audio, Videos/stream) are not exposed?**
These are client-consumption endpoints meant for media players, not CLI users. They require
complex negotiation (codec, bitrate, container) that does not map to CLI tasks. The `raw` escape
hatch covers edge cases.

**Why `Startup` endpoints are omitted?**
The startup wizard is a one-time interactive flow best done in the web UI. Exposing it in the CLI
adds complexity with near-zero repeat usage. If needed, `raw` covers it.

**Why InstantMix, Channels, DisplayPreferences, and similar are not top-level branches?**
These are low-frequency or niche features. InstantMix could be a future `jf items instant-mix`
sub-command. Channels could live under a future `items channels` if demand warrants it. Keeping
the top-level tree focused on high-frequency tasks prevents bloat.

**Why hostname-keyed `hosts` instead of a flat `profiles` map?**
The original design used a flat `profiles` map with a separate `credentials.json`. This works for
single-server setups but breaks down with multiple servers: profile names collide, credentials are
decoupled from profiles, and there is no natural place for per-server defaults. The hostname-keyed
model groups profiles under their server, co-locates credentials, and enables zero-config for the
common single-server case via single-entry inference. See `jf-cli-profile-system.md` for the full
specification.

**Why `--server` instead of `--host`?**
The generic CLI pattern in `references/service-cli-patterns.md` uses `--host`, which is appropriate when the value
is always a network hostname. This CLI accepts a full URL, a bare hostname, or an alias — so
`--server` more accurately describes what the flag accepts. The short flag is `-S` (not `-H`) to
match.
