// LUNA EVO · LunaEvo.ConfigManager.Core/Ops/DiscordNotifier.cs
// [Core] Posts short messages (server up/down, crash, player joined) to a Discord channel via a webhook.
// Used by: ServerPage, PlayersPage, SettingsPage (test button)
// Guide:   Meat and Potatoes/13-Automation.md

using System.Net.Http.Json;

namespace LunaEvo.ConfigManager.Core.Ops;

/// <summary>
/// A Discord webhook is a URL Discord gives you under Channel settings → Integrations → Webhooks. Anyone with the
/// URL can post to that channel, so it's stored only in LUNA EVO's own settings file, never in the server config.
/// </summary>
public sealed class DiscordNotifier(HttpClient http)
{
    /// <summary>True for a URL that looks like a Discord webhook (so a typo doesn't send messages somewhere else).</summary>
    public static bool IsWebhookUrl(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
        uri.Host is "discord.com" or "discordapp.com" or "ptb.discord.com" or "canary.discord.com" &&
        uri.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.Ordinal);

    /// <summary>Posts a message. Returns false (never throws) if the URL is wrong or Discord refused it.</summary>
    public async Task<bool> SendAsync(string? webhookUrl, string message, CancellationToken cancel = default)
    {
        if (!IsWebhookUrl(webhookUrl))
            return false;
        try
        {
            using var response = await http.PostAsJsonAsync(webhookUrl!.Trim(), new { username = "LUNA EVO", content = Trim(message) }, cancel);
            return response.IsSuccessStatusCode;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    // Discord rejects messages over 2000 characters.
    private static string Trim(string message) => message.Length <= 1900 ? message : message[..1900] + "…";
}
