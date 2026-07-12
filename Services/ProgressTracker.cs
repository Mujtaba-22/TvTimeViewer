using System.Collections.Concurrent;

namespace TvTimeViewer.Services;

public static class ProgressTracker
{
    private static readonly ConcurrentDictionary<string, (int Percent, string Message)> _progress = new();

    public static void Set(string key, int percent, string message)
    {
        _progress[key] = (percent, message);
    }

    public static (int Percent, string Message) Get(string key)
    {
        return _progress.TryGetValue(key, out var value) ? value : (0, "Starting...");
    }

    public static void Reset(string key)
    {
        _progress[key] = (0, "Starting...");
    }

    public static void Complete(string key, string message)
    {
        _progress[key] = (100, message);
    }
}