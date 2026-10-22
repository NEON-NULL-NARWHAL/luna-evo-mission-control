// LUNA EVO · LunaEvo.MissionControl.Tests/UiHarness.cs
// [Test] Opens the real window headlessly and drives it like a user (find by AutomationId, click, wait).
// Used by: All UI tests
// Guide:   Meat and Potatoes/05-Tests.md

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LunaEvo.MissionControl.Core.Tests;

[assembly: AvaloniaTestApplication(typeof(LunaEvo.MissionControl.Tests.TestApp))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace LunaEvo.MissionControl.Tests;

public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

/// <summary>Opens the real main window over a throwaway copy of server-configs and drives it like a user.</summary>
internal sealed class UiHarness : IDisposable
{
    /// <param name="fakeServer">Use a server folder whose "Server.exe" is the FakeLmpServer, so it can really be started.</param>
    /// <param name="openServer">False starts on the welcome page with nothing open (for the setup wizard).</param>
    public UiHarness(bool fakeServer = false, bool openServer = true)
    {
        // A copy, never the fixture itself: the app writes next to the server (LunaEvo/players.json, profiles...).
        ServerDirectory = fakeServer ? CreateFakeServerFolder() : CopyFixtureServer();
        ConfigDirectory = TestPaths.CopyConfigs();
        SettingsPath = Path.Combine(Path.GetTempPath(), "LunaEvoTests", Guid.NewGuid().ToString("N"), "settings.json");
        ServerPage.ExecutableName = fakeServer ? FakeServer.Executable : "Server.exe";
        // No real network in tests: GitHub and LMP's website go to this fake (unknown URLs get 404).
        Web = new FakeHttp();
        Core.Http.Override = Web.Client;
        JoinPage.StartKsp = (_, _) => throw new InvalidOperationException("A test tried to start KSP without faking it.");
        ModImportPanel.PickSharePath = _ => throw new InvalidOperationException("A test opened a save dialog without faking it.");
        MainWindow.ShowDesktopNotification = (title, message, alert) => Notifications.Add((title, message, alert));
        CraftsPage.PickExportPath = (_, _) => throw new InvalidOperationException("A test opened a save dialog without faking it.");
        CraftsPage.PickImportPath = _ => throw new InvalidOperationException("A test opened a file dialog without faking it.");
        HomeServer.ApplyStartWithWindows = (_, _) => throw new InvalidOperationException("A test touched the Windows startup list without faking it.");
        CkanPage.StartCkan = _ => throw new InvalidOperationException("A test tried to start CKAN without faking it.");
        CkanPage.CopyText = (_, text) => { Copied.Add(text); return Task.CompletedTask; };
        BrowserPage.Ping = _ => Task.FromResult<long?>(42); // never ping real addresses from tests
        JoinPage.PingServer = _ => Task.FromResult(new Core.Join.PingResult(4, 4, 12, 10));
        CkanPage.RunCkan = (_, _, _, _, _) => throw new InvalidOperationException("A test tried to run CKAN without faking it.");
        CkanPage.PickModList = _ => throw new InvalidOperationException("A test opened a file dialog without faking it.");
        CkanPage.PickCkanExe = _ => throw new InvalidOperationException("A test opened a file dialog without faking it.");
        CkanPage.PickExportPath = (_, _) => throw new InvalidOperationException("A test opened a save dialog without faking it.");
        Core.Setup.CkanTools.Root = Path.Combine(Path.GetTempPath(), "LunaEvoTests", "ckan-" + Guid.NewGuid().ToString("N"));
        Core.Setup.CkanTools.ConfigFolder = Path.Combine(Path.GetTempPath(), "LunaEvoTests", "ckancfg-" + Guid.NewGuid().ToString("N"));
        MainWindow.CurrentExe = null; // the test host must never be "updated"
        MainWindow.StartNewVersion = (_, _) => throw new InvalidOperationException("A test tried to restart the app without faking it.");
        Dialogs.Responder = (title, _, buttons) =>
        {
            DialogsShown.Add(title);
            return Answer?.Invoke(title, buttons) ?? (ConfirmDialogs ? 0 : buttons.Count - 1);
        };
        Window = new MainWindow(openServer ? new StartupOptions(ServerDirectory, ConfigDirectory, SettingsPath) : new StartupOptions(null, null, SettingsPath));
        Window.Show();
        Pump();
    }

    public MainWindow Window { get; }
    public FakeHttp Web { get; }
    public string ServerDirectory { get; }
    public string ConfigDirectory { get; }
    public string SettingsPath { get; }
    public List<string> DialogsShown { get; } = [];

    /// <summary>Text the app put on the clipboard (CKAN page → Copy log).</summary>
    public List<string> Copied { get; } = [];

    /// <summary>Windows notifications the app tried to show: (title, message, is an alert).</summary>
    public List<(string Title, string Message, bool Alert)> Notifications { get; } = [];
    public bool ConfirmDialogs { get; set; } = true;

    /// <summary>Picks a dialog button by title: return its index, or null for the default answer.</summary>
    public Func<string, IReadOnlyList<string>, int?>? Answer { get; set; }

    public Core.ConfigSession Session => Window.Session!;
    public ServerPage Server => Window.Server!;

    public void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Pumps the UI (and the console's line queue) until the condition holds.</summary>
    public void WaitFor(Func<bool> condition, string what, int seconds = 20)
    {
        // GitHub's runners are much slower under parallel load (a fake server once took over 20 s to start there).
        var deadline = DateTime.UtcNow.AddSeconds(Environment.GetEnvironmentVariable("CI") == "true" ? seconds * 3 : seconds);
        while (true)
        {
            Pump();
            Window.Server?.Flush();
            if (condition())
                return;
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Timed out waiting for: {what}");
            Thread.Sleep(30);
        }
    }

    /// <summary>
    /// Waits for an async UI method while pumping the dispatcher. Never block on .Result/.GetResult() in a UI test:
    /// the awaited code needs the UI thread to continue, so blocking it deadlocks.
    /// </summary>
    public void Await(Task task, string what = "task", int seconds = 30)
    {
        WaitFor(() => task.IsCompleted, what, seconds);
        task.GetAwaiter().GetResult(); // rethrows if it failed
    }

    /// <summary>Waits for an async app method and returns its result.</summary>
    public T Await<T>(Task<T> task, string what = "task", int seconds = 30)
    {
        WaitFor(() => task.IsCompleted, what, seconds);
        return task.GetAwaiter().GetResult();
    }

    /// <summary>Opens a page or a Server settings tab: clicks its button if it's on screen, else navigates as the rail would.</summary>
    public void OpenTab(string key)
    {
        if (TryFind<Button>("nav/" + key) is { } button)
            Click(button);
        else
            Window.Navigate(key);
        Pump();
    }

    public T Find<T>(string automationId) where T : Control =>
        TryFind<T>(automationId) ?? throw new InvalidOperationException($"No {typeof(T).Name} with id '{automationId}' on the current page.");

    public T? TryFind<T>(string automationId) where T : Control =>
        Window.GetVisualDescendants().OfType<T>().FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == automationId);

    public void Click(Button button)
    {
        Assert.True(button.IsEffectivelyEnabled, $"Button '{AutomationProperties.GetAutomationId(button)}' is disabled.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump();
    }

    public void Dispose()
    {
        if (Window.Server?.IsOwnedActive == true)
        {
            Window.Server.Kill();
            WaitFor(() => Window.Server.State == Core.Server.ServerState.Stopped, "server to die");
        }
        Answer = (title, buttons) => buttons.Count - 1; // never block on prompts while closing
        Window.Close();
        Pump();
        Dialogs.Responder = null;
        Dialogs.PromptResponder = null;
        Core.Http.Override = null;
        ServerPage.ExecutableName = "Server.exe";
        ServerPage.ExtraArguments = [];
        JoinPage.StartKsp = (_, _) => throw new InvalidOperationException("A test tried to start KSP without faking it.");
        ServerPage.RestartDelay = Core.Ops.CrashRestartPolicy.Delay;
    }

    /// <summary>LMP's real Server.dll (for the settings schema) and logs, plus the fake server exe to run.</summary>
    private static string CopyFixtureServer()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", "server-" + Guid.NewGuid().ToString("N"))).FullName;
        foreach (var file in Directory.GetFiles(TestPaths.LmpServer))
            File.Copy(file, Path.Combine(dir, Path.GetFileName(file)));
        var logs = Directory.CreateDirectory(Path.Combine(dir, "logs"));
        foreach (var log in Directory.GetFiles(Path.Combine(TestPaths.LmpServer, "logs")))
            File.Copy(log, Path.Combine(logs.FullName, Path.GetFileName(log)));
        return dir;
    }

    private static string CreateFakeServerFolder()
    {
        var dir = FakeServer.CreateServerFolder();
        foreach (var file in Directory.GetFiles(TestPaths.LmpServer))
        {
            var target = Path.Combine(dir, Path.GetFileName(file));
            if (!File.Exists(target))
                File.Copy(file, target);
        }
        var logs = Directory.CreateDirectory(Path.Combine(dir, "logs"));
        foreach (var log in Directory.GetFiles(Path.Combine(TestPaths.LmpServer, "logs")))
            File.Copy(log, Path.Combine(logs.FullName, Path.GetFileName(log)));
        return dir;
    }
}
