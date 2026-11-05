using Lpb.Visualizer;
using NUnit.Framework;

namespace Lpb.Visualizer.Tests;

internal sealed class TestClock : TimeProvider
{
    private DateTimeOffset now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan amount) => now += amount;
}

public class ExperimentStoreTests
{
    [Test]
    public void ReplacementIsOwnedAndStaleDeletionCannotRemoveNewResult()
    {
        using var store = new ExperimentStore(new TestClock());
        var owner = Guid.NewGuid(); var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var experiment = Fixtures.Run();
        store.Put(owner, first, experiment);
        Assert.That(store.Get(Guid.NewGuid(), first), Is.Null);
        store.Put(owner, second, experiment);
        Assert.That(store.Get(owner, first), Is.Null);
        Assert.That(store.Delete(owner, first), Is.False);
        Assert.That(store.Get(owner, second), Is.SameAs(experiment));
        Assert.That(store.Count, Is.EqualTo(1));
    }

    [Test]
    public void FourEntryLimitEvictsLeastRecentlyUsedAndTouchExtendsIdleLife()
    {
        var clock = new TestClock(); using var store = new ExperimentStore(clock);
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var experiment = Fixtures.Run();
        foreach (var id in ids.Take(4)) { store.Put(id, id, experiment); clock.Advance(TimeSpan.FromMinutes(1)); }
        store.Touch(ids[0], ids[0]);
        store.Put(ids[4], ids[4], experiment);
        Assert.That(store.Count, Is.EqualTo(4));
        Assert.That(store.Get(ids[1], ids[1]), Is.Null);
        Assert.That(store.Get(ids[0], ids[0]), Is.SameAs(experiment));
        clock.Advance(TimeSpan.FromMinutes(15)); store.Sweep();
        Assert.That(store.Count, Is.Zero);
    }

    [Test]
    public void ExpiryIsEnforcedOnLookupAndDisposeDropsReferences()
    {
        var clock = new TestClock(); using var store = new ExperimentStore(clock);
        var id = Guid.NewGuid(); store.Put(id, id, Fixtures.Run());
        clock.Advance(ExperimentStore.IdleLifetime);
        Assert.That(store.Get(id, id), Is.Null);
        store.Put(id, id, Fixtures.Run()); store.Dispose();
        Assert.That(store.Count, Is.Zero);
    }

    [Test]
    public void OperationGateRejectsInsteadOfQueuing()
    {
        using var store = new ExperimentStore(new TestClock());
        Assert.That(store.TryBeginOperation(), Is.True);
        Assert.That(store.TryBeginOperation(), Is.False);
        store.EndOperation();
        Assert.That(store.TryBeginOperation(), Is.True); store.EndOperation();
    }
}
