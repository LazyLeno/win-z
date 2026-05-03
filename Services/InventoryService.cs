using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Win32;
using WinZ.Models;

namespace WinZ.Services;

public static class InventoryService
{
    private static readonly string[] RegPaths = {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall"
    };

    public static async Task ComputeSizesAsync(IEnumerable<InstalledApp> apps) => await Task.Run(() => {
        foreach (var app in apps.Where(a => !string.IsNullOrEmpty(a.InstallLocation) && Directory.Exists(a.InstallLocation))) {
            var excludes = app.Name.Equals("Steam", StringComparison.OrdinalIgnoreCase) ? new[] { "steamapps" } : null;
            var size = GetDirSize(app.InstallLocation!, excludes);
            if (size > 0) { app.RawSize = size; app.Size = FormatSize(size); }
        }
    });

    public static async Task<List<InstalledApp>> GetInstalledAppsAsync() {
        return await Task.Run(() => {
            var apps = new List<InstalledApp>();
            foreach (var path in RegPaths) {
                using var root = path.StartsWith("Software") ? Registry.CurrentUser : Registry.LocalMachine;
                using var key = root.OpenSubKey(path);
                if (key == null) continue;
                foreach (var subName in key.GetSubKeyNames()) {
                    using var sub = key.OpenSubKey(subName);
                    if (sub?.GetValue("DisplayName") is not string name || sub.GetValue("UninstallString") is not string uninst) continue;
                    if (sub.GetValue("SystemComponent") is 1 || sub.GetValue("ParentKeyName") != null || IsClutter(name)) continue;

                    var app = new InstalledApp {
                        Name = name, 
                        Publisher = sub.GetValue("Publisher") as string ?? "?",
                        UninstallString = uninst, IconPath = sub.GetValue("DisplayIcon") as string,
                        Version = sub.GetValue("DisplayVersion") as string ?? "?",
                        InstallLocation = GetInstallDir(sub, name, uninst)
                    };
                    
                    long est = sub.GetValue("EstimatedSize") is int s ? s * 1024L : 0;
                    app.RawSize = est; 
                    app.Size = FormatSize(est);
                    apps.Add(app);
                }
            }
            return apps.OrderBy(a => a.Name).ToList();
        });
    }

    public static async Task LoadIconsAsync(IEnumerable<InstalledApp> apps) {
        await Task.Run(() => {
            foreach (var app in apps) {
                var icon = ExtractIcon(app);
                if (icon != null) {
                    Application.Current.Dispatcher.BeginInvoke(() => app.Icon = icon);
                }
            }
        });
    }

    private static bool IsClutter(string name) {
        var n = name.ToLowerInvariant();
        if (n.StartsWith("vs_") || n.Contains("coreeditorfonts") || n.Contains("vc_") || n.Contains("shared") || n.Contains("sdk")) return true;
        return n.Contains("driver") || n.Contains("redistributable") || n.Contains("update for") || n.StartsWith("microsoft .net") || (n.Contains("kb") && System.Text.RegularExpressions.Regex.IsMatch(n, @"kb\d{5,}"));
    }

    private static string? GetInstallDir(RegistryKey sub, string name, string uninst) {
        var loc = sub.GetValue("InstallLocation") as string;
        bool isSteam = uninst.Contains("steam://") || sub.Name.Contains("Steam App");

        if (isSteam) {
            if (string.IsNullOrEmpty(loc) || loc.ToLower().EndsWith("steam")) {
                // Try to find Steam root from registry if not provided
                using var steamKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                var steamPath = steamKey?.GetValue("SteamPath") as string;
                if (!string.IsNullOrEmpty(steamPath)) {
                    var common = Path.Combine(steamPath, "steamapps", "common");
                    if (Directory.Exists(common)) {
                        var d = Path.Combine(common, name);
                        if (Directory.Exists(d)) return d;
                        try { 
                            var match = Directory.GetDirectories(common).FirstOrDefault(f => name.Contains(Path.GetFileName(f), StringComparison.OrdinalIgnoreCase) || Path.GetFileName(f).Contains(name, StringComparison.OrdinalIgnoreCase));
                            if (match != null) return match;
                        } catch { }
                    }
                    if (string.IsNullOrEmpty(loc)) loc = steamPath;
                }
            }
        }

        if (IsValid(loc, name)) return loc;

        // Fallback: derive from DisplayIcon or UninstallString
        var icon = sub.GetValue("DisplayIcon") as string;
        foreach (var path in new[] { icon, uninst }) {
            if (string.IsNullOrEmpty(path)) continue;
            try {
                var clean = path.Split(',')[0].Trim('\"').Trim();
                if (clean.StartsWith("MsiExec", StringComparison.OrdinalIgnoreCase)) continue;
                var dir = Path.GetDirectoryName(clean);
                if (IsValid(dir, name)) return dir;
            } catch { }
        }
        return null;
    }

