using System;
using System.Diagnostics;
using WinZ.Models;
using WinZ.Services;
using System.Threading;
using System.Threading.Tasks;

namespace WinZ.Engine;

public class WingetInstaller(LogService log)
{
    public async Task<bool> InstallAsync(SetupTask task, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);
        
        if (string.IsNullOrEmpty(task.PackageId))
        {
            log.Error(string.Format("No PackageId for: {0}", task.Name));
            return false;
        }

        log.Cmd(string.Format("winget install --id {0} --silent --accept-package-agreements --accept-source-agreements", task.PackageId));

        var psi = new ProcessStartInfo
        {
            FileName = "winget",
            Arguments = string.Format("install --id {0} --silent --accept-package-agreements --accept-source-agreements", task.PackageId),
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
            log.Ok(string.Format("{0} installed successfully.", task.Name));
            return true;
        }

        log.Error(string.Format("{0} failed — winget exit code {1} (0x{2:X8})", task.Name, p.ExitCode, p.ExitCode));
        return false;
    }

    public static async Task<bool> IsAvailableAsync()
    {
        try
        {
            var psi = new ProcessStartInfo("winget", "--version")
            {
                RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            await p.WaitForExitAsync();
            return p.ExitCode == 0;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception) { return false; }
    }
}

