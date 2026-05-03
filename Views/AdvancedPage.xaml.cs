using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WinZ.Models;
using WinZ.Services;
using WinZ.ViewModels;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Windows.Shapes;

namespace WinZ.Views;

public partial class AdvancedPage : Page
{
    private readonly AdvancedViewModel _vm;
    private readonly DataService _dataService;
    private readonly DispatcherTimer _gcTimer;
    private int _gcTickCount;
    private DateTime _lastMenuCloseTime = DateTime.MinValue;

    public AdvancedPage(AdvancedViewModel vm, DataService dataService)
    {
        InitializeComponent();
        _vm = vm;
        _dataService = dataService;

        // Ensure clean slate in Advanced: uncheck everything by default
        foreach (var task in _vm.SelectedTasks.ToList())
            task.IsSelected = false;

        DataContext = _vm;

        // Wire gauge arcs — see HardwareService.ArcLenUnits for geometry
        UpdateArc(CpuArcPath, HardwareService.Instance.CpuUsage);
        UpdateArc(RamArcPath, HardwareService.Instance.RamUsage);
        UpdateArc(GpuArcPath, HardwareService.Instance.GpuUsage);
        HardwareService.Instance.PropertyChanged += OnHardwareChanged;

        // 1s fast Gen 0/1 collect for continuous hit-test cleanup.
        _gcTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _gcTimer.Tick += (_, _) =>
        {
            _gcTickCount++;
            if (_gcTickCount % 5 == 0) MemoryService.DeepOptimize();
            else MemoryService.FastOptimize();
        };
        _gcTimer.Start();
    }

