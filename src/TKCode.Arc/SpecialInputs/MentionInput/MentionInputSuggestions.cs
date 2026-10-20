using System.Globalization;
using AtomUI.Controls;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;

namespace TKCode.Arc;

/// <summary>
/// The suggestion popover: Core <see cref="ArcPopup"/> hanging from the trigger character, 6px off the line.
/// Arc pops it in from 0.96 scale and 4px toward the line on the snappy spring while the opacity fades in (fast,
/// ease enter), with the transform origin 14px from the left on the anchored edge; it leaves to the same pose in
/// 70% of the exit duration (standard ease). Reduced motion only fades. ArcPopup's own P10 motion uses one curve for
/// both, so the motions are replaced (the menu motions carry separate transform and opacity timing).
/// </summary>
internal sealed class MentionInputSuggestionPopup : ArcPopup
{
    private static readonly Easing s_linear = new LinearEasing();
    private readonly MenuSurfaceEnterMotion _enter = new();
    private readonly MenuSurfaceExitMotion _exit = new();
    private bool _above;
    private double _originX = 14.0 / 272;

    static MentionInputSuggestionPopup()
    {
        // Arc: the panel sits 6px off the line. It closes from the field's own state (blur, Escape, a press elsewhere),
        // never by light dismiss, so a press in the field only moves the caret.
        MarginToAnchorProperty.OverrideDefaultValue<MentionInputSuggestionPopup>(6);
        IsLightDismissEnabledProperty.OverrideDefaultValue<MentionInputSuggestionPopup>(false);
    }

    public MentionInputSuggestionPopup()
    {
        TakesFocusFromNativeControl = false;
        WindowManagerAddShadowHint = false;
        Placement = PlacementMode.BottomEdgeAlignedLeft;
        ApplyMotions();
    }

    /// <summary>True when the panel opens above the line (after the positioner's own flips).</summary>
    internal bool IsAbove { get; private set; }

    /// <summary>Hangs the panel from <paramref name="anchor"/> (placement-target coordinates).</summary>
    public void Place(bool above, Rect anchor)
    {
        _above = above;
        var placement = above ? PlacementMode.TopEdgeAlignedLeft : PlacementMode.BottomEdgeAlignedLeft;
        if (RequestedPlacement != placement)
        {
            Placement = placement;
        }
        if (PlacementRect != anchor)
        {
            PlacementRect = anchor;
        }
        ApplyMotions();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // ArcPopup rebuilds its own motions on these changes (and right before every open); put ours back.
        if (change.Property == PlacementProperty || change.Property == EnterOffsetProperty ||
            change.Property == EnterScaleProperty || change.Property == IsInstantProperty ||
            change.Property == ArcMotion.LevelProperty || change.Property == PlacementTargetProperty ||
            (change.Property == IsOpenProperty && change.GetNewValue<bool>()))
        {
            ApplyMotions();
        }
    }

    private void ApplyMotions()
    {
        var level = PlacementTarget is { } target ? ArcMotion.GetEffectiveLevel(target) : ArcMotion.GetEffectiveLevel(this);
        var reduced = level == ArcMotionLevel.Reduced;
        IsMotionEnabled = level != ArcMotionLevel.Off;
        var exit = TimeSpan.FromMilliseconds(ArcMotionPresets.Exit.TotalMilliseconds * 0.7);
        if (reduced)
        {
            // Reduced: a fade (fast) in, the same short fade out; no scale, no travel.
            _enter.Set(1, ArcMotionPresets.Fast, s_linear, ArcMotionPresets.Fast, ArcEasings.Standard);
            _exit.Set(1, 0, exit, ArcEasings.Standard);
        }
        else
        {
            var spring = ArcSpringEasing.For(ArcSpring.Snappy);
            _enter.Set(0.96, spring.SettleDuration, spring, ArcMotionPresets.Fast, ArcEasings.Enter);
            _exit.Set(0.96, 1, exit, ArcEasings.Standard);
        }
        _enter.FadeOnly = reduced;
        _exit.FadeOnly = reduced;
        _exit.FadesOut = true;
        SetOrigin(IsAbove, _originX);
        OpenMotion = _enter;
        CloseMotion = _exit;
    }

