using TKCode.Arc.DesignTokens;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One cart row: thumbnail, name, detail, quantity stepper, line price and ×; swiped left it reveals a red "Remove" layer
/// and, past 110px or on a flick, slides out; removed rows fade and collapse their height so the rows below move up.
/// </summary>
/// <remarks>
/// Code-driven motion: the swipe follows the pointer 1:1 and hands its release velocity to the snap-back (settle) or
/// slide-out (smooth) spring, and the height collapse is a real layout height on the smooth spring.
/// </remarks>
internal sealed class CartDrawerRow : Control
{
    private readonly Border _frame;
    private readonly Border _backdrop;
    private readonly StackPanel _backdropLabel;
    private readonly ScaleTransform _labelScale = new();
    private readonly Border _front;
    private readonly TranslateTransform _frontShift = new();
    private readonly Border _thumb;
    private readonly Image _thumbImage;
    private readonly TextBlock _name;
    private readonly TextBlock _detail;
    private readonly Border _stepper;
    private readonly CartDrawerPlainButton _decrease;
    private readonly CartDrawerPlainButton _increase;
    private readonly RollingDigits _quantity;
    private readonly RollingDigits _price;
    private readonly CartDrawerPlainButton _remove;
    private readonly DragTracker _drag;
    private MotionValue<double>? _offset;
    private MotionValue<double>? _collapse;
    private MotionValue<double>? _fade;
    private bool _armed;
    private double _heightFactor = 1;
    private int _quantityValue;
    private int _maxQuantity = CartDrawerMath.DefaultMaxQuantity;

