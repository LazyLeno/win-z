using System.ComponentModel.DataAnnotations;

namespace WinZ.Models;

public class AppMetadata
{
    [Key]
    public string Key { get; set; } = "";
    public string? Value { get; set; }
}
