using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Runtime.CompilerServices;
using WinZ.Models;
using WinZ.Services;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WinZ.ViewModels;

public class AdvancedViewModel : INotifyPropertyChanged
{
    private bool _isActive = true;
    public ObservableCollection<string> Categories { get; } = new();
    
    private string? _selectedCategory;
    public string? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            _selectedCategory = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCategorySelected));
            RefreshAvailableSections();
        }
    }

    public bool IsCategorySelected => !string.IsNullOrEmpty(_selectedCategory);

    private bool _isPortableMode;
    public bool IsPortableMode
    {
        get => _isPortableMode;
        set { _isPortableMode = value; OnPropertyChanged(); }
    }

    private bool _isCommunityEnhancedEnabled;
    public bool IsCommunityEnhancedEnabled
    {
        get => _isCommunityEnhancedEnabled;
        set { _isCommunityEnhancedEnabled = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> Sections { get; } = new();

    public ObservableCollection<InstalledApp> InstalledApps { get; } = new();
    private ICollectionView _installedAppsView;
    public ICollectionView InstalledAppsView => _installedAppsView;

    public ICommand SmartUninstallCommand { get; }
    public ICommand SortCommand { get; }

    private string _sortColumn = "Name";
    public string SortColumn
    {
        get => _sortColumn;
        set { _sortColumn = value; OnPropertyChanged(); }
    }

    private ListSortDirection _sortDirection = ListSortDirection.Ascending;
    public ListSortDirection SortDirection
    {
        get => _sortDirection;
        set { _sortDirection = value; OnPropertyChanged(); }
    }

    private string? _selectedSection;
    public string? SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (value != null && _selectedSection != value)
            {
                _selectedSection = value;
                OnPropertyChanged();
                
                if (value == "Overview") LoadInventory();
                else RefreshDisplayedTabs();
            }
        }
    }

    public ObservableCollection<CategoryTab> Tabs { get; } = new();

    private CategoryTab? _selectedTab;
    public CategoryTab? SelectedTab
    {
        get => _selectedTab;
        set 
        { 
            _selectedTab = value; 
            OnPropertyChanged(); 
            MemoryService.Optimize();
        }
    }

    public System.Windows.Input.ICommand BackCommand { get; }
    public System.Windows.Input.ICommand SelectCategoryCommand { get; }
    public System.Windows.Input.ICommand OpenInstallDirCommand { get; }

    private List<SetupTask> _allTasks = new();
    private List<InstalledApp> _masterInventory = new();
    private DataService? _dataService;
    private ListCollectionView? _selectedTasksView;
    public ListCollectionView? SelectedTasksView => _selectedTasksView;

    public AdvancedViewModel()
    {
        LanguageService.LanguageChanged += RefreshTranslations;
        _installedAppsView = CollectionViewSource.GetDefaultView(InstalledApps);
        _installedAppsView.Filter = FilterInstalledApps;

        BackCommand = new RelayCommand(OnBack);
        SelectCategoryCommand = new RelayCommand<string>(OnSelectCategory);
        SmartUninstallCommand = new RelayCommand<InstalledApp>(OnSmartUninstall);
        OpenInstallDirCommand = new RelayCommand<InstalledApp>(OnOpenInstallDir);
        SortCommand = new RelayCommand<string>(OnSort);

        // Apply default sort (Name, Ascending)
        _installedAppsView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
    }

    private void OnSort(string column)
    {
        if (SortColumn == column)
        {
            SortDirection = SortDirection == ListSortDirection.Ascending 
                ? ListSortDirection.Descending 
                : ListSortDirection.Ascending;
        }
        else
        {
            SortColumn = column;
            SortDirection = ListSortDirection.Ascending;
        }

        _installedAppsView.SortDescriptions.Clear();
        
        // Handle numerical sorting for size
        var sortProp = column switch {
            "Size" => "RawSize",
            _ => column
        };
        
        _installedAppsView.SortDescriptions.Add(new SortDescription(sortProp, SortDirection));
    }

    private bool FilterInstalledApps(object obj)
    {
        if (obj is not InstalledApp app) return false;
        if (string.IsNullOrWhiteSpace(_searchText)) return true;
        
        return app.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) || 
               (app.Publisher?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    private void ApplyFilter()
    {
        if (_selectedSection == "Overview")
        {
            _installedAppsView.Refresh();
        }
        else
        {
            foreach (var tab in Tabs)
            {
                tab.ApplyFilter(_searchText);
            }
        }
    }

    public IEnumerable<SetupTask> AllTasks => _allTasks;

    public IEnumerable<SetupTask> SelectedTasks =>
        _allTasks.Where(task => task.IsSelected);

    private int _selectedCount;
    public int SelectedCount => _selectedCount;

    public void Cleanup()
    {
        _isActive = false;
        LanguageService.LanguageChanged -= RefreshTranslations;
        Tabs.Clear();
    }

    private void RefreshTranslations()
    {
        OnPropertyChanged(nameof(Tabs));
        foreach (var tab in Tabs)
        {
            tab.NotifyRefresh();
        }
    }

    public async void Load(IEnumerable<SetupTask>? allTasks, DataService dataService)
    {
        if (!_isActive || allTasks == null) return;
        _dataService = dataService;
        _allTasks = allTasks.ToList();

        // Detect status for top bar icons
        IsPortableMode = DataService.IsPortableMode;
        IsCommunityEnhancedEnabled = await _dataService.GetSettingAsync("AllowModdedSoftware") == "True";

        Categories.Clear();
        foreach (var cat in _allTasks.Select(t => t.Category).Distinct().OrderBy(c => CategoryOrder(c)))
        {
            Categories.Add(cat);
        }

        foreach (var task in _allTasks)
        {
            task.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SetupTask.IsSelected) && s is SetupTask t)
                {
                    _ = _dataService?.SaveTaskSelectionAsync(t);
                    UpdateSelectedCount();
                    _selectedTasksView?.Refresh();
                }
            };
        }

        // Build the selected-tasks view (homepage summary)
        _selectedTasksView = new ListCollectionView(_allTasks);
        _selectedTasksView.Filter = o => o is SetupTask t && t.IsSelected;
        _selectedTasksView.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
        _selectedTasksView.SortDescriptions.Add(new SortDescription("Category", ListSortDirection.Ascending));
        _selectedTasksView.SortDescriptions.Add(new SortDescription("Section", ListSortDirection.Ascending));
        OnPropertyChanged(nameof(SelectedTasksView));

        SelectedCategory = null; // Start with nothing selected as requested
        UpdateSelectedCount();
    }

    private void RefreshAvailableSections()
    {
        Sections.Clear();
        if (string.IsNullOrEmpty(_selectedCategory)) return;

        if (_selectedCategory == "Software")
        {
            Sections.Add("Overview");
        }

        var sections = _allTasks
            .Where(t => t.Category == _selectedCategory)
            .Select(t => t.Section)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        foreach (var sec in sections) Sections.Add(sec);
        SelectedSection = Sections.FirstOrDefault();
    }

    private void OnBack() => SelectedCategory = null;

    private void OnSelectCategory(string cat) => SelectedCategory = cat;

    private async void LoadInventory()
    {
        Tabs.Clear();
        InstalledApps.Clear();
        
        _masterInventory = await InventoryService.GetInstalledAppsAsync();
        foreach (var app in _masterInventory.OrderBy(a => a.Name)) InstalledApps.Add(app);

        // Start asynchronous size calculation and icon loading in the background
        _ = InventoryService.ComputeSizesAsync(_masterInventory);
        _ = InventoryService.LoadIconsAsync(_masterInventory);
    }

    private void OnOpenInstallDir(InstalledApp? app)
    {
        if (app == null || string.IsNullOrEmpty(app.InstallLocation) || !System.IO.Directory.Exists(app.InstallLocation)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(app.InstallLocation) { UseShellExecute = true }); } catch { }
    }

    private async void OnSmartUninstall(InstalledApp app)
    {
        if (app == null || string.IsNullOrEmpty(app.UninstallString)) return;
        
        var owner = System.Windows.Application.Current.MainWindow;
        bool doCleanup = Views.WinZDialog.Show(owner, "L.Advanced.SmartUninstall.Title", 
            string.Format((string)System.Windows.Application.Current.FindResource("L.Advanced.SmartUninstall.Msg"), app.Name), 
            "L.Advanced.SmartUninstall.Confirm", "L.Advanced.SmartUninstall.Cancel", (System.Windows.Media.Geometry)System.Windows.Application.Current.FindResource("Icon.Trash"));

        try
        {
            var raw = app.UninstallString.Trim();
            string fileName;
            string arguments = "";

            if (raw.StartsWith("\""))
            {
                var secondQuote = raw.IndexOf("\"", 1);
                if (secondQuote > 0)
                {
                    fileName = raw.Substring(1, secondQuote - 1);
                    arguments = raw.Substring(secondQuote + 1).Trim();
                }
                else fileName = raw.Trim('\"');
            }
            else
            {
                var space = raw.IndexOf(" ");
                if (space > 0 && !File.Exists(raw))
                {
                    fileName = raw.Substring(0, space);
                    arguments = raw.Substring(space + 1).Trim();
                }
                else fileName = raw;
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            };

            var proc = System.Diagnostics.Process.Start(psi);

            // Immediate disappearance from the list
            InstalledApps.Remove(app);

            if (doCleanup && proc != null)
            {
                await proc.WaitForExitAsync();
                await Task.Delay(1500); // Wait for MSI service to settle
                new Views.CleanupWindow(app).Show();
            }
        }
        catch (Exception ex)
        {
            Views.WinZDialog.Show(owner, "WinZ Error", $"Failed to launch uninstaller: {ex.Message}", "OK", "");
        }
    }

    private void RefreshDisplayedTabs()
    {
        Tabs.Clear();
        if (string.IsNullOrEmpty(_selectedCategory) || string.IsNullOrEmpty(_selectedSection)) return;

        var tasksInSection = _allTasks
            .Where(t => t.Category == _selectedCategory && t.Section == _selectedSection)
            .ToList();

        // Create a single tab for the current section to hold all its sub-categories (cards)
        var sectionTab = new CategoryTab(_selectedSection);

        var groupedBySubCat = tasksInSection
            .GroupBy(t => t.SubCategory)
            .OrderBy(g => g.Key);

        foreach (var subGroup in groupedBySubCat)
        {
            var sg = new TaskGroup(subGroup.Key, subGroup.ToList());
            
            WeakEventManager<TaskGroup, EventArgs>.AddHandler(sg, nameof(TaskGroup.SelectionChanged), (s, e) => UpdateSelectedCount());
            
            sectionTab.SubGroups.Add(sg);
        }

        sectionTab.RefreshColumns();
        Tabs.Add(sectionTab);
        SelectedTab = sectionTab;
    }

    private void UpdateSelectedCount()
    {
        _selectedCount = _allTasks.Count(task => task.IsSelected);
        OnPropertyChanged(nameof(SelectedCount));
    }

    private static int CategoryOrder(string cat) => cat switch
    {
        "Web Browsers"     => 0,
        "Download Managers"=> 1,
        "Security"         => 2,
        "Communication"    => 3,
        "Productivity"     => 4,
        "Music"            => 5,
        "Media"            => 6,
        "Creativity"       => 7,
        "Gaming"           => 8,
        "Development"      => 9,
        "Utilities"        => 10,
        "File Managers"    => 11,
        "Hardware"         => 12,
        "System Tweaks"    => 13,
        "Privacy"          => 14,
        "Debloat"          => 15,
        _                  => 99
    };

    // ── .winz binary format ──────────────────────────────────────────────
    // Offset  Size  Value
    //   0      4    Magic: 0x57 0x49 0x4E 0x5A ("WINZ")
    //   4      2    Version: 0x01 0x00
    //   6      8    Creation timestamp (Unix ms, Int64 little-endian)
    //  14      *    GZip-compressed UTF-8 JSON array of selected task IDs
    // ─────────────────────────────────────────────────────────────────────

    private static readonly byte[] WinzMagic   = { 0x57, 0x49, 0x4E, 0x5A }; // "WINZ"
    private static readonly byte[] WinzVersion = { 0x01, 0x00 };

    public byte[] ExportSelectionBytes()
    {
        var ids     = AllTasks.Where(t => t.IsSelected).Select(t => t.Id).ToList();
        var json    = System.Text.Json.JsonSerializer.Serialize(ids);
        var payload = System.Text.Encoding.UTF8.GetBytes(json);

        using var ms  = new System.IO.MemoryStream();
        using var gz  = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.SmallestSize);
        gz.Write(payload, 0, payload.Length);
        gz.Flush();
        var compressed = ms.ToArray();

        using var out_ = new System.IO.MemoryStream();
        out_.Write(WinzMagic,   0, 4);
        out_.Write(WinzVersion, 0, 2);
        var ts = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        out_.Write(ts, 0, 8);
        out_.Write(compressed, 0, compressed.Length);
        return out_.ToArray();
    }

    public void ImportSelectionBytes(byte[] data)
    {
        try
        {
            // Validate magic
            if (data.Length < 14 ||
                data[0] != 0x57 || data[1] != 0x49 ||
                data[2] != 0x4E || data[3] != 0x5A)
                return;

            // Decompress payload (skip 14-byte header)
            using var compressed = new System.IO.MemoryStream(data, 14, data.Length - 14);
            using var gz         = new System.IO.Compression.GZipStream(compressed, System.IO.Compression.CompressionMode.Decompress);
            using var raw        = new System.IO.MemoryStream();
            gz.CopyTo(raw);

            var json = System.Text.Encoding.UTF8.GetString(raw.ToArray());
            var ids  = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
            if (ids == null) return;

            foreach (var task in AllTasks)
                task.IsSelected = ids.Contains(task.Id);

            UpdateSelectedCount();
        }
        catch { }
    }

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
        ApplyFilter("");
    }

    public void ApplyFilter(string query)
    {
        foreach (var sg in SubGroups) sg.ApplyFilter(query);

        var visibleGroups = SubGroups.Where(sg => sg.IsVisible).ToList();
        _col1 = visibleGroups.Where((_, i) => i % 3 == 0).ToList();
        _col2 = visibleGroups.Where((_, i) => i % 3 == 1).ToList();
        _col3 = visibleGroups.Where((_, i) => i % 3 == 2).ToList();
        OnPropertyChanged(nameof(Column1));
        OnPropertyChanged(nameof(Column2));
        OnPropertyChanged(nameof(Column3));
    }

    public void NotifyRefresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Column1));
        OnPropertyChanged(nameof(Column2));
        OnPropertyChanged(nameof(Column3));
        foreach (var sg in SubGroups) sg.NotifyRefresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class TaskGroup : INotifyPropertyChanged
{
    public string GroupName { get; }
    public List<SetupTask> AllTasks { get; }
    public ObservableCollection<SetupTask> Tasks { get; } = new();

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set { _isVisible = value; OnPropertyChanged(); }
    }

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
        AllTasks = tasks ?? new();
        ApplyFilter("");
        foreach (var t in AllTasks)
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

    public void ApplyFilter(string query)
    {
        IEnumerable<SetupTask> filtered = string.IsNullOrWhiteSpace(query)
            ? AllTasks
            : AllTasks.Where(t =>
                (t.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (t.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));

        var target = filtered.ToList();

        // Batch remove items no longer in filter (reverse order to avoid index shift)
        for (int i = Tasks.Count - 1; i >= 0; i--)
            if (!target.Contains(Tasks[i])) Tasks.RemoveAt(i);

        // Batch add new items at correct positions
        for (int i = 0; i < target.Count; i++)
            if (i >= Tasks.Count || !ReferenceEquals(Tasks[i], target[i])) Tasks.Insert(i, target[i]);

        IsVisible = Tasks.Count > 0;
    }

    public void NotifyRefresh()
    {
        OnPropertyChanged(nameof(GroupName));
        OnPropertyChanged(nameof(AllSelected));
        OnPropertyChanged(nameof(Tasks));
        foreach (var t in Tasks) t.NotifyRefresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class SortDirToAngleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is ListSortDirection dir && dir == ListSortDirection.Descending)
            return 180.0;
        return 0.0;
    }
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}

public class SortActiveToVisConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] != null && values[1] != null && values[0].ToString() == values[1].ToString())
            return Visibility.Visible;
        return Visibility.Collapsed;
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}

public class SortActiveToBrushConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] != null && values[1] != null && values[0].ToString() == values[1].ToString())
            return new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)); // Subtle 7% white
        return Brushes.Transparent;
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}
