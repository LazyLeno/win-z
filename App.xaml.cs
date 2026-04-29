using System;
using System.Windows;
using WinZ.Services;

namespace WinZ;

public partial class App : Application
{
    public static readonly System.Threading.CancellationTokenSource GlobalCts = new();

    protected override void OnStartup(StartupEventArgs? e)
    {
        // ... (existing exception handling)
        DispatcherUnhandledException += (s, ex) =>
        {
            if (ex.Exception != null)
            {
                MessageBox.Show(
                    string.Format("Unhandled error:\n\n{0}\n{1}\n\n{2}", 
                        ex.Exception.GetType().Name, 
                        ex.Exception.Message, 
                        ex.Exception.StackTrace),
                    "WinZ – Fatal Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
        
        if (e != null) base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        GlobalCts.Cancel(); // Force stop all running engines on close
        base.OnExit(e);
    }
}

