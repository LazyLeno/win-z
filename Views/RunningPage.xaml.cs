using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using WinZ.Engine;
using WinZ.Services;
using WinZ.ViewModels;
using System.Windows.Media;

namespace WinZ.Views;

public partial class RunningPage : Page
{
    private readonly RunningViewModel _vm;
    private bool _logOpen    = false;
    private bool _navigated  = false;   // guard against double navigation

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        _vm.Dispose();      // Detach all engine + log event handlers
        DataContext = null;
        MemoryService.Optimize();
    }


    public RunningPage(RunningViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        TaskList.ItemsSource = vm.Tasks;

        // Log lines
        vm.LogLines.CollectionChanged += (_, _) =>
        {
            if (_logOpen)
            {
                LogBox.Text = string.Join("\n", vm.LogLines);
                LogScroller.ScrollToBottom();
            }
        };

        // Navigate to Summary when complete
        vm.Completed += NavigateToSummary;
    }

    private void LogToggle_Click(object sender, RoutedEventArgs e)
    {
        _logOpen = !_logOpen;
        LogPanel.Height = _logOpen ? double.NaN : 0;
        LogArrow.Text   = _logOpen ? "▼" : "▶";

        if (_logOpen)
        {
            LogBox.Text = string.Join("\n", _vm.LogLines);
            LogScroller.ScrollToBottom();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (WinZDialog.Show(Window.GetWindow(this), "L.Diag.Cancel.Title", 
            "L.Diag.Cancel.Msg",
            "L.Global.OK", "L.Global.Cancel", (Geometry)FindResource("Icon.Warning")))
        {
            _vm.Cancel();
            CancelBtn.IsEnabled = false;
            CancelBtn.Content = Application.Current.FindResource("L.Run.Cancelling");
        }
    }

    private void NavigateToSummary(object? sender, List<SetupResult> results)
    {
        if (_navigated || results == null) return;

        _navigated = true;

        var summaryVm = new SummaryViewModel();
        summaryVm.Load(results);
        NavigationService?.Navigate(new SummaryPage(summaryVm));
    }
}
