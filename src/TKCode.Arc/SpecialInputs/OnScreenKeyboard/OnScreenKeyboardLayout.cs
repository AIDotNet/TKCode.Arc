using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A keyboard layout for <see cref="OnScreenKeyboard"/>: one or more pages (letters, symbols, ...), each a list of rows
/// of keys. The built-in layouts are instances of it (<see cref="Create"/>); add your own, in XAML or code, for other
/// languages or for special keypads. A keyboard follows changes made to the layout while it is shown.
/// </summary>
public partial class OnScreenKeyboardLayout
{
    /// <summary>Creates an empty layout.</summary>
    public OnScreenKeyboardLayout()
    {
        _ = new ModelChildren<OnScreenKeyboardPage>(Pages, static (page, handler, add) =>
        {
            if (add)
            {
                page.Changed += handler;
            }
            else
            {
                page.Changed -= handler;
            }
        }, OnChildChanged);
    }

    /// <summary>A name for the layout, such as "QWERTY" or "German"; it is not shown.</summary>
    public string? Name { get; set; }

    /// <summary>The pages; the first one is shown unless <see cref="OnScreenKeyboard.CurrentPage"/> names another.</summary>
    [Content]
    public AvaloniaList<OnScreenKeyboardPage> Pages { get; } = new();

    /// <summary>Raised when a page, row or key of the layout changes; the argument is the key that changed, or null for a change of structure.</summary>
    internal event Action<OnScreenKeyboardKey?>? Changed;

    /// <summary>The page called <paramref name="name"/>, or null.</summary>
    public OnScreenKeyboardPage? FindPage(string? name)
    {
        if (name is null)
        {
            return null;
        }
        foreach (var page in Pages)
        {
            if (string.Equals(page.Name, name, StringComparison.Ordinal))
            {
                return page;
            }
        }
        return null;
    }

    /// <summary>Every key of every page, in order (useful to rename or disable keys of a built-in layout).</summary>
    public IEnumerable<OnScreenKeyboardKey> AllKeys() => Pages.SelectMany(p => p.Rows).SelectMany(r => r.Keys);

    private void OnChildChanged(OnScreenKeyboardKey? key) => Changed?.Invoke(key);

    public override string ToString() => Name ?? nameof(OnScreenKeyboardLayout);
}

/// <summary>One page of an <see cref="OnScreenKeyboardLayout"/>, such as the letters or the symbols: rows of keys.</summary>
public class OnScreenKeyboardPage
{
    /// <summary>Creates an empty page.</summary>
    public OnScreenKeyboardPage()
    {
        _ = new ModelChildren<OnScreenKeyboardRow>(Rows, static (row, handler, add) =>
        {
            if (add)
            {
                row.Changed += handler;
            }
            else
            {
                row.Changed -= handler;
            }
        }, key => Changed?.Invoke(key));
    }

    /// <summary>Creates a page called <paramref name="name"/> with the given rows.</summary>
    public OnScreenKeyboardPage(string? name, params OnScreenKeyboardRow[] rows)
        : this()
    {
        Name = name;
        Rows.AddRange(rows);
    }

    /// <summary>The page's name, which <see cref="OnScreenKeyboardKey.TargetPage"/> of a page key refers to.</summary>
    public string? Name { get; set; }

    /// <summary>What screen readers announce when the page is switched to, such as "Symbols"; null announces nothing.</summary>
    public string? Label { get; set; }

    /// <summary>The rows, top to bottom.</summary>
    [Content]
    public AvaloniaList<OnScreenKeyboardRow> Rows { get; } = new();

    internal event Action<OnScreenKeyboardKey?>? Changed;

    public override string ToString() => Name ?? nameof(OnScreenKeyboardPage);
}

/// <summary>One row of keys. Rows are centred, so a row with fewer key units than the widest row is indented on both sides.</summary>
public class OnScreenKeyboardRow
{
    /// <summary>Creates an empty row.</summary>
    public OnScreenKeyboardRow()
    {
        _ = new ModelChildren<OnScreenKeyboardKey>(Keys, static (key, handler, add) =>
        {
            if (add)
            {
                key.Changed += handler;
            }
            else
            {
                key.Changed -= handler;
            }
        }, key => Changed?.Invoke(key));
    }

    /// <summary>Creates a row of the given keys.</summary>
    public OnScreenKeyboardRow(params OnScreenKeyboardKey[] keys)
        : this()
    {
        Keys.AddRange(keys);
    }

