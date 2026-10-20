using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// Row container of a <see cref="ReorderableList"/>: the grip handle, the item content and the rolling position
/// number. Drag motion is a render transform (vertical offset and the 1.02 lift scale) driven by springs, so a
/// drag never relayouts the list.
/// </summary>
[TemplatePart(HandlePart, typeof(ReorderableListHandle))]
[PseudoClasses(LiftedPseudoClass, DraggingPseudoClass)]
internal sealed class ReorderableListItem : ContentControl
{
    public const string HandlePart = "PART_Handle";
    public const string LiftedPseudoClass = ":lifted";
    public const string DraggingPseudoClass = ":dragging";

    internal static readonly DirectProperty<ReorderableListItem, string> PositionTextProperty =
        AvaloniaProperty.RegisterDirect<ReorderableListItem, string>(nameof(PositionText), o => o.PositionText);

    internal static readonly DirectProperty<ReorderableListItem, int> PositionDirectionProperty =
        AvaloniaProperty.RegisterDirect<ReorderableListItem, int>(nameof(PositionDirection), o => o.PositionDirection);

    private readonly ReorderableList _list;
    private readonly MatrixTransform _transform = new();
    private MotionValue<double>? _offset;
    private MotionValue<double>? _scale;
    private ReorderableListHandle? _handle;
    private string _positionText = "01";
    private int _positionDirection = 1;
    private int _position;
    private Task<bool>? _settle;

    public ReorderableListItem(ReorderableList list)
    {
        _list = list;
        RenderTransform = _transform;
        RenderTransformOrigin = RelativePoint.Center;
    }

    public ReorderableList List => _list;

    internal string PositionText { get => _positionText; private set => SetAndRaise(PositionTextProperty, ref _positionText, value); }

    internal int PositionDirection { get => _positionDirection; private set => SetAndRaise(PositionDirectionProperty, ref _positionDirection, value); }

    public ReorderableListHandle? Handle => _handle;

    /// <summary>Current vertical drag offset (render transform).</summary>
    public double Offset => _offset?.Value ?? 0;

    public double OffsetVelocity => _offset?.Velocity ?? 0;

    public bool IsSettling => _offset is { IsAnimating: true };

    public Task<bool>? Settle => _settle;

    /// <summary>One-based position shown in the row; the digits roll in the direction the row travelled.</summary>
    public void SetPosition(int position, int count)
    {
        if (position != _position)
        {
            PositionDirection = _position == 0 || position > _position ? 1 : -1;
            _position = position;
            PositionText = position.ToString("00", CultureInfo.InvariantCulture);
        }
        _handle?.Describe(_list.TitleOf(Content), position, count);
    }

    public void JumpOffset(double offset)
    {
        _offset ??= CreateOffset();
        _offset.Jump(offset);
        _settle = null;
    }

    /// <summary>Springs the offset to <paramref name="target"/>, keeping the current velocity unless one is given.</summary>
    public void AnimateOffset(double target, SpringSpec spec, double? velocity = null)
    {
        _offset ??= CreateOffset();
        _settle = _offset.AnimateTo(target, MotionSpec.Spring(spec), velocity);
    }

    private MotionValue<double> CreateOffset()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += _ => UpdateTransform();
        return value;
    }

    /// <summary>Raises the row (shadow layer, z-order, handle colors) and scales it to 1.02 on a snappy spring.</summary>
    public void SetLifted(bool lifted)
    {
        PseudoClasses.Set(LiftedPseudoClass, lifted);
        ZIndex = lifted ? 1 : IsSettling ? 1 : 0;
        var full = ArcMotion.IsFull(this);
        _scale ??= CreateScale();
        if (full)
        {
            _ = _scale.AnimateTo(lifted ? 1.02 : 1, MotionSpec.Spring(ArcSpring.Snappy));
        }
        else
        {
            _scale.Jump(1);
        }
        if (!lifted)
        {
            _ = LowerWhenSettled();
        }
    }

    private static readonly Cursor s_grab = new(StandardCursorType.Hand);
    private static readonly Cursor s_grabbing = new(StandardCursorType.DragMove);

    public void SetDragging(bool dragging)
    {
        PseudoClasses.Set(DraggingPseudoClass, dragging);
        if (_handle is not null)
        {
            // Arc: grab at rest, grabbing while dragging.
            _handle.Cursor = dragging ? s_grabbing : s_grab;
        }
    }

    private async Task LowerWhenSettled()
    {
        if (_settle is { } settle)
        {
            await settle;
        }
        if (!PseudoClasses.Contains(LiftedPseudoClass))
        {
            ZIndex = 0;
        }
    }

    private MotionValue<double> CreateScale()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += _ => UpdateTransform();
        return value;
    }

    private void UpdateTransform()
    {
        var scale = _scale?.Value ?? 1;
        _transform.Matrix = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(0, _offset?.Value ?? 0);
    }

    /// <summary>Snaps transforms (recycled or detached containers).</summary>
    public void ResetMotion()
    {
        _offset?.Jump(0);
        _scale?.Jump(1);
        _settle = null;
        PseudoClasses.Set(LiftedPseudoClass, false);
        PseudoClasses.Set(DraggingPseudoClass, false);
        ZIndex = 0;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _handle = e.NameScope.Find<ReorderableListHandle>(HandlePart);
        if (_handle is not null)
        {
            _handle.Owner = this;
        }
        _list.RefreshPositions();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ListItemAutomationPeer(this);
}

