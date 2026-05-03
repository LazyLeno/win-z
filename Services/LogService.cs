using System;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace WinZ.Services;

public class LogService : IDisposable
{
    private readonly string _logPath;
    private readonly Channel<string> _logChannel;
    private readonly Task _processTask;
    private bool _disposed;

    public LogService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinZ", "Logs");
        Directory.CreateDirectory(dir);
        _logPath = Path.Combine(dir, $"setup_{DateTime.Now:yyyyMMdd_HHmmss}.log");

        // Use a bounded channel to prevent memory bloat if writing is very slow
        _logChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(1000)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        _processTask = Task.Run(ProcessLogsAsync);
    }

    public event Action<string>? LineAppended;

    public void Info(string msg)  => Write("[INFO]", msg);
    public void Ok(string msg)    => Write("[OK]  ", msg);
    public void Warn(string msg)  => Write("[WARN]", msg);
    public void Error(string msg) => Write("[ERR] ", msg);
    public void Cmd(string msg)   => Write("[CMD] ", msg);

    private void Write(string level, string msg)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} {level} {msg}";
        _logChannel.Writer.TryWrite(line);
        LineAppended?.Invoke(line);
    }

    private async Task ProcessLogsAsync()
    {
        try
        {
            // Use FileStream with shared access for better performance
            using var sw = new StreamWriter(new FileStream(_logPath, FileMode.Append, FileAccess.Write, FileShare.Read));
            sw.AutoFlush = true;

            await foreach (var line in _logChannel.Reader.ReadAllAsync())
            {
                await sw.WriteLineAsync(line);
            }
        }
        catch (Exception)
        {
            // Fallback or silent ignore for logging errors
        }
    }

    public string LogPath => _logPath;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _logChannel.Writer.Complete();
        try { _processTask.Wait(1000); } catch { }
        GC.SuppressFinalize(this);
    }
}
