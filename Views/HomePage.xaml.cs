using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using WinZ.ViewModels;
using WinZ.Services;

namespace WinZ.Views;

public partial class HomePage : Page
{
    private static DataService? _sharedService;
    private static readonly object _lock = new();

    private DataService GetDataService()
    {
        if (_sharedService == null)
        {
            lock (_lock)
            {
                _sharedService ??= new DataService();
            }
        }
        return _sharedService;
    }

    public HomePage()
    {
        InitializeComponent();
        MemoryService.Optimize();
        
        // Start DB init in background so it's ready when clicks happen
        Task.Run(() => GetDataService());
    }

    private void Express_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var tasks = GetDataService().LoadTasks();
            NavigationService?.Navigate(new ExpressReviewPage(tasks, GetDataService()));
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format("Failed to load master config:\n{0}", ex.Message), "WinZ – Master Config Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Advanced_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var tasks = GetDataService().LoadTasks();
            var ds = GetDataService();
            var vm = new AdvancedViewModel();
            vm.Load(tasks, ds);
            NavigationService?.Navigate(new AdvancedPage(vm, ds));
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format("Failed to load master config:\n{0}", ex.Message), "WinZ – Master Config Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Sync_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (MessageBox.Show("This will reset the database to factory defaults. Continue?", "WinZ – Reset", 
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                GetDataService().ForceSyncFromCode();
                MessageBox.Show("Database successfully reset to master seed.", "WinZ – Reset Success");
            }
        }
        catch (IOException ex)
        {
            MessageBox.Show(string.Format("File error during reset: {0}", ex.Message), "Reset Error");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Reset Error");
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            GetDataService().ExportToJson("winz_config_export.json");
            MessageBox.Show("Config exported to winz_config_export.json", "WinZ – Export Success");
        }
        catch (IOException ex)
        {
            MessageBox.Show(string.Format("File error during export: {0}", ex.Message), "Export Error");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Export Error");
        }
    }
}

