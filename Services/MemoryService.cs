using System;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace WinZ.Services;

public static class MemoryService
{
    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, nint min, nint max);

    private static readonly CancellationTokenSource _reaperCts = new();

    static MemoryService()
    {
        // Configure GC for a low-memory-footprint interactive app:
        // - Server GC: off (default for desktop — lower baseline memory)
        // - Sustained low latency: allows GC to run more often at smaller sizes,
        //   preventing large one-time spikes that Task Manager shows as "rising RAM"
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        // Reaper: runs on a background thread every 45 seconds
        // Using a dedicated thread avoids blocking the ThreadPool
        var reaperThread = new Thread(() => ReaperLoop(_reaperCts.Token))
        {
            IsBackground  = true,
            Priority      = ThreadPriority.Lowest,
            Name          = "WinZ.MemoryReaper"
        };
        reaperThread.Start();
    }

    private static void ReaperLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            // Sleep on the dedicated thread — no ThreadPool slot consumed
            Thread.Sleep(TimeSpan.FromSeconds(45));
            if (!token.IsCancellationRequested)
                Optimize();
        }
    }

    /// <summary>
    /// Aggressively reclaims managed and native memory back to the OS.
    /// Safe to call from any thread.
    /// </summary>
    public static void Optimize()
    {
        try
        {
            // 1. Two-pass collection: first pass finalizes objects, second reclaims them
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

            // 2. Compact the Large Object Heap to defrag >85KB allocations
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();

            // 3. Trim native working set — returns physical pages to the OS
            using var p = Process.GetCurrentProcess();
            SetProcessWorkingSetSize(p.Handle, -1, -1);
        }
        catch { /* Best effort */ }
    }
}
