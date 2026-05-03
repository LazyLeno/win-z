using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Microsoft.Win32;

namespace WinZ.Services;

/// <summary>Polls CPU/GPU usage at 1s intervals. Singleton. Thread-safe property updates.</summary>
public sealed class HardwareService : INotifyPropertyChanged, IDisposable
{
    private static readonly Lazy<HardwareService> _instance = new(() => new HardwareService());
    public static HardwareService Instance => _instance.Value;

    // ── CPU ──────────────────────────────────────────────────────────
    private string _cpuName  = "Detecting…";
    private string _cpuSpecs = "";
    private double _cpuUsage;
    public string CpuName  { get => _cpuName;  private set => Set(ref _cpuName,  value); }
    public string CpuSpecs { get => _cpuSpecs; private set => Set(ref _cpuSpecs, value); }
    public double CpuUsage { get => _cpuUsage; private set => Set(ref _cpuUsage, value); }

    // ── GPU ──────────────────────────────────────────────────────────
    private string _gpuName  = "Detecting…";
    private string _gpuSpecs = "";
    private double _gpuUsage;
    public string GpuName  { get => _gpuName;  private set => Set(ref _gpuName,  value); }
    public string GpuSpecs { get => _gpuSpecs; private set => Set(ref _gpuSpecs, value); }
    public double GpuUsage { get => _gpuUsage; private set => Set(ref _gpuUsage, value); }

    // ── RAM ──────────────────────────────────────────────────────────
    private string _ramSpecs = "Detecting…";
    private string _ramLoad = "";
    private double _ramUsage;
    public string RamSpecs { get => _ramSpecs; private set => Set(ref _ramSpecs, value); }
    public string RamLoad { get => _ramLoad; private set => Set(ref _ramLoad, value); }
    public double RamUsage { get => _ramUsage; private set => Set(ref _ramUsage, value); }

    // ── OS ───────────────────────────────────────────────────────────
    private string _osSpecs = "Detecting…";
    private string _osStatus = "";
    private bool _isActivated;
    public string OsSpecs { get => _osSpecs; private set => Set(ref _osSpecs, value); }
    public string OsStatus { get => _osStatus; private set => Set(ref _osStatus, value); }
    public bool IsActivated { get => _isActivated; private set => Set(ref _isActivated, value); }

