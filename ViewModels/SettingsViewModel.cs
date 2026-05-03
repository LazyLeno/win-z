using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using WinZ.Models;
using WinZ.Services;

namespace WinZ.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly DataService _dataService;
    private bool _isActive = true;
    public ObservableCollection<CategoryTab> Tabs { get; } = new();

    private CategoryTab? _selectedTab;
    public CategoryTab? SelectedTab
    {
        get => _selectedTab;
        set { _selectedTab = value; OnPropertyChanged(); }
    }

    public List<KeyValuePair<string, string>> AvailableLanguages => LanguageService.AvailableLanguages.ToList();

    public KeyValuePair<string, string> SelectedLanguage
    {
        get => AvailableLanguages.FirstOrDefault(x => x.Key == LanguageService.CurrentLanguage);
        set
        {
            if (value.Key != LanguageService.CurrentLanguage)
            {
                LanguageService.SetLanguage(value.Key, true);
                OnPropertyChanged();
            }
        }
    }

    public SettingsViewModel()
    {
        _dataService = DataService.Instance;
        LanguageService.LanguageChanged += LoadSettings;
        LoadSettings();
    }

    public void Cleanup()
    {
        _isActive = false;
        LanguageService.LanguageChanged -= LoadSettings;
        Tabs.Clear();
    }

    private async void LoadSettings()
    {
        if (!_isActive) return;

        var currentTabName = SelectedTab?.Name;
        Tabs.Clear();

        // Special category
        var specialTab = new CategoryTab("Special");
        
        var allowModded = await _dataService.GetSettingAsync("AllowModdedSoftware") == "True";

        var moddedTask = new SetupTask 
        { 
            Id = "set_allow_modded",
            Name = "Allow modded software", 
            Description = "Enables alternative, community-modded versions of certain software applications.",
            IsSelected = allowModded
        };

        moddedTask.PropertyChanged += (s, e) => {
            if (e.PropertyName == nameof(SetupTask.IsSelected))
            {
                _ = _dataService.SaveSettingAsync("AllowModdedSoftware", moddedTask.IsSelected.ToString());
            }
        };

        specialTab.SubGroups.Add(new TaskGroup("Safety & Content", new List<SetupTask> { moddedTask }));
        specialTab.RefreshColumns();
        Tabs.Add(specialTab);

        // Safety category
        var safetyTab = new CategoryTab("Safety");
        var promptRestore = await _dataService.GetSettingAsync("PromptRestorePoint") != "False";

        var restoreTask = new SetupTask 
        { 
            Id = "set_prompt_restore",
            Name = "Always prompt for restore point", 
            Description = "Asks to create a system restore point before starting any installations or tweaks for maximum safety.",
            IsSelected = promptRestore
        };

        restoreTask.PropertyChanged += (s, e) => {
            if (e.PropertyName == nameof(SetupTask.IsSelected))
            {
                _ = _dataService.SaveSettingAsync("PromptRestorePoint", restoreTask.IsSelected.ToString());
            }
        };

        // Checksum verification
        var verifyChecksums = await _dataService.GetSettingAsync("VerifyChecksums") != "False";
        var checksumTask = new SetupTask
        {
            Id = "set_verify_checksums",
            Name = "Verify download checksums",
            Description = "Computes and validates SHA-256 hashes on all direct-download installers to detect tampering before execution.",
            IsSelected = verifyChecksums
        };
        checksumTask.PropertyChanged += (s, e) => {
            if (e.PropertyName == nameof(SetupTask.IsSelected))
                _ = _dataService.SaveSettingAsync("VerifyChecksums", checksumTask.IsSelected.ToString());
        };

        // Constrained Language Mode
        var clmEnabled = await _dataService.GetSettingAsync("ConstrainedLanguageMode") == "True";
        var clmTask = new SetupTask
        {
            Id = "set_clm_mode",
            Name = "Isolated PowerShell execution",
            Description = "Runs all tweak scripts in PowerShell Constrained Language Mode. Hardens execution but may break scripts that use .NET types or COM objects.",
            IsSelected = clmEnabled
        };
        clmTask.PropertyChanged += (s, e) => {
            if (e.PropertyName == nameof(SetupTask.IsSelected))
                _ = _dataService.SaveSettingAsync("ConstrainedLanguageMode", clmTask.IsSelected.ToString());
        };

        // Portable Mode (informational — actual switch requires restart)
        var portableTask = new SetupTask
        {
            Id = "set_portable_mode",
            Name = "Portable mode (memory-only)",
            Description = "Keeps all data in RAM — no database files are written to disk. Ideal for USB use. Requires restart; all session data is lost on exit.",
            IsSelected = DataService.IsPortableMode
        };
        portableTask.PropertyChanged += (s, e) => {
            if (e.PropertyName == nameof(SetupTask.IsSelected))
            {
                // Actual mode switch requires app restart; we just persist the flag via the sentinel file approach.
                // The UI code-behind handles the confirmation dialog before this fires.
            }
        };

        safetyTab.SubGroups.Add(new TaskGroup("System Protection", new List<SetupTask> { restoreTask }));
        safetyTab.SubGroups.Add(new TaskGroup("Execution Security", new List<SetupTask> { checksumTask, clmTask }));
        safetyTab.SubGroups.Add(new TaskGroup("Portability", new List<SetupTask> { portableTask }));
        safetyTab.RefreshColumns();
        Tabs.Add(safetyTab);

        // Automation category
        var automationTab = new CategoryTab("Automation");
        var autoCleanup = await _dataService.GetSettingAsync("AutoCleanup") == "True";
        var autoUpdate = await _dataService.GetSettingAsync("AutoUpdateRepos") == "True";

        var cleanupTask = new SetupTask 
        { 
            Id = "set_auto_cleanup",
            Name = "Post-setup auto cleanup", 
            Description = "Clears Windows Temp and package manager caches after a successful run.",
            IsSelected = autoCleanup
        };
        cleanupTask.PropertyChanged += (s, e) => {
            if (e.PropertyName == nameof(SetupTask.IsSelected))
                _ = _dataService.SaveSettingAsync("AutoCleanup", cleanupTask.IsSelected.ToString());
        };

        var updateTask = new SetupTask 
        { 
            Id = "set_auto_update",
            Name = "Background repo updates", 
            Description = "Automatically updates Winget and Scoop repositories on application launch.",
            IsSelected = autoUpdate
        };
        updateTask.PropertyChanged += (s, e) => {
            if (e.PropertyName == nameof(SetupTask.IsSelected))
                _ = _dataService.SaveSettingAsync("AutoUpdateRepos", updateTask.IsSelected.ToString());
        };

        automationTab.SubGroups.Add(new TaskGroup("Task Automation", new List<SetupTask> { cleanupTask, updateTask }));
        automationTab.RefreshColumns();
        Tabs.Add(automationTab);

        // General category
        var generalTab = new CategoryTab("General");
        generalTab.RefreshColumns();
        Tabs.Add(generalTab);

        if (!string.IsNullOrEmpty(currentTabName))
            SelectedTab = Tabs.FirstOrDefault(t => t.Name == currentTabName) ?? Tabs.FirstOrDefault();
        else
            SelectedTab = Tabs.FirstOrDefault();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
