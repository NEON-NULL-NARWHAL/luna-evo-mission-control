// LUNA EVO · LunaEvo.ConfigManager/ProfilesPage.cs
// [UI] Config profiles (save/switch whole setups) and Universe backups (snapshot/restore the game state).
// Used by: MainWindow
// Guide:   Meat and Potatoes/13-Automation.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using LunaEvo.ConfigManager.Core.Ops;

namespace LunaEvo.ConfigManager;

internal sealed class ProfilesPage
{
    private readonly MainWindow _host;
    private readonly StackPanel _profiles = new() { Spacing = 2 };
    private readonly StackPanel _backups = new() { Spacing = 2 };
    private readonly TextBlock _backupStatus = Ui.Text("", "sublabel").WithId("profiles/backupStatus");

    public ProfilesPage(MainWindow host)
    {
        _host = host;

        var saveProfile = Ui.Button("Save current config as a profile...", "text", "IconSave").WithId("profiles/save");
        saveProfile.Click += async (_, _) => await SaveProfileAsync();
        var backupNow = Ui.Button("Back up the Universe now", "text", "IconSave").WithId("profiles/backupNow");
        backupNow.Click += async (_, _) => await BackupNowAsync();
        var openBackups = Ui.Button("Open folder", "text", "IconFolder");
        openBackups.Click += (_, _) =>
        {
            if (_host.Session is { } s)
                Ui.Open(Directory.CreateDirectory(new UniverseBackups(s.ServerDirectory).Root).FullName);
        };

        var (profiles, profilesBody, _) = Ui.Group("Config profiles");
        profilesBody.Children.Add(new Border
        {
            Padding = new Thickness(12, 8, 12, 4),
            Child = Ui.Text("A profile is a copy of all 13 config files, e.g. \"Career campaign\" and \"Sandbox chaos night\". " +
                            "Switching backs up the current config first.", "sublabel"),
        });
        profilesBody.Children.Add(_profiles);
        profilesBody.Children.Add(new Border { Padding = new Thickness(4), Child = saveProfile });

        var (backups, backupsBody, _) = Ui.Group("Universe backups");
        backupsBody.Children.Add(new Border
        {
            Padding = new Thickness(12, 8, 12, 4),
            Child = Ui.Text("The Universe is the game itself: vessels, kerbals, science, contracts. These are dated zips you can go back to. " +
                            "Schedule them in App settings → Automation & alerts.", "sublabel"),
        });
        backupsBody.Children.Add(_backups);
        backupsBody.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4), Children = { backupNow, openBackups, _backupStatus } });
        _backupStatus.VerticalAlignment = VerticalAlignment.Center;

        View = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(28, 20, 28, 28),
                Spacing = 4,
                Children = { Ui.Text("Profiles & backups", "headline"), new Border { Height = 12 }, profiles, backups },
            },
        };
    }

    public Control View { get; }

    public void Refresh()
    {
        _profiles.Children.Clear();
        _backups.Children.Clear();
        if (_host.Session is not { } session)
            return;

        var store = new ProfileStore(session.ServerDirectory);
        foreach (var profile in store.List())
        {
            var apply = Ui.Button("Switch to this", "text").WithId("profiles/apply/" + profile.Name);
            apply.Click += async (_, _) => await ApplyProfileAsync(profile.Name);
            var delete = Ui.Button("Delete", "text").WithId("profiles/delete/" + profile.Name);
            delete.Click += async (_, _) =>
            {
                if (await Dialogs.ConfirmAsync(_host, "Delete profile?", $"Delete the profile \"{profile.Name}\"? The current config isn't affected.", "Delete"))
                {
                    store.Delete(profile.Name);
                    Refresh();
                }
            };
            _profiles.Children.Add(Ui.Row(profile.Name, $"Saved {profile.Saved:yyyy-MM-dd HH:mm} · {profile.FileCount} files",
                new StackPanel { Orientation = Orientation.Horizontal, Children = { apply, delete } }).WithId("profiles/row/" + profile.Name));
        }
        if (_profiles.Children.Count == 0)
            _profiles.Children.Add(Ui.Row("No profiles yet", null, null));

        foreach (var snapshot in new UniverseBackups(session.ServerDirectory).List())
        {
            var restore = Ui.Button("Restore", "text").WithId("profiles/restore/" + snapshot.Name);
            restore.Click += async (_, _) => await RestoreAsync(snapshot);
            _backups.Children.Add(Ui.Row($"{snapshot.Created:yyyy-MM-dd HH:mm:ss}",
                $"{(snapshot.Label.Length > 0 ? snapshot.Label + " · " : "")}{snapshot.Size / 1024.0:0} KB · {snapshot.Name}", restore)
                .WithId("profiles/backup/" + snapshot.Name));
        }
        if (_backups.Children.Count == 0)
            _backups.Children.Add(Ui.Row("No Universe backups yet", null, null));
    }

    internal async Task SaveProfileAsync()
    {
        if (_host.Session is not { } session)
            return;
        if (session.IsDirty && !await _host.SaveAsync())
            return; // a profile is the files on disk, so unsaved changes must be saved first
        var name = await Dialogs.PromptAsync(_host, "Save profile", "Name for this profile:", session.GetValue("GeneralSettings.xml", "GameMode") + " setup", "Save");
        if (string.IsNullOrWhiteSpace(name))
            return;
        new ProfileStore(session.ServerDirectory).Save(name, session.ConfigDirectory);
        Refresh();
    }

    internal async Task ApplyProfileAsync(string name)
    {
        if (_host.Session is not { } session)
            return;
        if (!await Dialogs.ConfirmAsync(_host, "Switch profile?",
                $"Replace the current config with \"{name}\"? The current files are backed up first" +
                (session.IsDirty ? ", and your unsaved changes will be lost." : ".") +
                (_host.Server?.IsRunning == true ? " Restart the server afterwards to use it." : ""), "Switch"))
            return;
        var backup = new ProfileStore(session.ServerDirectory).Apply(name, session.ConfigDirectory);
        _host.Reopen();
        await Dialogs.InfoAsync(_host, "Profile applied", $"Now using \"{name}\". The previous config is in {backup}.");
    }

    internal async Task BackupNowAsync()
    {
        if (_host.Session is not { } session)
            return;
        _backupStatus.Text = "Backing up...";
        if (_host.Server?.IsOwnedActive == true)
        {
            _host.Server.SendServerCommand("/backup now"); // fresh files first
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
        try
        {
            var backups = new UniverseBackups(session.ServerDirectory);
            var (snapshot, skipped) = await Task.Run(() => backups.Create("manual"));
            backups.Prune(_host.Settings.UniverseBackupsToKeep);
            _backupStatus.Text = $"Saved {snapshot.Name}{(skipped > 0 ? $" ({skipped} busy files skipped)" : "")}";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _backupStatus.Text = e.Message;
        }
        Refresh();
    }

    internal async Task RestoreAsync(UniverseSnapshot snapshot)
    {
        if (_host.Session is not { } session)
            return;
        if (_host.Server?.IsRunning == true)
        {
            await Dialogs.InfoAsync(_host, "Stop the server first", "The Universe can only be restored while the server is stopped.");
            return;
        }
        if (!await Dialogs.ConfirmAsync(_host, "Restore the Universe?",
                $"Go back to {snapshot.Created:yyyy-MM-dd HH:mm}? Everything since then (vessels, progress) is replaced. " +
                "The current Universe is backed up first, so you can undo this.", "Restore"))
            return;
        var safety = new UniverseBackups(session.ServerDirectory).Restore(snapshot.Path);
        _backupStatus.Text = safety != null ? $"Restored. The previous state is in {safety.Name}." : "Restored.";
        Refresh();
    }
}
