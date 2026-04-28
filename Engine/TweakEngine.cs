using System.Diagnostics;
using WinZ.Models;
using WinZ.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WinZ.Engine;

public class TweakEngine(LogService log)
{
    public async Task<bool> ApplyAsync(SetupTask task, CancellationToken ct)
    {
        if (task.TweakScript is null)
        {
            log.Error($"No script defined for tweak: {task.Name}");
            return false;
        }

        // Determine if it's a reg command or PowerShell
        bool isReg = task.TweakScript.TrimStart().StartsWith("reg ", StringComparison.OrdinalIgnoreCase);
        log.Cmd(task.TweakScript);

        ProcessStartInfo psi;
        if (isReg)
        {
            // Parse "reg add ..." directly
            psi = new ProcessStartInfo
            {
                FileName = "reg",
                Arguments = task.TweakScript.Substring(4), // strip "reg "
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else
        {
            psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{EscapePs(task.TweakScript)}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) log.Info(e.Data); };
        p.ErrorDataReceived  += (_, e) => { if (e.Data is not null) log.Error(e.Data); };
        p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
        await p.WaitForExitAsync(ct);

        if (p.ExitCode == 0) { log.Ok($"Tweak applied: {task.Name}"); return true; }
        log.Error($"Tweak failed: {task.Name} (exit {p.ExitCode})");
        return false;
    }

    private static string EscapePs(string s) => s.Replace("\"", "\\\"");
}
