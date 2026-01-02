using System.Collections.Concurrent;

namespace tgm.Api.Abstractions.ConcurrectHashSet;

public class ConcurrentHashSet<T> : ConcurrentDictionary<T, byte>, IEnumerable<T>
    where T : notnull
{
    public ConcurrentHashSet() : base() { }
    public ConcurrentHashSet(IEqualityComparer<T> comparer) : base(comparer) { }

    public bool TryAdd(T item) => TryAdd(item, 0);
    public bool TryRemove(T item) => TryRemove(item, out _);
    public new bool ContainsKey(T item) => base.ContainsKey(item);


    IEnumerator<T> IEnumerable<T>.GetEnumerator() => Keys.GetEnumerator();
}
