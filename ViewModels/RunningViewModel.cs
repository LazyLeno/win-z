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

    private string _currentTask = "L.Run.Starting";
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
            CurrentTask = t.Id;
            SubText = t.Type switch
            {
                TaskType.Install => t.Method switch {
                    InstallMethod.Winget => GetText("L.Run.Methods.Winget"),
                    InstallMethod.Scoop  => GetText("L.Run.Methods.Scoop"),
                    _ => GetText("L.Run.Methods.Direct")
                },
                TaskType.Tweak   => GetText("L.Run.Methods.Tweak"),
                TaskType.Remove  => GetText("L.Run.Methods.Remove"),
                _                => ""
            };
        });
    }

    private string GetText(string key) => Application.Current?.TryFindResource(key)?.ToString() ?? key;

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

    private CancellationTokenSource? _sessionCts;

    public async Task RunAsync(CancellationToken ct)
    {
        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        
        Results = await _engine.RunAsync(Tasks, false, _sessionCts.Token);

        if (Results != null && !_sessionCts.IsCancellationRequested)
        {
            await Task.WhenAll(Results.Select(r => _dataService.SaveResultAsync(r)));

            IsComplete  = true;
            CurrentTask = "L.Run.Done";
            int succ = Results.Count(r => r.Status == TaskStatus.Success);
            int fail = Results.Count(r => r.Status == TaskStatus.Failed);
            SubText     = $"{succ} {GetText("L.Sum.DoneItem")} · {fail} {GetText("L.Sum.FailedItem")}";
            Completed?.Invoke(this, Results);
        }
        else if (_sessionCts.IsCancellationRequested)
        {
            IsComplete = true;
            CurrentTask = "L.Run.Cancelled";
            SubText = GetText("L.Run.CancelSub");
            Completed?.Invoke(this, Results ?? new List<SetupResult>());
        }
    }

    public void Cancel()
    {
        _sessionCts?.Cancel();
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
