# Agent Usage Widget

[日本語](README.ja.md) | English

A Windows widget that displays Codex and Claude Code subscription usage in one window:
five-hour and weekly used percentages, remaining capacity, and reset times.
This is a fork of [ognjeeen/codex-usage-widget](https://github.com/ognjeeen/codex-usage-widget)
under the original MIT license. It is an independent utility.

![Sample usage](docs/images/agent-usage-widget-ja.png)

## Run

Extract `artifacts/release/agent-usage-widget-win-x64.zip` and run `AgentUsageWidget.exe`.
The portable package includes the .NET runtime. Build from source using the steps
below; portable packages have not yet been published to GitHub Releases.

Open Settings to choose Japanese or English, enable **Start with Windows**,
or toggle **Always on top**. Startup means launch at Windows sign-in.
**Show Codex** and **Show Claude Code** independently toggle provider visibility,
including tray tooltips. Preferences survive restarts; automatic polling continues
while hidden. Both can be hidden, and Settings remains available to restore them.
The app stays in the system tray. The minus button hides it and enables the small
Codex taskbar label; the close button exits it. Tray click opens the full widget.
With **Always on top** enabled, the opened widget stays visible when switching to
another app, including in taskbar indicator mode.
The tray icon and taskbar number show the selected Codex limit; the tray tooltip
also reports Claude usage. Both providers' full limits are visible in the main window.

## Connect providers

- **Codex:** install the Codex CLI and run `codex login`. The widget uses the official
  local app-server interface and never reads Codex authentication secrets directly.
  For a custom CLI location, set `CODEX_USAGE_WIDGET_CODEX_PATH` to `codex.cmd` or `codex.exe`.
- **Claude Code:** sign in with `/login` in the Windows CLI. The widget reads the existing
  `%USERPROFILE%\.claude\.credentials.json` OAuth access token and requests
  `https://api.anthropic.com/api/oauth/usage`. Use `USAGE_WIDGET_CLAUDE_CREDENTIALS`
  for a custom credentials file. It never modifies credentials or rotates tokens.
  If the token expires, update the login in Claude Code and refresh the widget.

Codex refreshes every two minutes and Claude every three minutes. Refresh updates
both independently. Claude errors retain the last successful values with a stale
label. HTTP 429 responses honor Retry-After, including during manual refresh.
Unavailable quota windows are omitted rather than presented as zero usage.
The Claude interface can change; WSL-only logins and API-key billing are not supported.
No prompts are generated, no telemetry is sent, and no remote backend is used.

## Coexistence

The executable is `AgentUsageWidget.exe`. Settings and logs use
`%LOCALAPPDATA%\AgentUsageWidget`. The startup entry, single-instance mutex, and
optional activity pipe are separate from the original Codex widget. Existing
Codex widget settings and hooks are preserved. Hooks are never installed at startup.

## Development

Use the .NET SDK pinned in `global.json`:

```powershell
dotnet restore .\CodexUsageWidget.slnx
dotnet build .\CodexUsageWidget.slnx -c Release --no-restore
dotnet test .\CodexUsageWidget.slnx -c Release --no-build
.\scripts\publish.ps1 -Runtime win-x64
```

The packaging script supports `win-x64` and `win-arm64` and produces
`artifacts/release/agent-usage-widget-<runtime>.zip`.

For sample data, without provider usage requests or real startup registration:

```powershell
dotnet run --project .\src\CodexUsageWidget\CodexUsageWidget.csproj -p:EnableUsagePreview=true -- --preview-usage
```

Preview settings such as language and always-on-top still use the normal local
settings folder. Standard release builds do not accept the preview flag.

See [architecture](docs/ARCHITECTURE.md), [research](docs/RESEARCH.ja.md), and
[release workflow](docs/RELEASING.md). MIT copyright and license text are retained in LICENSE.