    // ── Arc geometry constants (shared with code-behind) ─────────────
    // Small arc: radius=26, StrokeThickness=5
    // PathLength = π × 26 ≈ 81.68 px  →  in dash-units = 81.68/5 ≈ 16.34
    public const double ArcLenUnits = Math.PI * 26.0 / 5.0;

    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _gpuCounter;
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    private HardwareService()
    {
        _ = System.Threading.Tasks.Task.Run(LoadStaticInfo);
        _ = System.Threading.Tasks.Task.Run(InitCounters);

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void LoadStaticInfo()
    {
        try
        {
            // ── CPU ────────────────────────────────────────────────
            using var cpuQuery = new ManagementObjectSearcher(
                "SELECT Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed FROM Win32_Processor");
            foreach (ManagementObject o in cpuQuery.Get())
            {
                var raw  = o["Name"]?.ToString()?.Trim() ?? "Unknown CPU";
                // Strip marketing noise and technical clutter
                raw = System.Text.RegularExpressions.Regex.Replace(raw, @"\(R\)|\(TM\)|@.*$", "").Trim();
                raw = System.Text.RegularExpressions.Regex.Replace(raw, @"(?i)\d+-Core|Processor", "").Trim();
                
                var cores   = o["NumberOfCores"]?.ToString() ?? "?";
                var threads = o["NumberOfLogicalProcessors"]?.ToString() ?? "?";
                var ghz     = o["MaxClockSpeed"] is uint mhz ? (mhz / 1000.0).ToString("F1") : "?";
                Dispatch(() => { CpuName = raw; CpuSpecs = $"{cores}C / {threads}T  ·  {ghz} GHz"; });
                break;
            }

            // ── GPU name (WMI) ─────────────────────────────────────
            string gpuName = "Unknown GPU";
            using var gpuQuery = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_VideoController WHERE PNPDeviceID LIKE 'PCI%'");
            foreach (ManagementObject o in gpuQuery.Get())
            {
                var n = o["Name"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(n)) { gpuName = n; break; }
            }

            // ── GPU VRAM via registry (bypasses uint32 4 GB cap) ───
            long vramBytes = 0;
            try
            {
                const string kPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
                using var baseKey = Registry.LocalMachine.OpenSubKey(kPath);
                if (baseKey != null)
                {
                    foreach (var sub in baseKey.GetSubKeyNames().Where(n => n.Length == 4))
                    {
                        using var sk = baseKey.OpenSubKey(sub);
                        var raw = sk?.GetValue("HardwareInformation.qwMemorySize");
                        long v = raw switch
                        {
                            long l        => l,
                            byte[] b when b.Length >= 8 => BitConverter.ToInt64(b, 0),
                            _             => 0
                        };
                        if (v > vramBytes) vramBytes = v;
                    }
                }
            }
            catch { /* registry unavailable */ }

            // Fallback: WMI uint32 (max 4 GB — still useful for older cards)
            if (vramBytes == 0)
            {
                using var q2 = new ManagementObjectSearcher(
                    "SELECT AdapterRAM FROM Win32_VideoController WHERE PNPDeviceID LIKE 'PCI%'");
                foreach (ManagementObject o in q2.Get())
                {
                    if (o["AdapterRAM"] is uint u) vramBytes = Math.Max(vramBytes, (long)u);
                }
            }

            var vramStr = vramBytes > 0 ? $"{vramBytes / (1024.0 * 1024 * 1024):F0} GB VRAM" : "";
            Dispatch(() => { GpuName = gpuName; GpuSpecs = vramStr; });

            // ── RAM static info ────────────────────────────────────
            using var ramQuery = new ManagementObjectSearcher("SELECT Speed, MemoryType, SMBIOSMemoryType FROM Win32_PhysicalMemory");
            string ramType = "RAM";
            string ramSpeed = "";
            foreach (ManagementObject o in ramQuery.Get())
            {
                var speed = o["Speed"]?.ToString();
                if (!string.IsNullOrEmpty(speed)) ramSpeed = $"  ·  {speed} MHz";
                
                // SMBIOSMemoryType: 24=DDR3, 26=DDR4, 34=DDR5
                var smType = o["SMBIOSMemoryType"] is ushort s ? s : (ushort)0;
                ramType = smType switch {
                    26 => "DDR4",
                    34 => "DDR5",
                    24 => "DDR3",
                    _ => "RAM"
                };
                break;
            }
            
            // Total capacity via Win32 API for precision
            NativeMethods.GetSystemMemory(out long totalBytes, out _);
            var totalGb = Math.Round(totalBytes / (1024.0 * 1024 * 1024), 1);
            Dispatch(() => { RamSpecs = $"{totalGb} GB {ramType}{ramSpeed}"; });

            // ── OS Info ────────────────────────────────────────────
            string edition = "Windows";
            string build = "";
            using var osQuery = new ManagementObjectSearcher("SELECT Caption, BuildNumber FROM Win32_OperatingSystem");
            foreach (ManagementObject o in osQuery.Get())
            {
                edition = o["Caption"]?.ToString() ?? "Windows";
                edition = edition.Replace("Microsoft ", "").Replace("™", "").Replace("®", "").Trim();
                build = o["BuildNumber"]?.ToString() ?? "";
                break;
            }

            bool activated = false;
            string status = "Unactivated";
            try
            {
                // Primary: Query the licensing service for the active Windows product
                using var licQuery = new ManagementObjectSearcher("SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE ApplicationID = '55c28233-72f1-4ba9-aa59-4443a86910a2' AND PartialProductKey IS NOT NULL");
                foreach (ManagementObject o in licQuery.Get())
                {
                    if (o["LicenseStatus"]?.ToString() == "1")
                    {
                        activated = true;
                        status = "Activated";
                        break;
                    }
                }

                // Secondary: Check global service status if primary was inconclusive
                if (!activated)
                {
                    using var svcQuery = new ManagementObjectSearcher("SELECT LicenseStatus FROM SoftwareLicensingService");
                    foreach (ManagementObject o in svcQuery.Get())
                    {
                        if (o["LicenseStatus"]?.ToString() == "1")
                        {
                            activated = true;
                            status = "Activated";
                            break;
                        }
                    }
                }

                // Fallback: Try slmgr.vbs-based check for activation status (more reliable on some systems)
                if (!activated)
                {
                    var slmgr = GetSlmgrActivationStatus();
                    if (slmgr == "Activated")
                    {
                        activated = true;
                        status = "Activated";
                    }
                }
            }
            catch { }

            using var archQuery = new ManagementObjectSearcher("SELECT OSArchitecture FROM Win32_OperatingSystem");
            string arch = "x64";
            foreach (ManagementObject o in archQuery.Get()) arch = o["OSArchitecture"]?.ToString() ?? "x64";

            Dispatch(() => { 
                OsSpecs = $"{edition} ({arch})  ·  Build {build}";
                OsStatus = status;
                IsActivated = activated;
            });
        }
        catch { /* WMI failure */ }
    }

    private void InitCounters()
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
            _ = _cpuCounter.NextValue();
        }
        catch { _cpuCounter = null; }

