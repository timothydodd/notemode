using Avalonia;
using System;
using NoteMode.Services;

namespace NoteMode;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Single-instance: if another instance is already running, hand our
        // arguments (the files to open) to it and exit instead of launching again.
        if (!SingleInstanceManager.TryAcquire())
        {
            SingleInstanceManager.SendToPrimaryInstance(args);
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

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
