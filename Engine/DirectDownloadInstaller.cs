using System;
using System.IO;
using System.Net.Http;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WinZ.Models;
using WinZ.Services;

namespace WinZ.Engine;

public class DirectDownloadInstaller(LogService log)
{
    private static readonly HttpClient Http = new();

    public async Task<bool> InstallAsync(SetupTask task, CancellationToken ct)
    {
        if (task.FallbackUrl is null)
        {
            log.Error($"No fallback URL configured for {task.Name}");
            return false;
        }

        var tmp = Path.Combine(Path.GetTempPath(), $"WinZ_{task.Id}.exe");

        log.Cmd($"Downloading {task.FallbackUrl}");
        try
        {
            var bytes = await Http.GetByteArrayAsync(task.FallbackUrl, ct);
            await File.WriteAllBytesAsync(tmp, bytes, ct);
            log.Ok($"Downloaded {bytes.Length / 1024}KB → {tmp}");
        }
        catch (Exception ex)
        {
            log.Error($"Download failed: {ex.Message}");
            return false;
        }

        log.Cmd($"Running silent install: {tmp} /S");
        var psi = new ProcessStartInfo(tmp, "/S")
        {
            UseShellExecute = true,
            Verb = "runas"
        };
        using var p = Process.Start(psi)!;
        await p.WaitForExitAsync(ct);

        try { File.Delete(tmp); } catch { /* cleanup best-effort */ }

        if (p.ExitCode == 0) { log.Ok($"{task.Name} installed."); return true; }
        log.Error($"{task.Name} installer exit {p.ExitCode}");
        return false;
    }
}
