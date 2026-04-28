using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinZ.Models;

public enum TaskType   { Install, Tweak, Remove }
public enum TaskStatus { Queued, Running, Success, Failed, Skipped }
public enum InstallMethod { Winget, DirectDownload }

public class SetupTask : INotifyPropertyChanged
{
    public string Id          { get; init; } = Guid.NewGuid().ToString();
    public string Name        { get; init; } = "";
    public TaskType Type      { get; init; }
    public InstallMethod? Method { get; init; }
    public string? PackageId   { get; init; }
    public string? FallbackUrl { get; init; }
    public string? TweakScript { get; init; }
    public string Category    { get; init; } = "General";
    public string SubCategory { get; init; } = "Misc";
    public string Icon        { get; init; } = "";
    public string? IconUrl    { get; init; }

    public bool HasIcon => !string.IsNullOrWhiteSpace(Icon) || !string.IsNullOrWhiteSpace(IconUrl);

    private System.Windows.Media.ImageSource? _iconImage;
    public System.Windows.Media.ImageSource? IconImage
    {
        get
        {
            if (_iconImage == null && !string.IsNullOrEmpty(IconUrl))
            {
                // Trigger lazy load
                _iconImage = Services.AsyncImageLoader.GetImage(IconUrl, img => {
                    _iconImage = img;
                    OnPropertyChanged();
                });
            }
            return _iconImage;
        }
    }

    public string Description { get; init; } = "";
    public int RetryMax       { get; init; } = 3;

    private bool _isSelected = false;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    private TaskStatus _status = TaskStatus.Queued;
    public TaskStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(IsRunning)); OnPropertyChanged(nameof(IsDone)); }
    }

    private string _statusText = "Queued";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public bool IsRunning => Status == TaskStatus.Running;
    public bool IsDone    => Status is TaskStatus.Success or TaskStatus.Failed or TaskStatus.Skipped;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
