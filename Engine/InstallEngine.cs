using WinZ.Models;
using WinZ.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskStatus = WinZ.Models.TaskStatus;

namespace WinZ.Engine;

public class InstallEngine
{
    private readonly LogService _log;
    private readonly WingetInstaller _winget;
    private readonly DirectDownloadInstaller _direct;
    private readonly TweakEngine _tweaks;
    private readonly DebloatEngine _debloat;
    private readonly RetryPolicy _retry;

    public event Action<SetupTask>? TaskStarted;
    public event Action<SetupTask, bool>? TaskCompleted;

    public InstallEngine(LogService log)
    {
        _log = log;
        _winget  = new WingetInstaller(log);
        _direct  = new DirectDownloadInstaller(log);
        _tweaks  = new TweakEngine(log);
        _debloat = new DebloatEngine(log);
        _retry   = new RetryPolicy(log);
    }

    public async Task<List<SetupResult>> RunAsync(
        IEnumerable<SetupTask> tasks,
        bool forceDebloat = false,
        CancellationToken ct = default)
    {
        var results = new List<SetupResult>();
        bool wingetAvailable = await WingetInstaller.IsAvailableAsync();

        if (!wingetAvailable)
            _log.Error("winget not found — install tasks will fall back to direct download");

        foreach (var task in tasks.Where(t => t.IsSelected))
        {
            if (ct.IsCancellationRequested) break;

            task.Status = TaskStatus.Running;
            task.StatusText = "Running...";
            TaskStarted?.Invoke(task);
            _log.Info($"--- BEGIN: {task.Name} ---");

            bool ok = false;
            try
            {
                ok = task.Type switch
                {
                    TaskType.Install => await RunInstallAsync(task, wingetAvailable, ct),
                    TaskType.Tweak   => await _retry.ExecuteAsync(
                        t => _tweaks.ApplyAsync(task, t), task.Name, task.RetryMax, ct),
                    TaskType.Remove  => await _retry.ExecuteAsync(
                        t => _debloat.RemoveAsync(task, forceDebloat, t), task.Name, task.RetryMax, ct),
                    _ => false
                };
            }
            catch (OperationCanceledException)
            {
                task.Status = TaskStatus.Skipped;
                task.StatusText = "Cancelled";
                results.Add(new SetupResult(task.Name, TaskStatus.Skipped, "User cancelled"));
                continue;
            }
            catch (Exception ex)
            {
                _log.Error($"Unhandled exception in {task.Name}: {ex.Message}");
                ok = false;
            }

            task.Status = ok ? TaskStatus.Success : TaskStatus.Failed;
            task.StatusText = ok ? "Done" : $"Failed — check log";
            TaskCompleted?.Invoke(task, ok);
            results.Add(new SetupResult(task.Name, task.Status,
                ok ? null : $"See log at {_log.LogPath}"));

            _log.Info($"--- END: {task.Name} ({(ok ? "SUCCESS" : "FAILED")}) ---");
        }

        return results;
    }

    private async Task<bool> RunInstallAsync(SetupTask task, bool wingetAvailable, CancellationToken ct)
    {
        if (task.Method == InstallMethod.Winget && wingetAvailable)
            return await _retry.ExecuteAsync(
                t => _winget.InstallAsync(task, t), task.Name, task.RetryMax, ct);

        if (task.FallbackUrl is not null)
        {
            _log.Info($"Falling back to direct download for {task.Name}");
            return await _retry.ExecuteAsync(
                t => _direct.InstallAsync(task, t), task.Name, task.RetryMax, ct);
        }

        _log.Error($"No install method available for {task.Name}");
        return false;
    }
}

public record SetupResult(string Name, TaskStatus Status, string? ErrorDetail);
