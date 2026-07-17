namespace KAnimGui.Core.Collections;

/// <summary>
/// Small bounded least-recently-used cache for deterministic, memory-safe UI caches.
/// </summary>
public sealed class BoundedLruCache<TKey, TValue> where TKey : notnull
{
    private readonly int capacity;
    private readonly Dictionary<TKey, LinkedListNode<Entry>> entries = new();
    private readonly LinkedList<Entry> usage = new();

    public BoundedLruCache(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Cache capacity must be positive.");
        }

        this.capacity = capacity;
    }

    public int Count => entries.Count;

    public bool TryGet(TKey key, out TValue value)
    {
        if (!entries.TryGetValue(key, out var node))
        {
            value = default!;
            return false;
        }

        usage.Remove(node);
        usage.AddFirst(node);
        value = node.Value.Value;
        return true;
    }

    public void Set(TKey key, TValue value)
    {
        if (entries.TryGetValue(key, out var existing))
        {
            existing.Value = new Entry(key, value);
            usage.Remove(existing);
            usage.AddFirst(existing);
            return;
        }

        var node = usage.AddFirst(new Entry(key, value));
        entries[key] = node;

        if (entries.Count <= capacity)
        {
            return;
        }

        var leastRecentlyUsed = usage.Last!;
        usage.RemoveLast();
        entries.Remove(leastRecentlyUsed.Value.Key);
    }

    public void Clear()
    {
        entries.Clear();
        usage.Clear();
    }

    private readonly record struct Entry(TKey Key, TValue Value);
}
