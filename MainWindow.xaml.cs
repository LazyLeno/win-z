using System.Windows;
using WinZ.Views;

namespace WinZ;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => MainFrame.Navigate(new HomePage());
    }
}
