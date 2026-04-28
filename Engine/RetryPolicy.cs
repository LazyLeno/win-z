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
        ArgumentNullException.ThrowIfNull(action);
        string name = taskName ?? "Unknown Task";

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            if (ct.IsCancellationRequested) return false;
            
            try
            {
                bool result = await action(ct);
                if (result) return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                log.Error(string.Format("{0} — attempt {1}/{2} threw: {3}", name, attempt, maxRetries, ex.Message));
            }

            if (attempt < maxRetries)
            {
                int delay = attempt * 2000; // exponential backoff
                log.Info(string.Format("Retrying {0} in {1}s...", name, delay/1000));
                
                try
                {
                    await Task.Delay(delay, ct);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }
        }
        return false;
    }
}

