using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WinZ.Services;
using WinZ.ViewModels;

namespace WinZ.Views;

public partial class AdvancedPage : Page
{
    private readonly AdvancedViewModel _vm;

    public AdvancedPage(AdvancedViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        _vm.PropertyChanged += (_, _) => UpdateCount();
        UpdateCount();
    }

    private void UpdateCount()
    {
        int n = _vm.SelectedCount;
        CountLabel.Text  = $"{n} item{(n == 1 ? "" : "s")} selected";
        RunBtn.IsEnabled = n > 0;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoBack();

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        var selected = _vm.SelectedTasks.ToList();
        if (selected.Count == 0) return;

        var log = new LogService();
        var vm  = new RunningViewModel(selected, log);
        MemoryService.Optimize();
        NavigationService.Navigate(new RunningPage(vm));
        await vm.RunAsync();
    }
}
