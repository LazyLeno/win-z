using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinZ.Services;

/// <summary>
/// Two-tier memory management:
/// - FastOptimize: cheap Gen 0/1 collect, call every ~1s during active UI interaction.
/// - DeepOptimize: full Gen 2 + OS working-set trim, call only on page transitions.
/// </summary>
public static partial class MemoryService
{
    [LibraryImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

    /// <summary>
    /// Fast, non-blocking Gen 0+1 collection. Handles the short-lived WPF
    /// hit-test and binding allocations without causing UI pauses.
    /// </summary>
    public static void FastOptimize()
    {
        GC.Collect(1, GCCollectionMode.Optimized, false, false);
    }

    /// <summary>
    /// Full compacting Gen 2 collection + OS working set trim.
    /// Use sparingly — blocks briefly. Best called on navigation/page transitions.
    /// </summary>
    public static void DeepOptimize()
    {
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true, true);

        try
        {
            using var process = Process.GetCurrentProcess();
            if (process.Handle != IntPtr.Zero)
                SetProcessWorkingSetSize(process.Handle, (IntPtr)(-1), (IntPtr)(-1));
        }
        catch { }
    }

    // Keep old name as alias for existing callers
    public static void Optimize() => DeepOptimize();
}
