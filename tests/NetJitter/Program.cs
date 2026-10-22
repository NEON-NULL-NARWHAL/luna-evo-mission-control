// LUNA EVO · tests/NetJitter/Program.cs
// [Test] A UDP relay that delays every packet by a random time, so packets overtake each other the way they do online.
// Used by: the test rig's P1 runs (Meat and Potatoes 27); localhost only
// Guide:   Meat and Potatoes/27-Client-Patches.md

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using LunaEvo.ClientPatches;

// Lidgren's own network simulation only exists in DEBUG builds, so LMP's "-debug" can't make a bad network; this can.
var options = new Dictionary<string, string>();
for (var i = 0; i + 1 < args.Length; i += 2)
    options[args[i].TrimStart('-')] = args[i + 1];
var listenPort = int.Parse(options.GetValueOrDefault("listen", "8801"));
var target = new IPEndPoint(IPAddress.Loopback, int.Parse(options.GetValueOrDefault("target", "8800")));
var min = int.Parse(options.GetValueOrDefault("min", "20"));
var max = int.Parse(options.GetValueOrDefault("max", "120"));
var seconds = int.Parse(options.GetValueOrDefault("seconds", "600"));
TimerResolution.Raise(); // delays of a few ms must mean a few ms

using var front = new UdpClient(new IPEndPoint(IPAddress.Loopback, listenPort));
var back = new ConcurrentDictionary<IPEndPoint, UdpClient>();
var random = new Random(1);
long forward = 0, backward = 0;
Console.WriteLine($"Relaying 127.0.0.1:{listenPort} -> {target}, each packet delayed {min}-{max} ms, for {seconds} s");

void Later(Func<Task> send)
{
    int delay;
    lock (random)
        delay = random.Next(min, max + 1);
    _ = Task.Delay(delay).ContinueWith(_ => send());
}

async Task PumpBack(UdpClient upstream, IPEndPoint client)
{
    while (true)
    {
        UdpReceiveResult packet;
        try
        {
            packet = await upstream.ReceiveAsync();
        }
        catch (Exception)
        {
            return;
        }
        Interlocked.Increment(ref backward);
        Later(() => front.SendAsync(packet.Buffer, packet.Buffer.Length, client));
    }
}

var stop = DateTime.UtcNow.AddSeconds(seconds);
_ = Task.Run(async () =>
{
    while (DateTime.UtcNow < stop)
    {
        await Task.Delay(10_000);
        Console.WriteLine($"{Interlocked.Read(ref forward)} packets to the server, {Interlocked.Read(ref backward)} back");
    }
    Environment.Exit(0);
});
while (true)
{
    var packet = await front.ReceiveAsync();
    var upstream = back.GetOrAdd(packet.RemoteEndPoint, client =>
    {
        var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        socket.Connect(target);
        _ = PumpBack(socket, client);
        return socket;
    });
    Interlocked.Increment(ref forward);
    Later(() => upstream.SendAsync(packet.Buffer, packet.Buffer.Length));
}
