using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WinZ.Services;
using WinZ.ViewModels;

namespace WinZ.Views;

public partial class SummaryPage : Page
{
    private readonly SummaryViewModel _vm;

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        MemoryService.Optimize();
    }


    public SummaryPage(SummaryViewModel vm)
    {
        InitializeComponent();
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));

        ResultList.ItemsSource = _vm.Items;

        SucceededCount.Text = _vm.Succeeded.ToString();
        FailedCount.Text    = _vm.Failed.ToString();
        SkippedCount.Text   = _vm.Skipped.ToString();

        if (_vm.Failed == 0)
        {
            HeadingLabel.Text    = "Setup Complete  ✓";
            HeadingSubLabel.Text = "Everything ran successfully. Your system is ready.";
        }
        else
        {
            HeadingLabel.Text    = "Setup Finished with Errors";
            HeadingSubLabel.Text = string.Format("{0} task{1} failed. Review the details below.", _vm.Failed, _vm.Failed == 1 ? "" : "s");
        }
    }

    private void ViewLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinZ", "Logs");
            
            if (Directory.Exists(logDir))
            {
                Process.Start(new ProcessStartInfo(logDir) { UseShellExecute = true });
            }
            else
            {
                var parentDir = Path.GetDirectoryName(logDir);
                if (parentDir != null && Directory.Exists(parentDir))
                {
                    Process.Start(new ProcessStartInfo(parentDir) { UseShellExecute = true });
                }
            }
        }
        catch (Exception)
        {
            /* best-effort */
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
        => Application.Current?.Shutdown();
}

