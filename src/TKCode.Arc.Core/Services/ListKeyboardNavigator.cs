using Avalonia.Input;

namespace TKCode.Arc.Primitives;

/// <summary>Direction of list navigation.</summary>
public enum NavigationOrientation
{
    Vertical,
    Horizontal
}

/// <summary>
/// S9: one tab stop with arrow-key navigation inside composite widgets. Arrows move (optionally wrapping)
/// and skip disabled items; Home / End jump to the ends; PageUp / PageDown move by <see cref="PageSize"/>;
/// printable characters jump to the next item whose label starts with the typed prefix (500ms buffer).
/// </summary>
public sealed class ListKeyboardNavigator
{
    private readonly Func<int> _count;
    private readonly Func<int, bool> _isEnabled;
    private readonly Func<int, string?>? _label;
    private string _typeAhead = string.Empty;
    private DateTime _lastTyped;

    public ListKeyboardNavigator(Func<int> count, Func<int, bool> isEnabled, Func<int, string?>? label = null)
    {
        _count = count;
        _isEnabled = isEnabled;
        _label = label;
    }

    public bool Wrap { get; set; } = true;

    public int PageSize { get; set; } = 8;

    /// <summary>Number of columns for grid navigation (1 = list).</summary>
    public int Columns { get; set; } = 1;

    public NavigationOrientation Orientation { get; set; } = NavigationOrientation.Vertical;

    /// <summary>Returns the new index for <paramref name="key"/>, or null when the key is not handled.</summary>
    public int? Navigate(int current, Key key)
    {
        var count = _count();
        if (count == 0)
        {
            return null;
        }
        var vertical = Orientation == NavigationOrientation.Vertical;
        return key switch
        {
            Key.Down when Columns > 1 => Move(current, Columns),
            Key.Up when Columns > 1 => Move(current, -Columns),
            Key.Right when Columns > 1 => Move(current, 1),
            Key.Left when Columns > 1 => Move(current, -1),
            Key.Down when vertical => Move(current, 1),
            Key.Up when vertical => Move(current, -1),
            Key.Right when !vertical => Move(current, 1),
            Key.Left when !vertical => Move(current, -1),
            Key.Home => FirstEnabled(0, 1),
            Key.End => FirstEnabled(count - 1, -1),
            Key.PageDown => Clamp(current, PageSize),
            Key.PageUp => Clamp(current, -PageSize),
            _ => null
        };
    }

    /// <summary>Handles a typed character; returns the index of the next matching item.</summary>
    public int? TypeAhead(int current, string text)
    {
        if (_label is null || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var now = DateTime.UtcNow;
        _typeAhead = (now - _lastTyped).TotalMilliseconds > 500 ? text : _typeAhead + text;
        _lastTyped = now;
        var count = _count();
        for (var step = 1; step <= count; step++)
        {
            var index = (current + step) % count;
            if (_isEnabled(index) && (_label(index)?.StartsWith(_typeAhead, StringComparison.CurrentCultureIgnoreCase) ?? false))
            {
                return index;
            }
        }
        return null;
    }

    private int? Move(int current, int delta)
    {
        var count = _count();
        var index = current;
        for (var i = 0; i < count; i++)
        {
            index += delta;
            if (index < 0 || index >= count)
            {
                if (!Wrap)
                {
                    return current;
                }
                index = ((index % count) + count) % count;
            }
            if (_isEnabled(index))
            {
                return index;
            }
        }
        return current;
    }

    private int? Clamp(int current, int delta)
    {
        var count = _count();
        var target = Math.Clamp(current + delta, 0, count - 1);
        return FirstEnabled(target, delta > 0 ? -1 : 1) ?? current;
    }

    private int? FirstEnabled(int start, int step)
    {
        var count = _count();
        for (var index = start; index >= 0 && index < count; index += step)
        {
            if (_isEnabled(index))
            {
                return index;
            }
        }
        return null;
    }
}
