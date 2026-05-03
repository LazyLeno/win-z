using System;
using System.Diagnostics;
using WinZ.Models;
using WinZ.Services;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace WinZ.Engine;

public class ScoopInstaller(LogService log)
{
    public Task<bool> InstallAsync(SetupTask task, CancellationToken ct) => InstallAsync(task, false, ct);

    public async Task<bool> InstallAsync(SetupTask task, bool force, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);
        
        if (string.IsNullOrEmpty(task.EffectivePackageId))
        {
            log.Error(string.Format("No PackageId for Scoop: {0}", task.DisplayName));
            return false;
        }

        // Check if we need to add a bucket (e.g. extras/pear-desktop)
        if (task.EffectivePackageId.Contains('/') && !task.EffectivePackageId.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            string bucket = task.EffectivePackageId.Split('/')[0];
            await EnsureBucketAsync(bucket, ct);
        }

        // Remove force flag as Scoop was rejecting it; we'll rely on the HardCleanup in UninstallAsync
        log.Cmd(string.Format("scoop install {0}", task.EffectivePackageId));

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = string.Format("-NoProfile -ExecutionPolicy Bypass -Command \"scoop install {0}\"", task.EffectivePackageId),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

        p.OutputDataReceived += (_, e) => { if (e.Data is not null) log.Info(e.Data); };
        p.ErrorDataReceived  += (_, e) => { if (e.Data is not null) log.Error(e.Data); };

        if (!p.Start()) return false;
        
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        await p.WaitForExitAsync(ct);

        if (p.ExitCode == 0)
        {
            log.Ok(string.Format("{0} installed via Scoop.", task.DisplayName));
            return true;
        }

        log.Error(string.Format("{0} Scoop install failed (Code {1})", task.DisplayName, p.ExitCode));
        return false;
    }

    public async Task<bool> UninstallAsync(SetupTask task, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (string.IsNullOrEmpty(task.EffectivePackageId)) return false;

        log.Info(string.Format("Uninstalling {0} via Scoop...", task.DisplayName));
        string packageName = GetPackageName(task.EffectivePackageId);
        
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = string.Format("-NoProfile -Command \"scoop uninstall {0}\"", packageName),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) log.Info(e.Data); };
        p.ErrorDataReceived  += (_, e) => { if (e.Data is not null) log.Error(e.Data); };

        if (!p.Start()) return false;
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        await p.WaitForExitAsync(ct);
        
        if (p.ExitCode != 0)
        {
            log.Info(string.Format("Standard Scoop uninstall failed for {0}. Attempting hard cleanup...", task.DisplayName));
            await HardCleanupAsync(packageName, ct);
        }

        return true; // We return true to allow the install step to proceed even if cleanup was needed
    }

    private async Task HardCleanupAsync(string packageName, CancellationToken ct)
    {
        // Manually remove the app folder and shims if Scoop is stuck
        string cleanupScript = string.Format(
            "$appPath = Join-Path $env:USERPROFILE 'scoop/apps/{0}'; " +
            "if (Test-Path $appPath) {{ Remove-Item -Recurse -Force $appPath; Write-Host 'Removed app folder.' }}; " +
            "$shimPath = Join-Path $env:USERPROFILE 'scoop/shims/{0}.*'; " +
            "if (Test-Path $shimPath) {{ Remove-Item -Force $shimPath; Write-Host 'Removed shims.' }}", 
            packageName);

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = string.Format("-NoProfile -Command \"{0}\"", cleanupScript),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi);
        if (p != null)
        {
            p.OutputDataReceived += (_, e) => { if (e.Data is not null) log.Info(e.Data); };
            p.BeginOutputReadLine();
            await p.WaitForExitAsync(ct);
        }
    }

    private async Task EnsureBucketAsync(string bucket, CancellationToken ct)
    {
        log.Info(string.Format("Checking Scoop bucket: {0}", bucket));
        
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = string.Format("-NoProfile -Command \"scoop bucket add {0}\"", bucket),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi);
        if (p != null) await p.WaitForExitAsync(ct);
    }

    public static async Task<bool> IsAvailableAsync()
    {
        try
        {
            var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -Command \"scoop --version\"")
            {
                RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            await p.WaitForExitAsync();
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    private string GetPackageName(string packageId)
    {
        if (packageId.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(packageId);
                return System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
            }
            catch { return packageId; }
        }

        if (packageId.Contains('/') && !packageId.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return packageId.Split('/').Last();
        }

        return packageId;
    }
}
