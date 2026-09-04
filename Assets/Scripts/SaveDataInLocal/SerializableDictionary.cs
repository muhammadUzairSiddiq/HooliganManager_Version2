using System;
using System.Collections.Generic;

/// <summary>
/// A Dictionary wrapper that is serializable by Unity's JsonUtility.
/// Used for DestinationVisitCounts in PlayerData.
///
/// Usage:
///   var d = new SerializableDictionary&lt;string, int&gt;();
///   d.Set("East Docks", 1);
///   int count = d.GetOrDefault("East Docks", 0);
/// </summary>
[Serializable]
public class SerializableDictionary<TKey, TValue>
{
    [Serializable]
    private struct Pair
    {
        public TKey   key;
        public TValue value;
    }

    [UnityEngine.SerializeField]
    private List<Pair> _pairs = new List<Pair>();

    // ── Dictionary-like API ───────────────────────────────────────────────

    public bool ContainsKey(TKey key)
    {
        return _pairs.Exists(p => EqualityComparer<TKey>.Default.Equals(p.key, key));
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        foreach (var pair in _pairs)
        {
            if (EqualityComparer<TKey>.Default.Equals(pair.key, key))
            {
                value = pair.value;
                return true;
            }
        }
        value = default;
        return false;
    }

    public TValue GetOrDefault(TKey key, TValue defaultValue = default)
    {
        return TryGetValue(key, out var v) ? v : defaultValue;
    }

    public void Set(TKey key, TValue value)
    {
        for (int i = 0; i < _pairs.Count; i++)
        {
            if (EqualityComparer<TKey>.Default.Equals(_pairs[i].key, key))
            {
                _pairs[i] = new Pair { key = key, value = value };
                return;
            }
        }
        _pairs.Add(new Pair { key = key, value = value });
    }
}
