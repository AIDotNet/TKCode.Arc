using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>A visible row of a <see cref="JsonViewer"/>: a value, or the "Show N more" row of a long branch.</summary>
internal sealed class JsonViewerRowModel
{
    public JsonViewerRowModel(string id, JsonViewerNode? node, JsonViewerNode? owner, int level, bool isOpen, int position, int setSize, int hidden)
    {
        Id = id;
        Node = node;
        Owner = owner;
        Level = level;
        IsOpen = isOpen;
        Position = position;
        SetSize = setSize;
        Hidden = hidden;
    }

    public string Id { get; }

    /// <summary>The value; null for a "more" row.</summary>
    public JsonViewerNode? Node { get; }

    /// <summary>The branch a "more" row belongs to.</summary>
    public JsonViewerNode? Owner { get; }

    public int Level { get; }
    public bool IsOpen { get; }
    public int Position { get; }
    public int SetSize { get; }
    public int Hidden { get; }
}

/// <summary>Resolved brushes, fonts and metrics shared by the rows of one viewer.</summary>
internal sealed class JsonViewerPalette
{
    private readonly Dictionary<(IBrush, double), IPen> _pens = new();
    private readonly Dictionary<Color, IBrush> _fades = new();
    private int _cacheVersion = -1;

    public int Version { get; set; }
    public IBrush? Foreground { get; set; }
    public IBrush? Muted { get; set; }
    public IBrush? Secondary { get; set; }
    public IBrush? String { get; set; }
    public IBrush? Number { get; set; }
    public IBrush? Boolean { get; set; }
    public IBrush? Highlight { get; set; }
    public IBrush? HoverFill { get; set; }
    public IBrush? Mark { get; set; }
    public IBrush? MarkCurrent { get; set; }
    public IBrush? ActionPressed { get; set; }
    public IBrush? Success { get; set; }
    public IBrush? Danger { get; set; }
    public IBrush? Surface { get; set; }
    public IPen? FocusRing { get; set; }
    public FontFamily MonoFont { get; set; } = FontFamily.Default;
    public FontFamily BodyFont { get; set; } = FontFamily.Default;
    public double TreeFontSize { get; set; } = 13;
    public double SmallFontSize { get; set; } = 12;
    public double MoreFontSize { get; set; } = 14;
    public double SlotHeight { get; set; } = 30;
    public double Indent { get; set; } = 16;
    public double KeyMaxFraction { get; set; } = 0.45;

    private void EnsureFresh()
    {
        if (_cacheVersion != Version)
        {
            _pens.Clear();
            _fades.Clear();
            _cacheVersion = Version;
        }
    }

    public IPen? PenFor(IBrush? brush, double thickness)
    {
        if (brush is null)
        {
            return null;
        }
        EnsureFresh();
        if (!_pens.TryGetValue((brush, thickness), out var pen))
        {
            pen = new Pen(brush, thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _pens[(brush, thickness)] = pen;
        }
        return pen;
    }

    /// <summary>The soft fade behind the row actions: transparent to the row color over 22px, then solid.</summary>
    public IBrush FadeTo(Color color, double width)
    {
        EnsureFresh();
        if (!_fades.TryGetValue(color, out var brush))
        {
            var transparent = Color.FromArgb(0, color.R, color.G, color.B);
            brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = { new GradientStop(transparent, 0), new GradientStop(color, 22 / width), new GradientStop(color, 1) }
            }.ToImmutable();
            _fades[color] = brush;
        }
        return brush;
    }
}

/// <summary>The JsonViewer's row engine: rows are <see cref="JsonViewerRow"/>s keyed by path.</summary>
internal sealed class JsonViewerRowsPanel : TablesTreeRowsPanel
{
    public JsonViewerRowsPanel()
    {
        Motion = new TablesTreeRowMotion
        {
            EnterFade = ArcMotionPresets.Fast,
            EnterFadeEasing = ArcEasings.Standard,
            ExitFade = ArcMotionPresets.Fast,
            // Arc: with more than 400 visible rows, row changes complete immediately.
            BulkLimit = 400
        };
    }

