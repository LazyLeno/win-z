using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WinZ.Engine;
using WinZ.Models;

namespace WinZ.ViewModels;

public class SummaryViewModel
{
    public ObservableCollection<SummaryItem> Items { get; } = new();

    public int Succeeded { get; private set; }
    public int Failed    { get; private set; }
    public int Skipped   { get; private set; }
    public bool HasFailures => Failed > 0;

    public void Load(IEnumerable<SetupResult> results)
    {
        Items.Clear();
        foreach (var r in results)
        {
            Items.Add(new SummaryItem(r));
            switch (r.Status)
            {
                case TaskStatus.Success: Succeeded++; break;
                case TaskStatus.Failed:  Failed++;    break;
                case TaskStatus.Skipped: Skipped++;   break;
            }
        }
    }
}

public record SummaryItem(SetupResult Result)
{
    public string Name        => Result.Name;
    public TaskStatus Status  => Result.Status;
    public string? ErrorDetail => Result.ErrorDetail;
    public string StatusLabel => Result.Status switch
    {
        TaskStatus.Success => "Done",
        TaskStatus.Failed  => "Failed",
        TaskStatus.Skipped => "Skipped",
        _                  => ""
    };
    public string StatusIcon => Result.Status switch
    {
        TaskStatus.Success => "✓",
        TaskStatus.Failed  => "✗",
        TaskStatus.Skipped => "–",
        _                  => ""
    };
}
