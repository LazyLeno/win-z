using System;
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
        ArgumentNullException.ThrowIfNull(task);
        
        if (string.IsNullOrEmpty(task.PackageId))
        {
            log.Error(string.Format("No PackageId for: {0}", task.Name));
            return false;
        }

        var script = force
            ? string.Format("Get-AppxPackage -AllUsers *{0}* | Remove-AppxPackage -AllUsers; Get-AppxProvisionedPackage -Online | Where-Object DisplayName -like '*{0}*' | Remove-AppxProvisionedPackage -Online", task.PackageId)
            : string.Format("Get-AppxPackage *{0}* | Remove-AppxPackage", task.PackageId);

        log.Cmd(script);

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = string.Format("-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{0}\"", script.Replace("\"", "\\\"")),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) log.Info(e.Data); };
        p.ErrorDataReceived  += (_, e) => { if (e.Data is not null) log.Error(e.Data); };
        
        if (!p.Start()) return false;
        
        p.BeginOutputReadLine(); 
        p.BeginErrorReadLine();
        
        await p.WaitForExitAsync(ct);

        if (p.ExitCode == 0) 
        { 
            log.Ok(string.Format("Removed: {0}", task.Name)); 
            return true; 
        }
        
        log.Error(string.Format("Removal failed: {0} (exit {1})", task.Name, p.ExitCode));
        return false;
    }
}

