using System.Diagnostics;
using WinZ.Models;
using WinZ.Services;
using System.Threading;
using System.Threading.Tasks;

namespace WinZ.Engine;

public class DebloatEngine(LogService log)
{
    public async Task<bool> RemoveAsync(SetupTask task, bool force, CancellationToken ct)
    {
        var script = force
            ? $"Get-AppxPackage -AllUsers *{task.PackageId}* | Remove-AppxPackage -AllUsers; Get-AppxProvisionedPackage -Online | Where-Object DisplayName -like '*{task.PackageId}*' | Remove-AppxProvisionedPackage -Online"
            : $"Get-AppxPackage *{task.PackageId}* | Remove-AppxPackage";

        log.Cmd(script);

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) log.Info(e.Data); };
        p.ErrorDataReceived  += (_, e) => { if (e.Data is not null) log.Error(e.Data); };
        p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
        await p.WaitForExitAsync(ct);

        if (p.ExitCode == 0) { log.Ok($"Removed: {task.Name}"); return true; }
        log.Error($"Removal failed: {task.Name} (exit {p.ExitCode})");
        return false;
    }
}
