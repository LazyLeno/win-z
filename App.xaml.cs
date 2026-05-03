using System;
using System.Runtime;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using WinZ.Services;

namespace WinZ;

public partial class App : Application
{
    public static readonly System.Threading.CancellationTokenSource GlobalCts = new();

        protected override void OnStartup(StartupEventArgs? e)
        {
            try
            {
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
            
            GCSettings.LatencyMode = GCLatencyMode.LowLatency;

            // ── Portable Mode detection (must happen before DataService.Instance is accessed) ──
            bool portable = e?.Args != null && System.Array.Exists(e.Args, a => a.Equals("--portable", StringComparison.OrdinalIgnoreCase));
            if (!portable)
            {
                var exeDir = System.AppContext.BaseDirectory;
                portable = System.IO.File.Exists(System.IO.Path.Combine(exeDir, "winz.portable"));
            }
            DataService.IsPortableMode = portable;

            var startupCleanup = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(2.5)
            };
            startupCleanup.Tick += (s, _) =>
            {
                ((DispatcherTimer)s!).Stop();
                MemoryService.DeepOptimize();
            };
            startupCleanup.Start();

            LanguageService.Initialize();
            
            // Load saved language
            _ = Task.Run(async () => {
                var lang = await DataService.Instance.GetSettingAsync("Language");
                if (!string.IsNullOrEmpty(lang))
                {
                    Application.Current.Dispatcher.Invoke(() => LanguageService.SetLanguage(lang));
                }

                // Auto-Update Repos
                var autoUpdate = await DataService.Instance.GetSettingAsync("AutoUpdateRepos") == "True";
                if (autoUpdate)
                {
                    _ = Task.Run(() => {
                        try {
                            var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c winget source update & scoop update") {
                                CreateNoWindow = true,
                                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                            };
                            System.Diagnostics.Process.Start(psi)?.WaitForExit();
                        } catch { }
                    });
                }
            });

            base.OnStartup(e!);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup Crash:\n{ex.Message}\n\n{ex.StackTrace}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        GlobalCts.Cancel(); // Force stop all running engines on close
        base.OnExit(e);
    }
}

