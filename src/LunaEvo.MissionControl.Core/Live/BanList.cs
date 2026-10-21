// LUNA EVO · LunaEvo.MissionControl.Core/Live/BanList.cs
// [Core] LMP's ban list (LMPPlayerBans.txt: one player id per line), with unban.
// Used by: PlayersPage
// Guide:   Meat and Potatoes/16-Players-History-And-Alerts.md

namespace LunaEvo.MissionControl.Core.Live;

/// <summary>
/// LMP 0.29.2's /ban (Server/Command/Command/BanPlayerCommand.cs) adds the player's unique id to
/// &lt;server&gt;/LMPPlayerBans.txt, and the handshake refuses any id in that file. There's no /unban command.
/// LMP also keeps the list in memory and rewrites the file on the next ban, so unbanning while the server runs would
/// be undone: <see cref="Unban"/> is only offered while it's stopped.
/// </summary>
public sealed class BanList(string serverDirectory)
{
    public string FilePath { get; } = Path.Combine(serverDirectory, "LMPPlayerBans.txt");

    public IReadOnlyList<string> Ids()
    {
        try
        {
            return File.Exists(FilePath)
                ? File.ReadAllLines(FilePath).Select(l => l.Trim()).Where(l => l.Length > 0).Distinct(StringComparer.Ordinal).ToList()
                : [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    /// <summary>Removes an id, after copying the file to LunaEvo/Backups. False if it wasn't banned.</summary>
    public bool Unban(string id)
    {
        var ids = Ids();
        if (!ids.Contains(id, StringComparer.Ordinal))
            return false;
        var backups = Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(FilePath)!, "LunaEvo", "Backups")).FullName;
        File.Copy(FilePath, Path.Combine(backups, $"LMPPlayerBans_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"), overwrite: true);
        File.WriteAllLines(FilePath, ids.Where(i => i != id));
        return true;
    }
}
