namespace Lpb.Visualizer;

internal sealed class ExperimentStore(TimeProvider clock) : IDisposable
{
    internal const int MaximumResults = 4;
    internal static readonly TimeSpan IdleLifetime = TimeSpan.FromMinutes(15);
    private sealed record Entry(Guid Id, Experiment Experiment, DateTimeOffset LastAccess);
    private readonly Dictionary<Guid, Entry> entries = new();
    private readonly object sync = new();
    private int operation;

    public bool TryBeginOperation() => Interlocked.CompareExchange(ref operation, 1, 0) == 0;
    public void EndOperation() => Volatile.Write(ref operation, 0);

    public void Put(Guid owner, Guid id, Experiment experiment)
    {
        lock (sync)
        {
            SweepLocked();
            if (!entries.ContainsKey(owner) && entries.Count == MaximumResults)
                entries.Remove(entries.MinBy(pair => pair.Value.LastAccess).Key);
            entries[owner] = new(id, experiment, clock.GetUtcNow());
        }
    }

    public Experiment? Get(Guid owner, Guid id)
    {
        lock (sync)
        {
            SweepLocked();
            return entries.TryGetValue(owner, out var entry) && entry.Id == id ? entry.Experiment : null;
        }
    }

    public void Touch(Guid owner, Guid id)
    {
        lock (sync)
            if (entries.TryGetValue(owner, out var entry) && entry.Id == id)
                entries[owner] = entry with { LastAccess = clock.GetUtcNow() };
    }

    public bool Delete(Guid owner, Guid id)
    {
        lock (sync)
        {
            SweepLocked();
            return entries.TryGetValue(owner, out var entry) && entry.Id == id && entries.Remove(owner);
        }
    }

    public void Sweep() { lock (sync) SweepLocked(); }
    private void SweepLocked()
    {
        var now = clock.GetUtcNow();
        foreach (var owner in entries.Where(pair => now - pair.Value.LastAccess >= IdleLifetime).Select(pair => pair.Key).ToArray())
            entries.Remove(owner);
    }

    internal int Count { get { lock (sync) return entries.Count; } }
    public void Dispose() { lock (sync) entries.Clear(); }
}

internal sealed class ExperimentCleanup(ExperimentStore store, TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken)) store.Sweep();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
