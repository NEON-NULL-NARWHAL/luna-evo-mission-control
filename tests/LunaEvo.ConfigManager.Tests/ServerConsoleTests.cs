// LUNA EVO · LunaEvo.ConfigManager.Tests/ServerConsoleTests.cs
// [Test] The Server Console page against the fake server: start, commands, filters, stop, restart, log files.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using LunaEvo.ConfigManager.Core.Server;
using LunaEvo.ConfigManager.Core.Validation;

namespace LunaEvo.ConfigManager.Tests;

/// <summary>The Server Console page, against the fake LMP server.</summary>
public class ServerConsoleTests
{
    [AvaloniaFact]
    public void Starts_shows_output_sends_commands_filters_and_stops()
    {
        using var ui = new UiHarness(fakeServer: true);
        ui.OpenTab(MainWindow.ConsolePage);
        Assert.False(ui.Find<Button>("console/stop").IsEnabled);
        Assert.False(ui.Find<TextBox>("console/command").IsEnabled);

        ui.Click(ui.Find<Button>("console/start"));
        ui.WaitFor(() => ui.Server.State == ServerState.Running, "server running");
        ui.WaitFor(() => Messages(ui).Any(m => m.StartsWith("All systems up and running")), "ready line in console");
        Assert.True(ui.Find<Button>("console/stop").IsEnabled);
        Assert.False(ui.Find<Button>("console/start").IsEnabled);
        Assert.StartsWith("Server running", ui.Find<TextBlock>("status/server").Text);

        // Quick command chip fills the box; Send runs it.
        ui.Click(ui.Find<Button>("console/quick//help"));
        Assert.Equal("/help", ui.Find<TextBox>("console/command").Text);
        ui.Click(ui.Find<Button>("console/send"));
        ui.WaitFor(() => Messages(ui).Contains("Command input: /help"), "command echoed by the server");
        Assert.Equal("", ui.Find<TextBox>("console/command").Text);

        ui.Find<TextBox>("console/command").Text = "hello kerbals";
        ui.Click(ui.Find<Button>("console/send"));
        ui.WaitFor(() => ui.Server.VisibleLines.Any(l => l is { Kind: LogKind.Chat, Message: "Server: hello kerbals" }), "chat line");

        // Filters and search
        Assert.Contains(ui.Server.VisibleLines, l => l.Kind == LogKind.Debug);
        ui.Find<ToggleButton>("console/filter/Debug").IsChecked = false;
        ui.Pump();
        Assert.DoesNotContain(ui.Server.VisibleLines, l => l.Kind == LogKind.Debug);
        ui.Find<TextBox>("console/search").Text = "kerbals";
        ui.Pump();
        Assert.All(ui.Server.VisibleLines, l => Assert.Contains("kerbals", l.ToString()));
        ui.Find<TextBox>("console/search").Text = "";

        ui.Click(ui.Find<Button>("console/stop"));
        Assert.Contains("Stop the server?", ui.DialogsShown);
        ui.WaitFor(() => ui.Server.State == ServerState.Stopped, "server stopped");
        ui.WaitFor(() => Messages(ui).Contains("So long and thanks for all the fish!"), "LMP's goodbye (clean Ctrl+C stop)");
        Assert.Contains(ui.Server.VisibleLines, l => l is { Kind: LogKind.Manager, Message: "Server exited (code 0)." });
        Assert.True(ui.Find<Button>("console/start").IsEnabled);
        Assert.Equal("Server stopped", ui.Find<TextBlock>("status/server").Text);
    }

    [AvaloniaFact]
    public void Starting_with_unsaved_changes_offers_to_save_first()
    {
        using var ui = new UiHarness(fakeServer: true);
        ui.OpenTab(LmpRules.General);
        ui.Find<TextBox>($"{LmpRules.General}/ServerName").Text = "LUNA EVO";
        ui.Pump();

        ui.OpenTab(MainWindow.ConsolePage);
        Assert.Contains("LUNA EVO", CardText(ui, "Server name"));
        ui.Answer = (title, _) => title == "Unsaved changes" ? 0 : null; // "Save and start"
        ui.Click(ui.Find<Button>("console/start"));

        ui.WaitFor(() => ui.Server.State == ServerState.Running, "server running");
        Assert.Contains("Unsaved changes", ui.DialogsShown);
        Assert.False(ui.Session.IsDirty);
        Assert.Contains("<ServerName>LUNA EVO</ServerName>", File.ReadAllText(Path.Combine(ui.ConfigDirectory, LmpRules.General)));
    }

