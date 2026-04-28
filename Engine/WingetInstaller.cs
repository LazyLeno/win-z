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
        log.Cmd($"winget install --id {task.PackageId} --silent --accept-package-agreements --accept-source-agreements");

        var psi = new ProcessStartInfo
        {
            FileName = "winget",
            Arguments = $"install --id {task.PackageId} --silent --accept-package-agreements --accept-source-agreements",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

        p.OutputDataReceived += (_, e) => { if (e.Data is not null) log.Info(e.Data); };
        p.ErrorDataReceived  += (_, e) => { if (e.Data is not null) log.Error(e.Data); };

        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        await p.WaitForExitAsync(ct);

        if (p.ExitCode == 0)
        {
            log.Ok($"{task.Name} installed successfully.");
            return true;
        }

        log.Error($"{task.Name} failed — winget exit code {p.ExitCode} (0x{p.ExitCode:X8})");
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
            using var p = Process.Start(psi)!;
            await p.WaitForExitAsync();
            return p.ExitCode == 0;
        }
        catch { return false; }
    }
}
