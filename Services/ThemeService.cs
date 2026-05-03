using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WinZ.Services;

public static class ThemeService
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void ApplyDarkTitleBar(Window window)
    {
        if (window == null) return;

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            window.SourceInitialized += (s, e) => ApplyDarkTitleBar(window);
            return;
        }

        int useDarkMode = 1;
        // 20: Windows 10 build 18985+, Windows 11
        // 19: Windows 10 build 17763 to 18985
        DwmSetWindowAttribute(hwnd, 20, ref useDarkMode, sizeof(int));
        DwmSetWindowAttribute(hwnd, 19, ref useDarkMode, sizeof(int));
    }
}
