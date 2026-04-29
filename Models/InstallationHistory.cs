using System;
using System.ComponentModel.DataAnnotations;

namespace WinZ.Models;

public class InstallationHistory
{
    [Key]
    public int Id { get; set; }
    public string TaskName { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string? ErrorDetail { get; set; }
}
