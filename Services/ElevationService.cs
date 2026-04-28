using System.Diagnostics;
using System.Security.Principal;
using System;
using System.Windows;

namespace WinZ.Services;

public static class ElevationService
{
    public static bool IsElevated =>
        new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);

    public static void RelaunchAsAdmin()
    {
        string? path = Environment.ProcessPath;
        if (string.IsNullOrEmpty(path)) return;

        var proc = new ProcessStartInfo
        {
            UseShellExecute = true,
            WorkingDirectory = Environment.CurrentDirectory,
            FileName = path,
            Verb = "runas"
        };
        
        try
        {
            Process.Start(proc);
            Application.Current.Shutdown();
        }
        catch (Exception)
        {
            // If the user cancels the UAC prompt, we stay open
        }
    }
}

