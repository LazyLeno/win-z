using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinZ.Models;

namespace WinZ.Converters;

/// TaskStatus → accent SolidColorBrush
public class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is not TaskStatus s) return Brushes.Transparent;
        return s switch
        {
            TaskStatus.Success => new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80)),
            TaskStatus.Failed  => new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71)),
            TaskStatus.Running => new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)),
            TaskStatus.Skipped => new SolidColorBrush(Color.FromRgb(0x5A, 0x5A, 0x72)),
            _                  => new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x32)),
        };
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

/// TaskStatus → icon string
public class StatusToIconConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is not TaskStatus s) return "";
        return s switch
        {
            TaskStatus.Success => "✓",
            TaskStatus.Failed  => "✗",
            TaskStatus.Running => "⟳",
            TaskStatus.Skipped => "–",
            _                  => "○",
        };
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

/// bool IsSelected → strikethrough opacity (0.45 or 1.0)
public class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is bool b && b ? 1.0 : 0.45;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

/// TaskType → short label string
public class TaskTypeToLabelConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is not TaskType tt) return "";
        return tt switch
        {
            TaskType.Install => "INSTALL",
            TaskType.Tweak   => "TWEAK",
            TaskType.Remove  => "REMOVE",
            _                => ""
        };
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

/// TaskType → badge color
public class TaskTypeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is not TaskType tt) return Brushes.Transparent;
        return tt switch
        {
            TaskType.Install => new SolidColorBrush(Color.FromRgb(0x1A, 0x4A, 0x2A)),
            TaskType.Tweak   => new SolidColorBrush(Color.FromRgb(0x1A, 0x2A, 0x4A)),
            TaskType.Remove  => new SolidColorBrush(Color.FromRgb(0x4A, 0x1A, 0x1A)),
            _                => Brushes.Transparent
        };
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

/// bool or non-null string → Visibility
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        bool visible = value switch
        {
            bool b   => b,
            string s => !string.IsNullOrEmpty(s),
            null     => false,
            _        => value != null
        };
        return visible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

/// string IconUrl -> Visibility (Visible if not null/empty)
public class IconUrlToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => string.IsNullOrEmpty(value as string) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

/// string IconUrl -> Visibility (Visible if null/empty)
public class IconUrlToInverseVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => string.IsNullOrEmpty(value as string) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}


/// inverted bool → Visibility
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        bool hide = value switch
        {
            bool b   => b,
            string s => !string.IsNullOrEmpty(s),
            null     => false,
            _        => value != null
        };
        return hide ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}
