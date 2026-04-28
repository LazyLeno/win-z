using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using WinZ.Models;
using WinZ.ViewModels;
using WinZ.Services;

namespace WinZ.Views;

public partial class HomePage : Page
{
    private readonly DataService _dataService = new();

    public HomePage()
    {
        InitializeComponent();
        MemoryService.Optimize();
    }

    private void Express_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var tasks = _dataService.LoadTasks();
            NavigationService.Navigate(new ExpressReviewPage(tasks));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load master config:\n{ex.Message}", "WinZ – Master Config Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Advanced_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var tasks = _dataService.LoadTasks();
            var vm = new AdvancedViewModel();
            vm.Load(tasks);
            NavigationService.Navigate(new AdvancedPage(vm));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load master config:\n{ex.Message}", "WinZ – Master Config Error",
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
                _dataService.ForceSyncFromCode();
                MessageBox.Show("Database successfully reset to master seed.", "WinZ – Reset Success");
            }
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Reset Error"); }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _dataService.ExportToJson("winz_config_export.json");
            MessageBox.Show("Config exported to winz_config_export.json", "WinZ – Export Success");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Export Error"); }
    }
}