    public CartDrawerRow(CartDrawerProduct product)
    {
        Product = product;
        ClipToBounds = true;

        _thumbImage = new Image { Stretch = Stretch.UniformToFill };
        _thumb = new Border { Child = _thumbImage, ClipToBounds = true, Width = 64, Height = 64, VerticalAlignment = VerticalAlignment.Top }
            .BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        ArcTokens.Bind(_thumb, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);

        _name = CartDrawerBuild.Text(null, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, medium: true, lineHeight: 19.6);
        _detail = CartDrawerBuild.Text(null, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary, lineHeight: 16.8);

        _decrease = CartDrawerBuild.IconButton(ArcIcons.Trash2, 28, 14, string.Empty, ArcDesignTokenKind.TextSecondary);
        _decrease.Width = 30;
        _decrease.Click += (_, e) =>
        {
            e.Handled = true;
            DecreaseRequested?.Invoke(this, EventArgs.Empty);
        };
        _increase = CartDrawerBuild.IconButton(ArcIcons.Plus, 28, 14, string.Empty, ArcDesignTokenKind.TextSecondary);
        _increase.Width = 30;
        _increase.Click += (_, e) =>
        {
            e.Handled = true;
            IncreaseRequested?.Invoke(this, EventArgs.Empty);
        };
        _quantity = CartDrawerBuild.Digits(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground);
        _quantity.HorizontalAlignment = HorizontalAlignment.Center;
        var quantityBox = new Border { Width = 18, Child = _quantity };
        var stepperRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { _decrease, quantityBox, _increase } };
        _stepper = new Border
        {
            Child = stepperRow,
            Height = 30,
            Width = 80,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 6, 0, 0)
        }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(_stepper, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(_stepper, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);

        var info = new StackPanel { Spacing = 1, Margin = new Thickness(0, 2, 0, 0), Children = { _name, _detail, _stepper } };

        _price = CartDrawerBuild.Digits(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground);
        _price.HorizontalAlignment = HorizontalAlignment.Right;
        _remove = CartDrawerBuild.IconButton(ArcIcons.X, 28, 15, string.Empty, ArcDesignTokenKind.TextMuted);
        _remove.HorizontalAlignment = HorizontalAlignment.Right;
        _remove.Margin = new Thickness(0, 0, -4, 0);
        _remove.Click += (_, e) =>
        {
            e.Handled = true;
            RemoveRequested?.Invoke(this, EventArgs.Empty);
        };
        var side = new StackPanel { Spacing = 6, Margin = new Thickness(0, 2, 0, 0), Children = { _price, _remove } };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        grid.Children.Add(_thumb);
        Grid.SetColumn(info, 1);
        grid.Children.Add(info);
        Grid.SetColumn(side, 2);
        grid.Children.Add(side);

        _front = new Border { Child = grid, Padding = new Thickness(8), RenderTransform = _frontShift, Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) }
            .BindCornerRadius(ArcDesignTokenKind.RadiusControl);
        ArcTokens.Bind(_front, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceRaised);

        var bin = new ArcGlyph { Data = ArcIcons.Trash2, Size = 15, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center };
        var removeText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(removeText, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(removeText, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        _backdropLabel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 20, 0),
            RenderTransform = _labelScale,
            Children = { bin, removeText }
        };
        RemoveLabel = removeText;
        RemoveGlyph = bin;
        _backdrop = new Border { Child = _backdropLabel, Opacity = 0 }.BindCornerRadius(ArcDesignTokenKind.RadiusControl);
        _labelScale.ScaleX = _labelScale.ScaleY = 0.7;

        _frame = new Border
        {
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 0, 4),
            Child = new Panel { Children = { _backdrop, _front } }
        }.BindCornerRadius(ArcDesignTokenKind.RadiusControl);
        LogicalChildren.Add(_frame);
        VisualChildren.Add(_frame);

        _drag = new DragTracker(_front, new DragOptions { Axis = DragAxis.Horizontal, Threshold = 6 });
        _drag.CanBegin = _ => CanSwipe?.Invoke() ?? false;
        _drag.Moved += OnDragMoved;
        _drag.Completed += OnDragReleased;
        _drag.Cancelled += (_, _) => SnapBack(0);
    }

    public CartDrawerProduct Product { get; }

    /// <summary>The "Remove" text behind the row (its color is set by the block).</summary>
    public TextBlock RemoveLabel { get; }

    /// <summary>The bin behind the row.</summary>
    public ArcGlyph RemoveGlyph { get; }

    /// <summary>Asked before a swipe begins.</summary>
    public Func<bool>? CanSwipe { get; set; }

    public event EventHandler? DecreaseRequested;
    public event EventHandler? IncreaseRequested;
    public event EventHandler? RemoveRequested;

    /// <summary>Raised when a swipe is released past the remove distance or flicked (the row has started sliding out).</summary>
    public event EventHandler? SwipeRemoved;

    /// <summary>True from the moment the row starts leaving.</summary>
    public bool IsLeaving { get; private set; }

    public int Quantity => _quantityValue;

    public double SwipeOffset => _offset?.Value ?? 0;

    /// <summary>Horizontal speed (px/s) of the last swipe release.</summary>
    public double ReleaseVelocity { get; private set; }

    public CartDrawerPlainButton DecreaseButton => _decrease;
    public CartDrawerPlainButton IncreaseButton => _increase;
    public CartDrawerPlainButton RemoveButton => _remove;
    public Border Front => _front;
    public Border Backdrop => _backdrop;
    public Border Stepper => _stepper;

    public static readonly StyledProperty<IBrush?> RemoveFillProperty =
        AvaloniaProperty.Register<CartDrawerRow, IBrush?>(nameof(RemoveFill));

    public static readonly StyledProperty<IBrush?> RemoveFillArmedProperty =
        AvaloniaProperty.Register<CartDrawerRow, IBrush?>(nameof(RemoveFillArmed));

    /// <summary>The fill of the red layer.</summary>
    public IBrush? RemoveFill
    {
        get => GetValue(RemoveFillProperty);
        set => SetValue(RemoveFillProperty, value);
    }

    /// <summary>The fill of the red layer once a release would remove the row.</summary>
    public IBrush? RemoveFillArmed
    {
        get => GetValue(RemoveFillArmedProperty);
        set => SetValue(RemoveFillArmedProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RemoveFillProperty || change.Property == RemoveFillArmedProperty)
        {
            RefreshRemoveFill();
        }
    }

    /// <summary>Shows the product and quantity; amounts use the block's currency and culture.</summary>
    public void Update(int quantity, int max, CartDrawerLabels labels, string currency, int decimals)
    {
        _quantityValue = quantity;
        _maxQuantity = max;
        _thumbImage.Source = Product.Image;
        _name.Text = Product.Name;
        _detail.Text = Product.Detail;
        _detail.IsVisible = !string.IsNullOrEmpty(Product.Detail);
        _quantity.CultureName = labels.Culture.Name;
        _quantity.Value = quantity;
        CartDrawerFormat.Apply(_price, Product.Price * quantity, currency, labels.Culture, decimals);
        AutomationProperties.SetName(_price, CartDrawerFormat.Money(Product.Price * quantity, currency, labels.Culture, decimals));
        AutomationProperties.SetName(_quantity, quantity.ToString(labels.Culture));
        _decrease.Glyph = quantity <= 1 ? ArcIcons.Trash2 : ArcIcons.Minus;
        AutomationProperties.SetName(_decrease, quantity <= 1 ? labels.RemoveItem(Product.Name) : labels.Decrease(Product.Name));
        AutomationProperties.SetName(_increase, labels.Increase(Product.Name));
        AutomationProperties.SetName(_remove, labels.RemoveItem(Product.Name));
        AutomationProperties.SetName(_stepper, labels.QuantityGroup(Product.Name));
        _increase.IsEnabled = quantity < max;
        RemoveLabel.Text = labels.SwipeRemove;
    }

    /// <summary>Thumbnail side (64, or 52 in narrow blocks).</summary>
    public void SetThumbnailSize(double size)
    {
        _thumb.Width = size;
        _thumb.Height = size;
    }

    // ---- Swipe ----

    private MotionValue<double> Offset()
    {
        if (_offset is null)
        {
            _offset = new MotionValue<double>(this, 0, MotionVectors.Double);
            _offset.Changed += ApplyOffset;
        }
        return _offset;
    }

    private void OnDragMoved(object? sender, DragGestureEventArgs e)
    {
        if (IsLeaving)
        {
            return;
        }
        // 1:1 to the left without rubber banding; rightward travel is held at 0.
        Offset().Jump(Math.Min(0, e.Offset.X));
    }

    private void OnDragReleased(object? sender, DragGestureEventArgs e)
    {
        if (IsLeaving)
        {
            return;
        }
        var offset = Offset().Value;
        var velocity = e.Velocity.X;
        ReleaseVelocity = velocity;
        if (CartDrawerMath.ShouldRemove(offset, velocity))
        {
            SwipeRemoved?.Invoke(this, EventArgs.Empty);
            return;
        }
        SnapBack(velocity);
    }

    private void SnapBack(double velocity)
    {
        if (IsLeaving || _offset is null)
        {
            return;
        }
        _ = _offset.AnimateTo(0, MotionSpec.Spring(ArcSpring.Settle), velocity);
    }

    private void ApplyOffset(double x)
    {
        _frontShift.X = x;
        _backdrop.Opacity = CartDrawerMath.SwipeBackdropOpacity(x);
        var scale = CartDrawerMath.SwipeLabelScale(x);
        _labelScale.ScaleX = scale;
        _labelScale.ScaleY = scale;
        var armed = CartDrawerMath.IsSwipeArmed(x);
        if (armed != _armed || _backdrop.Background is null)
        {
            _armed = armed;
            _backdrop.Background = armed ? RemoveFillArmed : RemoveFill;
        }
    }

    /// <summary>Repaints the red layer after a theme change.</summary>
    public void RefreshRemoveFill()
    {
        _backdrop.Background = _armed ? RemoveFillArmed : RemoveFill;
    }

    // ---- Leaving ----

    /// <summary>
    /// Plays the row's exit: the row fades over 160ms and its height collapses on the smooth spring (after 77ms when a
    /// button removed it; at once, with the front sliding out with the release velocity, after a swipe).
    /// </summary>
    public async Task LeaveAsync(bool swiped, double velocity = 0)
    {
        IsLeaving = true;
        _drag.IsEnabled = false;
        IsHitTestVisible = false;
        _fade = new MotionValue<double>(this, Opacity, MotionVectors.Double);
        _fade.Changed += v => Opacity = v;
        _collapse = new MotionValue<double>(this, _heightFactor, MotionVectors.Double);
        _collapse.Changed += v =>
        {
            _heightFactor = Math.Max(0, v);
            InvalidateMeasure();
        };
        if (swiped)
        {
            // The slide-out does not gate the removal: once the row is faded and collapsed nothing of it shows, and a
            // fast release can keep the offset spring moving (or be superseded) after that.
            var width = Math.Max(Bounds.Width, 1);
            _ = Offset().AnimateTo(-width, MotionSpec.Spring(ArcSpring.Smooth), Math.Min(0, velocity));
        }
        await Task.WhenAll(
            _fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)),
            _collapse.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth, swiped ? TimeSpan.Zero : TimeSpan.FromMilliseconds(77))));
    }

    /// <summary>Height share still shown (1 at rest, 0 when collapsed).</summary>
    public double HeightFactor => _heightFactor;

    protected override Size MeasureOverride(Size availableSize)
    {
        _frame.Measure(availableSize);
        var size = _frame.DesiredSize;
        return new Size(size.Width, size.Height * _heightFactor);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _frame.Arrange(new Rect(0, 0, finalSize.Width, _frame.DesiredSize.Height));
        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _drag.Cancel();
    }
}