    private static bool IsValid(string? p, string name) {
        if (string.IsNullOrEmpty(p) || !Directory.Exists(p) || p.ToLower().Contains("system32")) return false;
        if (p.ToLower().EndsWith("steam") && !name.Equals("Steam", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static long GetDirSize(string p, string[]? ex = null) {
        try {
            var files = Directory.EnumerateFiles(p, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true });
            if (ex != null) foreach (var e in ex) { var ep = Path.Combine(p, e).ToLower(); files = files.Where(f => !f.ToLower().StartsWith(ep)); }
            return files.Sum(f => new FileInfo(f).Length);
        } catch { return 0; }
    }

    private static string FormatSize(long b) {
        if (b < 0) return "0 MB";
        double mb = b / 1048576.0;
        if (mb < 1024.0) return $"{mb:F0} MB";
        return $"{mb / 1024.0:F2} GB";
    }

    private static ImageSource? ExtractIcon(InstalledApp app) {
        if (!string.IsNullOrEmpty(app.IconPath)) { var i = Load(app.IconPath); if (i != null) return i; }
        if (string.IsNullOrEmpty(app.InstallLocation)) return null;
        try {
            // Non-recursive search for performance in large directories (games/VS/etc)
            var opt = new EnumerationOptions { RecurseSubdirectories = false, IgnoreInaccessible = true };
            
            // 1. Check top-level for .ico
            var icos = Directory.EnumerateFiles(app.InstallLocation, "*.ico", opt);
            foreach (var ico in icos) { var i = Load(ico); if (i != null) return i; }

            // 2. Check for .png or .jpg with app name (case-insensitive)
            var imgs = Directory.EnumerateFiles(app.InstallLocation, "*.*", opt)
                        .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase));
            
            foreach (var img in imgs) {
                if (Path.GetFileNameWithoutExtension(img).Equals(app.Name, StringComparison.OrdinalIgnoreCase)) {
                    var i = Load(img); if (i != null) return i;
                }
            }

            // 3. Check for executables (most likely have the icon)
            foreach (var exe in Directory.EnumerateFiles(app.InstallLocation, "*.exe", opt)) {
                // Try the main exe first if it matches the name
                if (Path.GetFileNameWithoutExtension(exe).Contains(app.Name, StringComparison.OrdinalIgnoreCase)) {
                    var i = Load(exe); if (i != null) return i;
                }
            }
            
            // 4. Last resort: any exe in the top level
            foreach (var exe in Directory.EnumerateFiles(app.InstallLocation, "*.exe", opt)) {
                var i = Load(exe); if (i != null) return i;
            }
        } catch { }
        return null;
    }

    private static ImageSource? Load(string p) {
        try {
            var path = p.Split(',')[0].Trim('\"').Trim();
            if (!File.Exists(path)) return null;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            
            // Optimized loading for standalone images
            if (ext == ".ico" || ext == ".png" || ext == ".jpg" || ext == ".jpeg") {
                var b = new BitmapImage();
                b.BeginInit();
                b.UriSource = new Uri(Path.GetFullPath(path));
                b.DecodePixelWidth = 32; // Optimized size
                b.CacheOption = BitmapCacheOption.OnLoad;
                b.EndInit();
                b.Freeze();
                return b;
            }

            // Fallback for executables/DLLs
            using var ico = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (ico == null) return null;
            var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(ico.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
            src.Freeze();
            return src;
        } catch { return null; }
    }
}

public enum CleanupType { Registry, File }
public class CleanupItem : INotifyPropertyChanged {
    private bool _isChecked;
    public CleanupType Type { get; set; }
    public string Path { get; set; } = "";
    public bool IsChecked { get => _isChecked; set { _isChecked = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public static class CleanupService {
    public static List<CleanupItem> FindLeftovers(InstalledApp app) {
        var items = new List<CleanupItem>();
        var name = app.Name;
        var pub = app.Publisher == "?" ? "" : app.Publisher;
        
        // 1. Registry Search
        string[] roots = { @"SOFTWARE", @"Software" };
        foreach (var rootName in roots) {
            using var rootKey = rootName == "SOFTWARE" ? Registry.LocalMachine : Registry.CurrentUser;
            foreach (var part in new[] { pub, name }.Where(s => !string.IsNullOrEmpty(s))) {
                try {
                    using var subKey = rootKey.OpenSubKey(rootName + "\\" + part);
                    if (subKey != null) items.Add(new CleanupItem { Type = CleanupType.Registry, Path = rootKey.Name + "\\" + rootName + "\\" + part, IsChecked = true });
                } catch { }
            }
        }

        // 2. File System Search
        var searchPaths = new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData))
        };

        foreach (var baseDir in searchPaths) {
            if (!Directory.Exists(baseDir)) continue;
            try {
                foreach (var dir in Directory.GetDirectories(baseDir)) {
                    var dName = Path.GetFileName(dir);
                    if (dName.Contains(name, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(pub) && dName.Contains(pub, StringComparison.OrdinalIgnoreCase))) {
                        items.Add(new CleanupItem { Type = CleanupType.File, Path = dir, IsChecked = true });
                    }
                }
            } catch { }
        }
        return items.GroupBy(i => i.Path).Select(g => g.First()).ToList();
    }

    public static void Delete(CleanupItem item) {
        try {
            if (item.Type == CleanupType.File && Directory.Exists(item.Path)) Directory.Delete(item.Path, true);
            else if (item.Type == CleanupType.Registry) {
                var parts = item.Path.Split('\\');
                using var root = parts[0] == "HKEY_LOCAL_MACHINE" ? Registry.LocalMachine : Registry.CurrentUser;
                var subPath = string.Join("\\", parts.Skip(1));
                root.DeleteSubKeyTree(subPath, false);
            }
        } catch { }
    }
}
