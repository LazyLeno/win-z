using System.IO;
using System.Windows;
using System.Windows.Controls;
using WinZ.ViewModels;

namespace WinZ.Views;

public partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        DataContext = new SettingsViewModel();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.Cleanup();
        DataContext = null;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mw)
            mw.NavigateBackToAdvanced();
    }

    private void CheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.DataContext is not WinZ.Models.SetupTask task)
            return;

        switch (task.Id)
        {
            case "set_allow_modded" when cb.IsChecked == true:
                if (!WinZDialog.Show(Window.GetWindow(this),
                    (string)FindResource("L.Diag.ModdedExp.Title"),
                    (string)FindResource("L.Diag.ModdedExp.Msg"),
                    (string)FindResource("L.Diag.ModdedExp.Confirm"),
                    (string)FindResource("L.Diag.ModdedExp.Cancel"),
                    (System.Windows.Media.Geometry)FindResource("Icon.Shield")))
                {
                    task.IsSelected = false;
                }
                break;

            case "set_clm_mode" when cb.IsChecked == true:
                if (!WinZDialog.Show(Window.GetWindow(this),
                    (string)FindResource("L.Diag.Clm.Title"),
                    (string)FindResource("L.Diag.Clm.Msg"),
                    (string)FindResource("L.Diag.Clm.Confirm"),
                    (string)FindResource("L.Diag.Clm.Cancel"),
                    (System.Windows.Media.Geometry)FindResource("Icon.Shield")))
                {
                    task.IsSelected = false;
                }
                break;

            case "set_portable_mode":
                bool enabling = cb.IsChecked == true;
                if (!WinZDialog.Show(Window.GetWindow(this),
                    (string)FindResource(enabling ? "L.Diag.Portable.Enable.Title" : "L.Diag.Portable.Disable.Title"),
                    (string)FindResource(enabling ? "L.Diag.Portable.Enable.Msg"   : "L.Diag.Portable.Disable.Msg"),
                    (string)FindResource("L.Diag.Portable.Confirm"),
                    (string)FindResource("L.Diag.Portable.Cancel"),
                    (System.Windows.Media.Geometry)FindResource("Icon.Shield")))
                {
                    // User cancelled — revert the toggle
                    task.IsSelected = !enabling;
                    break;
                }

                // Write or remove the sentinel file
                var exeDir = System.AppContext.BaseDirectory;
                var sentinelPath = Path.Combine(exeDir, "winz.portable");
                try
                {
                    if (enabling)
                        File.WriteAllText(sentinelPath, "WinZ Portable Mode");
                    else
                        File.Delete(sentinelPath);

                    // 🚀 Actual Restart
                    System.Diagnostics.Process.Start(System.Environment.ProcessPath!);
                    Application.Current.Shutdown();
                }
                catch
                {
                    task.IsSelected = !enabling; // revert if file op failed
                }
                break;
        }
    }
}
