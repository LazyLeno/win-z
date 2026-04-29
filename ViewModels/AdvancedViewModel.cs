using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using WinZ.Models;
using WinZ.Services;
using System;
using System.Windows;

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

    private DataService? _dataService;

    public void Load(IEnumerable<SetupTask>? allTasks, DataService dataService)
    {
        if (allTasks == null) return;
        _dataService = dataService;
        
        Tabs.Clear();

        var tasksList = allTasks.ToList();

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
                
                WeakEventManager<TaskGroup, EventArgs>.AddHandler(sg, nameof(TaskGroup.SelectionChanged), (s, e) => {
                    OnPropertyChanged(nameof(SelectedCount));
                });
                
                foreach (var task in sg.Tasks)
                {
                    WeakEventManager<SetupTask, PropertyChangedEventArgs>.AddHandler(task, nameof(SetupTask.PropertyChanged), (s, e) => {
                        if (e.PropertyName == nameof(SetupTask.IsSelected) && s is SetupTask t)
                        {
                            _dataService?.SaveTaskSelection(t);
                        }
                    });
                }

                tab.SubGroups.Add(sg);
            }
            tab.RefreshColumns(); // Stabilize columns
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

    // Use concrete Lists instead of IEnumerables to prevent iterator allocation during UI polling
    private List<TaskGroup> _col1 = new();
    private List<TaskGroup> _col2 = new();
    private List<TaskGroup> _col3 = new();

    public List<TaskGroup> Column1 => _col1;
    public List<TaskGroup> Column2 => _col2;
    public List<TaskGroup> Column3 => _col3;

    public CategoryTab(string name)
    {
        Name = name;
        SubGroups.CollectionChanged += (_, _) => RefreshColumns();
    }

    public void RefreshColumns()
    {
        _col1 = SubGroups.Where((_, i) => i % 3 == 0).ToList();
        _col2 = SubGroups.Where((_, i) => i % 3 == 1).ToList();
        _col3 = SubGroups.Where((_, i) => i % 3 == 2).ToList();
        OnPropertyChanged(nameof(Column1));
        OnPropertyChanged(nameof(Column2));
        OnPropertyChanged(nameof(Column3));
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
        {
            WeakEventManager<SetupTask, PropertyChangedEventArgs>.AddHandler(t, nameof(SetupTask.PropertyChanged), (s, e) => {
                if (e.PropertyName == nameof(SetupTask.IsSelected))
                {
                    OnPropertyChanged(nameof(AllSelected));
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            });
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
