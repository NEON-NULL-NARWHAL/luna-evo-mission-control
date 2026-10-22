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
// --viewer: a second KSP (TestRig/KSP2, kRPC on 50002) parked on the runway records where it shows the rocket (P7).
var viewerMode = options.ContainsKey("viewer");
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
Connection Connect(string name, int rpcPort)
{
    while (true)
    {
        try
        {
            return new Connection(name, IPAddress.Loopback, rpcPort, rpcPort + 1);
        }
        catch (Exception e) when (clock.Elapsed < TimeSpan.FromMinutes(8))
        {
            if (clock.Elapsed.TotalSeconds % 30 < 3)
                Log($"waiting for kRPC on {rpcPort} ({e.GetType().Name})");
            Thread.Sleep(3000);
        }
    }
}
var connection = Connect("LUNA EVO rig", 50000);
var krpc = connection.KRPC();
while (krpc.GameScene != GameScene.SpaceCenter && krpc.GameScene != GameScene.Flight)
    Thread.Sleep(1000);
Log($"kRPC {krpc.GetStatus().Version} answered, scene {krpc.GameScene}");
if (krpc.GameScene == GameScene.Flight)
    return Fail("already in flight: start from the Space Center");

// The viewer goes first, so the rocket has another player's vessel near it from the start (LMP then sends every
// VesselUpdatesMsInterval rather than SecondaryVesselUpdatesMsInterval), about 1.8 km from the pad.
Connection? viewerConnection = null;
if (viewerMode)
{
    viewerConnection = Connect("LUNA EVO rig viewer", 50002);
    var viewerKrpc = viewerConnection.KRPC();
    while (viewerKrpc.GameScene != GameScene.SpaceCenter)
        Thread.Sleep(1000);
    foreach (var save in Directory.GetDirectories(Path.Combine(rig, "KSP2", "saves")))
    {
        var sph = Directory.CreateDirectory(Path.Combine(save, "Ships", "SPH")).FullName;
        if (!File.Exists(Path.Combine(sph, "Bug-E Buggy.craft")))
            File.Copy(Path.Combine(rig, "KSP2", "Ships", "SPH", "Bug-E Buggy.craft"), Path.Combine(sph, "Bug-E Buggy.craft"));
    }
    viewerConnection.SpaceCenter().LaunchVessel("SPH", "Bug-E Buggy", "Runway", null, true, "");
    while (viewerKrpc.GameScene != GameScene.Flight)
        Thread.Sleep(500);
    Thread.Sleep(3000);
    Log("viewer on the runway as RigViewer");
    // --drive: out of the runway's safety bubble (100 m on the rig server), where LMP hides a vessel from other players
    // and the rocket doesn't count it as near (so sends only every SecondaryVesselUpdatesMsInterval).
    if (options.ContainsKey("drive"))
    {
        var buggy = viewerConnection.SpaceCenter().ActiveVessel;
        var frame = buggy.Orbit.Body.ReferenceFrame;
        var from = buggy.Position(frame);
        double Driven()
        {
            var p = buggy.Position(frame);
            return Math.Sqrt(Math.Pow(p.Item1 - from.Item1, 2) + Math.Pow(p.Item2 - from.Item2, 2) + Math.Pow(p.Item3 - from.Item3, 2));
        }
        buggy.Control.Brakes = false;
        buggy.Control.WheelThrottle = 1;
        var until = DateTime.UtcNow.AddSeconds(90);
        while (Driven() < 150 && DateTime.UtcNow < until)
            Thread.Sleep(500);
        // It keeps driving: the pilot's game is measured showing it, the other way round (peer to peer, both ways).
        Log($"viewer drove {Driven():0} m along the runway, still driving");
    }
}

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
// --throttle 0.8: a slower climb, so a long flight stays in the atmosphere (FLYING, where P7 works).
vessel.Control.Throttle = float.Parse(options.GetValueOrDefault("throttle", "1"), CultureInfo.InvariantCulture);
vessel.AutoPilot.TargetPitchAndHeading(90, 90);
vessel.AutoPilot.Engaged = true;
vessel.Control.ActivateNextStage();
var liftOff = DateTime.Now;
Log("lift-off");

