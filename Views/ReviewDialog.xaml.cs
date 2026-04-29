using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WinZ.Models;

namespace WinZ.Views;

public partial class ReviewDialog : Window
{
    public bool Result { get; private set; }

    public ReviewDialog(List<SetupTask> selectedTasks)
    {
        InitializeComponent();
        
        CountText.Text = $"You are about to apply {selectedTasks.Count} changes:";
        ReviewList.ItemsSource = selectedTasks;
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
