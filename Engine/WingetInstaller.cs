using System;
using System.Diagnostics;
using WinZ.Models;
using WinZ.Services;
using System.Threading;
using System.Threading.Tasks;

namespace WinZ.Engine;

public class WingetInstaller(LogService log)
{
    public Task<bool> InstallAsync(SetupTask task, CancellationToken ct) => InstallAsync(task, false, ct);

    public async Task<bool> InstallAsync(SetupTask task, bool force, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);
        
        if (string.IsNullOrEmpty(task.EffectivePackageId))
        {
            log.Error(string.Format("No PackageId for: {0}", task.DisplayName));
            return false;
        }

        string forceFlag = force ? " --force" : "";
        log.Cmd(string.Format("winget install --id {0} --silent --accept-package-agreements --accept-source-agreements{1}", task.EffectivePackageId, forceFlag));

        var psi = new ProcessStartInfo
        {
            FileName = "winget",
            Arguments = string.Format("install --id {0} --silent --accept-package-agreements --accept-source-agreements{1}", task.EffectivePackageId, forceFlag),
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
            log.Ok(string.Format("{0} installed successfully.", task.DisplayName));
            return true;
        }

        log.Error(string.Format("{0} failed — winget exit code {1} (0x{2:X8})", task.DisplayName, p.ExitCode, p.ExitCode));
        return false;
    }

    public async Task<bool> UninstallAsync(SetupTask task, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (string.IsNullOrEmpty(task.EffectivePackageId)) return false;

        log.Info(string.Format("Uninstalling {0}...", task.DisplayName));

        // Try Winget first
        var psi = new ProcessStartInfo
        {
            FileName = "winget",
            Arguments = string.Format("uninstall --id {0} --silent --accept-source-agreements", task.EffectivePackageId),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var p = new Process { StartInfo = psi, EnableRaisingEvents = true })
        {
            p.OutputDataReceived += (_, e) => { if (e.Data is not null) log.Info(e.Data); };
            if (p.Start())
            {
                p.BeginOutputReadLine();
                await p.WaitForExitAsync(ct);
                if (p.ExitCode == 0) return true;
            }
        }

        // Fallback: Try to find uninstaller in Registry
        log.Info("Winget uninstall failed or not applicable. Searching registry...");
        string? uninstaller = DetectionService.FindUninstaller(task.EffectivePackageId);
        if (!string.IsNullOrEmpty(uninstaller))
        {
            log.Cmd(string.Format("Running uninstaller: {0}", uninstaller));
            try
            {
                var uPsi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {uninstaller} /S /SILENT /VERYSILENT",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var up = Process.Start(uPsi);
                if (up != null)
                {
                    await up.WaitForExitAsync(ct);
                    return true;
                }
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Failed to run registry uninstaller: {0}", ex.Message));
            }
        }

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

