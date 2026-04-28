using WinZ.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WinZ.Engine;

public class RetryPolicy(LogService log)
{
    public async Task<bool> ExecuteAsync(
        Func<CancellationToken, Task<bool>> action,
        string taskName,
        int maxRetries,
        CancellationToken ct)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            if (ct.IsCancellationRequested) return false;
            
            try
            {
                bool result = await action(ct);
                if (result) return true;
            }
            catch (Exception ex)
            {
                log.Error($"{taskName} — attempt {attempt}/{maxRetries} threw: {ex.Message}");
            }

            if (attempt < maxRetries)
            {
                int delay = attempt * 2000; // exponential backoff
                log.Info($"Retrying {taskName} in {delay/1000}s...");
                await Task.Delay(delay, ct);
            }
        }
        return false;
    }
}
