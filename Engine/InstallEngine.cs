using WinZ.Models;
using WinZ.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using TaskStatus = WinZ.Models.TaskStatus;

namespace WinZ.Engine;

public partial class InstallEngine
{
    private readonly LogService _log;
    private readonly WingetInstaller _winget;
    private readonly ScoopInstaller  _scoop;
    private readonly DirectDownloadInstaller _direct;
    private readonly TweakEngine _tweaks;
    private readonly DebloatEngine _debloat;
    private readonly RetryPolicy _retry;
    private SetupTask? _activeTask;

    private static readonly SemaphoreSlim _globalLock = new(1, 1);

    [System.Text.RegularExpressions.GeneratedRegex(@"\[(\d+)/(\d+)\]")]
    private static partial System.Text.RegularExpressions.Regex ProgressRegex();

    public event EventHandler<SetupTask>? TaskStarted;
    public event EventHandler<(SetupTask task, bool success)>? TaskCompleted;

    public InstallEngine(LogService log)
    {
        _log = log;
        _winget  = new WingetInstaller(log);
        _scoop   = new ScoopInstaller(log);
        _direct  = new DirectDownloadInstaller(log);
        _tweaks  = new TweakEngine(log);
        _debloat = new DebloatEngine(log);
        _retry   = new RetryPolicy(log);

        // Parse progress from logs (e.g. "[1/5]")
        _log.LineAppended += (line) =>
        {
            if (_activeTask == null) return;
            var match = ProgressRegex().Match(line);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int cur) && 
                                 int.TryParse(match.Groups[2].Value, out int total) && total > 0)
            {
                _activeTask.Progress = (int)((double)cur / total * 100);
            }
        };
    }

    public Task<List<SetupResult>> RunAsync(IEnumerable<SetupTask> tasks)
    {
        return RunAsync(tasks, false, CancellationToken.None);
    }

    public Task<List<SetupResult>> RunAsync(IEnumerable<SetupTask> tasks, bool forceDebloat)
    {
        return RunAsync(tasks, forceDebloat, CancellationToken.None);
    }

    public async Task<List<SetupResult>> RunAsync(
        IEnumerable<SetupTask> tasks,
        bool forceDebloat,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        await _globalLock.WaitAsync(ct);
        try
        {
            var results = new List<SetupResult>();
            bool wingetAvailable = await WingetInstaller.IsAvailableAsync();
            bool scoopAvailable  = await ScoopInstaller.IsAvailableAsync();

            if (!wingetAvailable)
                _log.Error("winget not found — install tasks will fall back to alternatives");
            
            if (!scoopAvailable && tasks.Any(t => t.IsSelected && t.Method == InstallMethod.Scoop))
                _log.Error("Scoop not found — tasks requiring Scoop will fail or fall back");

            bool explorerKilled = false;
            var selectedTasks = tasks.Where(t => t.IsSelected).ToList();
            var sessionHistory = new Stack<SetupTask>();

            foreach (var task in selectedTasks)
            {
                if (ct.IsCancellationRequested) 
                {
                    _log.Info("Cancellation detected. Initiating rollback...");
                    await RollbackAsync(sessionHistory, wingetAvailable, scoopAvailable);
                    break;
                }

                if (task.RequiresExplorerRestart && !explorerKilled)
                {
                    _log.Info("Killing Explorer for optimized batch processing...");
                    KillExplorer();
                    explorerKilled = true;
                }

                var result = await RunTaskWithHandlingAsync(task, forceDebloat, wingetAvailable, scoopAvailable, ct);
                if (result != null) 
                {
                    results.Add(result);
                    if (result.Status == TaskStatus.Success)
                        sessionHistory.Push(task);
                }
            }

            if (explorerKilled)
            {
                _log.Info("Restarting Explorer after batch complete.");
                StartExplorer();
            }

            return results;
        }
        finally
        {
            _globalLock.Release();
        }
    }

    private async Task RollbackAsync(Stack<SetupTask> history, bool winget, bool scoop)
    {
        _log.Warn("--- STARTING ROLLBACK ---");
        while (history.Count > 0)
        {
            var task = history.Pop();
            _log.Info($"Reverting: {task.DisplayName}");
            
            try
            {
                if (task.EffectiveType == TaskType.Install)
                {
                    await RunUninstallAsync(task, winget, scoop, CancellationToken.None);
                }
                // Tweaks and Debloats are more complex to revert without stored state,
                // so we focus on unrolling installs for now.
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to revert {task.Name}: {ex.Message}");
            }
        }
        _log.Ok("Rollback complete.");
    }

    private void KillExplorer()
    {
        try
        {
            foreach (var p in Process.GetProcessesByName("explorer"))
            {
                p.Kill();
                p.WaitForExit(2000);
            }
        }
        catch { /* best-effort */ }
    }

    private void StartExplorer()
    {
        try
        {
            Process.Start("explorer.exe");
        }
        catch { /* best-effort */ }
    }

    private async Task<SetupResult?> RunTaskWithHandlingAsync(SetupTask task, bool forceDebloat, bool wingetAvailable, bool scoopAvailable, CancellationToken ct)
    {
        _activeTask = task;
        task.Progress = 0;
        task.Status = TaskStatus.Running;
        TaskStarted?.Invoke(this, task);
        _log.Info(string.Format("--- BEGIN: {0} ---", task.DisplayName));

        bool ok = false;
        try
        {
            if (task.ShouldUninstallFirst)
            {
                _log.Info(string.Format("Reinstall flag detected for {0}. Performing pre-uninstall...", task.Name));
                await RunUninstallAsync(task, wingetAvailable, scoopAvailable, ct);
            }
            ok = await ExecuteTaskAsync(task, forceDebloat, wingetAvailable, scoopAvailable, ct);
        }
        catch (OperationCanceledException)
        {
            task.Status = TaskStatus.Skipped;
            return new SetupResult(task.Name, TaskStatus.Skipped, "Err.Cancelled", task.Id);
        }
        catch (Exception ex)
        {
            _log.Error(string.Format("Unhandled exception in {0}: {1}", task.Name, ex.Message));
            ok = false;
        }

        task.Status = ok ? TaskStatus.Success : TaskStatus.Failed;
        task.Progress = 100;
        _activeTask = null;
        TaskCompleted?.Invoke(this, (task, ok));
        
        var result = new SetupResult(task.DisplayName, task.Status, ok ? null : string.Format("See log at {0}", _log.LogPath), task.Id);
        _log.Info(string.Format("--- END: {0} ({1}) ---", task.DisplayName, ok ? "SUCCESS" : "FAILED"));
        
        return result;
    }

    private async Task<bool> ExecuteTaskAsync(SetupTask task, bool forceDebloat, bool wingetAvailable, bool scoopAvailable, CancellationToken ct)
    {
        return task.EffectiveType switch
        {
            TaskType.Install => await RunInstallAsync(task, wingetAvailable, scoopAvailable, ct),
            TaskType.Tweak   => await _retry.ExecuteAsync(t => _tweaks.ApplyAsync(task, t), task.DisplayName ?? "", task.RetryMax, ct),
            TaskType.Remove  => await _retry.ExecuteAsync(t => _debloat.ApplyAsync(task, forceDebloat, t), task.DisplayName ?? "", task.RetryMax, ct),
            _ => false
        };
    }

    private async Task<bool> RunInstallAsync(SetupTask task, bool wingetAvailable, bool scoopAvailable, CancellationToken ct)
    {
        bool success = false;

        // Try primary method
        if (task.EffectiveMethod == InstallMethod.Winget && wingetAvailable)
        {
            success = await _retry.ExecuteAsync(
                t => _winget.InstallAsync(task, task.ShouldUninstallFirst, t), task.DisplayName ?? "", task.RetryMax, ct);
        }
        else if (task.EffectiveMethod == InstallMethod.Scoop && scoopAvailable)
        {
            success = await _retry.ExecuteAsync(
                t => _scoop.InstallAsync(task, task.ShouldUninstallFirst, t), task.DisplayName ?? "", task.RetryMax, ct);
        }
        else if (task.EffectiveMethod == InstallMethod.DirectDownload && task.EffectiveFallbackUrl != null)
        {
            success = await _retry.ExecuteAsync(
                t => _direct.InstallAsync(task, t), task.DisplayName ?? "", task.RetryMax, ct);
        }

        // Automatic Fallback logic (between package managers only)
        if (!success)
        {
            // If Winget failed but Scoop is available and it's NOT already the primary method
            if (task.EffectiveMethod == InstallMethod.Winget && scoopAvailable)
            {
                _log.Warn(string.Format("Winget failed for {0}. Attempting fallback to Scoop...", task.DisplayName));
                success = await _retry.ExecuteAsync(
                    t => _scoop.InstallAsync(task, task.ShouldUninstallFirst, t), task.DisplayName ?? "", 1, ct);
            }
        }

        if (!success)
            _log.Error(string.Format("Failed to install {0} using available methods.", task.DisplayName));

        return success;
    }

    private async Task<bool> RunUninstallAsync(SetupTask task, bool wingetAvailable, bool scoopAvailable, CancellationToken ct)
    {
        if (task.EffectiveMethod == InstallMethod.Winget && wingetAvailable)
            return await _winget.UninstallAsync(task, ct);

        if (task.EffectiveMethod == InstallMethod.Scoop && scoopAvailable)
            return await _scoop.UninstallAsync(task, ct);

        return true;
    }
}

public record SetupResult(string Name, TaskStatus Status, string? ErrorDetail, string? TaskId = null);

