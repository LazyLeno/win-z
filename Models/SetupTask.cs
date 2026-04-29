using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinZ.Models;

public enum TaskType   { Install, Tweak, Remove }
public enum TaskStatus { Queued, Running, Success, Failed, Skipped }
public enum InstallMethod { Winget, DirectDownload, Scoop }

public class SetupTask : INotifyPropertyChanged
{
    public string Id          { get; set; } = Guid.NewGuid().ToString();
    public string Name        { get; set; } = "";
    public TaskType Type      { get; set; }
    public InstallMethod? Method { get; set; }
    public string? PackageId   { get; set; }
    public Uri? FallbackUrl    { get; set; }
    public string? TweakScript { get; set; }
    public string Category    { get; set; } = "General";
    public string SubCategory { get; set; } = "Misc";

    /// <summary>Placeholder for future icon keys (e.g. emoji or resource key)</summary>
    public string Icon        { get; set; } = "";

    public string Description { get; set; } = "";
    public int RetryMax       { get; set; } = 3;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    private TaskStatus _status = TaskStatus.Queued;
    public TaskStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsDone));
        }
    }

    public bool IsRunning => Status == TaskStatus.Running;
    public bool IsDone    => Status is TaskStatus.Success or TaskStatus.Failed or TaskStatus.Skipped;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
