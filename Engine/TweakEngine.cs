using System.Diagnostics;
using WinZ.Models;
using WinZ.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Linq;

namespace WinZ.Engine;

public class TweakEngine(LogService log)
{
    public async Task<bool> ApplyAsync(SetupTask task, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);
        
        if (string.IsNullOrWhiteSpace(task.TweakScript))
        {
            log.Error(string.Format("No script defined for tweak: {0}", task.Name));
            return false;
        }

        // Determine if it's a reg command or PowerShell
        string trimmedScript = task.TweakScript.TrimStart();
        bool isReg = trimmedScript.StartsWith("reg ", StringComparison.OrdinalIgnoreCase);
        log.Cmd(task.TweakScript);

        if (isReg)
        {
            return ApplyRegistryTweak(task);
        }
        else
        {
            return await ApplyPowerShellTweakAsync(task, ct);
        }
    }

    private bool ApplyRegistryTweak(SetupTask task)
    {
        try
        {
            // Simple parser for "reg add HKLM\...\... /v ... /t ... /d ... /f"
            // This is a naive implementation but much safer than shell execution
            var parts = task.TweakScript!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !parts[1].Equals("add", StringComparison.OrdinalIgnoreCase))
            {
                // Fallback to reg.exe if complex but with safer execution
                return ApplySaferRegExe(task);
            }

            string path = parts[2];
            string? valueName = GetArg(parts, "/v");
            string? typeStr = GetArg(parts, "/t");
            string? data = GetArg(parts, "/d");

            RegistryKey root = path.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase) ? Registry.LocalMachine : Registry.CurrentUser;
            string subKeyPath = path.Substring(path.IndexOf('\\') + 1);

            using var key = root.CreateSubKey(subKeyPath, true);
            if (key == null) throw new Exception("Could not create/open registry key.");

            object valueData = data ?? "";
            RegistryValueKind kind = ParseKind(typeStr);

            if (kind == RegistryValueKind.DWord && int.TryParse(data, out int dword)) valueData = dword;
            if (kind == RegistryValueKind.QWord && long.TryParse(data, out long qword)) valueData = qword;

            key.SetValue(valueName ?? "", valueData, kind);
            
            log.Ok(string.Format("Tweak applied: {0}", task.Name));
            return true;
        }
        catch (Exception ex)
        {
            log.Error(string.Format("Registry tweak failed: {0} - {1}", task.Name, ex.Message));
            return false;
        }
    }

    private bool ApplySaferRegExe(SetupTask task)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "reg.exe",
            Arguments = task.TweakScript!.Substring(4),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };
        using var p = Process.Start(psi);
        p?.WaitForExit();
        return p?.ExitCode == 0;
    }

    private async Task<bool> ApplyPowerShellTweakAsync(SetupTask task, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = string.Format("-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{0}\"", EscapePs(task.TweakScript!)),
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
            log.Ok(string.Format("Tweak applied: {0}", task.Name)); 
            return true; 
        }
        
        log.Error(string.Format("Tweak failed: {0} (exit {1})", task.Name, p.ExitCode));
        return false;
    }

    private static string GetArg(string[] parts, string flag)
    {
        int idx = Array.FindIndex(parts, p => p.Equals(flag, StringComparison.OrdinalIgnoreCase));
        return (idx != -1 && idx + 1 < parts.Length) ? parts[idx + 1] : "";
    }

    private static RegistryValueKind ParseKind(string? s) => s?.ToUpper() switch
    {
        "REG_DWORD" => RegistryValueKind.DWord,
        "REG_QWORD" => RegistryValueKind.QWord,
        "REG_SZ"    => RegistryValueKind.String,
        "REG_EXPAND_SZ" => RegistryValueKind.ExpandString,
        "REG_BINARY" => RegistryValueKind.Binary,
        _ => RegistryValueKind.String
    };

    private static string EscapePs(string s) => s.Replace("\"", "\\\"");
}

