using System;
using System.IO;
using System.Net.Http;
using System.Diagnostics;
using System.Security.Cryptography;
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
        
        if (task.EffectiveFallbackUrl == null)
        {
            log.Error(string.Format("No fallback URL configured for {0}", task.DisplayName));
            return false;
        }

        var tmp = Path.Combine(Path.GetTempPath(), string.Format("WinZ_{0}.exe", task.Id));

        log.Cmd(string.Format("Downloading {0}", task.EffectiveFallbackUrl));
        try
        {
            using var response = await Http.GetAsync(task.EffectiveFallbackUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            
            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var fileStream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);
            
            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(65536);
            try
            {
                int read;
                long totalRead = 0;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, read, ct);
                    totalRead += read;
                    if (totalBytes != -1)
                    {
                        task.Progress = (int)((double)totalRead / totalBytes * 100);
                    }
                }
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }

            log.Ok(string.Format("Downloaded {0}KB → {1}", new FileInfo(tmp).Length / 1024, tmp));
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

        // ── Checksum Verification ──────────────────────────────────────────────
        bool verifySetting = await DataService.Instance.GetSettingAsync("VerifyChecksums") != "False";
        if (verifySetting)
        {
            if (string.IsNullOrWhiteSpace(task.ExpectedSha256))
            {
                log.Info(string.Format("[Checksum] No hash configured for {0} — skipping verification.", task.DisplayName));
            }
            else
            {
                log.Cmd(string.Format("Verifying SHA-256 checksum for {0}", task.DisplayName));
                string actualHash;
                try
                {
                    using var sha = SHA256.Create();
                    await using var fs = new FileStream(tmp, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
                    var hashBytes = await Task.Run(() => sha.ComputeHash(fs), ct);
                    actualHash = Convert.ToHexString(hashBytes);
                }
                catch (Exception ex)
                {
                    log.Error(string.Format("Checksum computation failed: {0}", ex.Message));
                    try { File.Delete(tmp); } catch (IOException) { }
                    return false;
                }

                if (!actualHash.Equals(task.ExpectedSha256.Replace("-", "").Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    log.Error(string.Format(
                        "⚠ CHECKSUM MISMATCH for {0}! Expected: {1} | Got: {2} — Aborting install.",
                        task.DisplayName, task.ExpectedSha256.ToUpper(), actualHash));
                    try { File.Delete(tmp); } catch (IOException) { }
                    return false;
                }

                log.Ok(string.Format("Checksum verified ✓ ({0})", actualHash[..16] + "..."));
            }
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
            log.Ok(string.Format("{0} installed.", task.DisplayName)); 
            return true; 
        }
        
        log.Error(string.Format("{0} installer exit {1}", task.DisplayName, p.ExitCode));
        return false;
    }
}

