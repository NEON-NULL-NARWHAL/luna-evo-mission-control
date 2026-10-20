// LUNA EVO · LunaEvo.ConfigManager.Tests/UiHarness.cs
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
using LunaEvo.ConfigManager.Core.Tests;

[assembly: AvaloniaTestApplication(typeof(LunaEvo.ConfigManager.Tests.TestApp))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace LunaEvo.ConfigManager.Tests;

public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

/// <summary>Opens the real main window over a throwaway copy of server-configs and drives it like a user.</summary>
internal sealed class UiHarness : IDisposable
{
    /// <param name="fakeServer">Use a server folder whose "Server.exe" is the FakeLmpServer, so it can really be started.</param>
    public UiHarness(bool fakeServer = false)
    {
        ServerDirectory = fakeServer ? CreateFakeServerFolder() : TestPaths.LmpServer;
        ConfigDirectory = TestPaths.CopyConfigs();
        SettingsPath = Path.Combine(Path.GetTempPath(), "LunaEvoTests", Guid.NewGuid().ToString("N"), "settings.json");
        ServerPage.ExecutableName = fakeServer ? FakeServer.Executable : "Server.exe";
        Dialogs.Responder = (title, _, buttons) =>
        {
            DialogsShown.Add(title);
            return Answer?.Invoke(title, buttons) ?? (ConfirmDialogs ? 0 : buttons.Count - 1);
        };
        Window = new MainWindow(new StartupOptions(ServerDirectory, ConfigDirectory, SettingsPath));
        Window.Show();
        Pump();
    }

    public MainWindow Window { get; }
    public string ServerDirectory { get; }
    public string ConfigDirectory { get; }
    public string SettingsPath { get; }
    public List<string> DialogsShown { get; } = [];
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
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
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

    /// <summary>Clicks a page in the navigation rail.</summary>
    public void OpenTab(string key) => Click(Find<Button>("nav/" + key));

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
        ServerPage.ExecutableName = "Server.exe";
    }

    /// <summary>LMP's real Server.dll (for the settings schema) and logs, plus the fake server exe to run.</summary>
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
