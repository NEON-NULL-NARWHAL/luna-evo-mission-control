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

// 2. A second player who only watches: the fake LMP client in observe mode, recording what LMP relays to it.
var observedCsv = Path.Combine(results, "observer.csv");
using var observer = Process.Start(new ProcessStartInfo(Path.GetFullPath(observerExe),
    ["--port", "8800", "--password", options.GetValueOrDefault("password", ""), "--name", "RigObserver", "--observe", observedCsv,
     "--seconds", (seconds + 25).ToString(CultureInfo.InvariantCulture)])
{
    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
})!;
Thread.Sleep(4000);
Log("observer joined as RigObserver");

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
vessel.Control.Throttle = 1;
vessel.AutoPilot.TargetPitchAndHeading(90, 90);
vessel.AutoPilot.Engaged = true;
vessel.Control.ActivateNextStage();
Log("lift-off");

// 4. The truth, 10 times a second: the game's own clock and altitude, stamped with the PC's clock.
var flight = vessel.Flight(vessel.Orbit.Body.ReferenceFrame);
var truth = new List<SyncAnalysis.Sample>();
var end = DateTime.UtcNow.AddSeconds(seconds);
while (DateTime.UtcNow < end)
{
    // Hold the game on one physics tick so the clock and the altitude come from the same moment.
    krpc.HoldTick();
    var ut = space.UT;
    var alt = flight.MeanAltitude;
    var speed = flight.Speed;
    krpc.ReleaseTick();
    truth.Add(new SyncAnalysis.Sample(DateTime.UtcNow.Ticks, ut, alt, speed));
    Thread.Sleep(100);
}
vessel.Control.Throttle = 0;
vessel.AutoPilot.Engaged = false;
File.WriteAllLines(Path.Combine(results, "game.csv"),
    ["wall_utc_ticks,ut,alt,speed", .. truth.Select(s => string.Join(",", s.WallTicks, R(s.Ut), R(s.Alt), R(s.Speed)))]);
observer.WaitForExit(60_000);
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
