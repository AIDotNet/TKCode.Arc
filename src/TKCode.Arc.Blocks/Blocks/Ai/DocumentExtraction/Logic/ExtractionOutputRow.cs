using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace TKCode.Arc;

/// <summary>
/// A row of a table in an <see cref="ExtractionOutput"/>: the cells by column key, enumerated in column order (a
/// <see cref="Dictionary{TKey,TValue}"/> does not promise an order, and the JSON follows the schema's).
/// </summary>
internal sealed class ExtractionOutputRow : IReadOnlyDictionary<string, object?>
{
    private readonly string[] _keys;
    private readonly object?[] _values;

    /// <summary>Creates a row; <paramref name="values"/> holds one value per key, in the same order.</summary>
    public ExtractionOutputRow(string[] keys, object?[] values)
    {
        if (keys.Length != values.Length)
        {
            throw new ArgumentException("One value per key", nameof(values));
        }
        _keys = keys;
        _values = values;
    }

    /// <inheritdoc />
    public int Count => _keys.Length;

    /// <inheritdoc />
    public IEnumerable<string> Keys => _keys;

    /// <inheritdoc />
    public IEnumerable<object?> Values => _values;

    /// <inheritdoc />
    public object? this[string key] => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException(key);

    /// <inheritdoc />
    public bool ContainsKey(string key) => Array.IndexOf(_keys, key) >= 0;

    /// <inheritdoc />
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out object? value)
    {
        var index = Array.IndexOf(_keys, key);
        value = index >= 0 ? _values[index] : null;
        return index >= 0;
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        for (var i = 0; i < _keys.Length; i++)
        {
            yield return new KeyValuePair<string, object?>(_keys[i], _values[i]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
