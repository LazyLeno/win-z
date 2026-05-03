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
        _vm = vm;
        DataContext = vm;
        ResultList.ItemsSource = vm.Items;
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
    {
        if (Window.GetWindow(this) is MainWindow mw)
        {
            mw.NavigateBackToAdvanced();
        }
        else
        {
            Application.Current?.Shutdown();
        }
    }
}

