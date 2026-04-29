using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WinZ.Engine;
using WinZ.Models;
using System;

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
        Failed = 0;
        Skipped = 0;

        var sorted = results
            .Where(r => r != null)
            .OrderBy(r => r.Status == TaskStatus.Success ? 0 : 1) // Successful on top
            .ThenBy(r => r.Name);

        foreach (var r in sorted)
        {
            Items.Add(new SummaryItem(r));
            switch (r.Status)
            {
                case TaskStatus.Success: Succeeded++; break;
                case TaskStatus.Failed:  Failed++;    break;
                case TaskStatus.Skipped: Skipped++;   break;
                default: break;
            }
        }
    }
}

public record SummaryItem(SetupResult Result)
{
    public string Name        => Result?.Name ?? "Unknown";
    public TaskStatus Status  => Result?.Status ?? TaskStatus.Skipped;
    public string? ErrorDetail => Result?.ErrorDetail;
    public string StatusLabel => Result?.Status switch
    {
        TaskStatus.Success => "Done",
        TaskStatus.Failed  => "Failed",
        TaskStatus.Skipped => "Skipped",
        _                  => ""
    };
    public string StatusIcon => Result?.Status switch
    {
        TaskStatus.Success => "✓",
        TaskStatus.Failed  => "✗",
        TaskStatus.Skipped => "–",
        _                  => ""
    };
}

