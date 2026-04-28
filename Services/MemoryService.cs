using System;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace WinZ.Services;

public static class MemoryService
{
    [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize")]
    private static extern int SetProcessWorkingSetSize(IntPtr process, int minimumWorkingSetSize, int maximumWorkingSetSize);

    /// <summary>
    /// Attempts to release unused RAM back to the OS.
    /// Should be called after heavy UI transitions or when navigating away from data-intensive pages.
    /// </summary>
    public static void Optimize()
    {
        try
        {
            // Force a collection of all generations
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            // Compact the Large Object Heap (LOH) which is often the cause of "spikes" in WPF
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

            // Ask Windows to trim the working set
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var process = Process.GetCurrentProcess();
                SetProcessWorkingSetSize(process.Handle, -1, -1);
            }
        }
        catch (Exception)
        {
            /* Best effort */
        }
    }
}