// 4. The truth, 20 times a second: the game's own clock and altitude, stamped with the PC's clock.
var body = vessel.Orbit.Body;
var bodyFrame = body.ReferenceFrame;
var flight = vessel.Flight(bodyFrame);
var root = vessel.Parts.Root;
var truth = new List<SyncAnalysis.Sample>();
var end = DateTime.UtcNow.AddSeconds(seconds);
// And what the viewer's game shows, as often as its kRPC answers (no HoldTick there: it would stall the replay).
var viewed = new List<SyncAnalysis.Viewed>();
// The buggy, both ways: where it really is (the viewer's own game) and where the pilot's game shows it.
var buggyTruth = new List<SyncAnalysis.Point>();
var buggyShown = new List<SyncAnalysis.Point>();
var buggyName = viewerConnection?.SpaceCenter().ActiveVessel.Name;
var viewerThread = viewerConnection == null ? null : new Thread(() =>
{
    var center = viewerConnection.SpaceCenter();
    var own = center.ActiveVessel;
    var ownFrame = own.Orbit.Body.ReferenceFrame;
    var ownRoot = own.Parts.Root;
    var ownFlight = own.Flight(ownFrame);
    Vessel? remote = null;
    while (DateTime.UtcNow < end)
    {
        try
        {
            var (x, y, z) = ownRoot.Position(ownFrame);
            buggyTruth.Add(new SyncAnalysis.Point(DateTime.UtcNow.Ticks, x, y, z, ownFlight.Speed));
            // LMP names other players' vessels by KSP's localization key there ("#autoLOC_..."): take the biggest other one
            // (dropped boosters are debris with fewer parts).
            remote ??= center.Vessels.Where(v => !v.Equals(own) && v.Situation is VesselSituation.Flying or VesselSituation.SubOrbital)
                .OrderByDescending(v => v.Parts.All.Count).FirstOrDefault();
            if (remote == null)
            {
                Thread.Sleep(200);
                continue;
            }
            var frame = remote.Orbit.Body.ReferenceFrame;
            var position = remote.Parts.Root.Position(frame);
            var wall = DateTime.UtcNow.Ticks;
            viewed.Add(new SyncAnalysis.Viewed(wall, remote.Orbit.Body.AltitudeAtPosition(position, frame)));
            // kRPC answers inside the game's frames: asking nonstop slowed the viewer's KSP below real time.
            Thread.Sleep(10);
        }
        catch (Exception)
        {
            remote = null; // not loaded yet, or LMP replaced the vessel
            Thread.Sleep(50);
        }
    }
});
viewerThread?.Start();
Vessel? shownBuggy = null;
while (DateTime.UtcNow < end)
{
    if (viewerThread != null)
    {
        try
        {
            // The other player's buggy in the pilot's game, by name (a probe rover: no crew to go by). LMP names it by
            // the same localization key ("#autoLOC_...") in both games.
            shownBuggy ??= space.Vessels.FirstOrDefault(v => !v.Equals(vessel) && v.Name == buggyName);
            if (shownBuggy != null)
            {
                var (x, y, z) = shownBuggy.Parts.Root.Position(bodyFrame);
                buggyShown.Add(new SyncAnalysis.Point(DateTime.UtcNow.Ticks, x, y, z, 0));
            }
        }
        catch (Exception)
        {
            shownBuggy = null;
        }
    }
    // Hold the game on one physics tick so the clock and the altitude come from the same moment.
    krpc.HoldTick();
    var ut = space.UT;
    var alt = flight.MeanAltitude;
    var speed = flight.Speed;
    var rootAlt = body.AltitudeAtPosition(root.Position(bodyFrame), bodyFrame);
    krpc.ReleaseTick();
    truth.Add(new SyncAnalysis.Sample(DateTime.UtcNow.Ticks, ut, alt, speed, rootAlt));
    Thread.Sleep(50);
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
if (viewerThread != null)
{
    viewerThread.Join();
    File.WriteAllLines(Path.Combine(results, "viewer.csv"),
        ["wall_utc_ticks,root_alt", .. viewed.Select(v => v.WallTicks + "," + R(v.RootAlt))]);
    foreach (var line in SyncAnalysis.AnalyzeView(truth, viewed).Split('\n'))
        Log(line);
    foreach (var (name, points) in new[] { ("buggy-truth.csv", buggyTruth), ("buggy-shown.csv", buggyShown) })
        File.WriteAllLines(Path.Combine(results, name),
            ["wall_utc_ticks,x,y,z,speed", .. points.Select(p => string.Join(",", p.WallTicks, R(p.X), R(p.Y), R(p.Z), R(p.Speed)))]);
    var (track, along) = SyncAnalysis.AlongTrack(buggyTruth, buggyShown);
    foreach (var line in SyncAnalysis.AnalyzeView(track, along, "pilot sees the buggy", minSpeed: 2, minStep: 0.2).Split('\n'))
        Log(line);
    // A game that fell behind real time (LMP then moves its clock) makes the numbers above meaningless: say so.
    foreach (var (who, folder) in new[] { ("pilot", "KSP"), ("viewer", "KSP2") })
    {
        string kspLog;
        using (var stream = new FileStream(Path.Combine(rig, folder, "KSP.log"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(stream))
            kspLog = reader.ReadToEnd();
        var jumps = kspLog.Split('\n').Count(l =>
            System.Text.RegularExpressions.Regex.Match(l, @"^\[\w+ (\d\d:\d\d:\d\d\.\d+)\] \[LMP\] Adjusted time from") is { Success: true } m &&
            DateTime.Today.Add(TimeSpan.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)) >= liftOff);
        Log(jumps == 0 ? $"  {who}'s clock: no LMP time jumps during the flight"
                       : $"  WARNING {who}'s clock: {jumps} LMP time jump(s) during the flight (the game fell behind): numbers not valid");
    }
    viewerConnection!.Dispose();
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
