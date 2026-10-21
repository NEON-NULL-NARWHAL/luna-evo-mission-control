// LUNA EVO · LunaEvo.MissionControl.Core/Http.cs
// [Core] The one HttpClient the app uses for GitHub and LMP's website JSON. Tests swap in fakes.
// Used by: SetupWizard, SettingsPage, PlayersPage, ServerPage
// Guide:   Meat and Potatoes/10-Setup-and-Updates.md

namespace LunaEvo.MissionControl.Core;

public static class Http
{
    private static readonly Lazy<HttpClient> Default = new(() =>
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        // GitHub's API rejects requests without a User-Agent.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LUNA-EVO-MissionControl");
        return client;
    });

    private static HttpClient? _override;

    /// <summary>The shared client. Tests set <see cref="Override"/> to one with a fake handler.</summary>
    public static HttpClient Client => _override ?? Default.Value;

    public static HttpClient? Override
    {
        get => _override;
        set => _override = value;
    }
}
