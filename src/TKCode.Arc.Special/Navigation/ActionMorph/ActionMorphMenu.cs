using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One row of the quick-action menu: glyph, name, optional description and the shortcut's key cap. The menu's gliding
/// highlight marks the active row (under the pointer or keyboard focus), so the row draws no focus ring of its own.
/// </summary>
[PseudoClasses(ActivePseudoClass)]
internal sealed class ActionMorphMenuItem : ActionMorphButtonBase
{
    internal const string ActivePseudoClass = ":active";

    /// <summary>The row's glyph.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<ActionMorphMenuItem, Geometry?>(nameof(Glyph));

    /// <summary>The row's name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ActionMorphMenuItem, string?>(nameof(Label));

    /// <summary>The line under the name.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ActionMorphMenuItem, string?>(nameof(Description));

    /// <summary>The shortcut letter, shown upper case in the key cap.</summary>
    public static readonly StyledProperty<string?> ShortcutProperty =
        AvaloniaProperty.Register<ActionMorphMenuItem, string?>(nameof(Shortcut));

    private readonly TranslateTransform _lift = new();
    private MotionValue<double>? _y;
    private MotionValue<double>? _opacity;

    public ActionMorphMenuItem(ActionMorphAction action)
    {
        Action = action;
        Glyph = action.Glyph;
        Label = action.Label;
        Description = action.Description;
        Shortcut = action.Shortcut?.Trim().ToUpperInvariant() is { Length: > 0 } key ? key : null;
        RenderTransform = _lift;
        AutomationProperties.SetName(this, action.Label);
        AutomationProperties.SetHelpText(this, action.Description);
        if (Shortcut is { } accelerator)
        {
            AutomationProperties.SetAcceleratorKey(this, accelerator);
        }
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.MenuItem);
    }

    public ActionMorphAction Action { get; }

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    public string? Shortcut { get => GetValue(ShortcutProperty); set => SetValue(ShortcutProperty, value); }

    /// <summary>Whether the highlight is on this row.</summary>
    public bool IsActive
    {
        get => PseudoClasses.Contains(ActivePseudoClass);
        set => PseudoClasses.Set(ActivePseudoClass, value);
    }

    protected override bool HasFocusRing => false;

    /// <summary>Plays the row's staggered entrance (or shows it at once).</summary>
    public void Reveal(int index, bool animate, Visual owner)
    {
        _y ??= CreateY(owner);
        _opacity ??= CreateOpacity(owner);
        if (!animate)
        {
            _y.Jump(0);
            _opacity.Jump(1);
            return;
        }
        // Arc Action morph (design reference): rows fade in over 200 ms on the enter curve and rise 8 px on a 0.38 s
        // spring with a little bounce, 95 ms after the face starts and 35 ms apart.
        var delay = TimeSpan.FromMilliseconds(95 + 35 * index);
        _y.Jump(8);
        _opacity.Jump(0);
        _ = _y.AnimateTo(0, MotionSpec.Spring(ActionMorphMenu.RowSpring, delay));
        _ = _opacity.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(200), ArcEasings.Enter, delay));
    }

    private MotionValue<double> CreateY(Visual owner)
    {
        var value = new MotionValue<double>(owner, 0, MotionVectors.Double);
        value.Changed += v => _lift.Y = v;
        return value;
    }

    private MotionValue<double> CreateOpacity(Visual owner)
    {
        var value = new MotionValue<double>(owner, 1, MotionVectors.Double);
        value.Changed += v => Opacity = v;
        return value;
    }
}

/// <summary>
/// The quick-action menu face: the title (the morph's label) with a close button, then one row per action over a
/// highlight that glides to the active row.
/// </summary>
[TemplatePart(HeaderPart, typeof(Control))]
[TemplatePart(ClosePart, typeof(ActionMorphIconButton))]
[TemplatePart(RowsPart, typeof(Panel))]
[TemplatePart(ItemsPart, typeof(Panel))]
[TemplatePart(HighlightPart, typeof(GlidingIndicator))]
internal sealed class ActionMorphMenu : TemplatedControl, IActionMorphFace
{
    internal const string HeaderPart = "PART_Header";
    internal const string ClosePart = "PART_Close";
    internal const string RowsPart = "PART_Rows";
    internal const string ItemsPart = "PART_Items";
    internal const string HighlightPart = "PART_Highlight";

    /// <summary>The menu's title.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ActionMorphMenu, string?>(nameof(Title));

    /// <summary>The close button's accessible name.</summary>
    public static readonly StyledProperty<string?> CloseLabelProperty =
        AvaloniaProperty.Register<ActionMorphMenu, string?>(nameof(CloseLabel));

    // Arc Action morph (design reference).
    internal static readonly SpringSpec RowSpring = SpringSpec.FromVisual(0.38, 0.05);
    private static readonly SpringSpec HighlightSpring = SpringSpec.FromVisual(0.28, 0);
    private static readonly TimeSpan HeaderFade = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan HeaderDelay = TimeSpan.FromMilliseconds(60);

    private readonly ActionMorph _owner;
    private readonly List<ActionMorphMenuItem> _rows = new();
    private Control? _header;
    private ActionMorphIconButton? _close;
    private Panel? _rowsPanel;
    private Panel? _items;
    private GlidingIndicator? _highlight;
    private MotionValue<double>? _headerOpacity;
    private ActionMorphMenuItem? _active;
    private bool _highlightPending;

