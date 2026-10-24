// LUNA EVO · LunaEvo.MissionControl.Core.Tests/FuzzTests.cs
// [Test] Every parser that reads text from outside (LMP logs and website JSON, master servers, CKAN files and output,
//        KSP .cfg files, craft and vessel files, invites, settings comments, Wingman agents' lines) fed thousands of mutated and random inputs.
//        A parser may only fail the way it documents; anything else would be a crash in the app.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using System.Text;
using System.Text.Json;
using LunaEvo.MissionControl.Core.Join;
using LunaEvo.MissionControl.Core.Live;
using LunaEvo.MissionControl.Core.Mods;
using LunaEvo.MissionControl.Core.Ops;
using LunaEvo.MissionControl.Core.Schema;
using LunaEvo.MissionControl.Core.Server;
using LunaEvo.MissionControl.Core.Setup;

namespace LunaEvo.MissionControl.Core.Tests;

public class FuzzTests
{
    private const int Rounds = 1500;
    private static readonly string[] Nasty = ["\0", "\r", "\n", "\t", "{", "}", "[", "]", "\"", "\\", "=", ":", ",", "(", ")", "//", "\u202e", "😀", "\uffff", "\ud800", "%", "-", "..", "\u0007"];

    /// <summary>Feeds mutations of the samples (and pure noise) to <paramref name="parse"/>; only the listed exception types may escape.</summary>
    private static void Fuzz(string[] samples, Action<string> parse, params Type[] allowed)
    {
        var random = new Random(4242); // fixed: a failure always reproduces
        for (var i = 0; i < Rounds; i++)
        {
            var input = i % 10 == 0 ? Noise(random) : Mutate(samples[random.Next(samples.Length)], random);
            try
            {
                parse(input);
            }
            catch (Exception e) when (allowed.Any(t => t.IsInstanceOfType(e)))
            {
                // Documented failure for bad input.
            }
            catch (Exception e)
            {
                Assert.Fail($"{e.GetType().Name} for input #{i} ({input.Length} chars): {e.Message}\n---\n{(input.Length > 300 ? input[..300] + "..." : input)}");
            }
        }
    }

    private static string Mutate(string sample, Random random)
    {
        var text = new StringBuilder(sample);
        var edits = random.Next(1, 8);
        for (var e = 0; e < edits && text.Length > 0; e++)
        {
            var at = random.Next(text.Length);
            switch (random.Next(7))
            {
                case 0: text.Remove(at, Math.Min(random.Next(1, 20), text.Length - at)); break;
                case 1: text.Insert(at, Nasty[random.Next(Nasty.Length)]); break;
                case 2: text[at] = (char)random.Next(0, 0x2FF); break;
                case 3: text.Length = at; break; // truncated
                case 4: text.Insert(at, text.ToString(at, Math.Min(50, text.Length - at)), random.Next(1, 40)); break; // repeated
                case 5: text.Insert(at, new string('[', random.Next(1, 300))); break; // deep nesting
                default: text.Insert(at, random.Next().ToString()); break;
            }
        }
        return text.ToString();
    }

    private static string Noise(Random random)
    {
        var chars = new char[random.Next(0, 400)];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = random.Next(4) == 0 ? Nasty[random.Next(Nasty.Length)][0] : (char)random.Next(1, 0x7F);
        return new string(chars);
    }

    /// <summary>Text that is JSON is parsed, then handed to the reader; text that isn't is skipped (JsonDocument's own error).</summary>
    private static Action<string> AsJson(Action<JsonElement> read) => text =>
    {
        JsonDocument json;
        try
        {
            json = JsonDocument.Parse(text);
        }
        catch (Exception e) when (e is JsonException or ArgumentException) // ArgumentException: a lone surrogate, not JSON at all
        {
            return;
        }
        using (json)
            read(json.RootElement);
    };

    // One throwaway server folder for the tracker (its players.json is written as it goes).
    private static readonly string Tracker = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", "fuzz-" + Guid.NewGuid().ToString("N"))).FullName;

    [Fact]
    public void Lmp_log_lines() => Fuzz(
        ["[12:34:56][Info]: Client Jeb (abc123) handshake successful, LMP Version: 0.29.2, KSP Version: 1.12.5",
         "[00:00:01][Warning]: Bill: Tried to run an admin command with an invalid password", "plain text line", "[99:99:99][Nope]: x"],
        line =>
        {
            var parsed = LogLine.Parse(line);
            var tracker = new PlayerTracker(Tracker);
            tracker.Observe(parsed);
        });

    [Fact]
    public void Lmp_website_json() => Fuzz(
        [File.ReadAllText(Path.Combine(TestPaths.Fixtures, "Web", "serverinfo.json"))],
        AsJson(root => ServerInfoClient.Parse(root)), typeof(InvalidDataException), typeof(JsonException));

    [Fact]
    public void Master_server_json() => Fuzz([TestData.MasterJson], AsJson(root => ServerBrowser.Parse(root)));

    [Fact]
    public void Ckan_mod_lists_and_output() => Fuzz(
        ["""{ "spec_version": "v1.6", "depends": [ { "name": "MechJeb2", "version": "2.14" }, { "any_of": [ { "name": "A" } ] } ], "download": "x" }""",
         "MechJeb2\nKerbalEngineerRedux 1.1\n# comment\n",
         "Parallax Continued requires KSPBurst, which can be provided by multiple mods.\n1) KSPBurst (KSPBurst Compiler)\n2) KSPBurst-Lite (Lite)",
         "Unhandled Exception: CKAN.ModuleNotFoundKraken: Module not found: X\n   at CKAN.Main()"],
        text =>
        {
            var (mods, _) = CkanModList.Parse(text);
            CkanModList.Export(mods, text.Length > 40 ? text[..40] : text);
            CkanModList.InstallCommands("C:\\KSP", mods);
            var lines = text.Split('\n');
            CkanOutput.Check(lines, 1);
            CkanOutput.Problem(lines, 127);
            CkanOutput.Choice(lines);
        });

