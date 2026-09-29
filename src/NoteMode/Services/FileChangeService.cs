using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using NoteMode.ViewModels;

namespace NoteMode.Services;

/// <summary>
/// Polls the open files' last-write times every few seconds (every second, plus their length, for
/// tabs tailing a log) and reports the ones changed by another program. The tab list is read and tabs are updated on the UI thread; only the disk checks (which
/// can be slow on a network share) run in the background.
/// </summary>
public class FileChangeService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private IEnumerable<TabViewModel> _tabs = Array.Empty<TabViewModel>();
    private bool _checking;
    private int _tick;

    // Tailed files are checked every tick; the rest every third one.
    private const int NormalCheckEvery = 3;
    private bool _disposed;

    public event EventHandler<TabViewModel>? FileChangedExternally;

    public FileChangeService()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
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

        var checkAll = _tick++ % NormalCheckEvery == 0;
        var watched = _tabs
            .Where(t => t.IsWatchingForExternalChanges && (checkAll || t.IsTailing))
            .Select(t => (Tab: t, Path: t.FilePath!, Tail: t.IsTailing))
            .ToList();
        if (watched.Count == 0)
            return;

        _checking = true;
        try
        {
            var results = await Task.Run(() => watched
                .Select(w => (Time: LastWriteTimeUtc(w.Path), Length: w.Tail ? TextFileIO.GetLength(w.Path) : null))
                .ToList());
            for (var i = 0; i < watched.Count; i++)
            {
                if (_disposed)
                    break;
                var tab = watched[i].Tab;
                var (time, length) = results[i];
                if ((time is { } t && tab.ReportDiskTimestamp(t)) || (length is { } l && tab.ReportTailLength(l)))
                    FileChangedExternally?.Invoke(this, tab);
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
