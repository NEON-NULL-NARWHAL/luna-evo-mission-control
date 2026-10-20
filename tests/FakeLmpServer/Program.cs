// LUNA EVO · FakeLmpServer/Program.cs
// [Test] A pretend LMP Server.exe for tests: same log format, stdin commands and Ctrl+C behaviour.
// Used by: ServerRunnerTests, ServerConsoleTests
// Guide:   Meat and Potatoes/05-Tests.md

// Mimics LMP 0.29.2's console behaviour (MainServer.cs, CommandHandler.cs, BaseLogger.cs) closely enough to test
// LUNA EVO's server control without starting a real, networked server.
//   --ignore-ctrl-c   ignore Ctrl+C (tests the kill fallback)
//   --fatal           fail during startup and wait on stdin like LMP does
//   --crash           exit with code 3 shortly after starting (tests auto-restart)
using System.Text;
using LunaEvo.ConfigManager.Core.Server;

// Tests point ConsoleControl.HelperPath here, so this exe also plays LUNA EVO's Ctrl+C helper.
if (ConsoleControl.RunHelper(args) is { } helperExit)
    return helperExit;

Console.OutputEncoding = Encoding.UTF8;
var ignoreCtrlC = args.Contains("--ignore-ctrl-c");
var exit = new TaskCompletionSource();

void Log(string type, string message) => Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}][{type}]: {message}");

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    if (ignoreCtrlC)
    {
        Log("Warning", "Ignoring Ctrl+C");
        return;
    }
    Log("LMP", "Exiting... Please wait until all threads are finished");
    exit.TrySetResult();
};

Log("Info", "Remember! Quit the server by using 'Control + C' so a backup is properly made before closing!");
Log("LMP", "Luna Server version: 0.29.2-fake");
Log("Debug", "Loading settings...");

if (args.Contains("--fatal"))
{
    Log("Fatal", "Error in main server thread, Exception: System.Net.Sockets.SocketException: Address already in use");
    Console.WriteLine("   at Server.Lidgren.LidgrenServer.SetupLidgrenServer()");
    Console.ReadLine(); // Avoid closing automatically
    return 1;
}

Log("LMP", "All systems up and running. Поехали!");

if (args.Contains("--crash"))
{
    await Task.Delay(300);
    Log("Fatal", "Simulated crash");
    return 3;
}

_ = Task.Run(async () =>
{
    while (Console.ReadLine() is { } input)
    {
        if (input.Length == 0)
            continue;
        Log("LMP", $"Command input: {input}");
        if (input == "/help")
            Log("LMP", "Available commands: help, say, listclients");
        else if (!input.StartsWith('/'))
            Log("Chat", $"Server: {input}");
        await Task.Delay(50);
    }
    Log("LMP", "End of stdin, stopping command listener");
});

await exit.Task;
Log("LMP", "So long and thanks for all the fish!");
return 0;
