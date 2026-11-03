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
    // Unique, app-specific names, one per user session.
    // Windows: "Local\" scopes the mutex to the session; pipe names are machine-wide, so the pipe
    // carries the user and session too (otherwise a second signed-in user's instance could not
    // create it and files would go to the wrong user).
    private const string MutexName = @"Local\NoteMode-SingleInstance-7b3f1c20";
    // macOS/Linux: a named mutex does not work there (the Native AOT build never shared one between
    // processes in testing, and .NET's "Local\" is the Unix session, a new one per terminal window),
    // so an exclusive lock on a per-user file in the temp folder decides instead; the OS drops it if
    // the process dies. The pipe is a Unix socket in the temp folder, whose path must stay under
    // ~104 characters (macOS's temp folder alone is ~50), so its name is kept short.
    private static readonly string PipeName = OperatingSystem.IsWindows()
        ? $"NoteMode-SingleInstance-Pipe-7b3f1c20-{Environment.UserName}-{System.Diagnostics.Process.GetCurrentProcess().SessionId}"
        : $"notemode-{Environment.UserName}";

    private static Mutex? _mutex;
    private static FileStream? _lockFile;

    /// <summary>True if this process is the primary (first) instance.</summary>
    public static bool TryAcquire()
    {
        if (OperatingSystem.IsWindows())
        {
            _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
            return createdNew;
        }

        try
        {
            _lockFile = new FileStream(
                Path.Combine(Path.GetTempPath(), $"notemode-{Environment.UserName}.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false; // held by the running instance
        }
        catch
        {
            return true; // cannot lock at all (temp folder unusable): just run
        }
    }

    /// <summary>Release the primary-instance lock on shutdown.</summary>
    public static void Release()
    {
        try { _mutex?.ReleaseMutex(); } catch { /* not owned */ }
        _mutex?.Dispose();
        _mutex = null;
        _lockFile?.Dispose();
        _lockFile = null;
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
