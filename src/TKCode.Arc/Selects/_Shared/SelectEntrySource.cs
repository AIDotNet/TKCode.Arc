using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>One option of a select list, resolved once from its source item (text, value, flags, search key).</summary>
internal sealed class SelectEntry
{
    private string? _search;

    public int Index { get; init; }
    public object? Item { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? Meta { get; init; }
    public object? Icon { get; init; }
    public string? Group { get; init; }
    public string? Keywords { get; init; }
    public bool IsDisabled { get; init; }

    /// <summary>Label, meta and keywords lowercased once (Arc matches all three).</summary>
    public string SearchText => _search ??= string.Join(' ', Text, Meta ?? string.Empty, Keywords ?? string.Empty)
                                                  .ToLower(CultureInfo.CurrentCulture);
}

/// <summary>
/// Projects a <see cref="ItemsControl"/>'s items (data objects, <see cref="SelectOption"/> records or inline
/// <see cref="SelectItem"/> declarations) into <see cref="SelectEntry"/> rows. Rebuilt lazily after a collection
/// or selector change; filtering works on these entries, so no container is ever created per option.
/// </summary>
internal sealed class SelectEntrySource
{
    private readonly ItemsControl _owner;
    private readonly Func<Func<object?, string?>?> _textSelector;
    private readonly Func<Func<object?, bool>?> _disabledSelector;
    private readonly List<SelectEntry> _entries = new();
    private readonly List<SelectItem> _watched = new();
    private SelectBindingEvaluator? _evaluator;
    private bool _dirty = true;

    public SelectEntrySource(ItemsControl owner, Func<Func<object?, string?>?> textSelector, Func<Func<object?, bool>?> disabledSelector)
    {
        _owner = owner;
        _textSelector = textSelector;
        _disabledSelector = disabledSelector;
        owner.ItemsView.CollectionChanged += OnCollectionChanged;
    }

    /// <summary>Raised after the entries were invalidated (items, selectors or option data changed).</summary>
    public event EventHandler? Changed;

    public int Count
    {
        get
        {
            Ensure();
            return _entries.Count;
        }
    }

    public SelectEntry this[int index]
    {
        get
        {
            Ensure();
            return _entries[index];
        }
    }

    public IReadOnlyList<SelectEntry> Entries
    {
        get
        {
            Ensure();
            return _entries;
        }
    }

    /// <summary>Label of the entry at <paramref name="index"/>, or null when out of range.</summary>
    public string? TextAt(int index) => index >= 0 && index < Count ? _entries[index].Text : null;

    public bool IsEnabledAt(int index) => index >= 0 && index < Count && !_entries[index].IsDisabled;

    public void Invalidate()
    {
        _dirty = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Invalidate();

    private void OnOptionChanged(object? sender, EventArgs e) => Invalidate();

    private void Ensure()
    {
        if (!_dirty)
        {
            return;
        }
        _dirty = false;
        foreach (var item in _watched)
        {
            item.OptionChanged -= OnOptionChanged;
        }
        _watched.Clear();
        _entries.Clear();
        var view = _owner.ItemsView;
        var textSelector = _textSelector();
        var disabledSelector = _disabledSelector();
        var display = _owner.DisplayMemberBinding;
        for (var i = 0; i < view.Count; i++)
        {
            var item = view[i];
            _entries.Add(Create(i, item, textSelector, disabledSelector, display));
            if (item is SelectItem declared)
            {
                declared.OptionChanged += OnOptionChanged;
                _watched.Add(declared);
            }
        }
        _evaluator?.Clear();
    }

    private SelectEntry Create(int index, object? item, Func<object?, string?>? textSelector, Func<object?, bool>? disabledSelector,
                               BindingBase? display)
    {
        var disabled = disabledSelector?.Invoke(item) ?? false;
        switch (item)
        {
            case SelectItem declared:
                return new SelectEntry
                {
                    Index = index,
                    Item = item,
                    Text = textSelector?.Invoke(item) ?? TextOf(declared.Content),
                    Meta = declared.Meta,
                    Icon = declared.Icon,
                    Group = declared.Group,
                    Keywords = declared.Keywords,
                    IsDisabled = disabled || !declared.IsEnabled
                };
            case SelectOption option:
                return new SelectEntry
                {
                    Index = index,
                    Item = item,
                    Text = textSelector?.Invoke(item) ?? option.Label,
                    Meta = option.Meta,
                    Icon = option.Icon,
                    Group = option.Group,
                    Keywords = option.Keywords,
                    IsDisabled = disabled || option.IsDisabled
                };
        }
        string text;
        if (textSelector is not null)
        {
            text = textSelector(item) ?? string.Empty;
        }
        else if (display is not null)
        {
            _evaluator ??= new SelectBindingEvaluator();
            text = TextOf(_evaluator.Evaluate(display, item));
        }
        else
        {
            text = TextOf(item);
        }
        return new SelectEntry { Index = index, Item = item, Text = text, IsDisabled = disabled };
    }

    internal static string TextOf(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        TextBlock block => block.Text ?? string.Empty,
        IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture),
        _ => value.ToString() ?? string.Empty
    };