    public JsonViewer? Owner { get; set; }

    protected override TablesTreeRow CreateRow() => new JsonViewerRow(this);

    protected override object KeyOf(object model) => ((JsonViewerRowModel)model).Id;
}

/// <summary>
/// One JSON row, drawn directly: hover fill, chevron (turns with Arc's CSS spring curve), key or index, colon,
/// the colored value or a collapsed preview with its count, search marks, and the copy value / copy path actions
/// that appear over a soft fade on hover or on the current row (their glyph pops to a check or a cross).
/// Motion uses <see cref="MotionValue{T}"/> because these parts are drawn by the row, not separate controls
/// with properties a ControlTheme transition could target.
/// </summary>
internal sealed class JsonViewerRow : TablesTreeRow
{
    private const double ActionSize = 26;
    private const double ActionsWidth = 24 + ActionSize * 2 + 1;

    private readonly JsonViewerRowsPanel _panel;
    private MotionValue<double>? _rotation;
    private MotionValue<double>? _actionsOpacity;
    private readonly MotionValue<double>?[] _actionPop = new MotionValue<double>?[2];
    private readonly CopyState[] _actionState = new CopyState[2];
    private readonly PausableTimer?[] _actionReset = new PausableTimer?[2];
    private bool _hovered;
    private int _hoverAction = -1;
    private int _pressedAction = -1;
    private bool _pressed;
    private bool _wasOpen;

    private int _layoutVersion = -1;
    private double _layoutWidth = double.NaN;
    private object? _layoutModel;
    private bool _layoutOpen;
    private TextLayout? _key;
    private TextLayout? _colon;
    private TextLayout? _preview;
    private TextLayout? _count;
    private TextLayout? _value;
    private TextLayout? _moreLabel;
    private TextLayout? _moreHidden;
    private string? _keyText;
    private string? _valueText;
    private string? _valueMarkText;
    private int _valueMarkOffset;

    public JsonViewerRow(JsonViewerRowsPanel panel)
    {
        _panel = panel;
    }

    public JsonViewer? Owner => _panel.Owner;

    private JsonViewerPalette? Palette => Owner?.Palette;

    public JsonViewerRowModel? Row => Model as JsonViewerRowModel;

    public bool IsActive => Row is { } row && Owner?.ActiveId == row.Id;

    internal CopyState ActionState(JsonViewerCopyKind kind) => _actionState[(int)kind];

