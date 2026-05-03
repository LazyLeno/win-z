using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WinZ.Engine;
using WinZ.Models;
using WinZ.Services;
using ModelTaskStatus = WinZ.Models.TaskStatus;

namespace WinZ.ViewModels;

public class SummaryViewModel
{
    public ObservableCollection<SummaryItem> Items { get; } = new();

    public int Succeeded { get; private set; }
    public int Failed    { get; private set; }
    public int Skipped   { get; private set; }
    public bool HasFailures => Failed > 0;

    public void Load(IEnumerable<SetupResult>? results)
    {
        if (results == null) return;

        Items.Clear();
        Succeeded = 0;
        Failed    = 0;
        Skipped   = 0;

        var sorted = results
            .Where(r => r != null)
            .OrderBy(r => r.Status == ModelTaskStatus.Success ? 0 : 1)
            .ThenBy(r => r.Name);

        foreach (var r in sorted)
        {
            Items.Add(new SummaryItem(r));
            switch (r.Status)
            {
                case ModelTaskStatus.Success: Succeeded++; break;
                case ModelTaskStatus.Failed:  Failed++;    break;
                case ModelTaskStatus.Skipped: Skipped++;   break;
                default: break;
            }
        }

        if (Failed == 0 && Succeeded > 0)
        {
            _ = Task.Run(async () =>
            {
                var autoCleanup = await DataService.Instance.GetSettingAsync("AutoCleanup") == "True";
                if (autoCleanup)
                {
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo(
                            "cmd.exe", "/c del /q /s %temp%\\* & winget cache clean --force")
                        {
                            CreateNoWindow = true,
                            WindowStyle    = System.Diagnostics.ProcessWindowStyle.Hidden
                        };
                        System.Diagnostics.Process.Start(psi)?.WaitForExit();
                    }
                    catch { }
                }
            });
        }
    }
}

public record SummaryItem(SetupResult Result)
{
    public string?          TaskId      => Result?.TaskId;
    public string           Name        => Result?.Name ?? "Unknown";
    public ModelTaskStatus  Status      => Result?.Status ?? ModelTaskStatus.Skipped;
    public string?          ErrorDetail => Result?.ErrorDetail;

    public string StatusLabel => Result?.Status switch
    {
        ModelTaskStatus.Success => "Sum.DoneItem",
        ModelTaskStatus.Failed  => "Sum.FailedItem",
        ModelTaskStatus.Skipped => "Sum.SkippedItem",
        _                       => ""
    };

    public string StatusIcon => Result?.Status switch
    {
        ModelTaskStatus.Success => "✓",
        ModelTaskStatus.Failed  => "✗",
        ModelTaskStatus.Skipped => "–",
        _                       => ""
    };
}
