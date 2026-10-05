namespace HPXPirate.Services;

/// <summary>A time-windowed ring buffer of (timestamp, value) samples.</summary>
public sealed class HistoryBuffer
{
    private readonly Queue<(DateTime t, double v)> _q = new();
    private readonly TimeSpan _maxAge;

    public HistoryBuffer(TimeSpan maxAge) => _maxAge = maxAge;

    public void Add(double value) => Add(DateTime.Now, value);

    public void Add(DateTime t, double value)
    {
        _q.Enqueue((t, value));
        while (_q.Count > 0 && t - _q.Peek().t > _maxAge) _q.Dequeue();
    }

    public IReadOnlyList<(DateTime t, double v)> Snapshot(TimeSpan window)
    {
        var cutoff = DateTime.Now - window;
        return _q.Where(x => x.t >= cutoff).ToList();
    }

    public double Last => _q.Count > 0 ? _q.Last().v : 0;
    public void Clear() => _q.Clear();
}
