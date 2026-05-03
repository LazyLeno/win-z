using System;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinZ.Services;

/// <summary>
/// High-performance asynchronous image loader with memory-efficient decoding and caching.
/// Designed to maintain sub-20MB RAM targets by freezing bitmaps and using DecodePixelWidth.
/// </summary>
public static class AsyncImageLoader
{
    private static readonly ConcurrentDictionary<string, ImageSource?> _cache = new();

    public static ImageSource? GetIcon(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_cache.TryGetValue(id, out var cached)) return cached;

        try
        {
            // pack://application:,,,/ refers to resources compiled with <Resource> in .csproj
            var uri = new Uri($"pack://application:,,,/Assets/Icons/{id}.png");

            // We check for resource existence to avoid first-chance exceptions in debuggers
            try 
            { 
                Application.GetResourceStream(uri); 
            }
            catch 
            { 
                _cache.TryAdd(id, null); 
                return null; 
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            
            // CORE OPTIMIZATION: Decode at exact display size to save massive heap space.
            // A 256x256 icon takes ~256KB RAM. A 24x24 icon takes ~2.2KB RAM.
            bitmap.DecodePixelWidth = 24; 
            
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            
            // CRITICAL: Freezing makes the image read-only and thread-safe.
            // It allows the WPF render thread to access the pixels without synchronization overhead.
            bitmap.Freeze();

            _cache.TryAdd(id, bitmap);
            return bitmap;
        }
        catch
        {
            _cache.TryAdd(id, null);
            return null;
        }
    }
}
