// LUNA EVO · LunaEvo.MissionControl.Core/Ops/Notifications.cs
// [Core] What LUNA EVO tells you about (server up/down/crashed, players, alerts), and Discord posting done safely.
// Used by: MainWindow.Notify, SettingsPage (test button)
// Guide:   Meat and Potatoes/17-Notifications.md

using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace LunaEvo.MissionControl.Core.Ops;

public enum NotificationKind
{
    ServerUp,
    ServerStopped,
    ServerCrashed,
    AutoRestartGaveUp,
    ScheduledRestart,
    PlayerJoined,
    PlayerLeft,
    AdminPasswordGuessing,
    BannedPlayerTried,
    LowDiskSpace,
}

/// <summary><paramref name="Subject"/> is untrusted text (a server or player name); it's escaped wherever it's shown.</summary>
public sealed record Notification(NotificationKind Kind, string Title, string Message, string Subject = "")
{
    /// <summary>Joins and leaves are chatty, so they have their own switch.</summary>
    public bool IsPlayerActivity => Kind is NotificationKind.PlayerJoined or NotificationKind.PlayerLeft;

    /// <summary>Alerts are always sent, whatever else is switched off.</summary>
    public bool IsAlert => Kind is NotificationKind.AdminPasswordGuessing or NotificationKind.BannedPlayerTried or
        NotificationKind.ServerCrashed or NotificationKind.AutoRestartGaveUp or NotificationKind.LowDiskSpace;
}

/// <summary>
/// Posts to a Discord channel through a webhook. Compared with 0.3.0 (removed in 0.4.0, see SECURITY.md H2/H3):
/// the URL is stored encrypted (<see cref="SecretProtector"/>), every post sets allowed_mentions to nothing so no
/// name or message can ping @everyone, and player-controlled text is escaped so it can't add formatting or links.
/// </summary>
public sealed partial class DiscordNotifier(HttpClient http)
{
    /// <summary>True for a URL that looks like a Discord webhook (so a typo doesn't send messages somewhere else).</summary>
    public static bool IsWebhookUrl(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
        uri.Host is "discord.com" or "discordapp.com" or "ptb.discord.com" or "canary.discord.com" &&
        uri.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.Ordinal) && string.IsNullOrEmpty(uri.UserInfo);

    /// <summary>The message as posted: a bold title, then the text. The subject is escaped inside both.</summary>
    public static string Format(Notification n) => Trim($"**{Escape(n.Title)}**\n{Escape(n.Message)}");

    /// <summary>Posts a notification. False (never throws) if the URL is wrong or Discord refused it.</summary>
    public Task<bool> SendAsync(string? webhookUrl, Notification n, CancellationToken cancel = default) =>
        PostAsync(webhookUrl, Format(n), cancel);

    public async Task<bool> PostAsync(string? webhookUrl, string content, CancellationToken cancel = default)
    {
        if (!IsWebhookUrl(webhookUrl))
            return false;
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        limit.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var body = new { username = "LUNA EVO", content, allowed_mentions = new { parse = Array.Empty<string>() } };
            using var response = await http.PostAsJsonAsync(webhookUrl!.Trim(), body, limit.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Makes outside text literal in Discord: Markdown characters are backslash-escaped, "@" and "#" can't start a
    /// mention, and "://" can't become a link. Belt and braces with allowed_mentions.
    /// </summary>
    public static string Escape(string text)
    {
        var escaped = MarkdownChars().Replace(text, @"\$0");
        return escaped.Replace("@", "@​").Replace("://", ":​//");
    }

    // Discord rejects messages over 2000 characters.
    private static string Trim(string message) => message.Length <= 1900 ? message : message[..1900] + "…";

    [GeneratedRegex(@"[\\*_~`|>\[\]()#<:-]")]
    private static partial Regex MarkdownChars();
}

/// <summary>
/// Encrypts secrets LUNA EVO has to keep (the Discord webhook URL) with Windows DPAPI, tied to your Windows account:
/// another account, or the settings file copied to another PC, can't decrypt it. Elsewhere, secrets aren't stored.
/// </summary>
public static class SecretProtector
{
    // Keeps the old app name on purpose: changing it would make every saved webhook undecryptable.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("LUNA EVO · Config Manager · secret v1");

    public static bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>Base64 of the encrypted text, or null where encryption isn't available.</summary>
    public static string? Protect(string secret)
    {
        if (!OperatingSystem.IsWindows())
            return null;
        return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.CurrentUser));
    }

    /// <summary>The secret, or null if it can't be decrypted (another account or PC, or damaged).</summary>
    public static string? Unprotect(string? protectedBase64)
    {
        if (string.IsNullOrEmpty(protectedBase64) || !OperatingSystem.IsWindows())
            return null;
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protectedBase64), Entropy, DataProtectionScope.CurrentUser));
        }
        catch (Exception e) when (e is CryptographicException or FormatException)
        {
            return null;
        }
    }
}
