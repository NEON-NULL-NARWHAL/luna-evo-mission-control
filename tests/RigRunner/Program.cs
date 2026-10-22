// LUNA EVO · tests/RigRunner/Program.cs
// [Test] Flies the test rig's KSP through kRPC and measures how far behind the server and a second player are.
// Used by: by hand, with the test rig running (D:\LUNA EVOLVED\TestRig); never by dotnet test
// Guide:   Meat and Potatoes/26-Server-Plugin.md

using System.Diagnostics;
using System.Globalization;
using System.Net;
using KRPC.Client;
using KRPC.Client.Services.KRPC;
using KRPC.Client.Services.SpaceCenter;
using LunaEvo.RigRunner;

var options = Args(args);
if (options.ContainsKey("selftest"))
    return SyncAnalysis.SelfTest();
var rig = options.GetValueOrDefault("rig", @"D:\LUNA EVOLVED\TestRig");
var ksp = Path.Combine(rig, "KSP");
var seconds = int.Parse(options.GetValueOrDefault("seconds", "25"), CultureInfo.InvariantCulture);
// --mode slow: the rig's KSP set to run slower than real time (Meat and Potatoes 27); counts LMP's clock jumps.
var slow = options.GetValueOrDefault("mode", "") == "slow";
// --mode fleet: stay on the pad while tests/FakeLmpClient --fleet moves a universe of other vessels; reads frames per second.
var fleetMode = options.GetValueOrDefault("mode", "") == "fleet";
var observerExe = options.GetValueOrDefault("observer",
    Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\FakeLmpClient\bin\Debug\net10.0\FakeLmpClient.exe"));
var results = Directory.CreateDirectory(Path.Combine(rig, "Results", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"))).FullName;
var clock = Stopwatch.StartNew();
void Log(string text)
{
    var line = $"[{clock.Elapsed:mm\\:ss}] {text}";
    Console.WriteLine(line);
    File.AppendAllText(Path.Combine(results, "run.log"), line + Environment.NewLine);
}

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
while (krpc.GameScene != GameScene.SpaceCenter && krpc.GameScene != GameScene.Flight)
    Thread.Sleep(1000);
Log($"kRPC {krpc.GetStatus().Version} answered, scene {krpc.GameScene}");
if (krpc.GameScene == GameScene.Flight)
    return Fail("already in flight: start from the Space Center");

// 2. Launch Kerbal X straight up, from a fresh rig universe (an old rocket on the pad blocks the launch: Start-Rig.ps1
//    -Fresh). LMP's save may not have the stock craft yet, so copy it into every save first.
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
// A second player who only watches: the fake LMP client in observe mode, recording what LMP relays to it.
var observedCsv = Path.Combine(results, "observer.csv");
using var observer = slow || fleetMode ? null : Process.Start(new ProcessStartInfo(Path.GetFullPath(observerExe),
    ["--port", options.GetValueOrDefault("observer-port", "8800"), "--password", options.GetValueOrDefault("password", ""), "--name", "RigObserver", "--observe", observedCsv,
     "--seconds", (seconds + 25).ToString(CultureInfo.InvariantCulture)])
{
    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
})!;
Thread.Sleep(3000);
if (observer != null)
    Log("observer joined as RigObserver");

var vessel = space.ActiveVessel;
if (fleetMode)
{
    // Frames per second from LMP's own heartbeat line in KSP.log, about once a second: "frame=18220".
    var since = DateTime.Now;
    Thread.Sleep(seconds * 1000);
    var beats = new List<(DateTime At, long Frame)>();
    using (var stream = new FileStream(Path.Combine(ksp, "KSP.log"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
    using (var reader = new StreamReader(stream))
        foreach (var line in reader.ReadToEnd().Split('\n'))
        {
            var m = System.Text.RegularExpressions.Regex.Match(line, @"^\[LOG (\d\d:\d\d:\d\d\.\d+)\] \[LMP\]\[HEARTBEAT\].* scene=FLIGHT .*frame=(\d+)");
            if (m.Success && DateTime.Today.Add(TimeSpan.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)) is var at && at >= since)
                beats.Add((at, long.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)));
        }
    if (beats.Count < 5)
        return Fail("not enough heartbeat lines in KSP.log");
    var fps = (beats[^1].Frame - beats[0].Frame) / (beats[^1].At - beats[0].At).TotalSeconds;
    Log($"RESULT fleet: {fps:0.0} frames per second on the pad over {(beats[^1].At - beats[0].At).TotalSeconds:0} s ({beats.Count} heartbeats)");
    connection.Dispose();
    return 0;
}
vessel.Control.Throttle = 1;
vessel.AutoPilot.TargetPitchAndHeading(90, 90);
vessel.AutoPilot.Engaged = true;
vessel.Control.ActivateNextStage();
Log("lift-off");

// 4. The truth, 10 times a second: the game's own clock and altitude, stamped with the PC's clock.
var body = vessel.Orbit.Body;
var bodyFrame = body.ReferenceFrame;
var flight = vessel.Flight(bodyFrame);
var root = vessel.Parts.Root;
var truth = new List<SyncAnalysis.Sample>();
var end = DateTime.UtcNow.AddSeconds(seconds);
while (DateTime.UtcNow < end)
{
    // Hold the game on one physics tick so the clock and the altitude come from the same moment.
    krpc.HoldTick();
    var ut = space.UT;
    var alt = flight.MeanAltitude;
    var speed = flight.Speed;
    var rootAlt = body.AltitudeAtPosition(root.Position(bodyFrame), bodyFrame);
    krpc.ReleaseTick();
    truth.Add(new SyncAnalysis.Sample(DateTime.UtcNow.Ticks, ut, alt, speed, rootAlt));
    Thread.Sleep(100);
}
vessel.Control.Throttle = 0;
vessel.AutoPilot.Engaged = false;
File.WriteAllLines(Path.Combine(results, "game.csv"),
    ["wall_utc_ticks,ut,alt,speed,root_alt", .. truth.Select(s => string.Join(",", s.WallTicks, R(s.Ut), R(s.Alt), R(s.Speed), R(s.RootAlt)))]);
if (slow)
{
    // How fast the game ran, and how often its clock jumped (more than a second of game time in one 0.1 s sample).
    var steps = truth.Zip(truth.Skip(1), (a, b) => (Game: b.Ut - a.Ut, Wall: (b.WallTicks - a.WallTicks) / 1e7)).ToList();
    var jumps = steps.Count(s => s.Game - s.Wall > 1);
    var calm = steps.Where(s => s.Game - s.Wall <= 1).ToList();
    string log;
    using (var stream = new FileStream(Path.Combine(ksp, "KSP.log"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
    using (var reader = new StreamReader(stream))
        log = reader.ReadToEnd();
    var lmpJumps = log.Split('\n').Count(l => l.Contains("[LMP] Adjusted time from"));
    var ownSubspace = log.Split('\n').Count(l => l.Contains("[LUNA EVO]") && l.Contains("taking a subspace"));
    Log($"RESULT slow game: ran at {calm.Sum(s => s.Game) / calm.Sum(s => s.Wall):0.00}x real time; {jumps} clock jump(s) seen by the game; " +
        $"KSP.log: {lmpJumps} LMP time jump(s), {ownSubspace} LUNA EVO subspace move(s)");
    connection.Dispose();
    return 0;
}
observer!.WaitForExit(60_000);
Log("observer: " + observer.StandardOutput.ReadToEnd().Trim().Replace(Environment.NewLine, " | "));
connection.Dispose();

// 5. Compare.
var report = SyncAnalysis.Analyze(truth, SyncAnalysis.ReadObserved(observedCsv));
foreach (var line in report.Split('\n'))
    Log(line);
return report.StartsWith("FAILED", StringComparison.Ordinal) ? 1 : 0;

int Fail(string why)
{
    Log("FAILED: " + why);
    return 1;
}

static string R(double value) => value.ToString("R", CultureInfo.InvariantCulture);

static Dictionary<string, string> Args(string[] args)
{
    var result = new Dictionary<string, string>();
    for (var i = 0; i < args.Length; i++)
        if (args[i].StartsWith('-'))
            result[args[i].TrimStart('-')] = i + 1 < args.Length && !args[i + 1].StartsWith('-') ? args[++i] : "";
    return result;
}