/// <summary>
/// The grip of a reorderable row. Only the handle starts a drag (it captures the pointer, Arc's
/// <c>touch-action: none</c>), so the rest of the row keeps scrolling the page on touch. Keyboard: ↑/↓ move the row
/// one place at once (Arc); Space / Enter picks it up, arrows move it, Space / Enter drops and Escape cancels.
/// </summary>
internal sealed class ReorderableListHandle : Decorator
{
    private ImmutablePen? _focusPen;
    private Color _focusColor;

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        Border.BackgroundProperty.AddOwner<ReorderableListHandle>();

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<ReorderableListHandle>();

    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<ReorderableListHandle, IBrush?>(nameof(FocusRingBrush));

    static ReorderableListHandle()
    {
        AffectsRender<ReorderableListHandle>(BackgroundProperty, CornerRadiusProperty);
    }

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public ReorderableListHandle()
    {
        Focusable = true;
        IsTabStop = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        FocusAdorner = null;
    }

    public IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }

    internal ReorderableListItem? Owner { get; set; }

    public void Describe(string title, int position, int count)
    {
        AutomationProperties.SetName(this, $"Reorder {title}. Position {position} of {count}. Use arrow keys.");
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Owner is { } owner && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && !e.Handled)
        {
            e.Handled = owner.List.BeginPointerDrag(owner, e);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Owner?.List.ContinuePointerDrag(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        Owner?.List.EndPointerDrag(e.Pointer, cancel: false);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        Owner?.List.EndPointerDrag(e.Pointer, cancel: false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && Owner is { } owner)
        {
            e.Handled = owner.List.HandleKey(owner, e.Key);
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Owner?.List.OnHandleLostFocus(Owner);
        InvalidateVisual();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (Background is { } background)
        {
            context.DrawRectangle(background, null, new Rect(Bounds.Size), CornerRadius.TopLeft, CornerRadius.TopLeft);
        }
        if (IsFocused && Classes.Contains(":focus-visible") && FocusRingBrush is ISolidColorBrush ring)
        {
            if (_focusPen is null || _focusColor != ring.Color)
            {
                _focusColor = ring.Color;
                _focusPen = new ImmutablePen(new ImmutableSolidColorBrush(ring.Color), 2);
            }
            var radius = CornerRadius.TopLeft + 3;
            context.DrawRectangle(null, _focusPen, new Rect(Bounds.Size).Inflate(3), radius, radius);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new HandlePeer(this);

    private sealed class HandlePeer : ControlAutomationPeer
    {
        public HandlePeer(ReorderableListHandle owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>Default content of a reorderable row: a title (14px medium) and an optional detail line (12px muted).</summary>
internal sealed class ReorderableListEntryTemplate : IDataTemplate
{
    public static readonly ReorderableListEntryTemplate Instance = new();

    public bool Match(object? data) => true;

    public Control? Build(object? param)
    {
        var title = new TextBlock
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            FontWeight = FontWeight.Medium
        };
        var detail = new TextBlock
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        };
        ArcTokens.Bind(title, TextBlock.FontSizeProperty, DesignTokens.ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(title, TextBlock.FontWeightProperty, DesignTokens.ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(detail, TextBlock.FontSizeProperty, DesignTokens.ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(detail, TextBlock.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
        var stack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { title, detail } };
        stack.DataContextChanged += (_, _) => Fill(stack.DataContext);
        Fill(param);
        return stack;

        void Fill(object? data)
        {
            if (data is ReorderableListEntry entry)
            {
                title.Text = entry.Title;
                detail.Text = entry.Detail;
                detail.IsVisible = !string.IsNullOrEmpty(entry.Detail);
            }
            else
            {
                title.Text = Convert.ToString(data, CultureInfo.CurrentCulture);
                detail.IsVisible = false;
            }
        }
    }
}
