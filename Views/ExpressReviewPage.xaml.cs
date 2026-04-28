using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using WinZ.Models;
using WinZ.Services;
using WinZ.ViewModels;

namespace WinZ.Views;

public partial class ExpressReviewPage : Page
{
    private readonly List<SetupTask> _tasks;

    public ExpressReviewPage(List<SetupTask> tasks)
    {
        InitializeComponent();
        _tasks = tasks ?? new();
        
        var grouped = _tasks.GroupBy(t => t.SubCategory ?? "Other")
                           .Select(g => new TaskGroup(g.Key, g.ToList()))
                           .ToList();

        DataContext = new { 
            Col1 = grouped.Where((_, i) => i % 3 == 0),
            Col2 = grouped.Where((_, i) => i % 3 == 1),
            Col3 = grouped.Where((_, i) => i % 3 == 2)
        };
        
        UpdateCount();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        MemoryService.Optimize();
    }



    private void OnSelectionChanged(object sender, RoutedEventArgs e) => UpdateCount();

    private void UpdateCount()
    {
        int n = _tasks.Count(t => t.IsSelected);
        CountLabel.Text = string.Format("{0} of {1} items selected", n, _tasks.Count);
        RunBtn.IsEnabled = n > 0;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService?.GoBack();

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        var selected = _tasks.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0) return;

        var log = new LogService();
        var vm  = new RunningViewModel(selected, log);
        NavigationService?.Navigate(new RunningPage(vm));
        await vm.RunAsync();
    }
}

