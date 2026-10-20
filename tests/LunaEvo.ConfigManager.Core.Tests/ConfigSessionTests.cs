// LUNA EVO · LunaEvo.ConfigManager.Core.Tests/ConfigSessionTests.cs
// [Test] Validation rules, greying out, presets, dirty tracking and save/backup.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using LunaEvo.ConfigManager.Core.Validation;

namespace LunaEvo.ConfigManager.Core.Tests;

public class ConfigSessionTests
{
    private static ConfigSession OpenCopy() => ConfigSession.Open(TestPaths.LmpServer, TestPaths.CopyConfigs());

    [Fact]
    public void Real_configs_have_no_errors()
    {
        var errors = OpenCopy().Validate().Where(i => i.Severity == Severity.Error);
        Assert.Empty(errors);
    }

    [Fact]
    public void Heartbeat_must_be_lower_than_timeout()
    {
        var session = OpenCopy();
        session.SetValue(LmpRules.Connection, "HearbeatMsInterval", "30000");

        var issue = Assert.Single(session.Validate(), i => i.Setting == "HearbeatMsInterval");
        Assert.Equal(Severity.Error, issue.Severity);
    }

    [Fact]
    public void Flags_values_outside_comment_limits()
    {
        var session = OpenCopy();
        session.SetValue("MasterServerSettings.xml", "MasterServerRegistrationMsInterval", "1000");
        session.SetValue(LmpRules.General, "ServerName", new string('x', 31));
        session.SetValue(LmpRules.General, "GameMode", "sandbox");

        var flagged = session.Validate().Where(i => i.Severity == Severity.Error).Select(i => i.Setting).Order();
        Assert.Equal(["GameMode", "MasterServerRegistrationMsInterval", "ServerName"], flagged);
    }

    [Fact]
    public void Website_port_matching_game_port_is_a_warning()
    {
        var session = OpenCopy();
        session.SetValue(LmpRules.Website, "Port", session.GetValue(LmpRules.Connection, "Port")!);

        Assert.Contains(session.Validate(), i => i is { FileName: LmpRules.Website, Setting: "Port", Severity: Severity.Warning });
    }

    [Fact]
    public void Greys_out_settings_that_have_no_effect()
    {
        var session = OpenCopy();

        session.SetValue(LmpRules.DedicatedServer, "UseRainbowEffect", "true");
        Assert.NotNull(LmpRules.InactiveReason(session, LmpRules.DedicatedServer, "Red"));
        session.SetValue(LmpRules.DedicatedServer, "UseRainbowEffect", "false");
        Assert.Null(LmpRules.InactiveReason(session, LmpRules.DedicatedServer, "Red"));

        session.SetValue(LmpRules.General, "GameMode", "Science");
        Assert.NotNull(LmpRules.InactiveReason(session, LmpRules.Gameplay, "StartingFunds"));
        Assert.Null(LmpRules.InactiveReason(session, LmpRules.Gameplay, "StartingScience"));
        session.SetValue(LmpRules.General, "GameMode", "Career");
        Assert.Null(LmpRules.InactiveReason(session, LmpRules.Gameplay, "StartingFunds"));
    }

    [Fact]
    public void Editing_gameplay_off_preset_warns_about_custom_difficulty()
    {
        var session = OpenCopy();
        session.SetValue(LmpRules.General, "GameDifficulty", "Hard");
        foreach (var setting in session.Schema.Find(LmpRules.Gameplay)!.Settings)
            session.SetValue(LmpRules.Gameplay, setting.Name, session.DefaultFor(LmpRules.Gameplay, setting.Name));
        Assert.DoesNotContain(session.Validate(), i => i.FileName == LmpRules.Gameplay);

        var current = session.GetValue(LmpRules.Gameplay, "CanRevert");
        session.SetValue(LmpRules.Gameplay, "CanRevert", current == "true" ? "false" : "true");
        Assert.Contains(session.Validate(), i => i is { FileName: LmpRules.Gameplay, Setting: null, Severity: Severity.Warning });
    }

    [Fact]
    public void Changing_a_value_back_leaves_the_file_clean()
    {
        var session = OpenCopy();
        var original = session.GetValue(LmpRules.General, "ServerName")!;

        session.SetValue(LmpRules.General, "ServerName", "LUNA EVO");
        Assert.True(session.IsDirty);

        session.SetValue(LmpRules.General, "ServerName", original);
        Assert.False(session.IsDirty);
        Assert.Null(session.Save());
    }

    [Fact]
    public void Save_backs_up_only_changed_files_then_writes()
    {
        var session = OpenCopy();
        var originalGeneral = File.ReadAllBytes(Path.Combine(session.ConfigDirectory, LmpRules.General));
        var untouchedBefore = File.GetLastWriteTimeUtc(Path.Combine(session.ConfigDirectory, LmpRules.Connection));

        session.SetValue(LmpRules.General, "ServerName", "LUNA EVO");
        Assert.True(session.IsModified(LmpRules.General, "ServerName"));
        var backup = session.Save();

        Assert.NotNull(backup);
        Assert.Equal([LmpRules.General], Directory.GetFiles(backup).Select(Path.GetFileName));
        Assert.Equal(originalGeneral, File.ReadAllBytes(Path.Combine(backup, LmpRules.General)));
        Assert.Equal(untouchedBefore, File.GetLastWriteTimeUtc(Path.Combine(session.ConfigDirectory, LmpRules.Connection)));
        Assert.False(session.IsDirty);
        Assert.False(session.IsModified(LmpRules.General, "ServerName"));

        var reopened = ConfigSession.Open(TestPaths.LmpServer, session.ConfigDirectory);
        Assert.Equal("LUNA EVO", reopened.GetValue(LmpRules.General, "ServerName"));
    }
}