    [Fact]
    public void Server_plugin_live_feed() => Fuzz(
        ["""{"Format":1,"Plugin":"1.0.0","Lmp":"0.29.2","WrittenUtc":"2026-10-22T11:33:00Z","Players":[{"Name":"Jeb","Subspace":99,"Vessel":"0738bb0f-cda0-4e47-938c-07900665ed9b"}],"Subspaces":[{"Id":99,"Time":1.5}],"Vessels":[{"Id":"0738bb0f-cda0-4e47-938c-07900665ed9b","Body":1,"BodyName":"Kerbin","Lat":0,"Lon":1,"Alt":2,"Orbit":[0,0,900000,0,0,6.2,1944034.8,1],"Velocity":[0,1,0],"GameTime":3,"Subspace":99,"Landed":false,"Splashed":false,"From":"Jeb","ReceivedUtc":"2026-10-22T11:33:00Z","FlownBy":"Jeb"}]}"""],
        text => Live.LiveFeed.Parse(System.Text.Encoding.UTF8.GetBytes(text)));

    [Fact]
    public void Lmp_command_line_addresses() => Fuzz(
        ["lmp://127.0.0.1:8800", "lmp://[2001:db8::1]:8810/", "kerbin.example.com:65535"],
        text => LunaEvo.ClientPatches.LmpAddress.TryParse(text, out _, out _));

    [Fact]
    public void Ksp_cfg_files() => Fuzz(
        ["PART\n{\n name = some.part\n MODULE\n {\n  name = ModuleEngines\n }\n}\n", "RESOURCE_DEFINITION { name = LiquidFuel }", "@PART[x]:NEEDS[y] { %a = b }"],
        text => KspConfigParser.Parse(text));

    [Fact]
    public void Craft_and_vessel_files() => Fuzz(
        ["ship = Kerbal X\nversion = 1.12.5\ntype = VAB\nPART\n{\n part = mk1pod.v2_4294\n}\n",
         "pid = 1\nname = Mun Base\ntype = Base\nsit = LANDED\nORBIT\n{\n REF = 2\n}\nPART\n{\n name = x\n crew = Jeb\n}\n",
         "pid = 2\nsit = ORBITING\nlat = 1\nlon = 2\nORBIT\n{\n SMA = -7e5\n ECC = 1.2\n INC = 5\n LAN = 1\n LPE = 2\n MNA = 3\n EPH = 4\n IDENT = Squad/Mun\n}\n"],
        text =>
        {
            try
            {
                CraftShare.Parse(text, "x.craft", "test");
            }
            catch (InvalidDataException)
            {
                // "Not a craft file" is the documented answer.
            }
            UniverseVessels.Parse(text, "v.txt");
        });

    [Fact]
    public void Tower_channel_requests()
    {
        // Whatever an agent sends, the tower answers with a status code, never an exception.
        using var server = new Tower.TowerServer(0, () => new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc));
        Fuzz(["""{"callSign":"Wingman","latitude":-0.05,"longitude":-74.7,"altitude":900,"heading":90,"speed":120,"situation":"Flying","phase":"x"}""",
              """{"callSign":"Wingman","kind":"request-approach","text":"land at KSC please"}""",
              """{"callSign":"Wingman","craft":"Aeris 3A","result":"landed","approach":"RNAV 09 SOUTH","goArounds":1,"minutes":8.3,"landing":{"downRunway":1329,"offCenterline":8,"headingError":4},"phases":["TakingOff","Climbing","OnRoute","OnFinal","Stopped"]}""",
              """{"callSign":"Wingman","skill":"land-rnav","text":"Slow to 85 m/s before F09.","evidence":[1,2,3]}""",
              """{"callSign":"Wingman","fixes":["KSC-DS","KSC-S1","KSC-S2","KSC-S3","F09"]}"""],
            text =>
            {
                server.Answer("POST", "/v1/report", text);
                server.Answer("POST", "/v1/say", text);
                server.Answer("POST", "/v1/flight", text);
                server.Answer("POST", "/v1/lesson", text);
                server.Answer("POST", "/v1/knowledge", text);
                server.Answer("POST", "/v1/plan", text);
                server.Answer("POST", "/v1/procedures", text);
            });
    }

    [Fact]
    public void Wingman_agent_lines() => Fuzz(
        ["""{"type":"event","time":"2026-10-23T15:00:00Z","kind":"phase","text":"Parked → TakingOff","data":{"from":"Parked","to":"TakingOff"}}""",
         """{"type":"event","kind":"flight","text":"Flight landed.","data":{"result":"landed","landing":{"downRunway":1105}}}""",
         "Unhandled exception. System.IO.IOException: boom"],
        text => Wingman.AgentEvent.Parse(text, DateTime.UtcNow));

    [Fact]
    public void Invites_addresses_versions_and_comments() => Fuzz(
        ["Join my KSP server \"X\"!\nAddress: 192.168.1.20:8810\nLMP version: 0.29.2-compiled\n", "lmp://[2001:db8::1]:8800/", "kerbin.example.com:8800",
         "Max 30 char. Min value = 5000. Values: Sandbox, Career, Science", "0.29.2-compiled", "60 Join our Discord!\n120 Back up your saves"],
        text =>
        {
            ServerAddress.Parse(text);
            ServerAddress.InviteVersion(text);
            ServerAddress.InviteAddress(text);
            ServerBrowser.Endpoint(text);
            CommentRules.Parse(text);
            LmpVersion.Parse(text);
            Announcements.Parse(text);
        });
}
