using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WinZ.Services;
using WinZ.Models;

namespace WinZ.Views;

public partial class ReviewDialog : Window
{
    public bool Result { get; private set; }

    public ReviewDialog(List<SetupTask> selectedTasks)
    {
        InitializeComponent();
        
        var format = Application.Current.TryFindResource("L.Rev.Msg")?.ToString() ?? "You are about to apply {0} changes:";
        CountText.Text = string.Format(format, selectedTasks.Count);
        ReviewList.ItemsSource = selectedTasks;
        
        Closed += (s, e) => 
        {
            ReviewList.ItemsSource = null;
            DataContext = null;
            MemoryService.Optimize();
        };
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        Result = true;
        Close();
    }
}
