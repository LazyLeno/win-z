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
        ArgumentNullException.ThrowIfNull(task);
        
        if (task.FallbackUrl == null)
        {
            log.Error(string.Format("No fallback URL configured for {0}", task.Name));
            return false;
        }

        var tmp = Path.Combine(Path.GetTempPath(), string.Format("WinZ_{0}.exe", task.Id));

        log.Cmd(string.Format("Downloading {0}", task.FallbackUrl));
        try
        {
            var bytes = await Http.GetByteArrayAsync(task.FallbackUrl, ct);

            await File.WriteAllBytesAsync(tmp, bytes, ct);
            log.Ok(string.Format("Downloaded {0}KB → {1}", bytes.Length / 1024, tmp));
        }
        catch (HttpRequestException ex)
        {
            log.Error(string.Format("Network error during download: {0}", ex.Message));
            return false;
        }
        catch (IOException ex)
        {
            log.Error(string.Format("File error during download: {0}", ex.Message));
            return false;
        }
        catch (Exception ex)
        {
            log.Error(string.Format("Download failed: {0}", ex.Message));
            return false;
        }

        log.Cmd(string.Format("Running silent install: {0} /S", tmp));
        var psi = new ProcessStartInfo(tmp, "/S")
        {
            UseShellExecute = true,
            Verb = "runas"
        };
        
        using var p = Process.Start(psi);
        if (p == null) 
        {
            log.Error(string.Format("Failed to start installer: {0}", tmp));
            return false;
        }
        
        await p.WaitForExitAsync(ct);

        try { File.Delete(tmp); } catch (IOException) { /* cleanup best-effort */ }

        if (p.ExitCode == 0) 
        { 
            log.Ok(string.Format("{0} installed.", task.Name)); 
            return true; 
        }
        
        log.Error(string.Format("{0} installer exit {1}", task.Name, p.ExitCode));
        return false;
    }
}

