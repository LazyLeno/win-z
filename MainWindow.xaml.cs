using System.Windows;
using WinZ.Views;

namespace WinZ;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        MainFrame.Navigated += (s, e) => Services.MemoryService.Optimize();
    }
}
