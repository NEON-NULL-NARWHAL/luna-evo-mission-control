// LUNA EVO · LunaEvo.ConfigManager.Core/Server/ServerRunner.cs
// [Core] Starts Server.exe hidden with its console piped to us; sends commands; stops it cleanly.
// Used by: ServerPage
// Guide:   Meat and Potatoes/03-Server-Control.md

using System.Diagnostics;
using System.Text;

namespace LunaEvo.ConfigManager.Core.Server;

public enum ServerState { Stopped, Starting, Running, Stopping }

/// <summary>
/// Starts Server.exe without a window and pipes its console to us, so the GUI can show the output and send commands.
/// LMP reads commands from stdin ("/help", "/kick name"; plain text is broadcast as chat) and stops cleanly on Ctrl+C.
/// </summary>
public sealed class ServerRunner : IDisposable
{
    public const string ReadyMarker = "All systems up and running";

    private readonly string _executable;
    private readonly Lock _gate = new();
    private Process? _process;
    private TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private LogKind _lastKind = LogKind.Normal;
    private bool _stopRequested;

    /// <summary>One runner per server folder. <paramref name="executableName"/> lets tests use the fake server.</summary>
    public ServerRunner(string serverDirectory, string executableName = "Server.exe")
    {
        ServerDirectory = serverDirectory;
        _executable = Path.Combine(serverDirectory, executableName);
    }

    public string ServerDirectory { get; }
    public ServerState State { get; private set; } = ServerState.Stopped;
    public int? ProcessId { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public bool IsActive => State != ServerState.Stopped;

    /// <summary>Raised on a background thread for every line the server prints (and a few of our own).</summary>
    public event Action<LogLine>? LineReceived;

    /// <summary>Raised on a background thread.</summary>
    public event Action<ServerState>? StateChanged;

    /// <summary>
    /// Raised on a background thread when the process ends: (exit code, whether we asked it to stop).
    /// "Not requested" means it crashed or was closed from outside, which is when auto-restart kicks in.
    /// </summary>
    public event Action<int, bool>? Exited;

    /// <summary>Starts the server and begins streaming its output. State goes Starting, then Running once LMP prints its ready line.</summary>
    public void Start(IEnumerable<string>? arguments = null)
    {
        lock (_gate)
        {
            if (IsActive)
                throw new InvalidOperationException("The server is already running.");
            if (!File.Exists(_executable))
                throw new FileNotFoundException($"{Path.GetFileName(_executable)} not found in {ServerDirectory}.", _executable);

            var info = new ProcessStartInfo(_executable)
            {
                WorkingDirectory = ServerDirectory,
                UseShellExecute = false,
                CreateNoWindow = true, // still gets a (hidden) console, which Ctrl+C needs
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            foreach (var argument in arguments ?? [])
                info.ArgumentList.Add(argument);

            var process = new Process { StartInfo = info, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => OnOutput(e.Data);
            process.ErrorDataReceived += (_, e) => OnOutput(e.Data, isError: true);
            process.Exited += (_, _) => OnExited(process);

            _process?.Dispose(); // the previous run's, kept until now so late callers never see a disposed process
            _exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _stopRequested = false;
            Emit(LogLine.FromManager($"Starting {Path.GetFileName(_executable)} in {ServerDirectory}"));
            SetState(ServerState.Starting);
            ConsoleControl.AllowCtrlCInChildren();
            process.Start();
            process.StandardInput.AutoFlush = true;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _process = process;
            ProcessId = process.Id;
            StartedAt = DateTime.Now;
        }
    }

    /// <summary>Sends a console command. LMP only accepts one every 500 ms.</summary>
    public void SendCommand(string text)
    {
        var process = _process;
        if (process == null || State is ServerState.Stopped or ServerState.Stopping)
            throw new InvalidOperationException("The server isn't running.");
        // LMP takes one command per line, so a pasted multi-line message would turn into several commands.
        text = text.Replace("\r", " ").Replace("\n", " ");
        process.StandardInput.WriteLine(text);
    }

    /// <summary>Asks the server to stop with Ctrl+C, and kills it if it hasn't exited after <paramref name="timeout"/>.</summary>
    /// <returns>True if it stopped by itself, false if it had to be killed.</returns>
    public async Task<bool> StopAsync(TimeSpan timeout)
    {
        var exited = _exited.Task;
        if (State == ServerState.Stopped || ProcessId is not { } pid)
            return true;

        _stopRequested = true;
        SetState(ServerState.Stopping);
        Emit(LogLine.FromManager("Stopping the server (Ctrl+C), waiting for LMP to finish its backup..."));
        if (!ConsoleControl.SendCtrlC(pid))
            Emit(LogLine.FromManager("Couldn't send Ctrl+C to the server."));

        try
        {
            await exited.WaitAsync(timeout);
            return true;
        }
        catch (TimeoutException)
        {
            Emit(LogLine.FromManager($"The server didn't stop within {timeout.TotalSeconds:0} s, so it was killed."));
            Kill();
            await exited;
            return false;
        }
    }

    /// <summary>Hard kill (no backup). StopAsync uses it only after the timeout.</summary>
    public void Kill()
    {
        try
        {
            if (State != ServerState.Stopped)
            {
                _stopRequested = true;
                _process?.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }
    }

    public void Dispose()
    {
        _process?.Dispose();
        _process = null;
    }

    private void OnOutput(string? data, bool isError = false)
    {
        if (data == null)
            return;

        var line = LogLine.Parse(data, isError ? LogKind.Error : _lastKind);
        if (line.Time != null)
            _lastKind = line.Kind;
        Emit(line);

        if (State == ServerState.Starting && line.Message.Contains(ReadyMarker, StringComparison.Ordinal))
            SetState(ServerState.Running);
        else if (State == ServerState.Starting && line.Kind == LogKind.Fatal)
            // On a startup failure LMP waits on Console.ReadLine() "to avoid closing"; closing stdin lets it exit.
            _process?.StandardInput.Close();
    }

    private void OnExited(Process process)
    {
        // Let the async readers drain the last lines before we announce the exit.
        process.WaitForExit();
        var code = process.ExitCode;
        lock (_gate)
        {
            if (!ReferenceEquals(process, _process))
                return;
            ProcessId = null;
            StartedAt = null;
        }
        Emit(LogLine.FromManager($"Server exited (code {code})."));
        SetState(ServerState.Stopped);
        _exited.TrySetResult();
        Exited?.Invoke(code, _stopRequested);
    }

    private void SetState(ServerState state)
    {
        if (State == state)
            return;
        State = state;
        StateChanged?.Invoke(state);
    }

    private void Emit(LogLine line) => LineReceived?.Invoke(line);
}
