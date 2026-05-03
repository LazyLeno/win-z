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
    public string? ExpectedSha256 { get; set; }
    public string Category    { get; set; } = "General";
    public string Section     { get; set; } = "";
    public string SubCategory { get; set; } = "Misc";

    /// <summary>Placeholder for future icon keys (e.g. emoji or resource key)</summary>
    public string Icon        { get; set; } = "";

    public string Description { get; set; } = "";
    public int RetryMax       { get; set; } = 3;

    // Modded Version Support
    public bool CanBeModded => !string.IsNullOrEmpty(ModdedName);
    public string? ModdedName { get; set; }
    public string? ModdedDescription { get; set; }
    public string? ModdedPackageId { get; set; }
    public InstallMethod? ModdedMethod { get; set; }
    public Uri? ModdedFallbackUrl { get; set; }
    public string? ModdedTweakScript { get; set; }
    public TaskType? ModdedType { get; set; }

    private bool _isModded;
    public bool IsModded
    {
        get => _isModded;
        set 
        { 
            _isModded = value; 
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(DisplayDescription));
        }
    }

    public string DisplayName => (IsModded && !string.IsNullOrEmpty(ModdedName)) ? ModdedName : Name;
    public string DisplayDescription => (IsModded && !string.IsNullOrEmpty(ModdedDescription)) ? ModdedDescription : Description;

    public TaskType EffectiveType => IsModded && ModdedType.HasValue ? ModdedType.Value : Type;
    public InstallMethod? EffectiveMethod => IsModded ? (ModdedMethod ?? (string.IsNullOrEmpty(ModdedPackageId) ? Method : InstallMethod.Winget)) : Method;
    public string? EffectivePackageId => IsModded && !string.IsNullOrEmpty(ModdedPackageId) ? ModdedPackageId : PackageId;
    public string? EffectiveTweakScript => IsModded && !string.IsNullOrEmpty(ModdedTweakScript) ? ModdedTweakScript : TweakScript;
    public Uri? EffectiveFallbackUrl => IsModded && ModdedFallbackUrl != null ? ModdedFallbackUrl : FallbackUrl;


    private bool _shouldUninstallFirst;
    public bool ShouldUninstallFirst
    {
        get => _shouldUninstallFirst;
        set { _shouldUninstallFirst = value; OnPropertyChanged(); }
    }

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

    private int _progress;
    public int Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    public bool RequiresExplorerRestart { get; set; }

    public bool IsRunning => Status == TaskStatus.Running;
    public bool IsDone    => Status is TaskStatus.Success or TaskStatus.Failed or TaskStatus.Skipped;

    public void NotifyRefresh()
    {
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(Status));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
