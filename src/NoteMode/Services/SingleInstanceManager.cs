using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace NoteMode.Services;

/// <summary>
/// Enforces a single running instance. The first instance owns a named mutex and
/// listens on a named pipe; later instances forward their command-line arguments
/// (the files to open) to the first instance and then exit.
/// </summary>
public static class SingleInstanceManager
{
    // Unique, app-specific names. The leading scope keeps the mutex per-user session.
    private const string MutexName = @"Local\NoteMode-SingleInstance-7b3f1c20";
    // Pipe names are machine-wide, so scope it like the mutex: one per user session. Otherwise a
    // second signed-in user's instance could not create it and files would go to the wrong user.
    private static readonly string PipeName =
        $"NoteMode-SingleInstance-Pipe-7b3f1c20-{Environment.UserName}-{System.Diagnostics.Process.GetCurrentProcess().SessionId}";

    private static Mutex? _mutex;

    /// <summary>True if this process is the primary (first) instance.</summary>
    public static bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        return createdNew;
    }

    /// <summary>Release the primary-instance lock on shutdown.</summary>
    public static void Release()
    {
        try { _mutex?.ReleaseMutex(); } catch { /* not owned */ }
        _mutex?.Dispose();
        _mutex = null;
    }

    /// <summary>
    /// Forward the given arguments to the already-running primary instance.
    /// Returns false if the primary instance could not be reached.
    /// </summary>
    public static bool SendToPrimaryInstance(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            // The primary instance starts listening once its window is up; give a cold start time.
            client.Connect(5000);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            foreach (var arg in args)
            {
                if (!string.IsNullOrEmpty(arg))
                    writer.WriteLine(arg);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Begin listening (on a background thread) for arguments forwarded by later
    /// instances. The callback is invoked on that background thread.
    /// </summary>
    public static void StartServer(Action<string[]> onArgsReceived)
    {
        var thread = new Thread(() => ServerLoop(onArgsReceived))
        {
            IsBackground = true,
            Name = "SingleInstanceServer"
        };
        thread.Start();
    }

    private static void ServerLoop(Action<string[]> onArgsReceived)
    {
        while (true)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None);
                server.WaitForConnection();

                var lines = new List<string>();
                using (var reader = new StreamReader(server))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                        lines.Add(line);
                }

                onArgsReceived(lines.ToArray());
            }
            catch
            {
                // Keep listening; a bad connection shouldn't kill the server. Pause so a
                // persistent failure (pipe cannot be created) does not spin the CPU.
                Thread.Sleep(500);
            }
        }
    }
}
