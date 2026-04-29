using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WinZ.Models;
using WinZ.Services;

namespace WinZ.Engine;

/// <summary>
/// Lean Debloat Engine that uses external shells to maintain a low RAM footprint.
/// </summary>
public class DebloatEngine(LogService log)
{
    public async Task<bool> ApplyAsync(SetupTask task, bool force, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);
        
        if (string.IsNullOrWhiteSpace(task.PackageId))
        {
            log.Error($"No PackageId defined for debloat: {task.Name}");
            return false;
        }

        log.Cmd($"Removing Appx: {task.PackageId}");
        
        // Use external powershell to avoid loading the SDK into our memory space
        return await Task.Run(() => RunPowerShellDebloat(task.PackageId), ct);
    }

    private bool RunPowerShellDebloat(string packageId)
    {
        string script = $"Get-AppxPackage -Name '{packageId}' -AllUsers | Remove-AppxPackage -ErrorAction SilentlyContinue";
        
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{script}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };

        using var p = Process.Start(psi);
        if (p == null) return false;

        string error = p.StandardError.ReadToEnd();
        p.WaitForExit();

        if (p.ExitCode == 0)
        {
            log.Ok($"Removed: {packageId}");
            return true;
        }
        else
        {
            log.Error($"Failed to remove {packageId}: {error}");
            return false;
        }
    }
}
