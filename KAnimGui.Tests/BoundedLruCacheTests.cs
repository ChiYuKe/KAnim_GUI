using KAnimGui.Core.Collections;

namespace KAnimGui.Tests;

public sealed class BoundedLruCacheTests
{
    [Fact]
    public void Set_EvictsLeastRecentlyUsedEntry()
    {
        var cache = new BoundedLruCache<string, int>(2);
        cache.Set("a", 1);
        cache.Set("b", 2);
        Assert.True(cache.TryGet("a", out _));

        cache.Set("c", 3);

        Assert.True(cache.TryGet("a", out var value));
        Assert.Equal(1, value);
        Assert.False(cache.TryGet("b", out _));
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void Set_UpdatesExistingEntryWithoutGrowingCache()
    {
        var cache = new BoundedLruCache<string, int>(2);
        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.Set("a", 4);

        Assert.True(cache.TryGet("a", out var value));
        Assert.Equal(4, value);
        Assert.Equal(2, cache.Count);
    }
}
