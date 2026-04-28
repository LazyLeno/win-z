using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace WinZ.Services;

public class LogService
{
    private readonly string _logPath;
    private readonly object _lock = new();

    public LogService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinZ", "Logs");
        Directory.CreateDirectory(dir);
        _logPath = Path.Combine(dir, $"setup_{DateTime.Now:yyyyMMdd_HHmmss}.log");
    }

    public event Action<string>? LineAppended;

    public void Info(string msg)  => Write("[INFO]", msg);
    public void Ok(string msg)    => Write("[OK]  ", msg);
    public void Error(string msg) => Write("[ERR] ", msg);
    public void Cmd(string msg)   => Write("[CMD] ", msg);

    private void Write(string level, string msg)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} {level} {msg}";
        lock (_lock) File.AppendAllText(_logPath, line + Environment.NewLine);
        LineAppended?.Invoke(line);
    }

    public string LogPath => _logPath;
}