    /// <summary>The keys, left to right.</summary>
    [Content]
    public AvaloniaList<OnScreenKeyboardKey> Keys { get; } = new();

    internal event Action<OnScreenKeyboardKey?>? Changed;

    /// <summary>
    /// A row with one text key for each character of <paramref name="characters"/> (each user-perceived character, so a
    /// letter with a combining accent stays one key), for example <c>FromCharacters("qwertyuiop")</c>.
    /// </summary>
    public static OnScreenKeyboardRow FromCharacters(string characters)
    {
        ArgumentNullException.ThrowIfNull(characters);
        var row = new OnScreenKeyboardRow();
        var elements = StringInfo.GetTextElementEnumerator(characters);
        while (elements.MoveNext())
        {
            row.Keys.Add(new OnScreenKeyboardKey((string)elements.Current));
        }
        return row;
    }
}

/// <summary>
/// One key of an <see cref="OnScreenKeyboardLayout"/>: what it does (<see cref="Kind"/>), what it types
/// (<see cref="Text"/>, <see cref="ShiftText"/>), how it looks (<see cref="Label"/>, <see cref="Glyph"/>,
/// <see cref="Width"/>) and how screen readers name it. Its properties can be bound; the keyboard follows changes.
/// </summary>
public class OnScreenKeyboardKey : AvaloniaObject
{
    /// <summary>What the key does: type <see cref="Text"/>, a special key (Backspace, Enter, arrows...), Shift, switch page, and so on.</summary>
    public static readonly StyledProperty<OnScreenKeyboardKeyKind> KindProperty =
        AvaloniaProperty.Register<OnScreenKeyboardKey, OnScreenKeyboardKeyKind>(nameof(Kind));

    /// <summary>The text a <c>Text</c> key types (one character or more). For a <c>Custom</c> key it identifies the key in <see cref="OnScreenKeyboard.ItemInvoked"/>.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<OnScreenKeyboardKey, string?>(nameof(Text));

    /// <summary>The text typed while Shift or Caps lock is on; null types <see cref="Text"/> in upper case (in the keyboard's culture).</summary>
    public static readonly StyledProperty<string?> ShiftTextProperty =
        AvaloniaProperty.Register<OnScreenKeyboardKey, string?>(nameof(ShiftText));

    /// <summary>The text shown on the key; null shows what the key types (upper case while shifted), or nothing for keys drawn with a glyph.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<OnScreenKeyboardKey, string?>(nameof(Label));

    /// <summary>A 24×24 stroked glyph (Lucide path data) drawn instead of the label; null uses the kind's own glyph (Backspace, Enter, Shift, arrows...).</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<OnScreenKeyboardKey, Geometry?>(nameof(Glyph));

    /// <summary>The key's width relative to a standard key (1). A row's keys share the keyboard's width by these units.</summary>
    public static readonly StyledProperty<double> WidthProperty =
        AvaloniaProperty.Register<OnScreenKeyboardKey, double>(nameof(Width), 1d, coerce: static (_, value) => double.IsFinite(value) && value > 0 ? value : 0);

    /// <summary>The <see cref="OnScreenKeyboardPage.Name"/> a <c>Page</c> key switches to.</summary>
    public static readonly StyledProperty<string?> TargetPageProperty =
        AvaloniaProperty.Register<OnScreenKeyboardKey, string?>(nameof(TargetPage));

    /// <summary>The key a <c>Key</c> key sends as a key press (for example <c>Escape</c>, <c>Delete</c> or <c>Home</c>).</summary>
    public static readonly StyledProperty<Key> KeyCodeProperty =
        AvaloniaProperty.Register<OnScreenKeyboardKey, Key>(nameof(KeyCode));

    /// <summary>Whether holding the key repeats it; null repeats Backspace and the arrow keys only.</summary>
    public static readonly StyledProperty<bool?> IsRepeatableProperty =
        AvaloniaProperty.Register<OnScreenKeyboardKey, bool?>(nameof(IsRepeatable));

    /// <summary>The name screen readers use; null uses the label or the kind's name ("Backspace", "Shift", "Enter"...).</summary>
    public static readonly StyledProperty<string?> AutomationNameProperty =
        AvaloniaProperty.Register<OnScreenKeyboardKey, string?>(nameof(AutomationName));