    [AvaloniaFact]
    public void Cancelling_the_unsaved_changes_prompt_does_not_start()
    {
        using var ui = new UiHarness(fakeServer: true);
        ui.OpenTab(LmpRules.General);
        ui.Find<TextBox>($"{LmpRules.General}/ServerName").Text = "LUNA EVO";
        ui.Pump();
        ui.OpenTab(MainWindow.ConsolePage);

        ui.ConfirmDialogs = false;
        ui.Click(ui.Find<Button>("console/start"));

        Assert.Equal(ServerState.Stopped, ui.Server.State);
        Assert.True(ui.Session.IsDirty);
    }

    [AvaloniaFact]
    public void Restart_stops_and_starts_again()
    {
        using var ui = new UiHarness(fakeServer: true);
        ui.OpenTab(MainWindow.ConsolePage);
        ui.Click(ui.Find<Button>("console/start"));
        ui.WaitFor(() => ui.Server.State == ServerState.Running, "server running");

        ui.Click(ui.Find<Button>("console/restart"));
        ui.WaitFor(() => Messages(ui).Count(m => m.StartsWith("All systems up and running")) == 2, "second start");
        Assert.Equal(ServerState.Running, ui.Server.State);
        Assert.Contains(ui.Server.VisibleLines, l => l.Message == "Server exited (code 0).");
    }

    [AvaloniaFact]
    public void Closing_while_the_server_runs_offers_to_stop_it()
    {
        using var ui = new UiHarness(fakeServer: true);
        ui.OpenTab(MainWindow.ConsolePage);
        ui.Click(ui.Find<Button>("console/start"));
        ui.WaitFor(() => ui.Server.State == ServerState.Running, "server running");
        var server = ui.Server;

        ui.Answer = (title, _) => title == "The server is still running" ? 0 : null; // "Stop server and close"
        ui.Window.Close();

        ui.WaitFor(() => !ui.Window.IsVisible, "window closed");
        Assert.Contains("The server is still running", ui.DialogsShown);
        Assert.Equal(ServerState.Stopped, server.State);
    }

    [AvaloniaFact]
    public void Log_files_tab_lists_and_opens_log_files()
    {
        using var ui = new UiHarness(fakeServer: true);
        ui.OpenTab(MainWindow.ConsolePage);
        ui.Click(ui.Find<Button>("console/filesTab"));

        var files = ui.Find<ListBox>("console/files");
        var listed = files.ItemsSource!.Cast<FileInfo>().ToList();
        Assert.Equal(LogFiles.List(ui.ServerDirectory).Select(f => f.Name), listed.Select(f => f.Name));
        Assert.Equal(0, files.SelectedIndex);

        var lines = ui.Find<ListBox>("console/fileLines").ItemsSource!.Cast<LogLine>().ToList();
        Assert.Equal(LogFiles.Read(listed[0].FullName).Count, lines.Count);
        Assert.False(ui.Find<ListBox>("console/lines").IsEffectivelyVisible);

        ui.Click(ui.Find<Button>("console/liveTab"));
        Assert.True(ui.Find<ListBox>("console/lines").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void Summary_cards_follow_the_config()
    {
        using var ui = new UiHarness(fakeServer: true);
        ui.OpenTab(MainWindow.ConsolePage);
        Assert.Equal($"{ui.Session.GetValue(LmpRules.Connection, "Port")} UDP", CardText(ui, "Game port"));

        ui.OpenTab(LmpRules.Connection);
        ui.Find<NumericUpDown>($"{LmpRules.Connection}/Port").Value = 8810;
        ui.Pump();
        ui.OpenTab(MainWindow.ConsolePage);
        Assert.Equal("8810 UDP", CardText(ui, "Game port"));
    }

    private static IReadOnlyList<string> Messages(UiHarness ui) => ui.Server.VisibleLines.Select(l => l.Message).ToList();

    private static string CardText(UiHarness ui, string label) =>
        ui.Find<Border>("console/card/" + label).GetVisualDescendants().OfType<TextBlock>().Last().Text ?? "";
}
