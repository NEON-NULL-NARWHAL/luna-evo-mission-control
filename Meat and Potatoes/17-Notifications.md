# 17 · Notifications (Windows and Discord)

[← Meat and Potatoes](README.md) · Code: `Core/Ops/Notifications.cs`, `App/DesktopNotifier.cs`, `App/MainWindow.cs` (`Notify`), `App/SettingsPage.cs`

Everything goes through **`MainWindow.Notify(Notification)`**, which sends it to Windows and Discord according to
LUNA EVO App settings → Notifications. It never throws.

| Kind | Sent by | Shown when |
|---|---|---|
| `ServerUp`, `ServerStopped`, `ScheduledRestart` | `ServerPage` | Always (if that channel is on) |
| `ServerCrashed`, `AutoRestartGaveUp` | `ServerPage.OnExitedAsync` | Always; counts as an alert |
| `AdminPasswordGuessing`, `BannedPlayerTried` | `PlayerTracker` events ([16](16-Players-History-And-Alerts.md)) | Always; alerts |
| `PlayerJoined`, `PlayerLeft` | `PlayerTracker` events | Only with "Also players joining and leaving" |

## Windows

`DesktopNotifier` uses the notification-area balloon (`Shell_NotifyIcon`, `NIF_INFO`), which Windows 10 and 11 show
as a normal notification and keep in the notification center. No NuGet package or app registration. The app name it
shows comes from the exe's **FileDescription** (`<AssemblyTitle>` in the csproj). The icon is added on the first
notification and removed when the window closes. Alerts use the warning icon. Not on Windows: nothing happens.

Tests replace `MainWindow.ShowDesktopNotification`; `UiHarness.Notifications` collects what would have been shown.

## Discord, done safely this time

Discord was removed in 0.4.0 because of SECURITY.md **H2** (plaintext webhook) and **H3** (player names could ping
`@everyone`). It came back in 0.5 with both fixed:

- **Stored encrypted.** `SecretProtector` uses Windows DPAPI (CurrentUser scope, plus app entropy):
  `AppSettings.DiscordWebhookProtected` is useless on another account or PC. The Settings page **never shows the
  saved URL again**, only "A webhook is saved". Elsewhere than Windows it refuses to store it.
- **No mentions, ever.** Every post sends `allowed_mentions: { parse: [] }`.
- **Escaped.** `DiscordNotifier.Escape` backslash-escapes Markdown, breaks `@` and `://` with a zero-width space.
  ⚠️ When testing, compare with `StringComparison.Ordinal`: culture-aware comparisons ignore the zero-width space.
- **Only real webhooks.** `IsWebhookUrl`: https, a Discord host, `/api/webhooks/`, no user info in the URL.
- 10-second limit; failures return false.

The old 0.3 `DiscordWebhookUrl` key is still scrubbed from `settings.json` on load.
