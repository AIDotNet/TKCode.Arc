// Ported from vscode (src/vs/editor/common/core/characterClassifier.ts, commit b57d5c9e), MIT license.

namespace TKCode.Arc.Editor;

/// <summary>A fast character classifier: a full table for char codes below 256 and a sparse map for the rest.</summary>
internal class CharacterClassifier
{
    private readonly byte[] _asciiMap = new byte[256];
    private readonly Dictionary<int, byte> _map = new();
    private readonly byte _defaultValue;

    public CharacterClassifier(byte defaultValue)
    {
        _defaultValue = defaultValue;
        Array.Fill(_asciiMap, defaultValue);
    }

    public void Set(int charCode, byte value)
    {
        if (charCode >= 0 && charCode < 256)
        {
            _asciiMap[charCode] = value;
        }
        else
        {
            _map[charCode] = value;
        }
    }

    public byte Get(int charCode)
    {
        if (charCode >= 0 && charCode < 256)
        {
            return _asciiMap[charCode];
        }
        return _map.TryGetValue(charCode, out var value) ? value : _defaultValue;
    }

    public void Clear()
    {
        Array.Fill(_asciiMap, _defaultValue);
        _map.Clear();
    }
}

/// <summary>A set of char codes.</summary>
internal sealed class CharacterSet
{
    private readonly CharacterClassifier _actual = new(0);

    public void Add(int charCode) => _actual.Set(charCode, 1);

    public bool Has(int charCode) => _actual.Get(charCode) == 1;

    public void Clear() => _actual.Clear();
}
