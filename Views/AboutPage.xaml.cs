using System.Windows;
using System.Windows.Controls;

namespace WinZ.Views;

public partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mw)
            mw.NavigateBackToAdvanced();
    }
}
