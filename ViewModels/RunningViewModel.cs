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

public class RunningViewModel : INotifyPropertyChanged
{
    private readonly InstallEngine _engine;
    private readonly LogService    _log;
    private readonly DataService   _dataService = new();

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

    public int TotalTasks => Tasks.Count(t => t.IsSelected);

    public List<SetupResult>? Results { get; private set; }

    public event System.Action<List<SetupResult>>? Completed;

    public RunningViewModel(IEnumerable<SetupTask> tasks, LogService log)
    {
        _log    = log;
        _engine = new InstallEngine(log);

        foreach (var t in tasks.Where(t => t.IsSelected))
            Tasks.Add(t);

        _engine.TaskStarted += t => Application.Current.Dispatcher.Invoke(() =>
        {
            CurrentTask = t.Name;
            SubText = t.Type switch
            {
                TaskType.Install => $"Installing via {t.Method}",
                TaskType.Tweak   => "Applying system tweak",
                TaskType.Remove  => "Removing via PowerShell",
                _                => ""
            };
        });

        _engine.TaskCompleted += (t, ok) => Application.Current.Dispatcher.Invoke(() =>
        {
            Progress++;
        });

        log.LineAppended += line => Application.Current.Dispatcher.Invoke(() =>
            LogLines.Add(line));
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        Results    = await _engine.RunAsync(Tasks, ct: ct);

        // Persistent history
        foreach (var r in Results) _dataService.SaveResult(r);

        IsComplete = true;
        CurrentTask = "Setup complete";
        SubText = $"{Results.Count(r => r.Status == TaskStatus.Success)} succeeded · " +
                  $"{Results.Count(r => r.Status == TaskStatus.Failed)} failed";
        Completed?.Invoke(Results);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPC([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
