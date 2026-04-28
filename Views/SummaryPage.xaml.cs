using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using WinZ.ViewModels;

namespace WinZ.Views;

public partial class SummaryPage : Page
{
    private readonly SummaryViewModel _vm;

    public SummaryPage(SummaryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;

        ResultList.ItemsSource = vm.Items;

        SucceededCount.Text = vm.Succeeded.ToString();
        FailedCount.Text    = vm.Failed.ToString();
        SkippedCount.Text   = vm.Skipped.ToString();

        if (vm.Failed == 0)
        {
            HeadingLabel.Text    = "Setup Complete  ✓";
            HeadingSubLabel.Text = "Everything ran successfully. Your system is ready.";
        }
        else
        {
            HeadingLabel.Text    = "Setup Finished with Errors";
            HeadingSubLabel.Text = $"{vm.Failed} task{(vm.Failed == 1 ? "" : "s")} failed. Review the details below.";
        }
    }

    private void ViewLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logDir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "WinZ", "Logs");
            Process.Start(new ProcessStartInfo(logDir) { UseShellExecute = true });
        }
        catch { /* best-effort */ }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
        => Application.Current.Shutdown();
}
