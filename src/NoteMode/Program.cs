using Avalonia;
using System;
using System.IO;
using System.Linq;
using NoteMode.Services;

namespace NoteMode;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Relative paths ("notemode notes.txt" in a terminal) mean nothing in the other process.
        args = args.Select(ToFullPath).ToArray();

        // Single-instance: if another instance is already running, hand our
        // arguments (the files to open) to it and exit instead of launching again.
        // If it cannot be reached (hung, still starting), open a window here rather than drop them.
        if (!SingleInstanceManager.TryAcquire() && SingleInstanceManager.SendToPrimaryInstance(args))
        {
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            SingleInstanceManager.Release();
        }
    }

    private static string ToFullPath(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg) || arg.StartsWith('-'))
            return arg;
        try
        {
            return Path.GetFullPath(arg);
        }
        catch
        {
            return arg;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
