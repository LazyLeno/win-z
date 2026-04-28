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
        var proc = new ProcessStartInfo
        {
            UseShellExecute = true,
            WorkingDirectory = Environment.CurrentDirectory,
            FileName = Environment.ProcessPath!,
            Verb = "runas"
        };
        Process.Start(proc);
        Application.Current.Shutdown();
    }
}
