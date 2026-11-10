using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One item of a <see cref="CatchUp"/>: a button that opens it (avatar or group glyph, title, detail, where and when, an
/// unread dot) and, beside it, a small button that marks it read or unread.
/// </summary>
internal sealed class CatchUpRow : TemplatedControl
{
    public static readonly StyledProperty<bool> IsUnreadProperty =
        AvaloniaProperty.Register<CatchUpRow, bool>(nameof(IsUnread));

    private CatchUpOpenButton? _open;
    private AvaloniaButton? _toggle;
    private ArcGlyph? _toggleGlyph;
    private CatchUpLabels? _labels;

    public CatchUpRow(CatchUpItem item)
    {
        Item = item;
    }

    public CatchUpItem Item { get; }

    public bool IsUnread { get => GetValue(IsUnreadProperty); set => SetValue(IsUnreadProperty, value); }

    public string Title => Item.Title;

    public string? Detail => Item.Detail;

    public string? Author => Item.Author;

    public string? Meta => string.Join(" · ", new[] { Item.Source, Item.Time }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public Geometry Glyph => Item.Kind switch
    {
        CatchUpKind.Mention => ArcIcons.AtSign,
        CatchUpKind.Decision => ArcIcons.Flag,
        _ => ArcIcons.Sparkles,
    };

    internal CatchUpOpenButton? OpenButton => _open;

    internal AvaloniaButton? ToggleButton => _toggle;

    public event EventHandler? Open;

    public event EventHandler? Toggle;

    public void FocusOpen() => _open?.Focus(NavigationMethod.Directional);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_open is not null)
        {
            _open.Click -= OnOpenClick;
        }
        if (_toggle is not null)
        {
            _toggle.Click -= OnToggleClick;
        }
        _open = e.NameScope.Find<CatchUpOpenButton>("PART_Open");
        _toggle = e.NameScope.Find<AvaloniaButton>("PART_Toggle");
        _toggleGlyph = e.NameScope.Find<ArcGlyph>("PART_ToggleGlyph");
        if (_open is not null)
        {
            _open.Title = Title;
            _open.Detail = Detail;
            _open.Author = Author;
            _open.Meta = Meta;
            _open.Glyph = Glyph;
            _open.Click += OnOpenClick;
        }
        if (_toggle is not null)
        {
            _toggle.Click += OnToggleClick;
        }
        PseudoClasses.Set(":unread", IsUnread);
        UpdateGlyph();
        UpdateNames(_labels ?? new CatchUpLabels());
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsUnreadProperty)
        {
            PseudoClasses.Set(":unread", IsUnread);
            UpdateGlyph();
        }
    }

    private void UpdateGlyph()
    {
        if (_open is not null)
        {
            _open.IsUnread = IsUnread;
        }
        if (_toggleGlyph is not null)
        {
            _toggleGlyph.Data = IsUnread ? ArcIcons.Check : ArcIcons.CircleDot;
        }
    }

    internal void UpdateNames(CatchUpLabels labels)
    {
        _labels = labels;
        if (_open is not null)
        {
            var name = IsUnread ? $"{Item.Title}, {labels.Unread}" : Item.Title;
            AutomationProperties.SetName(_open, name);
            AutomationProperties.SetHelpText(_open, string.Join(". ", new[] { Item.Detail, Item.Author, Meta }.Where(s => !string.IsNullOrWhiteSpace(s))));
        }
        if (_toggle is not null)
        {
            AutomationProperties.SetName(_toggle, IsUnread ? labels.MarkRead(Item.Title) : labels.MarkUnread(Item.Title));
        }
    }

    private void OnOpenClick(object? sender, RoutedEventArgs e) => Open?.Invoke(this, EventArgs.Empty);

    private void OnToggleClick(object? sender, RoutedEventArgs e) => Toggle?.Invoke(this, EventArgs.Empty);
}

/// <summary>The pressable face of a <see cref="CatchUpRow"/>, with a focus ring built on first keyboard focus.</summary>
internal sealed class CatchUpOpenButton : AvaloniaButton
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<CatchUpOpenButton, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<CatchUpOpenButton, string?>(nameof(Detail));

    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<CatchUpOpenButton, string?>(nameof(Meta));

    public static readonly StyledProperty<string?> AuthorProperty =
        AvaloniaProperty.Register<CatchUpOpenButton, string?>(nameof(Author));

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<CatchUpOpenButton, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<bool> IsUnreadProperty =
        AvaloniaProperty.Register<CatchUpOpenButton, bool>(nameof(IsUnread));

    private LazyFocusRing? _focusRing;

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public bool IsUnread { get => GetValue(IsUnreadProperty); set => SetValue(IsUnreadProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsUnreadProperty)
        {
            PseudoClasses.Set(":unread", IsUnread);
        }
    }

    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    public string? Author { get => GetValue(AuthorProperty); set => SetValue(AuthorProperty, value); }

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    protected override Type StyleKeyOverride => typeof(CatchUpOpenButton);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, CatchUpTokens.Key(CatchUpTokenKind.FocusMargin), ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep14));
        _focusRing.Attach(e.NameScope.Find<Panel>("PART_Root"));
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }
}

/// <summary>The recap at the top of a <see cref="CatchUp"/>: a sparkle and the written summary, or a double check and "all caught up".</summary>
internal sealed class CatchUpRecap : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<CatchUpRecap, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsCaughtUpProperty =
        AvaloniaProperty.Register<CatchUpRecap, bool>(nameof(IsCaughtUp));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public bool IsCaughtUp { get => GetValue(IsCaughtUpProperty); set => SetValue(IsCaughtUpProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCaughtUpProperty)
        {
            PseudoClasses.Set(":caught-up", IsCaughtUp);
        }
        else if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, Text);
        }
    }
}
