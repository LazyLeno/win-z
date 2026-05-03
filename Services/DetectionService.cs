using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinZ.Models;

namespace WinZ.Services;

public static class DetectionService
{
    private static HashSet<string>? _wingetList;
    private static DateTime _lastWingetFetch = DateTime.MinValue;

    public static async Task<bool> IsInstalledAsync(SetupTask task)
    {
        if (task.Type != TaskType.Install) return false;

        // Run checks in parallel to minimize latency (Point #2: Batch I/O where possible)
        var registryTask = Task.Run(() => CheckRegistry(task.PackageId, task.Name));
        var fileSystemTask = Task.Run(() => CheckFileSystem(task.Name));
        
        Task<bool>? wingetTask = null;
        if (task.Method == InstallMethod.Winget && !string.IsNullOrEmpty(task.PackageId))
        {
            wingetTask = CheckWingetAsync(task.PackageId);
        }

        Task<bool>? scoopTask = null;
        if (task.Method == InstallMethod.Scoop && !string.IsNullOrEmpty(task.PackageId))
        {
            scoopTask = Task.Run(() => CheckScoop(task.PackageId));
        }

        // Wait for any to return true, or all to complete
        var tasks = new List<Task<bool>> { registryTask, fileSystemTask };
        if (wingetTask != null) tasks.Add(wingetTask);
        if (scoopTask != null) tasks.Add(scoopTask);

        while (tasks.Any())
        {
            var finished = await Task.WhenAny(tasks);
            if (await finished) return true;
            tasks.Remove(finished);
        }

        return false;
    }

    private static async Task<bool> CheckWingetAsync(string packageId)
    {
        var list = await GetWingetListAsync();
        return list.Contains(packageId);
    }

    private static bool CheckScoop(string packageId)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appName = packageId.Split('/').Last();
        var scoopPath = Path.Combine(userProfile, "scoop", "apps", appName);
        return Directory.Exists(scoopPath);
    }

    private static bool CheckRegistry(string? packageId, string appName)
    {
        // Check both Machine and User hives
        var hives = new[] { Registry.LocalMachine, Registry.CurrentUser };
        var paths = new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" };

        foreach (var hive in hives)
        {
            foreach (var rootPath in paths)
            {
                using var rootKey = hive.OpenSubKey(rootPath);
                if (rootKey == null) continue;

                // 1. Direct check by ID (Fastest)
                if (!string.IsNullOrEmpty(packageId))
                {
                    using var subKey = rootKey.OpenSubKey(packageId);
                    if (subKey != null) return true;
                }

                // 2. Fuzzy check by name (Iterative)
                foreach (var subKeyName in rootKey.GetSubKeyNames())
                {
                    // Fast check key name first before opening
                    if (subKeyName.Contains(appName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)) return true;

                    using var subKey = rootKey.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    var displayName = subKey.GetValue("DisplayName")?.ToString();
                    if (displayName != null)
                    {
                        if (displayName.Contains(appName, StringComparison.OrdinalIgnoreCase)) return true;
                        if (!string.IsNullOrEmpty(packageId) && displayName.Contains(packageId, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                }
            }
        }
        return false;
    }

    private static bool CheckFileSystem(string appName)
    {
        var roots = new[] 
        { 
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Roaming")
        };

        var variants = new[] { appName, appName.Replace(" ", ""), appName.Split(' ')[0] };

        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;

            foreach (var variant in variants)
            {
                if (string.IsNullOrEmpty(variant) || variant.Length < 3) continue;

                try
                {
                    // Use EnumerationOptions for slightly better performance and error handling
                    var options = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = false, IgnoreInaccessible = true };
                    var dirs = Directory.EnumerateDirectories(root, $"*{variant}*", options);
                    
                    foreach (var dir in dirs)
                    {
                        // Optimization: just check for ANY .exe in top level of the found dir
                        if (Directory.EnumerateFiles(dir, "*.exe", SearchOption.TopDirectoryOnly).Any()) return true;
                    }
                }
                catch { }
            }
        }

        // Special case for IDM
        if (appName.Contains("Download Manager", StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Internet Download Manager"))) return true;
        }

        return false;
    }

    private static async Task<HashSet<string>> GetWingetListAsync()
    {
        if (_wingetList != null && (DateTime.Now - _lastWingetFetch).TotalMinutes < 5)
            return _wingetList;

        var list = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var psi = new ProcessStartInfo("winget", "list")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                string output = await p.StandardOutput.ReadToEndAsync();
                await p.WaitForExitAsync();
                
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines.Skip(2)) // Skip headers
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2) list.Add(parts[1]);
                }
            }
        }
        catch { }

        _wingetList = list;
        _lastWingetFetch = DateTime.Now;
        return list;
    }

    public static string? FindUninstaller(string packageId)
    {
        var hives = new[] { Registry.LocalMachine, Registry.CurrentUser };
        var paths = new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" };

        foreach (var hive in hives)
        {
            foreach (var rootPath in paths)
            {
                using var key = hive.OpenSubKey(rootPath);
                if (key == null) continue;

                using var subKey = key.OpenSubKey(packageId);
                if (subKey != null) return subKey.GetValue("UninstallString")?.ToString();

                foreach (var name in key.GetSubKeyNames())
                {
                    using var sk = key.OpenSubKey(name);
                    var disp = sk?.GetValue("DisplayName")?.ToString();
                    if (disp != null && disp.Contains(packageId, StringComparison.OrdinalIgnoreCase))
                    {
                        return sk?.GetValue("UninstallString")?.ToString();
                    }
                }
            }
        }
        return null;
    }
}
