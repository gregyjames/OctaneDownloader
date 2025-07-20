using System;
using System.Collections.Concurrent;

namespace OctaneEngineCore.Clients;

internal class KeyedObjectPool<T> where T : class, IDisposable
{
    private readonly ConcurrentDictionary<string, T> _items = new();
    private readonly Func<T> _objectGenerator;

    public KeyedObjectPool(Func<T> objectGenerator)
    {
        _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
    }

    public T Rent(string key)
    {
        var client = _items.GetOrAdd(key, _objectGenerator.Invoke());
        return client;
    }

    public void Return(string name, T item)
    {
        _items.AddOrUpdate(name, item, (_, existing) => existing ?? item);
    }

    public int Count => _items.Count;

    public void Clear()
    {
        foreach (var key in _items.Keys)
            Remove(key);
    }

    public void Remove(string key)
    {
        if (_items.TryRemove(key, out var item))
        {
            item.Dispose();
        }
    }
}