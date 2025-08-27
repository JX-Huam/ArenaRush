using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;

public class NetworkDictionary<TKey, TValue> : NetworkVariable<NetworkDictionaryEvent<TKey, TValue>>
{
    private readonly Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();

    public NetworkDictionary(NetworkVariableReadPermission readPerm = NetworkVariableReadPermission.Everyone, 
                            NetworkVariableWritePermission writePerm = NetworkVariableWritePermission.Server)
        : base(new NetworkDictionaryEvent<TKey, TValue>
        {
            Operation = NetworkDictionaryEvent<TKey, TValue>.EventType.Clear
        }, readPerm, writePerm)
    {
    }

    public int Count => dictionary.Count;

    public Dictionary<TKey, TValue>.KeyCollection Keys => dictionary.Keys;

    public Dictionary<TKey, TValue>.ValueCollection Values => dictionary.Values;

    public TValue this[TKey key]
    {
        get => dictionary[key];
        set
        {
            dictionary[key] = value;
            Value = new NetworkDictionaryEvent<TKey, TValue>
            {
                Operation = NetworkDictionaryEvent<TKey, TValue>.EventType.Add,
                Key = key,
                Value = value
            };
        }
    }

    public bool ContainsKey(TKey key) => dictionary.ContainsKey(key);

    public bool Remove(TKey key)
    {
        bool removed = dictionary.Remove(key);
        if (removed)
        {
            Value = new NetworkDictionaryEvent<TKey, TValue>
            {
                Operation = NetworkDictionaryEvent<TKey, TValue>.EventType.Remove,
                Key = key
            };
        }
        return removed;
    }

    public void Clear()
    {
        dictionary.Clear();
        Value = new NetworkDictionaryEvent<TKey, TValue>
        {
            Operation = NetworkDictionaryEvent<TKey, TValue>.EventType.Clear
        };
    }

    public new void OnValueChanged(NetworkDictionaryEvent<TKey, TValue> previousValue, NetworkDictionaryEvent<TKey, TValue> newValue)
    {
        switch (newValue.Operation)
        {
            case NetworkDictionaryEvent<TKey, TValue>.EventType.Add:
                dictionary[newValue.Key] = newValue.Value;
                break;
            case NetworkDictionaryEvent<TKey, TValue>.EventType.Remove:
                dictionary.Remove(newValue.Key);
                break;
            case NetworkDictionaryEvent<TKey, TValue>.EventType.Clear:
                dictionary.Clear();
                break;
        }
    }

    public List<TKey> GetKeys()
    {
        return new List<TKey>(dictionary.Keys);
    }
}

public struct NetworkDictionaryEvent<TKey, TValue>
{
    public enum EventType : byte
    {
        Add,
        Remove,
        Clear
    }

    public EventType Operation;
    public TKey Key;
    public TValue Value;
}