// LUNA EVO · tests/FakeLmpClient/Program.cs
// [Test] A pretend LMP player: joins a real LMP server and flies one vessel round a circular orbit, as position messages.
// Used by: the server plugin's real-world check and the rig's observer (RigRunner); never against someone else's server
// Guide:   Meat and Potatoes/26-Server-Plugin.md

using System.Net;
using Lidgren.Network;
using LmpCommon.Enums;
using LmpCommon.Locks;
using LmpCommon.Message;
using LmpCommon.Message.Client;
using LmpCommon.Message.Data.Handshake;
using LmpCommon.Message.Data.Lock;
using LmpCommon.Message.Data.Vessel;
using LmpCommon.Message.Data.Warp;
using LmpCommon.Message.Interface;
using LmpCommon.Message.Server;

var options = Args(args);
var host = options.GetValueOrDefault("host", "127.0.0.1");
var port = int.Parse(options.GetValueOrDefault("port", "8800"));
var name = options.GetValueOrDefault("name", "FakeKerman");
var vessel = Guid.Parse(options.GetValueOrDefault("vessel", Guid.NewGuid().ToString()));
var body = int.Parse(options.GetValueOrDefault("body", "1"));
var sma = double.Parse(options.GetValueOrDefault("sma", "700000"), System.Globalization.CultureInfo.InvariantCulture);
var seconds = double.Parse(options.GetValueOrDefault("seconds", "20"), System.Globalization.CultureInfo.InvariantCulture);
const double KerbinMu = 3.5316e12;

var clientFactory = new ClientMessageFactory();
var serverFactory = new ServerMessageFactory();
var client = new NetClient(new NetPeerConfiguration("LMP"));
client.Start();
var hail = client.CreateMessage();
hail.Write(options.GetValueOrDefault("password", ""));
var connection = client.Connect(new IPEndPoint(IPAddress.Parse(host), port), hail);
Console.WriteLine($"Connecting to {host}:{port} as {name}...");

var start = DateTime.UtcNow;
var authenticated = false;
var subspace = 0;
double subspaceTime = 0;
long serverStart = 0;
while (DateTime.UtcNow - start < TimeSpan.FromSeconds(15) && !authenticated)
{
    if (connection.Status == NetConnectionStatus.Connected && connection.Tag == null)
    {
        connection.Tag = "hello";
        var request = clientFactory.CreateNewMessageData<HandshakeRequestMsgData>();
        request.PlayerName = name;
        request.UniqueIdentifier = "lunaevo-fake-" + name;
        request.KspVersion = "1.12.5";
        Send(clientFactory.CreateNew<HandshakeCliMsg>(request));
    }
    Pump(message =>
    {
        if (message.Data is HandshakeReplyMsgData reply)
        {
            Console.WriteLine($"Handshake: {reply.Response} {reply.Reason}");
            authenticated = reply.Response == HandshakeReply.HandshookSuccessfully;
            serverStart = reply.ServerStartTime;
        }
    });
    Thread.Sleep(20);
}
if (!authenticated)
{
    Console.WriteLine($"Not accepted (connection {connection.Status}).");
    return 1;
}

// Ask for the subspaces and go to the newest, as a real client does, then take the vessel's control lock.
Send(clientFactory.CreateNew<WarpCliMsg>(clientFactory.CreateNewMessageData<WarpSubspacesRequestMsgData>()));
var waited = DateTime.UtcNow;
while (DateTime.UtcNow - waited < TimeSpan.FromSeconds(5) && subspaceTime == 0)
{
    Pump(message =>
    {
        if (message.Data is WarpSubspacesReplyMsgData list && list.SubspaceCount > 0)
        {
            var newest = list.Subspaces.Take(list.SubspaceCount).OrderByDescending(s => s.SubspaceTime).First();
            subspace = newest.SubspaceKey;
            subspaceTime = newest.SubspaceTime;
        }
    });
    Thread.Sleep(20);
}
var change = clientFactory.CreateNewMessageData<WarpChangeSubspaceMsgData>();
change.PlayerName = name;
change.Subspace = subspace;
Send(clientFactory.CreateNew<WarpCliMsg>(change));

