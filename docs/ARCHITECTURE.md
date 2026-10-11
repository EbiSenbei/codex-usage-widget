# Architecture

## Agent Usage Widget fork

This fork keeps the original Codex app-server integration and adds an independent
`ClaudeUsageProvider` under `Infrastructure/Claude/`. It reads the existing Windows
Claude Code OAuth access token without changing credentials and requests only the
Anthropic usage endpoint. HTTP 429 responses retain their retry deadline even when
the user refreshes manually. Parsing produces the same domain `UsageSnapshot` type;
missing windows are omitted rather than synthesized as zero usage.

`App` composes two `UsageMonitor` instances. `MainWindow` renders Claude through
`ClaudeUsageView` and `ClaudeUsageViewModel`; a failure in either monitor cannot
block the other. The Claude view retains the last successful snapshot with a stale
message on failure. Monitor disposal cancels and waits for active reads before
disposing transport resources. The original Codex details and reset use cases are
kept separate from Claude.

Japanese uses the existing .NET resource and language preference mechanism.
`AlwaysOnTopPreferenceStore` persists the desktop widget's z-order preference.
`ProviderVisibilityStore` saves independent Codex/Claude visibility flags. The
desktop widget resizes immediately and tray content follows the selection;
hidden providers continue to refresh. Both flags can be off while Settings remains
accessible. Missing or malformed preferences default to both providers visible.
The executable, app-data folder, startup registration, mutex, and optional activity
pipe use `AgentUsageWidget` identities, preserving a separately installed original
Codex widget. The optional hooks recognize this fork's handlers, not the original's.
Preview builds use sample data for both providers and an in-memory startup store.

The following sections describe the inherited Codex-specific implementation.

The solution uses explicit composition in `App.xaml.cs` and keeps framework,
application, domain, and infrastructure responsibilities separate without a
dependency-injection package.

## Project layout

```text
src/CodexUsageWidget/
├── Application/             Refresh orchestration, activity state and presentation formatting
├── Domain/                  Rate-limit, credit, spend-control and activity models
├── Infrastructure/
│   ├── Codex/               App-server integration plus lifecycle-hook parsing and local IPC
│   ├── Logging/             Local file diagnostics
│   ├── Settings/            Persistent preferences and pending reset attempts
│   └── Windows/             Tray icon and taskbar Win32 integration
└── Views/                   WPF shell, presentation models and focused controls
tests/CodexUsageWidget.Tests/ Unit tests for parsing, formatting and persistence
```

## Runtime flow

1. `Program` handles activity-hook/configuration command modes before WPF startup. A normal
   launch from a temporary ZIP location is copied atomically to a versioned per-user app
   directory and relaunched; `App` then acquires the single-instance mutex and constructs
   the widget object graph.
2. `UsageMonitor` owns refresh scheduling, timeout handling and refresh coalescing.
3. `CodexUsageProvider` coordinates required rate-limit reads and optional token-activity reads.
4. `RateLimitResetUseCase` coordinates explicit redemption, normalizes failures for the view,
   and waits for a fresh usage read after every definitive outcome.
5. `CodexRateLimitResetConsumer` owns the app-server request while
   `RateLimitResetAttemptStore` durably keeps one idempotency key for retries until the server
   returns a definitive outcome.
6. `CodexAppServerSession` owns initialized app-server connection lifetime.
7. `JsonRpcConnection` owns stdin/stdout request correlation and process lifetime.
8. Endpoint-specific parsers convert Codex payloads into domain records.
9. A path-independent PowerShell hook bridge forwards minimal lifecycle signals to
   `CodexActivityPipeSignalSource` over a current-user-only named pipe;
   `CodexActivityMonitor` owns one active turn per session and emits only final boolean
   transitions.
10. `CodexActivityHookSetupService` coordinates reviewable hook-file changes and reads
   trust state through `hooks/list`; `CodexHookTrustStatusParser` owns the protocol shape.
