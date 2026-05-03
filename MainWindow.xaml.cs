using System;
using System.Windows;
using WinZ.Services;
using WinZ.ViewModels;
using WinZ.Views;

namespace WinZ;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ThemeService.ApplyDarkTitleBar(this);
        MemoryService.Optimize();

        // Boot directly into AdvancedPage — no HomePage
        Loaded += async (_, _) =>
        {
            try
            {
                var ds = DataService.Instance;
                var tasks = await ds.LoadTasksAsync();
                var vm = new AdvancedViewModel();
                vm.Load(tasks, ds);
                MainFrame.Navigate(new AdvancedPage(vm, ds));
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException != null ? $"{ex.Message}\n\nInner Error: {ex.InnerException.Message}" : ex.Message;
                MessageBox.Show(
                    $"Failed to load master config:\n{msg}",
                    "WinZ \u2013 Startup Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
    }

    /// <summary>
    /// Called by AdvancedPage buttons to navigate to Settings or About.
    /// </summary>
    public void NavigateSettings() =>
        MainFrame.Navigate(new SettingsPage());

    public void NavigateAbout() =>
        MainFrame.Navigate(new AboutPage());

    /// <summary>
    /// Called by Settings/About pages to return to a fresh AdvancedPage.
    /// </summary>
    public async void NavigateBackToAdvanced()
    {
        // Optimization: If we already have the AdvancedPage in memory or can just reset it, do that.
        // For now, navigating to a new instance is fine if the Load is fast.
        // We'll keep the tasks in memory to avoid hitting the DB again.
        try
        {
            var ds = DataService.Instance;
            
            // Re-use current viewmodel if possible to avoid 2s DB reload
            if (MainFrame.Content is AdvancedPage ap && ap.DataContext is AdvancedViewModel existingVm)
            {
                foreach (var t in existingVm.AllTasks) t.IsSelected = false;
                return;
            }

            var tasks = await ds.LoadTasksAsync();
            var vm = new AdvancedViewModel();
            vm.Load(tasks, ds);
            MainFrame.Navigate(new AdvancedPage(vm, ds));
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format("Failed to reload:\n{0}", ex.Message),
                "WinZ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MainFrame_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
    {
        // Aggressively clear the navigation history to prevent memory leaks from old pages
        while (MainFrame.CanGoBack)
            MainFrame.RemoveBackEntry();

        Services.MemoryService.DeepOptimize();
    }

    protected override void OnClosed(EventArgs e)
    {
        App.GlobalCts.Cancel();
        base.OnClosed(e);
    }
}