    /// <summary>Stored value of a source item: the selector, a declared or record value, or the item itself.</summary>
    internal static object? ValueOf(object? item, Func<object?, object?>? selector) => selector is not null
        ? selector(item)
        : item switch
        {
            SelectItem declared => declared.Value ?? declared.Content,
            SelectOption option => option.Value,
            _ => item
        };

    /// <summary>
    /// Fills <paramref name="into"/> with the indices of entries matching <paramref name="query"/> (trimmed,
    /// case-insensitive substring of label, meta and keywords), or with every index for an empty query.
    /// A custom <paramref name="predicate"/> receives the source item and the trimmed query.
    /// </summary>
    public void Filter(string? query, Func<object?, string, bool>? predicate, List<int> into)
    {
        Ensure();
        into.Clear();
        var trimmed = query?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                into.Add(i);
            }
            return;
        }
        var needle = trimmed.ToLower(CultureInfo.CurrentCulture);
        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            var match = predicate is not null
                ? predicate(entry.Item, trimmed)
                : entry.SearchText.Contains(needle, StringComparison.Ordinal);
            if (match)
            {
                into.Add(i);
            }
        }
    }
}

/// <summary>Evaluates a display binding against arbitrary items (Avalonia keeps its own evaluator internal).</summary>
internal sealed class SelectBindingEvaluator : StyledElement
{
    public static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<SelectBindingEvaluator, object?>("Value");

    private BindingBase? _binding;
    private IDisposable? _expression;

    public object? Evaluate(BindingBase binding, object? item)
    {
        if (!ReferenceEquals(binding, _binding))
        {
            _expression?.Dispose();
            _binding = binding;
            _expression = Bind(ValueProperty, binding);
        }
        DataContext = item;
        return GetValue(ValueProperty);
    }

    public void Clear() => DataContext = null;
}

/// <summary>
/// Default <c>SelectedValueBinding</c> of the selects: maps an item to its stored value (value selector,
/// <see cref="SelectItem.Value"/>, <see cref="SelectOption.Value"/>, or the item). A path-less compiled binding,
/// so it stays AOT and trimming safe.
/// </summary>
internal sealed class SelectValueConverter : IValueConverter
{
    private readonly Func<Func<object?, object?>?> _selector;

    private SelectValueConverter(Func<Func<object?, object?>?> selector) => _selector = selector;

    public static CompiledBinding CreateBinding(Func<Func<object?, object?>?> selector) =>
        new(new CompiledBindingPath()) { Converter = new SelectValueConverter(selector) };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        SelectEntrySource.ValueOf(value, _selector());

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}

/// <summary>
/// Radix-style typeahead: characters typed within the reset window build a prefix; repeating one letter cycles
/// through the options starting with it; the search starts after the current match and wraps.
/// </summary>
internal sealed class SelectTypeahead
{
    private string _buffer = string.Empty;
    private long _lastAt;

    public SelectTypeahead(int resetMilliseconds) => ResetMilliseconds = resetMilliseconds;

    public int ResetMilliseconds { get; }

    /// <summary>True while a search is being typed (Space then belongs to the search, not to selection).</summary>
    public bool IsTyping => _buffer.Length > 0 && Environment.TickCount64 - _lastAt < ResetMilliseconds;

    public void Reset() => _buffer = string.Empty;

    /// <summary>Appends <paramref name="text"/> and returns the index of the next match, or -1.</summary>
    public int Next(string text, int current, int count, Func<int, string?> label, Func<int, bool> isEnabled)
    {
        var now = Environment.TickCount64;
        _buffer = now - _lastAt > ResetMilliseconds ? text : _buffer + text;
        _lastAt = now;
        if (count == 0 || _buffer.Length == 0)
        {
            return -1;
        }
        var repeated = _buffer.Length > 1 && _buffer.All(c => char.ToLowerInvariant(c) == char.ToLowerInvariant(_buffer[0]));
        var search = repeated ? _buffer[..1] : _buffer;
        var excludeCurrent = search.Length == 1;
        var start = Math.Max(current, 0);
        for (var step = 0; step < count; step++)
        {
            var index = (start + step) % count;
            if (excludeCurrent && index == current)
            {
                continue;
            }
            if (isEnabled(index) && (label(index)?.StartsWith(search, StringComparison.CurrentCultureIgnoreCase) ?? false))
            {
                return index == current ? -1 : index;
            }
        }
        return -1;
    }
}
