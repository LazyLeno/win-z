using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinZ.Models;
using WinZ.Services;

namespace WinZ.Engine;

/// <summary>
/// High-performance, low-memory Tweak Engine.
/// Uses native Registry APIs for speed and spawns external shells for scripts 
/// to keep the main process memory footprint under 20MB.
/// </summary>
public class TweakEngine(LogService log)
{
    public async Task<bool> ApplyAsync(SetupTask task, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);
        
        if (string.IsNullOrWhiteSpace(task.TweakScript))
        {
            log.Error($"No script defined for tweak: {task.Name}");
            return false;
        }

        string trimmedScript = task.TweakScript.TrimStart();
        bool isReg = trimmedScript.StartsWith("reg ", StringComparison.OrdinalIgnoreCase);
        log.Cmd(task.TweakScript);

        if (isReg)
        {
            return ApplyRegistryTweak(task);
        }
        else
        {
            // Spawn external powershell to keep main process memory at <15MB
            return await Task.Run(() => RunExternalPowerShell(task.TweakScript), ct);
        }
    }

    private bool ApplyRegistryTweak(SetupTask task)
    {
        try
        {
            var parts = task.TweakScript!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !parts[1].Equals("add", StringComparison.OrdinalIgnoreCase))
            {
                return RunExternalProcess("reg.exe", task.TweakScript!.Substring(4));
            }

            string path = parts[2];
            string? valueName = GetArg(parts, "/v");
            string? typeStr = GetArg(parts, "/t");
            string? data = GetArg(parts, "/d");

            RegistryKey root = path.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase) ? Registry.LocalMachine : Registry.CurrentUser;
            string subKeyPath = path.Contains('\\') ? path.Substring(path.IndexOf('\\') + 1) : "";

            using var key = root.CreateSubKey(subKeyPath, true);
            if (key == null) throw new Exception("Access Denied");

            object valueData = data ?? "";
            RegistryValueKind kind = ParseKind(typeStr);

            if (kind == RegistryValueKind.DWord && int.TryParse(data, out int dword)) valueData = dword;
            if (kind == RegistryValueKind.QWord && long.TryParse(data, out long qword)) valueData = qword;

            key.SetValue(valueName ?? "", valueData, kind);
            log.Ok($"Registry tweak applied: {task.Name}");
            return true;
        }
        catch (Exception ex)
        {
            log.Error($"Registry failure: {task.Name} - {ex.Message}");
            return false;
        }
    }

    private bool RunExternalPowerShell(string script)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{script.Replace("\"", "\\\"")}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var p = Process.Start(psi);
        if (p == null) return false;

        string output = p.StandardOutput.ReadToEnd();
        string error = p.StandardError.ReadToEnd();
        p.WaitForExit();

        if (!string.IsNullOrWhiteSpace(output)) log.Info(output);
        if (!string.IsNullOrWhiteSpace(error)) log.Error(error);

        return p.ExitCode == 0;
    }

    private bool RunExternalProcess(string fileName, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        p?.WaitForExit();
        return p?.ExitCode == 0;
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
}
