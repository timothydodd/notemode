using System;
using System.Collections.Generic;
using System.Timers;
using Flit.ViewModels;

namespace Flit.Services;

public class FileChangeService : IDisposable
{
    private readonly System.Timers.Timer _timer;
    private IEnumerable<TabViewModel> _tabs = Array.Empty<TabViewModel>();
    private bool _disposed;

    public event EventHandler<TabViewModel>? FileChangedExternally;

    public FileChangeService()
    {
        _timer = new System.Timers.Timer(3000);
        _timer.AutoReset = true;
        _timer.Elapsed += OnTimerElapsed;
    }

    public void SetTabs(IEnumerable<TabViewModel> tabs)
    {
        _tabs = tabs;
    }

    public void Start()
    {
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        foreach (var tab in _tabs)
        {
            if (tab.CheckForExternalChanges())
            {
                FileChangedExternally?.Invoke(this, tab);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Dispose();
    }
}