    /// <summary>Whether the key can be pressed. A disabled key is dimmed and does nothing.</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<OnScreenKeyboardKey, bool>(nameof(IsEnabled), true);

    /// <summary>Whether the key is shown. A hidden key takes no room; the rest of its row closes up.</summary>
    public static readonly StyledProperty<bool> IsVisibleProperty =
        AvaloniaProperty.Register<OnScreenKeyboardKey, bool>(nameof(IsVisible), true);

    /// <summary>Creates a text key with no text.</summary>
    public OnScreenKeyboardKey()
    {
    }

    /// <summary>Creates a key that types <paramref name="text"/>.</summary>
    public OnScreenKeyboardKey(string text)
    {
        Text = text;
    }

    /// <summary>Creates a key of <paramref name="kind"/>, <paramref name="width"/> units wide.</summary>
    public OnScreenKeyboardKey(OnScreenKeyboardKeyKind kind, double width = 1)
    {
        Kind = kind;
        Width = width;
    }

    /// <inheritdoc cref="KindProperty"/>
    public OnScreenKeyboardKeyKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="ShiftTextProperty"/>
    public string? ShiftText { get => GetValue(ShiftTextProperty); set => SetValue(ShiftTextProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <inheritdoc cref="WidthProperty"/>
    public double Width { get => GetValue(WidthProperty); set => SetValue(WidthProperty, value); }

    /// <inheritdoc cref="TargetPageProperty"/>
    public string? TargetPage { get => GetValue(TargetPageProperty); set => SetValue(TargetPageProperty, value); }

    /// <inheritdoc cref="KeyCodeProperty"/>
    public Key KeyCode { get => GetValue(KeyCodeProperty); set => SetValue(KeyCodeProperty, value); }

    /// <inheritdoc cref="IsRepeatableProperty"/>
    public bool? IsRepeatable { get => GetValue(IsRepeatableProperty); set => SetValue(IsRepeatableProperty, value); }

    /// <inheritdoc cref="AutomationNameProperty"/>
    public string? AutomationName { get => GetValue(AutomationNameProperty); set => SetValue(AutomationNameProperty, value); }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    /// <inheritdoc cref="IsVisibleProperty"/>
    public bool IsVisible { get => GetValue(IsVisibleProperty); set => SetValue(IsVisibleProperty, value); }

    /// <summary>Whether holding the key repeats it (<see cref="IsRepeatable"/>, or its kind's default).</summary>
    internal bool RepeatsOnHold => IsRepeatable ?? Kind is OnScreenKeyboardKeyKind.Backspace or OnScreenKeyboardKeyKind.Left
        or OnScreenKeyboardKeyKind.Right or OnScreenKeyboardKeyKind.Up or OnScreenKeyboardKeyKind.Down;

    internal event Action<OnScreenKeyboardKey?>? Changed;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this);
    }

    public override string ToString() => Label ?? Text ?? Kind.ToString();
}

/// <summary>
/// Keeps the change subscriptions of a model collection: children added to the collection are subscribed,
/// removed ones unsubscribed, and every change (of a child, or of the collection itself) is reported upwards.
/// </summary>
internal sealed class ModelChildren<T> where T : class
{
    private readonly List<T> _subscribed = new();
    private readonly Action<T, Action<OnScreenKeyboardKey?>, bool> _subscribe;
    private readonly Action<OnScreenKeyboardKey?> _report;

    public ModelChildren(AvaloniaList<T> items, Action<T, Action<OnScreenKeyboardKey?>, bool> subscribe, Action<OnScreenKeyboardKey?> report)
    {
        _subscribe = subscribe;
        _report = report;
        items.CollectionChanged += (_, e) => OnCollectionChanged(items, e);
    }

    private void OnChildChanged(OnScreenKeyboardKey? key) => _report(key);

    private void OnCollectionChanged(AvaloniaList<T> items, NotifyCollectionChangedEventArgs e)
    {
        // Resubscribe from the current contents: a reset carries no old items, and a child can appear more than once.
        foreach (var old in _subscribed)
        {
            _subscribe(old, OnChildChanged, false);
        }
        _subscribed.Clear();
        foreach (var item in items)
        {
            if (item is not null)
            {
                _subscribe(item, OnChildChanged, true);
                _subscribed.Add(item);
            }
        }
        _report(null);
    }
}
