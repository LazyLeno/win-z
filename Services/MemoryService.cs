using System;
using System.Runtime;
using System.Runtime.InteropServices;

namespace WinZ.Services;

public static class MemoryService
{
    [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize")]
    private static extern int SetProcessWorkingSetSize(IntPtr process, int minimumWorkingSetSize, int maximumWorkingSetSize);

    /// <summary>
    /// Forces a full collection and attempts to release as much RAM as possible back to the OS.
    /// </summary>
    public static void Optimize()
    {
        try
        {
            // Collect all generations
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            
            // Compact the Large Object Heap
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();

            // Ask Windows to trim the working set (the "aggressive" cut)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var process = System.Diagnostics.Process.GetCurrentProcess();
                SetProcessWorkingSetSize(process.Handle, -1, -1);
            }
        }
        catch { /* Best effort */ }
    }
}
