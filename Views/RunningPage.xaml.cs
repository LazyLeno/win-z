using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using WinZ.Engine;
using WinZ.Services;
using WinZ.ViewModels;

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

        TaskList.ItemsSource = vm.Tasks;

        // Bind header text changes
        vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(RunningViewModel.CurrentTask):
                    CurrentTaskLabel.Text = vm.CurrentTask;
                    break;
                case nameof(RunningViewModel.SubText):
                    SubLabel.Text = vm.SubText;
                    break;
                case nameof(RunningViewModel.Progress):
                    UpdateProgressBar();
                    break;
            }
        };

        // Log lines
        vm.LogLines.CollectionChanged += (_, _) =>
        {
            if (_logOpen)
            {
                LogBox.Text = string.Join("\n", vm.LogLines);
                LogScroller.ScrollToBottom();
            }
        };

        // Navigate to Summary when complete — single pathway via Completed event only
        vm.Completed += NavigateToSummary;
    }

    private void UpdateProgressBar()
    {
        int done  = _vm.Progress;
        int total = _vm.TotalTasks;
        if (total == 0) return;

        ProgressLabel.Text = $"{done} / {total}";

        // Get track width from parent container
        if (ProgressFill.Parent is Border track && track.ActualWidth > 0)
            ProgressFill.Width = (double)done / total * track.ActualWidth;
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

    private void NavigateToSummary(object? sender, List<SetupResult> results)
    {
        if (_navigated || results == null) return;

        _navigated = true;

        // Fill progress bar to 100%
        if (ProgressFill.Parent is Border track)
            ProgressFill.Width = track.ActualWidth;
        ProgressLabel.Text = $"{_vm.TotalTasks} / {_vm.TotalTasks}";

        var summaryVm = new SummaryViewModel();
        summaryVm.Load(results);
        NavigationService?.Navigate(new SummaryPage(summaryVm));
    }
}
