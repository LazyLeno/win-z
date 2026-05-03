using System.Windows;
using System.Windows.Media;
using WinZ.Services;

namespace WinZ.Views;

public partial class WinZDialog : Window
{
    public bool Result { get; private set; }

    public WinZDialog(string title, string message, string primaryText = "Continue", string secondaryText = "Cancel", Geometry? icon = null)
    {
        InitializeComponent();
        TitleLabel.Text = TryLocalize(title);
        
        var localizedMessage = TryLocalize(message);
        TextFormatter.ApplyFormattedText(MessageLabel, localizedMessage);
        
        PrimaryBtn.Content = TryLocalize(primaryText);
        SecondaryBtn.Content = TryLocalize(secondaryText);
        IconPath.Data = icon ?? (Geometry)FindResource("Icon.Info");
        
        Closed += (s, e) => MemoryService.Optimize();
    }

    private string TryLocalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        
        // Check if it's a key
        var res = Application.Current.TryFindResource(text);
        if (res != null) return res.ToString() ?? text;
        
        // Auto-prefix if it looks like a key but missing 'L.'
        if (!text.StartsWith("L."))
        {
            res = Application.Current.TryFindResource("L." + text);
            if (res != null) return res.ToString() ?? text;
        }
        
        return text;
    }


    private void Primary_Click(object sender, RoutedEventArgs e)
    {
        Result = true;
        Close();
    }

    private void Secondary_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }

    public static bool Show(Window owner, string title, string message, string primary = "L.Global.OK", string secondary = "L.Global.Cancel", Geometry? icon = null)
    {
        var diag = new WinZDialog(title, message, primary, secondary, icon) { Owner = owner };
        diag.ShowDialog();
        return diag.Result;
    }
}


