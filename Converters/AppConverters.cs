using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WinZ.Models;

namespace WinZ.Converters;

// ── CORE OPTIMIZATION: Pre-allocated, frozen, static brushes ─────────────────
// Allocating `new SolidColorBrush()` on every converter call is the #1 cause of
// idle RAM growth in WPF. Every task row rendered triggers Convert() calls.
// With 100+ tasks, this means hundreds of short-lived heap objects per second.
// Frozen brushes are created exactly once, are immutable, and cost zero RAM per render.

internal static class B
{
    internal static SolidColorBrush Make(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

/// TaskStatus → accent SolidColorBrush
public class StatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush _success = B.Make(0x4A, 0xDE, 0x80);
    private static readonly SolidColorBrush _failed  = B.Make(0xF8, 0x71, 0x71);
    private static readonly SolidColorBrush _running = B.Make(0x60, 0xA5, 0xFA);
    private static readonly SolidColorBrush _skipped = B.Make(0x5A, 0x5A, 0x72);
    private static readonly SolidColorBrush _queued  = B.Make(0x2A, 0x2A, 0x32);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is TaskStatus s ? s switch
        {
            TaskStatus.Success => _success,
            TaskStatus.Failed  => _failed,
            TaskStatus.Running => _running,
            TaskStatus.Skipped => _skipped,
            _                  => _queued,
        } : Brushes.Transparent;

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

/// TaskStatus → icon string
public class StatusToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is TaskStatus s ? s switch
        {
            TaskStatus.Success => "✓",
            TaskStatus.Failed  => "✗",
            TaskStatus.Running => "⟳",
            TaskStatus.Skipped => "–",
            _                  => "○",
        } : "";

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

/// bool → opacity
public class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? 1.0 : 0.45;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

/// TaskType → short label string
public class TaskTypeToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is TaskType tt ? tt switch
        {
            TaskType.Install => "INSTALL",
            TaskType.Tweak   => "TWEAK",
            TaskType.Remove  => "REMOVE",
            _                => ""
        } : "";
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

/// TaskType → badge color — pre-frozen static brushes, zero allocation per render
public class TaskTypeToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush _install = B.Make(0x1A, 0x4A, 0x2A);
    private static readonly SolidColorBrush _tweak   = B.Make(0x1A, 0x2A, 0x4A);
    private static readonly SolidColorBrush _remove  = B.Make(0x4A, 0x1A, 0x1A);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is TaskType tt ? tt switch
        {
            TaskType.Install => _install,
            TaskType.Tweak   => _tweak,
            TaskType.Remove  => _remove,
            _                => Brushes.Transparent
        } : Brushes.Transparent;

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

/// bool → Visibility — pre-boxed to avoid enum boxing on every binding evaluation
public class BoolToVisibilityConverter : IValueConverter
{
    private static readonly object _vis = Visibility.Visible;
    private static readonly object _col = Visibility.Collapsed;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool show = value switch
        {
            bool b   => b,
            string s => !string.IsNullOrEmpty(s),
            null     => false,
            _        => true
        };
        return show ? _vis : _col;
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

/// string Icon → Visibility (Visible if not empty)
public class IconUrlToVisibilityConverter : IValueConverter
{
    private static readonly object _vis = Visibility.Visible;
    private static readonly object _col = Visibility.Collapsed;
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? _col : _vis;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

/// string Icon → Visibility (Visible if empty)
public class IconUrlToInverseVisibilityConverter : IValueConverter
{
    private static readonly object _vis = Visibility.Visible;
    private static readonly object _col = Visibility.Collapsed;
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? _vis : _col;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

/// inverted bool → Visibility
public class InverseBoolToVisibilityConverter : IValueConverter
{
    private static readonly object _vis = Visibility.Visible;
    private static readonly object _col = Visibility.Collapsed;
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool hide = value switch
        {
            bool b   => b,
            string s => !string.IsNullOrEmpty(s),
            null     => false,
            _        => true
        };
        return hide ? _col : _vis;
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}
