using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WinZ.Engine;
using WinZ.Models;
using WinZ.Services;
using TaskStatus = WinZ.Models.TaskStatus;

namespace WinZ.ViewModels;

public class RunningViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly InstallEngine _engine;
    private readonly LogService    _log;
    private readonly DataService   _dataService;

    public ObservableCollection<SetupTask> Tasks    { get; } = new();
    public ObservableCollection<string>   LogLines { get; } = new();

    private string _currentTask = "Starting…";
    public string CurrentTask { get => _currentTask; set { _currentTask = value; OnPC(); } }

    private string _subText = "";
    public string SubText { get => _subText; set { _subText = value; OnPC(); } }

    private bool _isComplete;
    public bool IsComplete { get => _isComplete; set { _isComplete = value; OnPC(); } }

    private int _progress;
    public int Progress { get => _progress; set { _progress = value; OnPC(); } }

    public int TotalTasks => Tasks.Count;
    public List<SetupResult>? Results { get; private set; }

    public event EventHandler<List<SetupResult>>? Completed;

    private bool _disposed;

    public RunningViewModel(IEnumerable<SetupTask> tasks, LogService log, DataService dataService)
    {
        _log         = log;
        _dataService = dataService;
        _engine      = new InstallEngine(log);

        foreach (var t in tasks)
            Tasks.Add(t);

        // Store handlers as fields so we can detach them in Dispose()
        _engine.TaskStarted   += OnTaskStarted;
        _engine.TaskCompleted += OnTaskCompleted;
        _log.LineAppended     += OnLogLine;
    }

    private void OnTaskStarted(object? s, SetupTask t)
    {
        Application.Current?.Dispatcher?.InvokeAsync(() =>
        {
            CurrentTask = t.Name ?? "";
            SubText = t.Type switch
            {
                TaskType.Install => t.Method switch {
                    InstallMethod.Winget => "Installing via Winget",
                    InstallMethod.Scoop  => "Installing via Scoop",
                    _ => "Downloading and installing"
                },
                TaskType.Tweak   => "Applying system tweak",
                TaskType.Remove  => "Removing via PowerShell",
                _                => ""
            };
        });
    }

    private void OnTaskCompleted(object? s, (SetupTask task, bool success) args)
    {
        Application.Current?.Dispatcher?.InvokeAsync(() => Progress++);
    }

    private void OnLogLine(string line)
    {
        Application.Current?.Dispatcher?.InvokeAsync(() =>
        {
            // Cap at 500 lines — prevents unbounded ObservableCollection growth
            if (LogLines.Count >= 500) LogLines.RemoveAt(0);
            LogLines.Add(line);
        });
    }

    public Task RunAsync() => RunAsync(CancellationToken.None);

    public async Task RunAsync(CancellationToken ct)
    {
        Results = await _engine.RunAsync(Tasks, false, ct);

        if (Results != null)
        {
            foreach (var r in Results) _dataService.SaveResult(r);

            IsComplete  = true;
            CurrentTask = "Setup complete";
            SubText     = $"{Results.Count(r => r.Status == TaskStatus.Success)} succeeded · {Results.Count(r => r.Status == TaskStatus.Failed)} failed";
            Completed?.Invoke(this, Results);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Explicit detach — guaranteed cleanup regardless of GC timing
        _engine.TaskStarted   -= OnTaskStarted;
        _engine.TaskCompleted -= OnTaskCompleted;
        _log.LineAppended     -= OnLogLine;
        GC.SuppressFinalize(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPC([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
