using System.Collections.Concurrent;

namespace TvTimeViewer.Services;

public class ProgressState
{
    public int Percent { get; set; }
    public string Label { get; set; } = "Starting...";
    public bool Completed { get; set; }
    public string? ResultMessage { get; set; }
}

public class ProgressTrackingService
{
    private readonly ConcurrentDictionary<string, ProgressState> _jobs = new();

    public string CreateJob()
    {
        var id = Guid.NewGuid().ToString();
        _jobs[id] = new ProgressState();
        return id;
    }

    public void Update(string id, int percent, string label)
    {
        if (_jobs.TryGetValue(id, out var state))
        {
            state.Percent = percent;
            state.Label = label;
        }
    }

    public void Complete(string id, string resultMessage)
    {
        if (_jobs.TryGetValue(id, out var state))
        {
            state.Percent = 100;
            state.Completed = true;
            state.ResultMessage = resultMessage;
        }
    }

    public void Fail(string id, string errorMessage)
    {
        if (_jobs.TryGetValue(id, out var state))
        {
            state.Completed = true;
            state.ResultMessage = $"Error: {errorMessage}";
        }
    }

    public ProgressState? Get(string id) => _jobs.TryGetValue(id, out var state) ? state : null;

    public void Remove(string id) => _jobs.TryRemove(id, out _);
}