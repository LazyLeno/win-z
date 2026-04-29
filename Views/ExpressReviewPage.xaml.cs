using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.ComponentModel;
using System.Windows.Media;
using WinZ.Models;
using WinZ.Services;
using WinZ.ViewModels;

namespace WinZ.Views;

public partial class ExpressReviewPage : Page
{
    private readonly List<SetupTask> _tasks;
    private readonly DataService _dataService;

    public ExpressReviewPage(List<SetupTask> tasks, DataService dataService)
    {
        InitializeComponent();
        _tasks = tasks ?? new();
        _dataService = dataService;
        
        var grouped = _tasks.GroupBy(t => t.SubCategory ?? "Other")
                           .Select(g => new TaskGroup(g.Key, g.ToList()))
                           .ToList();

        foreach (var task in _tasks)
        {
            WeakEventManager<SetupTask, PropertyChangedEventArgs>.AddHandler(task, nameof(SetupTask.PropertyChanged), OnTaskPropertyChanged);
        }

        // Use concrete Lists — NOT IEnumerable iterators — to stop allocation loops during WPF layout passes
        DataContext = new
        {
            Col1 = grouped.Where((_, i) => i % 3 == 0).ToList(),
            Col2 = grouped.Where((_, i) => i % 3 == 1).ToList(),
            Col3 = grouped.Where((_, i) => i % 3 == 2).ToList()
        };

        UpdateCount();
    }

    private void OnTaskPropertyChanged(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SetupTask.IsSelected) && s is SetupTask t)
        {
            _dataService.SaveTaskSelection(t);
            UpdateCount();
        }
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

        using var log = new LogService();
        var vm  = new RunningViewModel(selected, log, _dataService);
        NavigationService?.Navigate(new RunningPage(vm));
        await vm.RunAsync();
    }
}