        try
        {
            var cat = new PerformanceCounterCategory("GPU Engine");
            var inst = cat.GetInstanceNames()
                .Where(n => n.EndsWith("engtype_3D", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (inst.Length > 0)
            {
                _gpuCounter = new PerformanceCounter("GPU Engine", "Utilization Percentage", inst[0], true);
                _ = _gpuCounter.NextValue();
            }
        }
        catch { _gpuCounter = null; }
    }

    private void OnTick(object? s, EventArgs e)
    {
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                double cpu = Math.Round(Math.Clamp(_cpuCounter?.NextValue() ?? 0, 0, 100), 1);
                double gpu = Math.Round(Math.Clamp(_gpuCounter?.NextValue() ?? 0, 0, 100), 1);
                
                NativeMethods.GetSystemMemory(out long total, out long avail);
                double used = total - avail;
                double ramPct = (used / total) * 100.0;
                string loadStr = $"{used / (1024.0 * 1024 * 1024):F1} / {total / (1024.0 * 1024 * 1024):F1} GB";

                Dispatch(() => 
                { 
                    CpuUsage = cpu; 
                    GpuUsage = gpu; 
                    RamUsage = Math.Round(ramPct, 1);
                    RamLoad = loadStr;
                });
            }
            catch { }
        });
    }

    private static void Dispatch(Action a)
        => System.Windows.Application.Current?.Dispatcher.BeginInvoke(a, DispatcherPriority.Background);

    // Attempts to determine activation status via slmgr.vbs as a robust fallback
    private string GetSlmgrActivationStatus()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cscript",
                Arguments = "//Nologo slmgr.vbs /xpr",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return "Unknown";
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            if (!string.IsNullOrWhiteSpace(output))
            {
                var low = output.ToLowerInvariant();
                if (low.Contains("permanently activated") || low.Contains("licensed") || low.Contains("activated")) return "Activated";
                if (low.Contains("not activated") || low.Contains("inactive")) return "Unactivated";
            }
            return "Unknown";
        }
        catch { return "Unknown"; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _cpuCounter?.Dispose();
        _gpuCounter?.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T f, T v, [CallerMemberName] string? n = null)
    {
        if (EqualityComparer<T>.Default.Equals(f, v)) return;
        f = v;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX() { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] MEMORYSTATUSEX lpBuffer);

        public static void GetSystemMemory(out long total, out long avail)
        {
            var ms = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(ms))
            {
                total = (long)ms.ullTotalPhys;
                avail = (long)ms.ullAvailPhys;
            }
            else { total = 0; avail = 0; }
        }
    }
}
