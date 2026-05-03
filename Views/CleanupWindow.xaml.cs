using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WinZ.Models;
using WinZ.Services;

namespace WinZ.Views;

public partial class CleanupWindow : Window
{
    private readonly InstalledApp _app;
    private List<CleanupItem> _items = new();

    public CleanupWindow(InstalledApp app)
    {
        InitializeComponent();
        _app = app;
        StartScan();
    }

    private async void StartScan()
    {
        ProgressView.Visibility = Visibility.Visible;
        SummaryView.Visibility = Visibility.Collapsed;
        
        // Let uninstaller finish a bit more if it just closed
        await Task.Delay(1000); 
        
        _items = await Task.Run(() => CleanupService.FindLeftovers(_app));
        
        if (_items.Count == 0)
        {
            ProgressStatus.Text = (string)Application.Current.FindResource("L.Cleanup.NoLeftovers");
            await Task.Delay(1500);
            this.Close();
            return;
        }

        ProgressView.Visibility = Visibility.Collapsed;
        SummaryView.Visibility = Visibility.Visible;
        CleanBtn.Visibility = Visibility.Visible;
        SubtitleTxt.Text = string.Format((string)Application.Current.FindResource("L.Cleanup.Found"), _items.Count, _app.Name);
        LeftoversList.ItemsSource = _items;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => this.Close();

    private async void Clean_Click(object sender, RoutedEventArgs e)
    {
        SummaryView.Visibility = Visibility.Collapsed;
        ProgressView.Visibility = Visibility.Visible;
        CleanBtn.Visibility = Visibility.Collapsed;
        CancelBtn.IsEnabled = false;
        
        CleanupProgress.IsIndeterminate = false;
        CleanupProgress.Maximum = _items.Count(i => i.IsChecked);
        CleanupProgress.Value = 0;
        
        var toClean = _items.Where(i => i.IsChecked).ToList();
        for (int i = 0; i < toClean.Count; i++)
        {
            ProgressStatus.Text = string.Format((string)Application.Current.FindResource("L.Cleanup.Deleting"), System.IO.Path.GetFileName(toClean[i].Path));
            await Task.Run(() => CleanupService.Delete(toClean[i]));
            CleanupProgress.Value = i + 1;
            await Task.Delay(50); // Visual feedback
        }

        ProgressStatus.Text = (string)Application.Current.FindResource("L.Cleanup.Complete");
        await Task.Delay(1000);
        this.Close();
    }
}
