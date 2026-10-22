// LUNA EVO · tests/RigRunner/Program.cs
// [Test] Flies the test rig's KSP through kRPC and compares what the game says with what the LUNA EVO server plugin received.
// Used by: by hand, with the test rig running (D:\LUNA EVOLVED\TestRig); never by dotnet test
// Guide:   Meat and Potatoes/26-Server-Plugin.md

using System.Diagnostics;
using System.Net;
using System.Text.Json;
using KRPC.Client;
using KRPC.Client.Services.KRPC;
using KRPC.Client.Services.SpaceCenter;

var options = Args(args);
var ksp = options.GetValueOrDefault("ksp", @"D:\LUNA EVOLVED\TestRig\KSP");
var server = options.GetValueOrDefault("server", @"D:\LUNA EVOLVED\TestRig\Server");
var pilot = options.GetValueOrDefault("pilot", "RigPilot");
var seconds = int.Parse(options.GetValueOrDefault("seconds", "20"));
var clock = Stopwatch.StartNew();
void Log(string text) => Console.WriteLine($"[{clock.Elapsed:mm\\:ss}] {text}");

// 1. kRPC answers once KSP has loaded and a game is open (it has no main-menu scene): LMP joins the server first.
Connection? connection = null;
while (connection == null)
{
    try
    {
        connection = new Connection("LUNA EVO rig", IPAddress.Loopback, 50000, 50001);
    }
    catch (Exception e) when (clock.Elapsed < TimeSpan.FromMinutes(8))
    {
        if (clock.Elapsed.TotalSeconds % 30 < 3)
            Log($"waiting for kRPC ({e.GetType().Name})");
        Thread.Sleep(3000);
    }
}
var krpc = connection.KRPC();
Log($"kRPC {krpc.GetStatus().Version} answered, scene {krpc.GameScene}");

// 2. LMP connects from the main menu (-lmp on the command line) and syncs, then KSP shows the Space Center.
while (krpc.GameScene != GameScene.SpaceCenter)
{
    if (clock.Elapsed > TimeSpan.FromMinutes(12))
        return Fail($"never reached the Space Center (scene {krpc.GameScene})");
    Thread.Sleep(2000);
}
Log($"in the Space Center; server sees: {string.Join(", ", Feed()?.Players.Select(p => p.Name) ?? ["(no live.json)"])}");

// 3. Launch Kerbal X straight up. LMP's save may not have the stock craft yet, so copy it into every save first.
foreach (var save in Directory.GetDirectories(Path.Combine(ksp, "saves")))
{
    var vab = Directory.CreateDirectory(Path.Combine(save, "Ships", "VAB")).FullName;
    var craft = Path.Combine(vab, "Kerbal X.craft");
    if (!File.Exists(craft))
        File.Copy(Path.Combine(ksp, "Ships", "VAB", "Kerbal X.craft"), craft);
}
var space = connection.SpaceCenter();
space.LaunchVessel("VAB", "Kerbal X", "LaunchPad", null, true, "");
while (krpc.GameScene != GameScene.Flight)
    Thread.Sleep(500);
Thread.Sleep(3000); // let the vessel settle on the pad
var vessel = space.ActiveVessel;
Log($"on the pad: {vessel.Name}");
vessel.Control.Throttle = 1;
vessel.AutoPilot.TargetPitchAndHeading(90, 90);
vessel.AutoPilot.Engaged = true;
vessel.Control.ActivateNextStage();
Log("lift-off");

// 4. Each second: the game's altitude next to the last position the server plugin received for the pilot's vessel.
var flight = vessel.Flight(vessel.Orbit.Body.ReferenceFrame);
var samples = new List<(double Game, double Server, double AgeMs, double Speed)>();
for (var i = 0; i < seconds; i++)
{
    Thread.Sleep(1000);
    var game = flight.MeanAltitude;
    var speed = flight.Speed;
    var feed = Feed();
    var mine = feed?.Vessels.FirstOrDefault(v => v.FlownBy == pilot);
    if (mine == null)
    {
        Log($"alt {game,9:0} m  speed {speed,6:0} m/s  server: nothing for {pilot} yet");
        continue;
    }
    var ageMs = (DateTime.UtcNow - mine.ReceivedUtc).TotalMilliseconds;
    samples.Add((game, mine.Alt, ageMs, speed));
    Log($"alt {game,9:0} m  speed {speed,6:0} m/s  server alt {mine.Alt,9:0} m  diff {game - mine.Alt,7:0} m  message age {ageMs,5:0} ms");
}
vessel.Control.Throttle = 0;
vessel.AutoPilot.Engaged = false;

if (samples.Count == 0)
    return Fail("the server plugin never saw the pilot's vessel");
Log($"RESULT {samples.Count} samples: server behind the game by {samples.Average(s => s.Game - s.Server):0} m on average " +
    $"(max {samples.Max(s => Math.Abs(s.Game - s.Server)):0} m) at {samples.Average(s => s.Speed):0} m/s; " +
    $"last message {samples.Average(s => s.AgeMs):0} ms old on average");
connection.Dispose();
return 0;

int Fail(string why)
{
    Log("FAILED: " + why);
    return 1;
}

Feed? Feed()
{
    try
    {
        var path = Path.Combine(server, "LunaEvo", "live.json");
        return File.Exists(path) ? JsonSerializer.Deserialize<Feed>(File.ReadAllText(path)) : null;
    }
    catch (Exception e) when (e is IOException or JsonException)
    {
        return null; // the plugin is replacing it right now
    }
}

static Dictionary<string, string> Args(string[] args)
{
    var result = new Dictionary<string, string>();
    for (var i = 0; i + 1 < args.Length; i += 2)
        result[args[i].TrimStart('-')] = args[i + 1];
    return result;
}

// The parts of the plugin's live.json this runner needs (Meat and Potatoes 26).
internal sealed record Feed(List<FeedPlayer> Players, List<FeedVessel> Vessels);
internal sealed record FeedPlayer(string Name, int Subspace, string? Vessel);
internal sealed record FeedVessel(string Id, double Alt, string? FlownBy, DateTime ReceivedUtc);
