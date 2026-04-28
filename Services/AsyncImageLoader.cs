using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace WinZ.Services;

public static class AsyncImageLoader
{
    private static readonly ConcurrentDictionary<string, BitmapImage> _cache = new();
    private static readonly HttpClient _httpClient = new();

    static AsyncImageLoader()
    {
        _httpClient.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    }

    public static BitmapImage? GetImage(string url, Action<BitmapImage> onLoaded)
    {
        if (string.IsNullOrEmpty(url)) return null;

        if (_cache.TryGetValue(url, out var existing))
            return existing;

        // Start background download
        Task.Run(async () =>
        {
            try
            {
                var data = await _httpClient.GetByteArrayAsync(url);
                
                // Jump back to UI thread to create the BitmapImage
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    using var ms = new MemoryStream(data);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = ms;
                    bitmap.DecodePixelWidth = 48;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    _cache[url] = bitmap;
                    onLoaded(bitmap);
                });
            }
            catch { /* Fallback to null/placeholder */ }
        });

        return null; // Return null immediately while loading
    }
}
