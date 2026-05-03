using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace WinZ.Services;

public static class SafetyService
{
    public static async Task<bool> CreateRestorePointAsync(string description)
    {
        return await Task.Run(() =>
        {
            try
            {
                // We use PowerShell to create the restore point. 
                // It requires admin privileges, which the app already has.
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -WindowStyle Hidden -Command \"Checkpoint-Computer -Description '{description}' -RestorePointType APPLICATION_INSTALL\"",
                    UseShellExecute = true,
                    Verb = "runas"
                };

                using var process = Process.Start(startInfo);
                if (process == null) return false;
                
                // We don't want to block the UI forever, but restore points can take 30-60s.
                process.WaitForExit(60000); 
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        });
    }
}
