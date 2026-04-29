using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using WinZ.Models;

namespace WinZ.Views;

public partial class ReinstallDialog : Window
{
    public List<ReinstallItem> Items { get; }
    public bool Result { get; private set; }

    public ReinstallDialog(List<SetupTask> tasksToReview)
    {
        InitializeComponent();
        Items = tasksToReview.Select(t => new ReinstallItem { Name = t.Name, Task = t }).ToList();
        ReinstallList.ItemsSource = Items;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        Result = true;
        Close();
    }
}

public class ReinstallItem : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public SetupTask Task { get; set; } = null!;

    private bool _isReinstallSelected = true;
    public bool IsReinstallSelected
    {
        get => _isReinstallSelected;
        set { _isReinstallSelected = value; OnPC(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPC([System.Runtime.CompilerServices.CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