    private void OnHardwareChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HardwareService.CpuUsage))
            UpdateArc(CpuArcPath, HardwareService.Instance.CpuUsage);
        else if (e.PropertyName == nameof(HardwareService.RamUsage))
            UpdateArc(RamArcPath, HardwareService.Instance.RamUsage);
        else if (e.PropertyName == nameof(HardwareService.GpuUsage))
            UpdateArc(GpuArcPath, HardwareService.Instance.GpuUsage);
    }

    /// <summary>
    /// Drives the half-circle arc fill using StrokeDashArray (values in stroke-thickness units).
    /// WPF dash values are multiples of StrokeThickness — NOT pixels.
    /// ArcLenUnits = π × r / thickness  (defined in HardwareService to stay in sync with XAML geometry).
    /// </summary>
    private static void UpdateArc(System.Windows.Shapes.Path arc, double pct)
    {
        double total  = HardwareService.ArcLenUnits;
        double filled = total * Math.Clamp(pct / 100.0, 0, 1);
        double gap    = total - filled + 1000; // large gap hides the rest
        arc.StrokeDashArray = new DoubleCollection { filled, gap };
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        HardwareService.Instance.PropertyChanged -= OnHardwareChanged;
        _gcTimer.Stop();
        _vm.Cleanup();
        DataContext = null;
        MemoryService.Optimize();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
        => Application.Current.Shutdown();


    private void Info_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            if ((DateTime.Now - _lastMenuCloseTime).TotalMilliseconds < 150)
            {
                return;
            }

            var cm = new ContextMenu { Style = (Style)FindResource("WinZContextMenu") };
            cm.Closed += (s, ev) => _lastMenuCloseTime = DateTime.Now;
            var aboutItem = new MenuItem { 
                Header = FindResource("L.Menu.About"), 
                Icon = CreateIcon("Icon.Info") 
            };
            aboutItem.Click += (s, ev) => {
                if (Window.GetWindow(this) is MainWindow mw)
                    mw.NavigateAbout();
            };
            cm.Items.Add(aboutItem);
            
            cm.PlacementTarget = btn;
            cm.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu = cm;
            cm.IsOpen = true;
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            if ((DateTime.Now - _lastMenuCloseTime).TotalMilliseconds < 150)
            {
                return;
            }

            var cm = new ContextMenu { Style = (Style)FindResource("WinZContextMenu") };
            cm.Closed += (s, ev) => _lastMenuCloseTime = DateTime.Now;
            var prefItem = new MenuItem { 
                Header = FindResource("L.Menu.Preferences"), 
                Icon = CreateIcon("Icon.Settings")
            };
            prefItem.Click += (s, ev) => {
                if (Window.GetWindow(this) is MainWindow mw)
                    mw.NavigateSettings();
            };
            
            cm.Items.Add(prefItem);
            cm.Items.Add(new Separator { Style = (Style)FindResource("DropdownSeparator") });
            
            var portableItem = new MenuItem { 
                Header = FindResource("L.Menu.Portable"), 
                IsCheckable = true, 
                IsChecked = DataService.IsPortableMode 
            };
            portableItem.Click += Portable_Click;
            cm.Items.Add(portableItem);

            cm.PlacementTarget = btn;
            cm.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu = cm;
            cm.IsOpen = true;
        }
    }

    private Path CreateIcon(string resourceKey)
    {
        return new Path
        {
            Data = (Geometry)FindResource(resourceKey),
            Fill = (Brush)FindResource("SecondaryBrush"),
            Stretch = Stretch.Uniform,
            Width = 18,
            Height = 18
        };
    }

    private void FileMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            if ((DateTime.Now - _lastMenuCloseTime).TotalMilliseconds < 150)
            {
                return;
            }

            var cm = new ContextMenu { Style = (Style)FindResource("WinZContextMenu") };
            cm.Closed += (s, ev) => _lastMenuCloseTime = DateTime.Now;
            
            var exportItem = new MenuItem { 
                Header = FindResource("L.Menu.Export"),
                Icon = new Path { Data = Geometry.Parse("M5,20H19V18H5M19,9H15V3H9V9H5L12,16L19,9Z"), Fill = (Brush)FindResource("SecondaryBrush"), Stretch = Stretch.Uniform, Width = 18, Height = 18 }
            };
            exportItem.Click += Export_Click;
            
            var importItem = new MenuItem { 
                Header = FindResource("L.Menu.Import"),
                Icon = new Path { Data = Geometry.Parse("M5,20H19V18H5M5,10H9V16H15V10H19L12,3L5,10Z"), Fill = (Brush)FindResource("SecondaryBrush"), Stretch = Stretch.Uniform, Width = 18, Height = 18 }
            };
            importItem.Click += Import_Click;
            
            var exitItem = new MenuItem { 
                Header = FindResource("L.Menu.Exit"),
                InputGestureText = "Alt+F4",
                Icon = new Path { Data = Geometry.Parse("M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20M12,2C6.47,2 2,6.47 2,12C2,17.53 6.47,22 12,22C17.53,22 22,17.53 22,12C22,6.47 17.53,2 12,2M13,12V7H11V12H13Z"), Fill = (Brush)FindResource("SecondaryBrush"), Stretch = Stretch.Uniform, Width = 18, Height = 18 }
            };
            exitItem.Click += Back_Click;

            cm.Items.Add(exportItem);
            cm.Items.Add(importItem);
            cm.Items.Add(new Separator { Style = (Style)FindResource("DropdownSeparator") });
            cm.Items.Add(exitItem);

            cm.PlacementTarget = btn;
            cm.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu = cm;
            cm.IsOpen = true;
        }
    }

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        var selected = _vm.AllTasks.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0) return;

        // Review Prompt (Custom Dialog)
        var reviewDiag = new ReviewDialog(selected);
        reviewDiag.Owner = Window.GetWindow(this);
        reviewDiag.ShowDialog();

        if (!reviewDiag.Result) return;

        // Re-filter in case user unchecked items in the dialog
        selected = selected.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0) return;

        // Check for already installed items in parallel (Point #2: Task.WhenAll)
        var checkTasks = selected.Select(async task => 
        {
            if (await DetectionService.IsInstalledAsync(task))
                return task;
            return null;
        });
        var checkedResults = await Task.WhenAll(checkTasks);
        var alreadyInstalled = checkedResults.Where(r => r != null).Cast<SetupTask>().ToList();

        if (alreadyInstalled.Any())
        {
            var diag = new ReinstallDialog(alreadyInstalled);
            diag.Owner = Window.GetWindow(this);
            diag.ShowDialog();

            if (!diag.Result) return;

            foreach (var item in diag.Items)
            {
                item.Task.ShouldUninstallFirst = item.IsReinstallSelected;
            }
        }

        if (!WinZDialog.Show(Window.GetWindow(this), "L.Diag.Ready.Title", 
            "L.Diag.Ready.Msg",
            "L.Diag.Ready.Confirm", "L.Back", (Geometry)FindResource("Icon.Warning"))) return;

        // Restore Point Prompt (Safety Feature)
        var promptSetting = await _dataService.GetSettingAsync("PromptRestorePoint");
        if (promptSetting != "False")
        {
            if (WinZDialog.Show(Window.GetWindow(this), "L.Diag.Safety.Title",
                "L.Diag.Safety.Msg",
                "L.Diag.Safety.Confirm", "L.Diag.Safety.Skip", (Geometry)FindResource("Icon.Shield")))
            {
                var originalCursor = this.Cursor;
                this.Cursor = System.Windows.Input.Cursors.Wait;
                try
                {
                    // Descriptive name as requested
                    await SafetyService.CreateRestorePointAsync("WinZ Pre-Installation Backup");
                }
                finally
                {
                    this.Cursor = originalCursor;
                }
            }
        }

        using var log = new LogService();
        var vm  = new RunningViewModel(selected, log, _dataService);
        MemoryService.Optimize();
        NavigationService.Navigate(new RunningPage(vm));
        await vm.RunAsync(App.GlobalCts.Token);
    }

    private void CheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: SetupTask task } cb && task.IsModded)
        {
            if (cb.IsChecked == true)
            {
                if (!WinZDialog.Show(Window.GetWindow(this), "L.Diag.Modded.Title",
                    "L.Diag.Modded.Msg",
                    "L.Global.Continue", "L.Global.Cancel", (Geometry)FindResource("Icon.Warning")))
                {
                    task.IsSelected = false;
                }
            }
        }
    }

    private void ToggleMod_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: SetupTask task })
        {
            task.IsModded = !task.IsModded;
            
            // If turning ON modded, show the warning if it's selected
            if (task.IsModded && task.IsSelected)
            {
                 if (!WinZDialog.Show(Window.GetWindow(this), "L.Diag.Modded.Title",
                    "L.Diag.Modded.Msg",
                    "L.Global.Continue", "L.Global.Cancel", (Geometry)FindResource("Icon.Warning")))
                {
                    task.IsModded = false;
                }
            }
        }
    }

    private void OpenWebsite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: SetupTask task } && task.EffectiveFallbackUrl != null)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = task.EffectiveFallbackUrl.ToString(),
                    UseShellExecute = true
                });
            }
            catch { /* best effort */ }
        }
    }

    private void Portable_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;

        bool enabling = mi.IsChecked;
        if (!WinZDialog.Show(Window.GetWindow(this),
            (string)FindResource(enabling ? "L.Diag.Portable.Enable.Title" : "L.Diag.Portable.Disable.Title"),
            (string)FindResource(enabling ? "L.Diag.Portable.Enable.Msg" : "L.Diag.Portable.Disable.Msg"),
            (string)FindResource("L.Diag.Portable.Confirm"),
            (string)FindResource("L.Diag.Portable.Cancel"),
            (System.Windows.Media.Geometry)FindResource("Icon.Shield")))
        {
            // User cancelled — revert the toggle
            mi.IsChecked = !enabling;
            return;
        }

        // Write or remove the sentinel file
        var exeDir = System.AppContext.BaseDirectory;
        var sentinelPath = System.IO.Path.Combine(exeDir, "winz.portable");
        try
        {
            if (enabling)
                System.IO.File.WriteAllText(sentinelPath, "WinZ Portable Mode");
            else
                System.IO.File.Delete(sentinelPath);

            // 🚀 Actual Restart
            System.Diagnostics.Process.Start(System.Environment.ProcessPath!);
            System.Windows.Application.Current.Shutdown();
        }
        catch
        {
            mi.IsChecked = !enabling; // revert if file op failed
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdvancedViewModel vm)
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "WinZ Profile (*.winz)|*.winz",
                FileName = "MyProfile.winz"
            };

            if (sfd.ShowDialog() == true)
            {
                var bytes = vm.ExportSelectionBytes();
                System.IO.File.WriteAllBytes(sfd.FileName, bytes);
            }
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdvancedViewModel vm)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "WinZ Profile (*.winz)|*.winz"
            };

            if (ofd.ShowDialog() == true)
            {
                var bytes = System.IO.File.ReadAllBytes(ofd.FileName);
                vm.ImportSelectionBytes(bytes);
            }
        }
    }
}
