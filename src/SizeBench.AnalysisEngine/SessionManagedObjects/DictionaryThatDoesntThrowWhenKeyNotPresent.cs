using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace SizeBench.AnalysisEngine;

// This type isn't really interesting to test, it's almost all boilerplate, so just exclude from
// coverage numbers.
[ExcludeFromCodeCoverage]
public sealed class DictionaryThatDoesntThrowWhenKeyNotPresent<TValue> : INotifyCollectionChanged,
                                                                         IDictionary<string, TValue>,
                                                                         IReadOnlyDictionary<string, TValue>
    where TValue : notnull
{
    public DictionaryThatDoesntThrowWhenKeyNotPresent()
    {
        // Default to Ordinal comparisons since that's performant for most cases
        this._dictionary = new Dictionary<string, TValue>(StringComparer.Ordinal);
    }

    public DictionaryThatDoesntThrowWhenKeyNotPresent(IEqualityComparer<string> comparer)
    {
        this._dictionary = new Dictionary<string, TValue>(comparer);
    }

    public DictionaryThatDoesntThrowWhenKeyNotPresent(IDictionary<string, TValue> source)
    {
        this._dictionary = new Dictionary<string, TValue>(source, StringComparer.Ordinal);
    }

    public DictionaryThatDoesntThrowWhenKeyNotPresent(IDictionary<string, TValue> source, IEqualityComparer<string> comparer)
    {
        this._dictionary = new Dictionary<string, TValue>(source, comparer);
    }

    private readonly Dictionary<string, TValue> _dictionary;

    public TValue this[string key]
    {
        get
        {
            if (this._dictionary.TryGetValue(key, out var value))
            {
                return value;
            }
            else
            {
#pragma warning disable CS8603, CS8653 // A default expression introduces a null value for a type parameter.  This type's whole purpose is to gracefully fallback when a key isn't present, so it does the best it can.
                return default;
            }
#pragma warning restore CS8603, CS8653
        }

        set => this._dictionary[key] = value ?? throw new ArgumentNullException(nameof(value));
    }

    public int Count => this._dictionary.Count;

    public bool IsReadOnly => ((IDictionary)this._dictionary).IsReadOnly;

    public ICollection<string> Keys => this._dictionary.Keys;

    public ICollection<TValue> Values => this._dictionary.Values;

    IEnumerable<string> IReadOnlyDictionary<string, TValue>.Keys => ((IReadOnlyDictionary<string, TValue>)this._dictionary).Keys;

    IEnumerable<TValue> IReadOnlyDictionary<string, TValue>.Values => ((IReadOnlyDictionary<string, TValue>)this._dictionary).Values;

    public void Add(KeyValuePair<string, TValue> item)
        => this._dictionary.Add(item.Key, item.Value);

    public void Add(string key, TValue value)
        => this._dictionary.Add(key, value);

    public void Clear()
        => this._dictionary.Clear();

    public bool Contains(KeyValuePair<string, TValue> item)
        => ((IDictionary)this._dictionary).Contains(item);

    public bool ContainsKey(string key)
        => this._dictionary.ContainsKey(key);

    public void CopyTo(KeyValuePair<string, TValue>[] array, int arrayIndex)
        => ((IDictionary)this._dictionary).CopyTo(array, arrayIndex);

    public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator()
        => this._dictionary.GetEnumerator();

    public bool Remove(KeyValuePair<string, TValue> item)
        => ((IDictionary<string, TValue>)this._dictionary).Remove(item);

    public bool Remove(string key)
        => this._dictionary.Remove(key);

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out TValue value)
        => this._dictionary.TryGetValue(key, out value);

    IEnumerator IEnumerable.GetEnumerator()
        => this._dictionary.GetEnumerator();

    #region INotifyCollectionChanged 

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    internal void RaiseCollectionChangedReset()
        => CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

    #endregion
}
