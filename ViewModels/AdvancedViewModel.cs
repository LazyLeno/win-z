using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using WinZ.Models;
using WinZ.Services;
using System;

namespace WinZ.ViewModels;

public class AdvancedViewModel : INotifyPropertyChanged
{
    public ObservableCollection<CategoryTab> Tabs { get; } = new();

    private CategoryTab? _selectedTab;
    public CategoryTab? SelectedTab
    {
        get => _selectedTab;
        set { _selectedTab = value; OnPropertyChanged(); }
    }

    public IEnumerable<SetupTask> SelectedTasks =>
        Tabs.SelectMany(t => t.SubGroups).SelectMany(g => g.Tasks).Where(task => task.IsSelected);

    public int SelectedCount =>
        Tabs.SelectMany(t => t.SubGroups).SelectMany(g => g.Tasks).Count(task => task.IsSelected);

    public void Load(IEnumerable<SetupTask>? allTasks)
    {
        if (allTasks == null) return;
        
        Tabs.Clear();

        // Advanced mode should start with a clean slate (nothing pre-checked)
        var tasksList = allTasks.ToList();
        foreach (var t in tasksList) t.IsSelected = false;

        var groupedByCategory = tasksList
            .GroupBy(t => t.Category)
            .OrderBy(g => CategoryOrder(g.Key ?? "Other"));

        foreach (var catGroup in groupedByCategory)
        {
            var tab = new CategoryTab(catGroup.Key ?? "Other");
            var subGrouped = catGroup
                .GroupBy(t => t.SubCategory)
                .OrderBy(g => g.Key);

            foreach (var sub in subGrouped)
            {
                var sg = new TaskGroup(sub.Key ?? "Other", sub.ToList());
                sg.SelectionChanged += (_, _) => OnPropertyChanged(nameof(SelectedCount));
                tab.SubGroups.Add(sg);
            }
            Tabs.Add(tab);
        }

        SelectedTab = Tabs.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedCount));
        MemoryService.Optimize();
    }


    private static int CategoryOrder(string cat) => cat switch
    {
        "Web Browsers"     => 0,
        "Download Managers"=> 1,
        "VPN"              => 2,
        "Communication"    => 3,
        "Music"            => 4,
        "File Managers"    => 5,
        "Windows Tweaks"   => 6,
        "Debloat"          => 7,
        _                  => 99
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class CategoryTab : INotifyPropertyChanged
{
    public string Name { get; }
    public ObservableCollection<TaskGroup> SubGroups { get; } = new();

    public IEnumerable<TaskGroup> AllGroups => SubGroups;


    // Split sub-groups into 3 columns for stable masonry-like stacking
    public IEnumerable<TaskGroup> Column1 => SubGroups.Where((_, i) => i >= 0 && i % 3 == 0);
    public IEnumerable<TaskGroup> Column2 => SubGroups.Where((_, i) => i >= 0 && i % 3 == 1);
    public IEnumerable<TaskGroup> Column3 => SubGroups.Where((_, i) => i >= 0 && i % 3 == 2);

    public CategoryTab(string name)
    {
        Name = name;
        SubGroups.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Column1));
            OnPropertyChanged(nameof(Column2));
            OnPropertyChanged(nameof(Column3));
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class TaskGroup : INotifyPropertyChanged
{
    public string GroupName { get; }
    public ObservableCollection<SetupTask> Tasks { get; }

    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public System.Windows.Input.ICommand ToggleExpandCommand { get; }

    public event EventHandler? SelectionChanged;

    public bool AllSelected
    {
        get => Tasks.All(t => t.IsSelected);
        set
        {
            foreach (var t in Tasks) t.IsSelected = value;
            OnPropertyChanged();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public TaskGroup(string name, List<SetupTask> tasks)
    {
        GroupName = name;
        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        Tasks = new ObservableCollection<SetupTask>(tasks ?? new());
        foreach (var t in Tasks)
            t.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SetupTask.IsSelected))
                {
                    OnPropertyChanged(nameof(AllSelected));
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