    protected override void OnBound(object? previous, bool sameKey)
    {
        var row = Row;
        if (row is null)
        {
            ClearLayouts();
            _hovered = false;
            _hoverAction = -1;
            _pressedAction = -1;
            IsTabStop = false;
            return;
        }
        if (!sameKey)
        {
            for (var i = 0; i < 2; i++)
            {
                _actionState[i] = CopyState.Idle;
                _actionReset[i]?.Stop();
                _actionPop[i]?.Jump(1);
            }
            _hovered = IsPointerOver;
        }
        IsTabStop = IsActive && !IsGhost;
        var angle = row.IsOpen ? 90.0 : 0.0;
        if (sameKey && row.IsOpen != _wasOpen && IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full)
        {
            _rotation ??= CreateValue(_wasOpen ? 90 : 0);
            _ = _rotation.AnimateTo(angle, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
        }
        else
        {
            _rotation?.Jump(angle);
        }
        _wasOpen = row.IsOpen;
        UpdateActionsVisibility(animate: sameKey);
        if (!ReferenceEquals(_layoutModel, row) || _layoutOpen != row.IsOpen)
        {
            ClearLayouts();
        }
        AutomationProperties.SetName(this, Describe(row));
        AutomationProperties.SetPositionInSet(this, row.Position);
        AutomationProperties.SetSizeOfSet(this, row.SetSize);
        var tip = row.Node is { Type: JsonViewerValueType.String } node && node.PrimitiveText.Length > 40 ? node.PrimitiveText : null;
        Tooltip.SetTip(this, tip);
    }

    private string Describe(JsonViewerRowModel row)
    {
        var owner = Owner;
        if (row.Node is not { } node)
        {
            var size = owner?.PageSize ?? 50;
            return string.Create(CultureInfo.InvariantCulture, $"Show {Math.Min(size, row.Hidden)} more, {row.Hidden} hidden");
        }
        var name = node.IsRoot ? owner?.RootName ?? "root" : node.Key ?? node.Index.ToString(CultureInfo.InvariantCulture);
        return node.IsBranch
            ? $"{name}, {JsonViewerPaths.TypeName(node.Type)}, {JsonViewerPaths.CountLabel(node.Type, node.Count)}"
            : $"{name}: {node.PrimitiveText}";
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private void ClearLayouts()
    {
        _key = _colon = _preview = _count = _value = _moreLabel = _moreHidden = null;
        _layoutModel = null;
        _layoutWidth = double.NaN;
    }

    #region Actions and input

    private bool ShowsActions => Owner?.IsCopyable == true && Row?.Node is not null && !IsGhost;

    private void UpdateActionsVisibility(bool animate)
    {
        var visible = ShowsActions && (_hovered || IsActive) ? 1.0 : 0.0;
        _actionsOpacity ??= CreateValue(visible);
        if (!animate || ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            _actionsOpacity.Jump(visible);
        }
        else if (Math.Abs(_actionsOpacity.Target - visible) > 0.001)
        {
            _ = _actionsOpacity.AnimateTo(visible, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
    }

    private Rect ActionRect(int index)
    {
        var width = Bounds.Width;
        var slot = Palette?.SlotHeight ?? 30;
        return new Rect(width - 1 - ActionSize * (2 - index), (slot - ActionSize) / 2, ActionSize, ActionSize);
    }

    private int ActionAt(Point point)
    {
        if (!ShowsActions || (_actionsOpacity?.Value ?? 0) < 0.5)
        {
            return -1;
        }
        for (var i = 0; i < 2; i++)
        {
            if (ActionRect(i).Contains(point))
            {
                return i;
            }
        }
        return -1;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Touch || Row is null || IsGhost)
        {
            return;
        }
        _hovered = true;
        UpdateActionsVisibility(animate: true);
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        var action = ActionAt(e.GetPosition(this));
        if (action != _hoverAction)
        {
            _hoverAction = action;
            Cursor = action >= 0 ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = false;
        _hoverAction = -1;
        _pressedAction = -1;
        _pressed = false;
        UpdateActionsVisibility(animate: true);
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Row is null || IsGhost || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        e.Handled = true;
        _pressedAction = ActionAt(e.GetPosition(this));
        _pressed = _pressedAction < 0;
        InvalidateVisual();
    }

    protected override async void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var point = e.GetPosition(this);
        if (_pressedAction >= 0)
        {
            var action = _pressedAction;
            _pressedAction = -1;
            InvalidateVisual();
            if (ActionRect(action).Contains(point) && Row is { } row && Owner is { } owner)
            {
                e.Handled = true;
                var ok = await owner.CopyAsync(row, (JsonViewerCopyKind)action);
                if (ReferenceEquals(Row, row) || (Row is { } current && current.Id == row.Id))
                {
                    SetActionState(action, ok ? CopyState.Copied : CopyState.Error);
                }
            }
            return;
        }
        if (_pressed)
        {
            _pressed = false;
            if (new Rect(Bounds.Size).Contains(point))
            {
                e.Handled = true;
                Owner?.OnRowClicked(this);
            }
        }
    }

    private void SetActionState(int action, CopyState state)
    {
        _actionState[action] = state;
        _actionPop[action] ??= CreateValue(1);
        if (IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off)
        {
            _actionPop[action]!.Jump(ArcMotion.IsFull(this) ? 0 : 0.6);
            _ = _actionPop[action]!.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(260), ArcEasings.Enter));
        }
        else
        {
            _actionPop[action]!.Jump(1);
        }
        if (state != CopyState.Idle)
        {
            _actionReset[action] ??= new PausableTimer(TimeSpan.FromMilliseconds(1400), () => SetActionState(action, CopyState.Idle));
            _actionReset[action]!.Restart(TimeSpan.FromMilliseconds(1400));
        }
        InvalidateVisual();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Owner?.OnRowFocused(this);
        UpdateActionsVisibility(animate: true);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var timer in _actionReset)
        {
            timer?.Stop();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new JsonViewerRowAutomationPeer(this);

    #endregion

    #region Rendering

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = Palette?.SlotHeight ?? 30;
        return new Size(double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width, height);
    }

    private static TextLayout Layout(string text, FontFamily family, double size, IBrush? brush, double maxWidth,
                                     FontWeight weight = FontWeight.Normal)
        => new(text, new Typeface(family, FontStyle.Normal, weight), size, brush, TextAlignment.Left, TextWrapping.NoWrap,
            TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, Math.Max(1, maxWidth));

    private IBrush? ValueBrush(JsonViewerPalette p, JsonViewerValueType type) => type switch
    {
        JsonViewerValueType.String => p.String,
        JsonViewerValueType.Number => p.Number,
        JsonViewerValueType.Boolean => p.Boolean,
        _ => p.Muted
    };

    private void EnsureLayouts(JsonViewerPalette p, JsonViewerRowModel row, double width)
    {
        if (_layoutModel == row && _layoutVersion == p.Version && Math.Abs(_layoutWidth - width) < 0.5 && _layoutOpen == row.IsOpen)
        {
            return;
        }
        ClearLayouts();
        _layoutModel = row;
        _layoutVersion = p.Version;
        _layoutWidth = width;
        _layoutOpen = row.IsOpen;
        var lineLeft = 4 + (row.Level - 1) * p.Indent;
        var lineWidth = Math.Max(0, width - lineLeft - 6);
        if (row.Node is not { } node)
        {
            var size = Owner?.PageSize ?? 50;
            _moreLabel = Layout(string.Create(CultureInfo.InvariantCulture, $"Show {Math.Min(size, row.Hidden)} more"), p.BodyFont,
                p.MoreFontSize, p.Foreground, lineWidth, FontWeight.Medium);
            _moreHidden = Layout(string.Create(CultureInfo.InvariantCulture, $"{row.Hidden} hidden"), p.BodyFont, p.SmallFontSize, p.Muted, lineWidth);
            return;
        }
        var owner = Owner;
        _keyText = node.IsRoot ? owner?.RootName ?? "root" : node.Key ?? node.Index.ToString(CultureInfo.InvariantCulture);
        var keyBrush = node.Index >= 0 ? p.Muted : p.Foreground;
        _key = Layout(_keyText, p.MonoFont, p.TreeFontSize, keyBrush, lineWidth * p.KeyMaxFraction);
        var x = lineLeft + 14 + 6 + _key.WidthIncludingTrailingWhitespace;
        if (!(node.IsRoot && node.IsBranch))
        {
            _colon = Layout(":", p.MonoFont, p.TreeFontSize, p.Muted, 20);
            x += 1 + _colon.WidthIncludingTrailingWhitespace;
        }
        x += 6;
        var remaining = Math.Max(0, width - 6 - x);
        if (node.IsBranch)
        {
            _count = Layout(JsonViewerPaths.CountLabel(node.Type, node.Count), p.BodyFont, p.SmallFontSize, p.Muted, remaining);
            if (!row.IsOpen)
            {
                _preview = Layout(JsonViewerPaths.Preview(node), p.MonoFont, p.TreeFontSize, p.Muted,
                    Math.Max(0, remaining - 8 - _count.WidthIncludingTrailingWhitespace));
            }
            return;
        }
        var text = node.PrimitiveText;
        _valueMarkOffset = node.Type == JsonViewerValueType.String ? 1 : 0;
        _valueText = node.Type == JsonViewerValueType.String ? string.Concat("\"", text, "\"") : text;
        _valueMarkText = text;
        _value = Layout(_valueText, p.MonoFont, p.TreeFontSize, ValueBrush(p, node.Type), remaining);
    }

    public override void Render(DrawingContext context)
    {
        var p = Palette;
        var row = Row;
        if (p is null || row is null)
        {
            return;
        }
        var width = Bounds.Width;
        // A transparent fill makes the whole slot hit-testable, not only the drawn text.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        var slot = p.SlotHeight;
        var active = IsActive;
        var rowRect = new Rect(0, 1, width, slot - 2);
        if (_hovered && !active && p.HoverFill is { } hoverFill)
        {
            context.DrawRectangle(hoverFill, null, new RoundedRect(rowRect, 9));
        }
        EnsureLayouts(p, row, width);
        var centerY = slot / 2;
        var lineLeft = 4 + (row.Level - 1) * p.Indent;

        if (row.Node is not { } node)
        {
            var mx = lineLeft + 20;
            if (_moreLabel is not null)
            {
                _moreLabel.Draw(context, new Point(mx, centerY - _moreLabel.Height / 2));
                mx += _moreLabel.WidthIncludingTrailingWhitespace + 10;
            }
            if (_moreHidden is not null && _moreLabel is not null)
            {
                // Baseline-aligned with the label.
                var baseline = centerY - _moreLabel.Height / 2 + _moreLabel.Baseline;
                _moreHidden.Draw(context, new Point(mx, baseline - _moreHidden.Baseline));
            }
            DrawFocusRing(context, p, rowRect);
            return;
        }

        if (node.IsBranch)
        {
            var emphasized = active || _hovered;
            var pen = p.PenFor(emphasized ? p.Foreground : p.Muted, 1.75);
            if (pen is not null)
            {
                DrawGlyph(context, ArcIcons.ChevronRight, pen, new Point(lineLeft + 7, centerY), 14, _rotation?.Value ?? (row.IsOpen ? 90 : 0), 1, 1);
            }
        }
        var x = lineLeft + 14 + 6;
        var search = Owner?.Search;
        var matched = search is not null && search.MatchSet.Contains(row.Id);
        var current = matched && Owner?.CurrentMatch == row.Id;
        if (_key is not null)
        {
            var origin = new Point(x, centerY - _key.Height / 2);
            if (matched && node.Key is not null && _keyText is not null)
            {
                DrawMarks(context, p, _key, _keyText, 0, search!.Needle, origin, current);
            }
            _key.Draw(context, origin);
            x += _key.WidthIncludingTrailingWhitespace;
        }
        if (_colon is not null)
        {
            x += 1;
            _colon.Draw(context, new Point(x, centerY - _colon.Height / 2));
            x += _colon.WidthIncludingTrailingWhitespace;
        }
        x += 6;
        if (node.IsBranch)
        {
            double baseline = centerY;
            if (_preview is not null)
            {
                var origin = new Point(x, centerY - _preview.Height / 2);
                _preview.Draw(context, origin);
                baseline = origin.Y + _preview.Baseline;
                x += _preview.WidthIncludingTrailingWhitespace + 8;
            }
            else if (_key is not null)
            {
                baseline = centerY - _key.Height / 2 + _key.Baseline;
            }
            _count?.Draw(context, new Point(x, baseline - _count.Baseline));
        }
        else if (_value is not null && _valueMarkText is not null)
        {
            var origin = new Point(x, centerY - _value.Height / 2);
            if (matched)
            {
                DrawMarks(context, p, _value, _valueMarkText, _valueMarkOffset, search!.Needle, origin, current);
            }
            _value.Draw(context, origin);
        }
        DrawActions(context, p, width, active);
        DrawFocusRing(context, p, rowRect);
    }

    private void DrawFocusRing(DrawingContext context, JsonViewerPalette p, Rect rowRect)
    {
        if (IsFocused && p.FocusRing is { } ring && Classes.Contains(":focus-visible"))
        {
            context.DrawRectangle(null, ring, new RoundedRect(rowRect.Deflate(1), 8));
        }
    }

    private static void DrawMarks(DrawingContext context, JsonViewerPalette p, TextLayout layout, string text, int offset, string needle,
                                  Point origin, bool current)
    {
        if (needle.Length == 0)
        {
            return;
        }
        var from = 0;
        while (true)
        {
            var at = text.IndexOf(needle, from, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                break;
            }
            foreach (var rect in layout.HitTestTextRange(at + offset, needle.Length))
            {
                var r = rect.Translate(new Vector(origin.X, origin.Y));
                if (p.Mark is { } mark)
                {
                    context.DrawRectangle(mark, null, new RoundedRect(r.Inflate(1), 4));
                }
                if (current && p.MarkCurrent is { } strong)
                {
                    context.DrawRectangle(strong, null, new RoundedRect(r, 3));
                }
            }
            from = at + needle.Length;
        }
    }

    private void DrawActions(DrawingContext context, JsonViewerPalette p, double width, bool active)
    {
        var opacity = _actionsOpacity?.Value ?? 0;
        if (!ShowsActions || opacity <= 0.001)
        {
            return;
        }
        var slot = p.SlotHeight;
        var background = active ? p.Highlight : _hovered ? p.HoverFill : p.Surface;
        var color = background is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
        using (context.PushOpacity(opacity))
        {
            var area = new Rect(width - ActionsWidth, 1, ActionsWidth, slot - 2);
            context.DrawRectangle(p.FadeTo(color, ActionsWidth), null, new RoundedRect(area, 0, 9, 9, 0));
            for (var i = 0; i < 2; i++)
            {
                var rect = ActionRect(i);
                var hovered = _hoverAction == i;
                if (_pressedAction == i && p.ActionPressed is { } pressed)
                {
                    context.DrawRectangle(pressed, null, new RoundedRect(rect, 8));
                }
                else if (hovered && p.Highlight is { } hoverBg)
                {
                    context.DrawRectangle(hoverBg, null, new RoundedRect(rect, 8));
                }
                var state = _actionState[i];
                var brush = state switch
                {
                    CopyState.Copied => p.Success,
                    CopyState.Error => p.Danger,
                    _ => hovered ? p.Foreground : p.Secondary
                };
                var glyph = state switch
                {
                    CopyState.Copied => ArcIcons.Check,
                    CopyState.Error => ArcIcons.X,
                    _ => i == 0 ? ArcIcons.Copy : ArcIcons.Link2
                };
                if (p.PenFor(brush, state == CopyState.Idle ? 1.75 : 2) is { } pen)
                {
                    var pop = Math.Clamp(_actionPop[i]?.Value ?? 1, 0, 1);
                    DrawGlyph(context, glyph, pen, rect.Center, 14, 0, 0.6 + 0.4 * pop, Math.Clamp(pop / 0.45, 0, 1));
                }
            }
        }
    }

    private static void DrawGlyph(DrawingContext context, Geometry geometry, IPen pen, Point center, double size, double degrees,
                                  double scale, double opacity)
    {
        if (opacity <= 0.001 || scale <= 0.001)
        {
            return;
        }
        var s = size / 24.0 * scale;
        var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(s, s);
        if (Math.Abs(degrees) > 0.01)
        {
            matrix *= Matrix.CreateRotation(degrees * Math.PI / 180);
        }
        matrix *= Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushOpacity(opacity))
        using (context.PushTransform(matrix))
        {
            context.DrawGeometry(null, pen, geometry);
        }
    }

    #endregion

    private sealed class JsonViewerRowAutomationPeer : ControlAutomationPeer, IExpandCollapseProvider, ISelectionItemProvider
    {
        public JsonViewerRowAutomationPeer(JsonViewerRow owner) : base(owner)
        {
        }

        private JsonViewerRow Row => (JsonViewerRow)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TreeItem;

        public ExpandCollapseState ExpandCollapseState => Row.Row is not { Node.IsBranch: true } row
            ? ExpandCollapseState.LeafNode
            : row.IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (Row.Row is { } row)
            {
                Row.Owner?.Toggle(row.Id, true);
            }
        }

        public void Collapse()
        {
            if (Row.Row is { } row)
            {
                Row.Owner?.Toggle(row.Id, false);
            }
        }

        public bool IsSelected => Row.IsActive;

        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }

        public void Select()
        {
            if (Row.Row is { } row)
            {
                Row.Owner?.FocusPath(row.Id);
            }
        }
    }
}
