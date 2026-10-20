// LUNA EVO · LunaEvo.ConfigManager.Core/Join/KspLauncher.cs
// [Core] Finds KSP and its LMP client, adds a server to LMP's own server list, and launches KSP straight into it.
// Used by: JoinPage
// Guide:   Meat and Potatoes/15-Joining-Servers.md

using System.Diagnostics;
using LunaEvo.ConfigManager.Core.Files;
using LunaEvo.ConfigManager.Core.Mods;

namespace LunaEvo.ConfigManager.Core.Join;

/// <summary>How a launch will connect, decided by <see cref="KspLauncher.PlanFor"/>.</summary>
public enum JoinMode
{
    /// <summary>KSP gets "-lmp lmp://host" and LMP connects by itself at the main menu.</summary>
    AutoConnect,

    /// <summary>The server is put at the top of LMP's server list; the player clicks it once KSP is up.</summary>
    PickFromList,
}

/// <summary>
/// What LMP 0.29.2's client supports (LmpClient/MainSystem.cs, HandleCommandLineArgs):
/// <list type="bullet">
/// <item>"-lmp lmp://host" connects automatically once the main menu loads, on port 8800.</item>
/// <item>A port in that address ("lmp://host:9000") is mis-parsed by LMP (it slices with an index from the unsliced
///   string), so it fails or crashes. And there's no way to pass a password.</item>
/// </list>
/// So: auto-connect only for port 8800 without a password; otherwise add the server (with port and password) to LMP's
/// own list in GameData/LunaMultiplayer/Data/settings.xml and let the player pick it. Fixing that parser is a good
/// first change for the LUNA EVO fork of LMP.
/// </summary>
public static class KspLauncher
{
    public const string ExeName = "KSP_x64.exe";

    /// <summary>KSP's folder from Steam's libraries, or null.</summary>
    public static string? FindKsp() => GameDataScanner.FindKspGameData() is { } gameData ? Path.GetDirectoryName(gameData) : null;

    public static bool IsKspFolder(string? folder) => folder != null && File.Exists(Path.Combine(folder, ExeName));

    /// <summary>The installed LMP client mod's version ("0.29.2"), or null if LMP isn't installed in this KSP.</summary>
    public static Version? ClientVersion(string kspFolder)
    {
        var dll = Path.Combine(kspFolder, "GameData", "LunaMultiplayer", "Plugins", "LmpClient.dll");
        return File.Exists(dll) ? Setup.LmpVersion.Parse(FileVersionInfo.GetVersionInfo(dll).ProductVersion) : null;
    }

    public static string ClientSettingsPath(string kspFolder) =>
        Path.Combine(kspFolder, "GameData", "LunaMultiplayer", "Data", "settings.xml");

    /// <summary>True if this KSP is running (LMP saves its settings while it runs, so we mustn't edit them then).</summary>
    public static bool IsRunning(string kspFolder) => ServerProcess.IsRunning(kspFolder, ExeName);

    public static JoinMode PlanFor(ServerAddress server, bool hasPassword) =>
        server.Port == ServerAddress.DefaultPort && !hasPassword ? JoinMode.AutoConnect : JoinMode.PickFromList;

    /// <summary>The command-line arguments for KSP. Empty when LMP can't auto-connect to this server.</summary>
    public static IReadOnlyList<string> LaunchArguments(ServerAddress server, bool hasPassword) =>
        PlanFor(server, hasPassword) == JoinMode.AutoConnect ? ["-lmp", "lmp://" + server.Host] : [];

    /// <summary>
    /// Adds the server to LMP's in-game server list, or updates it if the same address and port is already there.
    /// The old file is kept as settings.xml.lunaevo-backup. Throws if KSP is running or LMP has never been started.
    /// </summary>
    /// <returns>True if a new entry was added, false if an existing one was updated.</returns>
    public static bool AddToServerList(string kspFolder, string name, ServerAddress server, string? password)
    {
        if (IsRunning(kspFolder))
            throw new InvalidOperationException("Close KSP first: LMP saves its server list while it runs and would overwrite the change.");
        var path = ClientSettingsPath(kspFolder);
        if (!File.Exists(path))
            throw new FileNotFoundException("LMP hasn't created its settings yet. Start KSP with LMP once, then try again.", path);

        var file = ConfigFile.Load(path);
        var servers = (file.GetObjectList("Servers") ?? []).Select(s => new Dictionary<string, string>(s)).ToList();
        var existing = servers.FirstOrDefault(s =>
            string.Equals(s.GetValueOrDefault("Address"), server.Host, StringComparison.OrdinalIgnoreCase) &&
            s.GetValueOrDefault("Port") == server.Port.ToString());

        var entry = existing ?? new Dictionary<string, string>();
        entry["Port"] = server.Port.ToString();
        entry["Name"] = name;
        entry["Address"] = server.Host;
        entry["Password"] = password ?? entry.GetValueOrDefault("Password") ?? "";
        if (existing == null)
            servers.Insert(0, entry);

        File.Copy(path, path + ".lunaevo-backup", overwrite: true);
        file.SetObjectList("Servers", "ServerEntry", ["Port", "Name", "Address", "Password"],
            servers.Select(s => (IReadOnlyDictionary<string, string>)s));
        file.Save();
        return existing == null;
    }

    /// <summary>Starts KSP (from its own folder, so it finds GameData), optionally telling LMP to connect.</summary>
    public static Process? Launch(string kspFolder, IReadOnlyList<string> arguments)
    {
        var info = new ProcessStartInfo(Path.Combine(kspFolder, ExeName)) { WorkingDirectory = kspFolder, UseShellExecute = false };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);
        return Process.Start(info);
    }
}
