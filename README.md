# LoupixDeck.Plugin.Claude
<img width="128" height="128" align="right" src="./icon.png"/>

<img width="307" height="232" alt="Screenshot 2026-10-08 at 17 28 28" src="https://github.com/user-attachments/assets/1c08f4df-4dce-4abe-bce9-99f9524e1fe0" />

A [LoupixDeck](https://github.com/RadiatorTwo/LoupixDeck) plugin for day-to-day
Claude: open Claude (optionally with a prompt), Quick entry, a Claude Code
session launcher, usage gauges for your 5-hour and 7-day limits, status keys for
Claude's services from status.claude.com, and a key that lights when a Claude Code
session is waiting for you. Built
against `LoupixDeck.PluginSdk` 1.30.0 (so it needs a LoupixDeck build with that
SDK) and targets .NET 10; runs on macOS, Linux
and Windows, with the limits listed under [Platform support](#platform-support).

Made by [**web-engineer**](https://www.web-engineer.co.uk/)
([@web-engineer](https://github.com/web-engineer)).

This is an independent project and is not affiliated with or endorsed by
Anthropic. Claude is a trademark of Anthropic.

## Platform support

| Feature | macOS | Windows | Linux |
|---|---|---|---|
| Open, New Code session, Continue last | Desktop app (deep link) or browser | Desktop app (deep link) or browser | Browser only |
| Claude CLI | Terminal.app, in the front Finder window's folder | Default terminal, in the active Explorer window's folder | Desktop's terminal, in the home folder |
| Quick entry | Yes | Yes | `No desktop app` (needs the desktop app on every OS) |
| Current state key: state and project name | Yes | Yes | Yes |
| Current state key: tap focuses the session | Yes | Desktop app sessions only; otherwise the key shows the session name | Key shows the session name |
| Usage keys | Yes | Yes | Yes |
| Status keys | Yes | Yes | Yes |

Deep links (`claude://`) need the Claude desktop app, which exists for macOS and
Windows. Without it:

- `Claude.Open` falls back to `https://claude.ai/new`.
- `Claude.NewCodeSession` and `Claude.ContinueLast` fall back to `https://claude.ai/code`.

## Install

**From the Plugin Store.** Once the plugin is listed, it appears under
LoupixDeck → Plugins → Store and updates from there.

**Manually.** Download `claude-<version>-any.zip` from the
[Releases](https://github.com/web-engineer/LoupixDeck.Plugin.Claude/releases)
page and unzip it into the plugin folder:

| OS | Folder |
|---|---|
| macOS, Linux | `~/.config/LoupixDeck/plugins/claude/` |
| Windows | `%APPDATA%\LoupixDeck\plugins\claude\` |

Restart LoupixDeck, then enable the plugin for your device under
Plugins → Installed. The folder must contain `LoupixDeck.Plugin.Claude.dll`,
its `.deps.json`, `icon.png` and `plugin.json`.

**Then**, for the keys that need it:

- Quick entry: set Claude › Settings › System › Quick access shortcut to the
  same shortcut as the plugin's *Quick entry shortcut* setting (see below).
- Usage: run `claude` once and sign in (`claude`, then `/login`), on every OS.
  The plugin then finds the sign-in itself. See [Usage keys](#usage-keys).
- The Quick entry keystroke needs the Accessibility permission that
  LoupixDeck already asks for on macOS.

## Build from source

Requires the .NET 10 SDK. The project sits at the repository root:

```bash
dotnet build -c Release
```

The build restores `LoupixDeck.PluginSdk` from nuget.org. A post-build step copies
the output into the host's plugin folder, so a rebuild followed by a LoupixDeck
restart is the whole loop:

| Host build | Target folder |
|---|---|
| Release host (default) | `~/.config/LoupixDeck/plugins/claude/` |
| Debug host (`-p:LoupixDebugHost=true`) | `~/.config/LoupixDeck/debug/plugins/claude/` |
| Windows | `%AppData%\LoupixDeck\plugins\claude\` |

Pass `-p:LoupixSkipInstall=true` to build without copying. `LoupixDeck.PluginSdk.dll`
and `*.runtimeconfig.json` are deliberately not copied: the host supplies the SDK
assembly and a second copy breaks plugin loading. Stop LoupixDeck before
rebuilding; the host holds the DLL open.

## Settings

Open LoupixDeck → Plugins → Claude → Settings. Saving applies immediately; the
workers restart from the next key render.

| Setting | Key | Default | Notes |
|---|---|---|---|
| Quick entry shortcut | `quick_entry_shortcut` | `Alt+Space` (macOS, i.e. Option+Space), `Ctrl+Alt+Space` (Windows, Linux) | Sent as a key press. Must match Claude › Settings › System › Quick access shortcut. Empty: the key shows `Set shortcut`. |
| Show usage | `show_usage` | on | Off: no requests are made and the usage keys show `off`. |
| Access token (optional) | `access_token` | empty | Only needed when the Claude Code sign-in cannot be found. See [Usage keys](#usage-keys). |
| Alert me when Claude wants input | `alert_waiting` | on | Off: the session folder is not watched and the Current state key shows `Off`. |
| Session folder (optional) | `sessions_dir` | empty (`~/.claude/sessions`) | Override only if Claude Code keeps its session files elsewhere. |
| Show status | `show_status` | on | Off: status.claude.com is not polled and the status keys show `Off`. |

Key names in the shortcut field use the host's macro syntax: `Alt` is the
Option key on macOS, and `Cmd` and `Win` both mean the Command key.

Buttons on the page:

- **Test usage lookup** resolves the token, queries the usage endpoint and reports
  where the token came from and both windows, for example
  `Token from macOS Keychain. 5-hour window 42% used, resets in 3h12; 7-day window 18% used, resets in 4d.`
  or the reason it failed.
- **List Claude Code sessions** reports each live session as
  `name (project): busy|waiting|idle`, or that there are none.
- **Test status lookup** fetches status.claude.com and reports the overall status,
  each service and any open incidents, for example
  `All Systems Operational. claude.ai operational; Claude API (api.anthropic.com) operational; …`.
- **Get support** opens [web-engineer.co.uk/contact](https://web-engineer.co.uk/contact),
  with our public Slack and our tracker.
- **Report a bug or request a feature** opens this repository's
  [GitHub issues](https://github.com/web-engineer/LoupixDeck.Plugin.Claude/issues).
  Please report security issues privately through the contact form instead.

## Commands

Command ids are permanent. All commands are in the group "Claude".

Claude keys share one look so they stand out on the deck: a Claude-orange
(`#FF551C`) background with white icons and text. On a touch key the launcher
and usage commands bring that look as ordinary layers when you assign them: an
icon layer, text layers and, for usage, an indicator layer, with the orange as
the key's own background colour. Restyle any of them in the button editor. A key
that had the command before version 0.2.0 has no layers; assign the command
again to get them.

| Command id | Key shows |
|---|---|
| `Claude.Open` | Chat icon, `Open Claude` |
| `Claude.QuickEntry` | Prompt icon, `Claude` |
| `Claude.NewCodeSession` | Code icon, `New Code` |
| `Claude.ContinueLast` | History icon, `Continue` |
| `Claude.CodeTerminal` | Console icon, `Claude CLI` |
| `Claude.Design` | Palette icon, `Design` |
| `Claude.Customize` | Sliders icon, `Customize` |
| `Claude.Scheduled` | Calendar icon, `Scheduled` |

| Command id | Parameter | Targets | What it does |
|---|---|---|---|
| `Claude.Open` | none | all | Opens an empty new chat via `claude://claude.ai/new`, or `https://claude.ai/new` without the desktop app. |
| `Claude.QuickEntry` | none | all | Sends the *Quick entry shortcut* with `System.KeyCombination(...)`. Quick entry has no deep link. Without the desktop app it shows `No desktop app`. |
| `Claude.NewCodeSession` | none | all | Opens `claude://code/new`, or `https://claude.ai/code`. |
| `Claude.ContinueLast` | none | all | Opens `claude://code/continue?session=last`, or `https://claude.ai/code`. |
| `Claude.CodeTerminal` | none | all | Starts the Claude Code CLI (`claude`) in a terminal. See [Claude CLI](#claude-cli). |
| `Claude.Design` | none | all | Opens Claude's Design page: `claude://claude.ai/design`, or `https://claude.ai/design`. |
| `Claude.Customize` | none | all | Opens Customize (skills, connectors, plugins): `claude://claude.ai/customize`, or `https://claude.ai/customize`. |
| `Claude.Scheduled` | none | all | Opens Claude Code's scheduled tasks: `claude://claude.ai/epitaxy/scheduled`, or the same page on claude.ai. |
| `Claude.Waiting` | none | touch buttons | Stateful key for Claude Code sessions. See below. |
| `Claude.UsageFiveHour` | none | touch buttons | The 5-hour window: one ring, percentage and time until reset. |
| `Claude.UsageWeekly` | none | touch buttons | The 7-day window: one ring, percentage and time until reset. |
| `Claude.UsageModelWeekly` | `Model` (default `Fable`) | touch buttons | One model's own 7-day limit, e.g. Fable: one ring, percentage and time until reset. |
| `Claude.Status` | none | touch buttons | Claude's overall status from status.claude.com. See [Status keys](#status-keys). |
| `Claude.Status.ClaudeAi` | none | touch buttons | Status of claude.ai. |
| `Claude.Status.Api` | none | touch buttons | Status of the Claude API (api.anthropic.com). |
| `Claude.Status.Code` | none | touch buttons | Status of Claude Code. |
| `Claude.Status.Console` | none | touch buttons | Status of the Claude Console (platform.claude.com). |
| `Claude.Status.Cowork` | none | touch buttons | Status of Claude Cowork. |

When nothing could be launched, a touch button briefly shows `Claude not found`.

### Claude CLI

`Claude.CodeTerminal` opens a new terminal window running `claude`, so the
Claude Code CLI must be installed and on your `PATH`. The window stays open when
`claude` exits.

- **Windows:** starts `cmd /k claude` in a new console. Windows opens it in the
  terminal set under Settings › System › For developers › Terminal (Windows
  Terminal or the console host). The folder is the one shown in the topmost
  File Explorer window, and in its selected tab on Windows 11.
- **macOS:** runs `claude` in a new Terminal.app window, in the folder of the
  front Finder window. The first time, macOS asks whether LoupixDeck may control
  Finder and Terminal.
- **Linux:** uses `$TERMINAL` when set, otherwise the desktop's own terminal
  (Konsole, GNOME Console or GNOME Terminal, Xfce Terminal), then
  `x-terminal-emulator`, kitty, Alacritty, foot or xterm. `claude` runs in your
  login shell. Linux file managers have no common way to report their folder,
  so the CLI starts in your home folder.

Without a file manager window, or when its folder is not a real directory (such
as This PC), the CLI starts in your home folder. When no terminal could be
started, a touch button briefly shows `No terminal`.

### Current state key

`Claude.Waiting` (listed as *Current state*) has three button states that follow the live sessions. Each
state brings its own layers when you assign the command, and you can restyle
them per state in the button editor:

| State | Meaning | Layers |
|---|---|---|
| `waiting` | At least one session is waiting on a permission prompt or a question | Inverted, orange on white: a bell, the project name (text source *Value*) and `+N more` when several are waiting (*Value detail*) |
| `busy` | Sessions are working, none needs you | An hourglass on orange |
| `idle` | No session needs you | Zzz on orange; a *Value* text shows `Off` while alerts are switched off |

A Current state key assigned before version 0.2.0 keeps its old empty states. Clear
the key and assign the command again to get these layers.

A session that has merely finished its turn is `idle` and does not light the key.

Tap the key to focus the waiting session. With several waiting, each tap moves
to the next one. With nothing waiting it shows `Nothing waiting`.

The key reads the session files Claude Code writes to `~/.claude/sessions/<pid>.json`
(`status` is `busy`, `waiting` or `idle`). This covers the CLI, the VS Code
extension and the desktop app's Code tab. No hooks and no Claude Code
configuration are needed. Sessions whose process has exited are ignored, so a
crashed session never lights the key.

Focusing:

- **macOS.** The plugin walks up the process tree from the session's pid to the
  first app bundle (VS Code, Terminal, iTerm and so on) and activates it. For the
  Claude desktop app it opens `claude://code/needs-input`, which also selects the
  right tab.
- **Windows.** Only sessions started from the Claude desktop app are focused
  (via `claude://code/needs-input`). For anything else the key shows the session
  name instead of focusing a window.
- **Linux.** The key never focuses a window; it shows the session name.

### Usage keys

`Claude.UsageFiveHour`, `Claude.UsageWeekly` and `Claude.UsageModelWeekly` each
show one window. The last one shows the weekly limit Anthropic sets for a single
model (Fable by default; set the `Model` parameter to the model's name as Claude
shows it). Accounts without such a limit see a dash. Its *Window* layer reads
`Fable`; edit that text if you choose another model. The plugin
reports the value and the key's layers draw it:

| Layer | Kind | Shows |
|---|---|---|
| Usage ring | Indicator | A white ring on a darkened track; full is 100 % used |
| Window | Text | `5h` or `week` |
| Percent | Text, source *Value* | The percentage, e.g. `17%` |
| Reset | Text, source *Value detail* | Time until the window resets, e.g. `3h12` or `4d` |

The combined two-ring key (`Claude.Usage`) from 0.1.0 is gone; use the two keys
side by side.

Tap either of them to fetch now and show a short overlay with both percentages and
reset times.

**This is an undocumented endpoint.** The key calls
`https://api.anthropic.com/api/oauth/usage`, the same unofficial endpoint Claude
Code's `/usage` uses, with the Claude Code OAuth token. It may change or
disappear without notice, and it may rate-limit you. The response is parsed
leniently; a response without either window shows `Unexpected response`.

- The key polls every 5 minutes while it is in use.
- On HTTP 429 the poll backs off, doubling from 5 minutes up to a 30 minute cap,
  and returns to 5 minutes after a good response.
- On any failure the last good values stay on the keys, marked `! stale` where the
  reset time normally is. With no values yet the keys show a dash and a short
  error.
- A 401 or 403 shows `Sign in to Claude Code again`.

**Token lookup order:**

1. macOS only: the Keychain item `Claude Code-credentials`, read with the
   `security` command line tool.
2. `~/.claude/.credentials.json`, which Claude Code writes on Linux and Windows
   (on Windows `%USERPROFILE%\.claude\.credentials.json`).
3. The *Access token* setting.

On every OS the fix is the same: run `claude` once and sign in (`claude`, then
`/login`), and the token is found automatically. If the plugin says it cannot
find a sign-in, paste into the *Access token* field either the whole credentials
JSON (the `claudeAiOauth` object Claude Code stores) or the bare access token
(it starts with `sk-ant-`).

The token is held only in memory and is never logged. A pasted token is stored in
the plugin's `settings.json` like any other setting. Access tokens expire and
Claude Code refreshes them when it runs; the plugin does not refresh anything, so
a pasted token will need pasting again. Prefer the automatic lookup.

### Status keys

`Claude.Status` shows the overall status of [status.claude.com](https://status.claude.com/);
`Claude.Status.ClaudeAi`, `.Api`, `.Code`, `.Console` and `.Cowork` each show one
service. Tap any of them to open the status page in the browser (the key also
fetches the status again).

Each service has its own command id instead of a parameter: LoupixDeck switches the
state of every key that shares a command name, so keys for different services
must not share one.

The keys have five button states. Each brings its own layers when you assign the
command; restyle them per state in the button editor:

| State | Meaning | Look | Status text |
|---|---|---|---|
| `operational` | Working normally | Check mark on orange | `OK` |
| `degraded` | Degraded performance (overall: minor incident) | Warning sign, dark text on amber | `Degraded` |
| `outage` | Partial or major outage | Octagon on red | `Partial` or `Outage` |
| `maintenance` | Maintenance in progress | Wrench on slate | `Maint.` |
| `unknown` | No status yet, the lookup failed, the service is not listed, or *Show status* is off | Cloud with a question mark on orange | `—` or `Off` |

Layers in every state: an icon, *Service* (the service name, plain text) and
*Status* (text source *Value*). When a lookup fails, the previous status stays
on the key; *Test status lookup* in the settings shows the error.

The keys read `https://status.claude.com/api/v2/summary.json`, the public
Statuspage summary. No token or account data is sent. On HTTP 429 or a server
error the poll backs off, doubling from 2 minutes up to 15, and returns to 2
minutes after a good response.

## Polling and cost

All workers start on demand and stop when nobody is looking:

| Worker | Runs while | Stops after |
|---|---|---|
| Session watcher (file watcher plus a 10 s rescan) | A `Claude.Waiting` key is on the current page and *Alert me when Claude wants input* is on | 30 s without the key being rendered |
| Usage poller (every 5 min) | A usage key is on the current page and *Show usage* is on | 12 min without the key being rendered |
| Status poller (every 2 min) | A status key is on the current page and *Show status* is on | 5 min without the key being rendered |

All three toggles are on by default. With a LoupixDeck build that pauses display
polling while the device is off, the workers also stop when the device is off.
The launcher commands run no background work.

## Privacy

The plugin makes two kinds of request: the usage request to `api.anthropic.com`,
made with your Claude Code token, only while a usage key is in use; and the
status request to `status.claude.com`, a public page fetched without any token or
account data, only while a status key is in use. Nothing else leaves the
machine. Session files in `~/.claude/sessions` are read locally and never
sent anywhere. Launcher commands open `claude://` links or claude.ai pages
through your system, which is the same as opening them yourself.

## Logging

Plugin lines are prefixed `plugin:claude` in LoupixDeck's log
(`~/.config/LoupixDeck/loupixdeck-startup.log` on macOS and Linux). At startup
the plugin logs whether the Claude desktop app was found. Workers log when they
start and stop, a successful usage lookup logs the two percentages and the token
source (never the token), a change of Claude's overall status is logged, and a
429 (or a status page server error) is logged as a warning.

## Licence and credits

MIT licensed: see [LICENSE](LICENSE). Copyright (c) 2026 Web-Engine Limited,
trading as [web-engineer](https://www.web-engineer.co.uk/).

Built on [LoupixDeck](https://github.com/RadiatorTwo/LoupixDeck) and its
[Plugin SDK](https://github.com/RadiatorTwo/LoupixDeck.PluginSdk) by RadiatorTwo,
both MIT licensed. The SDK is not redistributed with this plugin; LoupixDeck
supplies it at runtime.

Need help with a LoupixDeck plugin, a Claude integration or something else?
[Get in touch with web-engineer](https://www.web-engineer.co.uk/).
