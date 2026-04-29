using System;
using System.Diagnostics;
using WinZ.Models;
using WinZ.Services;
using System.Threading;
using System.Threading.Tasks;

namespace WinZ.Engine;

public class ScoopInstaller(LogService log)
{
    public async Task<bool> InstallAsync(SetupTask task, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);
        
        if (string.IsNullOrEmpty(task.PackageId))
        {
            log.Error(string.Format("No PackageId for Scoop: {0}", task.Name));
            return false;
        }

        // Check if we need to add a bucket (e.g. extras/pear-desktop)
        if (task.PackageId.Contains('/'))
        {
            string bucket = task.PackageId.Split('/')[0];
            await EnsureBucketAsync(bucket, ct);
        }

        log.Cmd(string.Format("scoop install {0}", task.PackageId));

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = string.Format("-NoProfile -ExecutionPolicy Bypass -Command \"scoop install {0}\"", task.PackageId),
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
            log.Ok(string.Format("{0} installed via Scoop.", task.Name));
            return true;
        }

        log.Error(string.Format("{0} Scoop install failed (Code {1})", task.Name, p.ExitCode));
        return false;
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
}
