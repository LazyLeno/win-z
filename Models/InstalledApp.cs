using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace WinZ.Models;

public class InstalledApp : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public string? Publisher { get; set; }
    public string? UninstallString { get; set; }
    public string? IconPath { get; set; }
    public ImageSource? Icon { get; set; }
    public string? Version { get; set; }
    public long RawSize { get; set; }
    
    private string? _size;
    public string? Size 
    { 
        get => _size; 
        set { _size = value; OnPropertyChanged(); } 
    }

    public string? InstallLocation { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