    private void SetOrigin(bool above, double originX)
    {
        IsAbove = above;
        _originX = originX;
        var origin = new RelativePoint(Math.Clamp(originX, 0, 1), above ? 1 : 0, RelativeUnit.Relative);
        // Arc: initial y -4 below the line (it drops into place), +4 above it.
        var offset = new Vector(0, above ? 4 : -4);
        _enter.Origin = origin;
        _enter.Offset = offset;
        _exit.Origin = origin;
        _exit.Offset = offset;
    }

    protected override void OnPlacementResolved(CustomPopupPlacement placement, Thickness shadow, double marginToAnchor,
                                                bool isOverlayHost, bool flipX, bool flipY)
    {
        // Arc: transform-origin 14px from the left, on the edge facing the line (after the positioner's flips).
        var above = flipY ? !_above : _above;
        var width = placement.PopupSize.Width;
        var originX = width > 0 ? (placement.Deflate.Left + 14) / width : _originX;
        SetOrigin(above, originX);
    }
}

/// <summary>
/// The suggestion list (Arc <c>.popoverBody</c> + <c>.list</c>): 6px padding, rows stacked, the highlight
/// (Foreground 6.5%, radius 12) gliding between rows on a 0.3s / 0.1 bounce spring, and the panel height springing
/// (0.4s, no bounce) when results come and go. First placements jump. The rows are light render-based controls.
/// </summary>
internal sealed class MentionInputSuggestionList : Control
{
    internal static readonly StyledProperty<IBrush?> HighlightFillProperty =
        AvaloniaProperty.Register<MentionInputSuggestionList, IBrush?>("HighlightFill");

    internal static readonly StyledProperty<IBrush?> StrongBrushProperty =
        AvaloniaProperty.Register<MentionInputSuggestionList, IBrush?>("StrongBrush");

    internal static readonly StyledProperty<IBrush?> SecondaryBrushProperty =
        AvaloniaProperty.Register<MentionInputSuggestionList, IBrush?>("SecondaryBrush");

    internal static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<MentionInputSuggestionList, IBrush?>("MutedBrush");

    internal static readonly StyledProperty<IBrush?> AvatarFillProperty =
        AvaloniaProperty.Register<MentionInputSuggestionList, IBrush?>("AvatarFill");

    internal static readonly StyledProperty<double> SmallTextSizeProperty =
        AvaloniaProperty.Register<MentionInputSuggestionList, double>("SmallTextSize", 14);

    internal static readonly StyledProperty<double> TinyTextSizeProperty =
        AvaloniaProperty.Register<MentionInputSuggestionList, double>("TinyTextSize", 12);

    internal const double Inset = 6;
    private const double EmptyPadding = 10;
    private const double Leading = 1.4;
    private static readonly SpringSpec s_glide = SpringSpec.FromVisual(0.3, 0.1);
    private static readonly SpringSpec s_grow = SpringSpec.FromVisual(0.4, 0);

    private readonly List<MentionInputSuggestionRow> _rows = new();
    private readonly MotionValue<double> _panelHeight;
    private readonly MotionValue<double> _highlightY;
    private readonly MotionValue<double> _highlightHeight;
    private IReadOnlyList<MentionInputSuggestion> _items = Array.Empty<MentionInputSuggestion>();
    private string _emptyText = string.Empty;
    private TextLayout? _emptyLayout;
    private double _emptyLayoutWidth = -1;
    private int _active;
    private bool _sized;
    private bool _placed;
    private bool _inMeasure;
    private Rect? _highlightTarget;

    static MentionInputSuggestionList()
    {
        AffectsRender<MentionInputSuggestionList>(HighlightFillProperty, SecondaryBrushProperty);
        AffectsMeasure<MentionInputSuggestionList>(SmallTextSizeProperty, TinyTextSizeProperty);
        FocusableProperty.OverrideDefaultValue<MentionInputSuggestionList>(false);
        ClipToBoundsProperty.OverrideDefaultValue<MentionInputSuggestionList>(true);
    }

