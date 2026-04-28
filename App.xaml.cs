using System;
using System.Windows;
using WinZ.Services;

namespace WinZ;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Catch any unhandled UI thread exceptions and show them
        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show(
                $"Unhandled error:\n\n{ex.Exception.GetType().Name}\n{ex.Exception.Message}\n\n{ex.Exception.StackTrace}",
                "WinZ – Fatal Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
            Shutdown(1);
        };

        if (!ElevationService.IsElevated)
        {
            var r = MessageBox.Show(
                "WinZ needs administrator privileges to install software and modify system settings.\n\nRelaunch as administrator?",
                "Elevation Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (r == MessageBoxResult.Yes)
                ElevationService.RelaunchAsAdmin();
            else
                Shutdown();
            return;
        }
        base.OnStartup(e);
    }
}