// --observe <file.csv>: be a second player who only watches. Every position LMP relays to us is written down with
// the moment it arrived, so the rig can work out what this player's game would show (tests/RigRunner).
if (options.TryGetValue("observe", out var csvPath))
{
    Console.WriteLine($"Observing in subspace {subspace} for {seconds} s, writing {csvPath}");
    using var csv = new StreamWriter(csvPath);
    csv.WriteLine("arrival_utc_ticks,vessel,game_time,lat,lon,alt,subspace");
    var watching = DateTime.UtcNow;
    var received = 0;
    while (DateTime.UtcNow - watching < TimeSpan.FromSeconds(seconds) && connection.Status == NetConnectionStatus.Connected)
    {
        Pump(message =>
        {
            if (message.Data is VesselPositionMsgData p && p.LatLonAlt?.Length == 3)
            {
                // Copied at once: LMP reuses its message objects.
                csv.WriteLine(string.Join(",", DateTime.UtcNow.Ticks, p.VesselId, R(p.GameTime), R(p.LatLonAlt[0]), R(p.LatLonAlt[1]), R(p.LatLonAlt[2]), p.SubspaceId));
                received++;
            }
        });
        Thread.Sleep(1);
    }
    Console.WriteLine($"Received {received} position messages.");
    client.Disconnect("bye");
    Thread.Sleep(300);
    return 0;
}

var acquire = clientFactory.CreateNewMessageData<LockAcquireMsgData>();
acquire.Lock = new LockDefinition(LockType.Control, name, vessel);
Send(clientFactory.CreateNew<LockCliMsg>(acquire));
Console.WriteLine($"Flying {vessel} in subspace {subspace} round body {body} at {sma:0} m for {seconds} s");

// The game clock as LMP clients keep it: seconds since the server started, plus the subspace's offset.
double GameTime() => (DateTime.UtcNow.Ticks - serverStart) / 1e7 + subspaceTime;
var period = 2 * Math.PI * Math.Sqrt(sma * sma * sma / KerbinMu);
var sent = 0;
while (DateTime.UtcNow - start < TimeSpan.FromSeconds(15 + seconds) && connection.Status == NetConnectionStatus.Connected)
{
    var now = GameTime();
    var position = clientFactory.CreateNewMessageData<VesselPositionMsgData>();
    position.VesselId = vessel;
    position.GameTime = now;
    position.BodyIndex = body;
    position.BodyName = body == 1 ? "Kerbin" : "";
    position.SubspaceId = subspace;
    var meanAnomaly = 2 * Math.PI * (now % period) / period;
    // INC, ECC, SMA, LAN, LPE, MNA (radians at EPH), EPH, REF: the order LMP's client sends.
    position.Orbit = [0, 0, sma, 0, 0, meanAnomaly, now, body];
    position.LatLonAlt = [0, meanAnomaly * 180 / Math.PI - 180, sma - 600_000];
    position.VelocityVector = [0, Math.Sqrt(KerbinMu / sma), 0];
    Send(clientFactory.CreateNew<VesselCliMsg>(position));
    sent++;
    Pump(_ => { });
    Thread.Sleep(50); // 20 a second, LMP's default when other players are near
}
Console.WriteLine($"Sent {sent} position messages.");
client.Disconnect("bye");
Thread.Sleep(300);
return 0;

void Send(IMessageBase message)
{
    var outgoing = client.CreateMessage(message.GetMessageSize());
    message.Serialize(outgoing);
    client.SendMessage(outgoing, message.NetDeliveryMethod, message.Channel);
    client.FlushSendQueue();
}

void Pump(Action<IServerMessageBase> handle)
{
    NetIncomingMessage? incoming;
    while ((incoming = client.ReadMessage()) != null)
    {
        if (incoming.MessageType == NetIncomingMessageType.Data && serverFactory.Deserialize(incoming, DateTime.UtcNow.Ticks) is IServerMessageBase message)
            handle(message);
        client.Recycle(incoming);
    }
}

static Dictionary<string, string> Args(string[] args)
{
    var result = new Dictionary<string, string>();
    for (var i = 0; i + 1 < args.Length; i += 2)
        result[args[i].TrimStart('-')] = args[i + 1];
    return result;
}

static string R(double value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
