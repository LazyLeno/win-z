using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WinZ.Services;
using WinZ.ViewModels;

namespace WinZ.Views;

public partial class AdvancedPage : Page
{
    private readonly AdvancedViewModel _vm;
    private readonly DataService _dataService;

    public AdvancedPage(AdvancedViewModel vm, DataService dataService)
    {
        InitializeComponent();
        _vm = vm;
        _dataService = dataService;
        DataContext = _vm;
        WeakEventManager<AdvancedViewModel, PropertyChangedEventArgs>.AddHandler(_vm, nameof(_vm.PropertyChanged), OnVmPropertyChanged);
        UpdateCount();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        => UpdateCount();

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        DataContext = null;
        MemoryService.Optimize();
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

        using var log = new LogService();
        var vm  = new RunningViewModel(selected, log, _dataService);
        MemoryService.Optimize();
        NavigationService.Navigate(new RunningPage(vm));
        await vm.RunAsync();
    }
}