11. `ActivityHookSetupControl` presents setup status inside Settings while a separate review
   dialog shows the exact proposed file content before installation or removal.
12. `UsageWidgetViewModel` maps snapshots to immutable presentation state.
13. `AppThemeController` applies the saved system, light, or dark theme plus the selected
    accent palette, and observes Windows theme changes without leaking registry access into
    view code.
14. `AppLanguageController` resolves the saved system, English, or Simplified Chinese
    preference, while standard .NET resources and a notifying WPF binding refresh existing UI.
15. `TimeTextFormatter` applies the saved Windows regional, 24-hour, or 12-hour clock
    preference to user-visible times while protocol and diagnostic timestamps remain unchanged.
16. `MainWindow` remains a window-lifecycle shell while the Settings window coordinates
    activity-hook setup plus immediate theme, accent, time-format, widget-layout,
    displayed-limit, and Windows startup preferences. Focused user controls render compact,
    detailed, and repeated limit-row content.

## Dependency direction

- Domain types do not depend on WPF, WinForms, process APIs, or JSON.
- Application orchestration depends on domain types and the `IUsageProvider` port.
- Infrastructure implements that port and owns OS/external-process details.
- Views consume application/domain state and do not parse protocol payloads.

## Reliability decisions

- All transport awaits use `ConfigureAwait(false)` so shutdown cannot deadlock the
  WPF UI thread.
- Failed app-server startup is disposed before a later refresh reconnects.
- Optional token-activity failures degrade only the detailed activity section; core
  rate-limit monitoring remains available.
- Usage preview mode owns synthetic reset credits and redemption outcomes, so UI tests and
  manual preview checks never consume a real account reset.
- Redemption writes its idempotency key before contacting Codex, preserves it across app
  restarts after an uncertain response, and removes it only after a definitive outcome.
- Post-redemption refresh waits behind an active usage read instead of discarding the request,
  so the view cannot offer a consumed credit again from a stale snapshot.
- A semaphore prevents concurrent refreshes and a mutex prevents duplicate apps.
- Activity hook IPC is bounded and local to the current Windows user. The hook path does not
  depend on the release extraction directory and does not start WPF. Accepted clients are
  consumed in order with a per-client read timeout, while separate pipe instances keep parallel
  Codex sessions connectable. Duplicate events are idempotent, a new turn replaces an orphaned
  turn in the same session, late completion for the replaced turn is ignored, and session end
  removes only that session.
- UI hook setup reuses the same compare-before-write configuration plan as the CLI flow.
  Codex remains the owner of hook trust; the widget only reads trust state and opens the
  interactive CLI for the user's explicit `/hooks` approval.
- Activity state is not persisted or reconstructed with private transcript/database polling.
  While active, the monitor checks tracked turns every 15 seconds through
  `CodexTurnCompletionReader`, using official `thread/turns/list` metadata with items omitted.
  Only an exact turn match with a terminal status and completion timestamp clears activity;
  missing or unavailable evidence preserves it. Each check is bounded to five seconds and
  shutdown cancels pending checks before disposing the shared app-server session. A hard
  termination with no recorded completion still requires a later lifecycle event or restart.
- Unhandled exceptions and CLI diagnostics are recorded locally for support.
- Publish trimming is disabled because WPF is not a safe trimming boundary.

## Extending the app

- Add another usage source by implementing `IUsageProvider`.
- Add new Codex payload variants to the parser for that endpoint with fixture-based tests.
- Keep reusable presentation state in `Views/ViewModels` and focused visual sections in
  `Views/Controls`; `MainWindow` should not absorb endpoint or rendering responsibilities.
- Keep Win32 calls under `Infrastructure/Windows` and UI rendering under `Views`.
- Extend user preferences through the Settings window. Keep appearance application in
  `AppThemeController` instead of placing theme decisions in individual views.
- Avoid placing persistence, process management, or protocol parsing in code-behind.
