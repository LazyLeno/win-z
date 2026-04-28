using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace WinZ.Services;

public static class AsyncImageLoader
{
    private static readonly ConcurrentDictionary<string, WeakReference<BitmapImage>> _weakCache = new();
    private static readonly ConcurrentQueue<BitmapImage> _strongCache = new();
    private static readonly ConcurrentDictionary<string, BitmapImage> _downloading = new();
    private static readonly System.Threading.SemaphoreSlim _downloadSemaphore = new(3);
    private const int MaxStrongCache = 32;

    public static BitmapImage? GetImage(string url, Action<BitmapImage> onLoaded)
    {
        if (string.IsNullOrEmpty(url)) return null;

        if (_weakCache.TryGetValue(url, out var weak) && weak.TryGetTarget(out var existing))
            return existing;

        if (onLoaded == null) return null;

        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(async () =>
        {
            if (_weakCache.TryGetValue(url, out var w) && w.TryGetTarget(out var cached))
            {
                onLoaded(cached);
                return;
            }

            if (_downloading.ContainsKey(url)) return;

            try
            {
                await _downloadSemaphore.WaitAsync();
                
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(url);
                bitmap.DecodePixelWidth = 32;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                if (bitmap.IsDownloading)
                {
                    _downloading[url] = bitmap;
                    bitmap.DownloadCompleted += (s, e) =>
                    {
                        _downloading.TryRemove(url, out _);
                        _downloadSemaphore.Release();
                        FinalizeImage(url, bitmap, onLoaded);
                    };
                    bitmap.DownloadFailed += (s, e) => 
                    {
                        _downloading.TryRemove(url, out _);
                        _downloadSemaphore.Release();
                    };
                }
                else
                {
                    _downloadSemaphore.Release();
                    FinalizeImage(url, bitmap, onLoaded);
                }
            }
            catch (Exception)
            {
                _downloading.TryRemove(url, out _);
                if (_downloadSemaphore.CurrentCount < 3) _downloadSemaphore.Release();
            }
        }));

        return null;
    }

    private static void FinalizeImage(string url, BitmapImage bitmap, Action<BitmapImage> onLoaded)
    {
        try { bitmap.Freeze(); } catch { }
        _weakCache[url] = new WeakReference<BitmapImage>(bitmap);
        
        // LRU-ish strong cache
        _strongCache.Enqueue(bitmap);
        while (_strongCache.Count > MaxStrongCache)
            _strongCache.TryDequeue(out _);

        onLoaded(bitmap);
    }
}







