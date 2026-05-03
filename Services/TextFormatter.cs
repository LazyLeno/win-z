using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Controls;

namespace WinZ.Services;

public static class TextFormatter
{
    public static readonly DependencyProperty FormattedTextProperty =
        DependencyProperty.RegisterAttached("FormattedText", typeof(string), typeof(TextFormatter),
            new PropertyMetadata(null, OnFormattedTextChanged));

    public static string GetFormattedText(DependencyObject obj) => (string)obj.GetValue(FormattedTextProperty);
    public static void SetFormattedText(DependencyObject obj, string value) => obj.SetValue(FormattedTextProperty, value);

    private static void OnFormattedTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock textBlock)
        {
            var text = e.NewValue as string ?? "";
            
            // Resolve localization if it's a key
            if (text.StartsWith("L.") || (text.Length < 64 && Application.Current.TryFindResource(text) != null))
            {
                var res = Application.Current.TryFindResource(text) ?? Application.Current.TryFindResource("L." + text);
                if (res != null) text = res.ToString() ?? text;
            }

            ApplyFormattedText(textBlock, text);
        }
    }

    public static void ApplyFormattedText(TextBlock textBlock, string text)
    {
        textBlock.Inlines.Clear();
        if (string.IsNullOrEmpty(text)) return;

        // Standardize newlines
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        // Colors
        var accentBrush = (Brush)Application.Current.FindResource("AccentBrush");
        
        // Split by bold tags, highlight tags, and newlines
        // We use capturing groups so the tags/newlines are included in the split parts
        var parts = Regex.Split(text, @"(\*\*.*?\*\*|\[.*?\]|\n)");
        
        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part)) continue;

            if (part == "\n")
            {
                textBlock.Inlines.Add(new LineBreak());
            }
            else if (part.StartsWith("**") && part.EndsWith("**") && part.Length >= 4)
            {
                textBlock.Inlines.Add(new Run(part.Substring(2, part.Length - 4)) 
                { 
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                });
            }
            else if (part.StartsWith("[") && part.EndsWith("]") && part.Length >= 2)
            {
                textBlock.Inlines.Add(new Run(part.Substring(1, part.Length - 2)) 
                { 
                    Foreground = accentBrush,
                    FontWeight = FontWeights.SemiBold
                });
            }
            else
            {
                // Check if this part contains a bullet point (possibly with leading spaces)
                // If it does, we split it further or just handle the bullet
                if (part.Contains("•"))
                {
                    var bulletParts = Regex.Split(part, @"(\s*•)");
                    foreach (var bPart in bulletParts)
                    {
                        if (string.IsNullOrEmpty(bPart)) continue;
                        if (bPart.Trim() == "•")
                        {
                            textBlock.Inlines.Add(new Run("    •  ") { Foreground = accentBrush, FontWeight = FontWeights.Bold });
                        }
                        else
                        {
                            textBlock.Inlines.Add(new Run(bPart));
                        }
                    }
                }
                else
                {
                    textBlock.Inlines.Add(new Run(part));
                }
            }
        }
    }
}



