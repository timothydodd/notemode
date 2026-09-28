using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using NoteMode.ViewModels;

namespace NoteMode.Services;

/// <summary>
/// Polls the open files' last-write times every few seconds and reports the ones changed by another
/// program. The tab list is read and tabs are updated on the UI thread; only the disk checks (which
/// can be slow on a network share) run in the background.
/// </summary>
public class FileChangeService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private IEnumerable<TabViewModel> _tabs = Array.Empty<TabViewModel>();
    private bool _checking;
    private bool _disposed;

    public event EventHandler<TabViewModel>? FileChangedExternally;

    public FileChangeService()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += OnTick;
    }

    public void SetTabs(IEnumerable<TabViewModel> tabs)
    {
        _tabs = tabs;
    }

    public void Start()
    {
        _timer.Start();
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        if (_checking)
            return;

        var watched = _tabs
            .Where(t => t.IsWatchingForExternalChanges)
            .Select(t => (Tab: t, Path: t.FilePath!))
            .ToList();
        if (watched.Count == 0)
            return;

        _checking = true;
        try
        {
            var times = await Task.Run(() => watched.Select(w => LastWriteTimeUtc(w.Path)).ToList());
            for (var i = 0; i < watched.Count; i++)
            {
                if (!_disposed && times[i] is { } time && watched[i].Tab.ReportDiskTimestamp(time))
                    FileChangedExternally?.Invoke(this, watched[i].Tab);
            }
        }
        finally
        {
            _checking = false;
        }
    }

    private static DateTime? LastWriteTimeUtc(string path)
    {
        try
        {
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
    }
}
