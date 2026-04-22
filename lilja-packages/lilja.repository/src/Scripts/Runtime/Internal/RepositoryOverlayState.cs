#nullable enable
using System.Collections.Generic;

namespace Lilja.Repository.Internal
{
internal sealed class RepositoryOverlayState<TKey, TValue>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _committedState;
    private readonly Dictionary<TKey, TValue> _upserts;
    private readonly HashSet<TKey> _deletedKeys;

    public RepositoryOverlayState(Dictionary<TKey, TValue> committedState)
    {
        _committedState = committedState;
        _upserts = new Dictionary<TKey, TValue>();
        _deletedKeys = new HashSet<TKey>();
    }

    public bool ContainsKey(TKey key)
    {
        if (_upserts.ContainsKey(key))
        {
            return true;
        }

        if (_deletedKeys.Contains(key))
        {
            return false;
        }

        return _committedState.ContainsKey(key);
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        if (_upserts.TryGetValue(key, out value!))
        {
            return true;
        }

        if (_deletedKeys.Contains(key))
        {
            value = default!;
            return false;
        }

        return _committedState.TryGetValue(key, out value!);
    }

    public void Upsert(TKey key, TValue value)
    {
        _deletedKeys.Remove(key);
        _upserts[key] = value;
    }

    public void Delete(TKey key)
    {
        _upserts.Remove(key);
        _deletedKeys.Add(key);
    }

    public int Count()
    {
        var count = _committedState.Count;
        foreach (var deletedKey in _deletedKeys)
        {
            if (_committedState.ContainsKey(deletedKey))
            {
                count--;
            }
        }

        foreach (var pair in _upserts)
        {
            if (!_committedState.ContainsKey(pair.Key))
            {
                count++;
            }
        }

        return count;
    }

    public Dictionary<TKey, TValue> Materialize()
    {
        var materialized = new Dictionary<TKey, TValue>(_committedState);

        foreach (var deletedKey in _deletedKeys)
        {
            materialized.Remove(deletedKey);
        }

        foreach (var pair in _upserts)
        {
            materialized[pair.Key] = pair.Value;
        }

        return materialized;
    }
}
}