    public MentionInputSuggestionList()
    {
        ArcTokens.Bind(this, StrongBrushProperty, DesignTokens.ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(this, SecondaryBrushProperty, DesignTokens.ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(this, MutedBrushProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(this, AvatarFillProperty, DesignTokens.ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(this, SmallTextSizeProperty, DesignTokens.ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(this, TinyTextSizeProperty, DesignTokens.ArcDesignTokenKind.TypeXS);
        _panelHeight = new MotionValue<double>(this, 0, MotionVectors.Double);
        _panelHeight.Changed += _ =>
        {
            if (!_inMeasure)
            {
                InvalidateMeasure();
            }
        };
        _highlightY = new MotionValue<double>(this, 0, MotionVectors.Double);
        _highlightHeight = new MotionValue<double>(this, 0, MotionVectors.Double);
        _highlightY.Changed += _ => InvalidateVisual();
        _highlightHeight.Changed += _ => InvalidateVisual();
    }

    internal MentionInput? Owner { get; set; }

    internal MentionKind Kind { get; private set; }

    /// <summary>The query as typed (the empty state quotes it).</summary>
    internal string Query { get; private set; } = string.Empty;

    /// <summary>Accessible name: "People" or "Channels".</summary>
    internal string ListName { get; private set; } = string.Empty;

    internal IReadOnlyList<MentionInputSuggestion> Items => _items;

    internal IReadOnlyList<MentionInputSuggestionRow> Rows => _rows;

    internal int ActiveIndex => _active;

    internal string EmptyText => _emptyText;

    internal IBrush? StrongBrush => GetValue(StrongBrushProperty);

    internal IBrush? SecondaryBrush => GetValue(SecondaryBrushProperty);

    internal IBrush? MutedBrush => GetValue(MutedBrushProperty);

    internal IBrush? AvatarFill => GetValue(AvatarFillProperty);

    internal double SmallTextSize => GetValue(SmallTextSizeProperty);

    internal double TinyTextSize => GetValue(TinyTextSizeProperty);

    internal double SmallLineHeight => SmallTextSize * Leading;

    internal double TinyLineHeight => TinyTextSize * Leading;

    /// <summary>The highlight rectangle currently drawn (tests).</summary>
    internal Rect HighlightRect => new(Inset, Inset + _highlightY.Value, Math.Max(0, Bounds.Width - 2 * Inset), _highlightHeight.Value);

    /// <summary>The height the panel is springing to.</summary>
    internal double TargetHeight => _panelHeight.Target;

    /// <summary>Shows a new result set. Rows are pooled and reused.</summary>
    public void Show(MentionKind kind, IReadOnlyList<MentionInputSuggestion> items, int active, string query, string emptyText,
                     string listName)
    {
        Kind = kind;
        Query = query;
        ListName = listName;
        _items = items;
        _active = active;
        if (!string.Equals(_emptyText, emptyText, StringComparison.Ordinal))
        {
            _emptyText = emptyText;
            _emptyLayout?.Dispose();
            _emptyLayout = null;
        }
        while (_rows.Count < items.Count)
        {
            var row = new MentionInputSuggestionRow(this, _rows.Count);
            _rows.Add(row);
            VisualChildren.Add(row);
            LogicalChildren.Add(row);
        }
        var trimmed = query.Trim();
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            if (i < items.Count)
            {
                row.IsVisible = true;
                row.SetData(items[i], trimmed, i == active);
            }
            else
            {
                row.IsVisible = false;
            }
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Moves the highlight to <paramref name="index"/>.</summary>
    public void SetActive(int index)
    {
        if (index == _active)
        {
            return;
        }
        _active = index;
        for (var i = 0; i < _items.Count && i < _rows.Count; i++)
        {
            _rows[i].SetActive(i == index);
        }
        InvalidateArrange();
    }

    /// <summary>The leaving list keeps its highlight while it fades; the next open places it (and its size) fresh.</summary>
    public void ResetPlacement()
    {
        _sized = false;
        _placed = false;
    }

    internal void OnRowPointerMoved(int index)
    {
        if (index != _active)
        {
            Owner?.ActivateSuggestion(index);
        }
    }

    internal void OnRowClicked(int index) => Owner?.ChooseSuggestion(index);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 272 - 2 : availableSize.Width;
        var rowWidth = Math.Max(0, width - 2 * Inset);
        double natural;
        if (_items.Count > 0)
        {
            natural = 2 * Inset;
            for (var i = 0; i < _items.Count && i < _rows.Count; i++)
            {
                _rows[i].Measure(new Size(rowWidth, double.PositiveInfinity));
                natural += _rows[i].DesiredSize.Height;
            }
        }
        else
        {
            natural = 2 * Inset + EmptyLayout(rowWidth).Height + 2 * EmptyPadding;
        }
        foreach (var row in _rows)
        {
            if (!row.IsVisible)
            {
                row.Measure(default);
            }
        }
        _inMeasure = true;
        try
        {
            if (!_sized)
            {
                _sized = true;
                _panelHeight.Jump(natural);
            }
            else if (Math.Abs(_panelHeight.Target - natural) > 0.01)
            {
                _ = _panelHeight.AnimateTo(natural, MotionSpec.Spring(s_grow));
            }
        }
        finally
        {
            _inMeasure = false;
        }
        return new Size(width, Math.Max(0, _panelHeight.Value));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = Inset;
        var rowWidth = Math.Max(0, finalSize.Width - 2 * Inset);
        Rect? activeRect = null;
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            if (i >= _items.Count)
            {
                row.Arrange(default);
                continue;
            }
            var height = row.DesiredSize.Height;
            var rect = new Rect(Inset, y, rowWidth, height);
            row.Arrange(rect);
            if (i == _active)
            {
                activeRect = rect;
            }
            y += height;
        }
        PlaceHighlight(activeRect);
        return finalSize;
    }

    private void PlaceHighlight(Rect? rect)
    {
        if (rect is not { } target)
        {
            return;
        }
        if (_highlightTarget == target && _placed)
        {
            return;
        }
        _highlightTarget = target;
        var top = target.Y - Inset;
        if (!_placed)
        {
            _placed = true;
            _highlightY.Jump(top);
            _highlightHeight.Jump(target.Height);
            return;
        }
        _ = _highlightY.AnimateTo(top, MotionSpec.Spring(s_glide));
        _ = _highlightHeight.AnimateTo(target.Height, MotionSpec.Spring(s_glide));
    }

    private TextLayout EmptyLayout(double rowWidth)
    {
        var maxWidth = Math.Max(0, rowWidth - 2 * EmptyPadding);
        if (_emptyLayout is null || Math.Abs(_emptyLayoutWidth - maxWidth) > 0.01)
        {
            _emptyLayout?.Dispose();
            _emptyLayout = new TextLayout(_emptyText, new Typeface(TextElement.GetFontFamily(this)), SmallTextSize, SecondaryBrush,
                TextAlignment.Left, TextWrapping.Wrap, null, null, FlowDirection.LeftToRight, maxWidth, double.PositiveInfinity,
                SmallLineHeight);
            _emptyLayoutWidth = maxWidth;
        }
        return _emptyLayout;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SecondaryBrushProperty || change.Property == SmallTextSizeProperty ||
            change.Property == TextElement.FontFamilyProperty)
        {
            _emptyLayout?.Dispose();
            _emptyLayout = null;
        }
        if (change.Property == StrongBrushProperty || change.Property == SecondaryBrushProperty ||
            change.Property == MutedBrushProperty || change.Property == AvatarFillProperty ||
            change.Property == TextElement.FontFamilyProperty || change.Property == SmallTextSizeProperty ||
            change.Property == TinyTextSizeProperty)
        {
            foreach (var row in _rows)
            {
                row.InvalidateText();
            }
        }
    }

    public override void Render(DrawingContext context)
    {
        if (_items.Count > 0)
        {
            if (_placed && GetValue(HighlightFillProperty) is { } fill)
            {
                context.DrawRectangle(fill, null, HighlightRect, 12, 12);
            }
            return;
        }
        var rowWidth = Math.Max(0, Bounds.Width - 2 * Inset);
        EmptyLayout(rowWidth).Draw(context, new Point(Inset + EmptyPadding, Inset + EmptyPadding));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ListPeer(this);

    private sealed class ListPeer : ControlAutomationPeer
    {
        public ListPeer(MentionInputSuggestionList owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string? GetNameCore()
        {
            var list = (MentionInputSuggestionList)Owner;
            return list.Items.Count > 0 ? list.ListName : list.EmptyText;
        }
    }
}

/// <summary>
/// One suggestion (Arc <c>.option</c>): 7 10 7 8 padding, 10px gap, TypeSM. People get a 28px avatar (SurfaceMuted,
/// TextSecondary initials TypeXS 500, or the portrait); channels a 16px Lucide Hash in a 28px slot. The label is
/// TextSecondary weight 500 (Foreground on the active row) with the matched part in Foreground, the meta line TypeXS
/// TextMuted, and a channel's member count TypeXS TextMuted tabular at the right. All drawn in <see cref="Render"/>;
/// only a portrait URL adds a child (AtomUI's async image, clipped to the circle).
/// </summary>
internal sealed class MentionInputSuggestionRow : Control
{
    private const double SlotSize = 28;
    private const double Gap = 10;
    private static readonly Thickness s_padding = new(8, 7, 10, 7);
    private static readonly FontFeatureCollection s_tabular = new() { new FontFeature { Tag = "tnum", Value = 1 } };

    private readonly MentionInputSuggestionList _list;
    private MentionInputSuggestion _item;
    private string _query = string.Empty;
    private bool _active;
    private TextLayout? _label;
    private TextLayout? _meta;
    private TextLayout? _count;
    private TextLayout? _initials;
    private double _layoutWidth = -1;
    private Border? _portraitHost;
    private AsyncImage? _portrait;
    private string? _portraitUrl;
    private bool _pressed;
    private Pen? _hashPen;
    private IBrush? _hashPenBrush;

    public MentionInputSuggestionRow(MentionInputSuggestionList list, int index)
    {
        _list = list;
        Index = index;
        Focusable = false;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    internal int Index { get; }

    internal MentionInputSuggestion Item => _item;

    internal bool IsActive => _active;

    /// <summary>The label as drawn (tests).</summary>
    internal TextLayout? LabelLayout => _label;

    public void SetData(MentionInputSuggestion item, string query, bool active)
    {
        _item = item;
        _query = query;
        _active = active;
        UpdatePortrait();
        InvalidateText();
    }

    public void SetActive(bool active)
    {
        if (_active == active)
        {
            return;
        }
        _active = active;
        _label?.Dispose();
        _label = null;
        InvalidateVisual();
    }

    internal void InvalidateText()
    {
        _label?.Dispose();
        _meta?.Dispose();
        _count?.Dispose();
        _initials?.Dispose();
        _label = _meta = _count = _initials = null;
        _layoutWidth = -1;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void UpdatePortrait()
    {
        var url = _item.Person is { AvatarImage: null, Avatar: { Length: > 0 } avatar } ? avatar : null;
        if (url == _portraitUrl)
        {
            return;
        }
        _portraitUrl = url;
        if (url is null)
        {
            if (_portraitHost is not null)
            {
                _portraitHost.IsVisible = false;
                _portrait!.Source = null;
            }
            return;
        }
        if (_portraitHost is null)
        {
            _portrait = new AsyncImage { Stretch = Stretch.UniformToFill, IsHitTestVisible = false };
            // Initials stay visible until the portrait has loaded (and when it fails).
            _portrait.PropertyChanged += (_, e) =>
            {
                if (e.Property == AsyncImage.IsLoadedProperty || e.Property == AsyncImage.IsFailedProperty)
                {
                    InvalidateVisual();
                }
            };
            _portraitHost = new Border
            {
                Width = SlotSize,
                Height = SlotSize,
                CornerRadius = new CornerRadius(SlotSize / 2),
                ClipToBounds = true,
                IsHitTestVisible = false,
                Child = _portrait
            };
            VisualChildren.Add(_portraitHost);
            LogicalChildren.Add(_portraitHost);
        }
        _portraitHost.IsVisible = true;
        _portrait!.Source = ImageSource.TryParse(url, out var source) ? source : null;
    }

    private Typeface Face(FontWeight weight) => new(TextElement.GetFontFamily(this), FontStyle.Normal, weight);

    private bool HasCount => _item.Kind == MentionKind.Channel && _item.Channel?.Members is not null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var textHeight = _list.SmallLineHeight + (string.IsNullOrEmpty(_item.Meta) ? 0 : _list.TinyLineHeight);
        var height = s_padding.Top + Math.Max(SlotSize, textHeight) + s_padding.Bottom;
        _portraitHost?.Measure(new Size(SlotSize, SlotSize));
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _portraitHost?.Arrange(new Rect(s_padding.Left, (finalSize.Height - SlotSize) / 2, SlotSize, SlotSize));
        if (Math.Abs(_layoutWidth - finalSize.Width) > 0.01)
        {
            _label?.Dispose();
            _meta?.Dispose();
            _label = _meta = null;
            _layoutWidth = finalSize.Width;
        }
        return finalSize;
    }

    private double CountWidth()
    {
        if (!HasCount)
        {
            return 0;
        }
        _count ??= new TextLayout(_item.Channel!.Members!.Value.ToString(CultureInfo.InvariantCulture), Face(FontWeight.Normal),
            _list.TinyTextSize, _list.MutedBrush, TextAlignment.Left, TextWrapping.NoWrap, null, null,
            FlowDirection.LeftToRight, double.PositiveInfinity, double.PositiveInfinity, _list.TinyLineHeight, 0, 0, s_tabular, null);
        return _count.WidthIncludingTrailingWhitespace;
    }

    private double TextWidth(double rowWidth)
    {
        var count = CountWidth();
        var left = s_padding.Left + SlotSize + Gap;
        var right = s_padding.Right + (count > 0 ? count + Gap : 0);
        return Math.Max(0, rowWidth - left - right);
    }

    private TextLayout Label(double maxWidth)
    {
        if (_label is not null)
        {
            return _label;
        }
        var face = Face(FontWeight.Medium);
        var name = _item.Name;
        var at = _query.Length > 0 ? name.IndexOf(_query, StringComparison.OrdinalIgnoreCase) : -1;
        IReadOnlyList<ValueSpan<TextRunProperties>>? spans = null;
        if (at >= 0)
        {
            spans = new[]
            {
                new ValueSpan<TextRunProperties>(at, _query.Length,
                    new GenericTextRunProperties(face, _list.SmallTextSize, null, _list.StrongBrush))
            };
        }
        var brush = _active ? _list.StrongBrush : _list.SecondaryBrush;
        _label = new TextLayout(name, face, _list.SmallTextSize, brush, TextAlignment.Left, TextWrapping.NoWrap,
            TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, maxWidth, double.PositiveInfinity,
            _list.SmallLineHeight, 0, 0, null, spans);
        return _label;
    }

    private TextLayout? Meta(double maxWidth)
    {
        var meta = _item.Meta;
        if (string.IsNullOrEmpty(meta))
        {
            return null;
        }
        _meta ??= new TextLayout(meta, Face(FontWeight.Normal), _list.TinyTextSize, _list.MutedBrush, TextAlignment.Left,
            TextWrapping.NoWrap, TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, maxWidth,
            double.PositiveInfinity, _list.TinyLineHeight);
        return _meta;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        // The whole row is the hit target, not only its glyphs.
        context.FillRectangle(Brushes.Transparent, new Rect(bounds.Size));
        var slotTop = (bounds.Height - SlotSize) / 2;
        var slot = new Rect(s_padding.Left, slotTop, SlotSize, SlotSize);
        if (_item.Kind == MentionKind.Person)
        {
            RenderAvatar(context, slot);
        }
        else
        {
            RenderHash(context, slot);
        }
        var textWidth = TextWidth(bounds.Width);
        var label = Label(textWidth);
        var meta = Meta(textWidth);
        var textHeight = label.Height + (meta?.Height ?? 0);
        var x = s_padding.Left + SlotSize + Gap;
        var y = (bounds.Height - textHeight) / 2;
        label.Draw(context, new Point(x, y));
        meta?.Draw(context, new Point(x, y + label.Height));
        if (HasCount && _count is { } count)
        {
            count.Draw(context, new Point(bounds.Width - s_padding.Right - count.WidthIncludingTrailingWhitespace,
                (bounds.Height - count.Height) / 2));
        }
    }

    private void RenderAvatar(DrawingContext context, Rect slot)
    {
        var center = slot.Center;
        context.DrawEllipse(_list.AvatarFill, null, center, SlotSize / 2, SlotSize / 2);
        if (_item.Person?.AvatarImage is { } image)
        {
            var size = image.Size;
            if (size.Width > 0 && size.Height > 0)
            {
                // object-fit: cover inside the circle.
                var scale = Math.Max(SlotSize / size.Width, SlotSize / size.Height);
                var source = new Rect((size.Width - SlotSize / scale) / 2, (size.Height - SlotSize / scale) / 2,
                    SlotSize / scale, SlotSize / scale);
                using (context.PushGeometryClip(new EllipseGeometry(slot)))
                {
                    context.DrawImage(image, source, slot);
                }
            }
            return;
        }
        if (_portraitHost is { IsVisible: true } && _portrait is { IsLoaded: true, IsFailed: false })
        {
            return;
        }
        _initials ??= new TextLayout(MentionInputEngine.Initials(_item.Name), Face(FontWeight.Medium), _list.TinyTextSize,
            _list.SecondaryBrush, TextAlignment.Left, TextWrapping.NoWrap, null, null, FlowDirection.LeftToRight,
            double.PositiveInfinity, double.PositiveInfinity, _list.TinyLineHeight);
        _initials.Draw(context, new Point(center.X - _initials.WidthIncludingTrailingWhitespace / 2, center.Y - _initials.Height / 2));
    }

    private void RenderHash(DrawingContext context, Rect slot)
    {
        // Lucide Hash at 16px with strokeWidth 1.75 in the 24-unit viewBox (it scales with the icon).
        var brush = _list.SecondaryBrush;
        if (brush is null)
        {
            return;
        }
        if (_hashPen is null || !ReferenceEquals(_hashPenBrush, brush))
        {
            _hashPen = new Pen(brush, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _hashPenBrush = brush;
        }
        const double size = 16;
        var scale = size / 24;
        var offset = new Point(slot.X + (SlotSize - size) / 2, slot.Y + (SlotSize - size) / 2);
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset.X, offset.Y)))
        {
            context.DrawGeometry(null, _hashPen, ArcIcons.Hash);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        // Arc activates rows on pointer move; hover never follows touch.
        if (e.Pointer.Type != PointerType.Touch)
        {
            _list.OnRowPointerMoved(Index);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Arc prevents the mousedown default so the textarea keeps focus; nothing here is focusable either.
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
        }
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var pressed = _pressed;
        _pressed = false;
        if (pressed && e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            _list.OnRowClicked(Index);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    private sealed class RowPeer : ControlAutomationPeer, ISelectionItemProvider, IInvokeProvider
    {
        public RowPeer(MentionInputSuggestionRow owner)
            : base(owner)
        {
        }

        private MentionInputSuggestionRow Row => (MentionInputSuggestionRow)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string? GetNameCore() => Row._item.Person is null && Row._item.Channel is null ? null : Row._item.Name;

        protected override string? GetHelpTextCore() => Row._item.Person is null && Row._item.Channel is null ? null : Row._item.Meta;

        public bool IsSelected => Row._active;

        public ISelectionProvider? SelectionContainer => null;

        public void Select() => Row._list.OnRowPointerMoved(Row.Index);

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }

        public void Invoke() => Row._list.OnRowClicked(Row.Index);
    }
}