    public ActionMorphMenu(ActionMorph owner)
    {
        _owner = owner;
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Menu);
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
    }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? CloseLabel { get => GetValue(CloseLabelProperty); set => SetValue(CloseLabelProperty, value); }

    internal IReadOnlyList<ActionMorphMenuItem> Rows => _rows;

    internal ActionMorphMenuItem? ActiveRow => _active;

    internal GlidingIndicator? Highlight => _highlight;

    internal ActionMorphIconButton? CloseButton => _close;

    /// <summary>Builds one row per action.</summary>
    public void SetActions(IEnumerable<ActionMorphAction> actions)
    {
        foreach (var row in _rows)
        {
            row.Click -= OnRowClick;
            row.PointerEntered -= OnRowPointerEntered;
            row.GotFocus -= OnRowGotFocus;
        }
        _rows.Clear();
        _items?.Children.Clear();
        _active = null;
        foreach (var action in actions)
        {
            var row = new ActionMorphMenuItem(action);
            row.Click += OnRowClick;
            row.PointerEntered += OnRowPointerEntered;
            row.GotFocus += OnRowGotFocus;
            _rows.Add(row);
            _items?.Children.Add(row);
        }
    }

    /// <summary>The row of <paramref name="action"/>, if any.</summary>
    public ActionMorphMenuItem? RowOf(ActionMorphAction? action) => _rows.FirstOrDefault(r => ReferenceEquals(r.Action, action));

    /// <summary>Moves the highlight to <paramref name="row"/> (null hides it).</summary>
    public void SetActive(ActionMorphMenuItem? row, bool animate = true)
    {
        if (_active is not null && !ReferenceEquals(_active, row))
        {
            _active.IsActive = false;
        }
        _active = row;
        if (row is null)
        {
            _highlight?.Hide(animate);
            return;
        }
        row.IsActive = true;
        PlaceHighlight(animate);
    }

    void IActionMorphFace.OnEntering(bool animate)
    {
        // The highlight starts on the first row, as the focus does.
        SetActive(_rows.Count > 0 ? _rows[0] : null, animate: false);
        _highlightPending = true;
        _headerOpacity ??= CreateHeaderOpacity();
        if (animate)
        {
            _headerOpacity.Jump(0);
            _ = _headerOpacity.AnimateTo(1, MotionSpec.Tween(HeaderFade, ArcEasings.Enter, HeaderDelay));
        }
        else
        {
            _headerOpacity.Jump(1);
        }
        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].Reveal(i, animate, _owner);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_close is not null)
        {
            _close.Click -= OnCloseClick;
        }
        if (_rowsPanel is not null)
        {
            _rowsPanel.PointerExited -= OnRowsPointerExited;
        }
        _header = e.NameScope.Find<Control>(HeaderPart);
        _close = e.NameScope.Find<ActionMorphIconButton>(ClosePart);
        _rowsPanel = e.NameScope.Find<Panel>(RowsPart);
        _items = e.NameScope.Find<Panel>(ItemsPart);
        _highlight = e.NameScope.Find<GlidingIndicator>(HighlightPart);
        if (_close is not null)
        {
            _close.Click += OnCloseClick;
        }
        if (_rowsPanel is not null)
        {
            _rowsPanel.PointerExited += OnRowsPointerExited;
        }
        if (_items is not null)
        {
            _items.Children.Clear();
            foreach (var row in _rows)
            {
                _items.Children.Add(row);
            }
        }
        if (_header is not null && _headerOpacity is not null)
        {
            _header.Opacity = _headerOpacity.Value;
        }
        _highlightPending = true;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        if (_highlightPending)
        {
            _highlightPending = false;
            PlaceHighlight(animate: false);
        }
        return size;
    }

    private void PlaceHighlight(bool animate)
    {
        if (_highlight is null || _active is null || _active.Bounds.Width <= 0)
        {
            _highlightPending = _active is not null;
            return;
        }
        // The row's layout box, not its rising transform: the highlight does not stagger.
        var rect = _active.Bounds.Translate(_items?.Bounds.Position ?? default);
        _highlight.MoveTo(rect, HighlightSpring, animate);
    }

    private MotionValue<double> CreateHeaderOpacity()
    {
        var value = new MotionValue<double>(_owner, 1, MotionVectors.Double);
        value.Changed += v =>
        {
            if (_header is not null)
            {
                _header.Opacity = v;
            }
        };
        return value;
    }

    private void OnRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ActionMorphMenuItem row)
        {
            e.Handled = true;
            _owner.OpenComposer(row.Action);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _owner.Collapse();
    }

    private void OnRowPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is ActionMorphMenuItem row && e.Pointer.Type != PointerType.Touch)
        {
            SetActive(row);
        }
    }

    private void OnRowsPointerExited(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch)
        {
            SetActive(null);
        }
    }

    private void OnRowGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is ActionMorphMenuItem row && !ReferenceEquals(row, _active))
        {
            SetActive(row);
        }
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (_rows.Count == 0 || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var focused = _rows.FindIndex(r => r.IsFocused);
        int target;
        switch (e.Key)
        {
            case Key.Down:
                target = focused < 0 ? 0 : (focused + 1) % _rows.Count;
                break;
            case Key.Up:
                target = focused < 0 ? _rows.Count - 1 : (focused - 1 + _rows.Count) % _rows.Count;
                break;
            case Key.Home:
                target = 0;
                break;
            case Key.End:
                target = _rows.Count - 1;
                break;
            default:
                return;
        }
        e.Handled = true;
        _rows[target].Focus(NavigationMethod.Directional);
        SetActive(_rows[target]);
    }
}
