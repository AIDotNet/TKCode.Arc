using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The items panel of a <see cref="MagnifyDock"/>: lays the icons out once at rest, bottom-aligned and centred, and
/// reserves the room the widest magnified row and its labels need, so magnifying never re-measures the page. The
/// magnification itself is each icon's render transform, written by the dock every frame.
/// </summary>
internal sealed class MagnifyDockPanel : Panel
{
    internal MagnifyDock? Dock { get; set; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dock = this.FindAncestorOfType<MagnifyDock>();
        Dock?.AttachPanel(this);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Dock?.DetachPanel(this);
        Dock = null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dock = Dock;
        var model = dock?.Model;
        var size = model?.ItemSize ?? 48;
        foreach (var child in Children)
        {
            child.Measure(new Size(size, size));
        }
        if (dock is null || model is null)
        {
            return default;
        }
        model.Resize(Children.Count);
        var padding = dock.ShelfPadding;
        var width = model.MaxWidth() + padding * 2;
        var height = size * Math.Max(1, model.Magnification) + padding * 2 + dock.LabelSpace;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var dock = Dock;
        var model = dock?.Model;
        if (dock is null || model is null)
        {
            return finalSize;
        }
        var size = model.ItemSize;
        var origin = dock.RowOrigin(finalSize.Width);
        var bottom = finalSize.Height - dock.ShelfPadding;
        for (var i = 0; i < Children.Count; i++)
        {
            var center = origin + model.RestCenter(i);
            Children[i].Arrange(new Rect(center - size / 2, bottom - size, size, size));
        }
        dock.ApplyPoses();
        return finalSize;
    }
}

/// <summary>
/// The shelf behind a <see cref="MagnifyDock"/>'s icons: a rounded bar that widens with the magnified row, and a dot
/// under every running icon.
/// </summary>
internal sealed class MagnifyDockShelf : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<MagnifyDockShelf, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<MagnifyDockShelf, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<IBrush?> DotFillProperty =
        AvaloniaProperty.Register<MagnifyDockShelf, IBrush?>(nameof(DotFill));

    public static readonly StyledProperty<BoxShadows> ShadowProperty =
        AvaloniaProperty.Register<MagnifyDockShelf, BoxShadows>(nameof(Shadow));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<MagnifyDockShelf, CornerRadius>(nameof(CornerRadius));

    private IPen? _pen;

    static MagnifyDockShelf()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<MagnifyDockShelf>(false);
        AffectsRender<MagnifyDockShelf>(FillProperty, DotFillProperty, ShadowProperty, CornerRadiusProperty);
    }

    internal MagnifyDock? Dock { get; set; }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    public IBrush? DotFill { get => GetValue(DotFillProperty); set => SetValue(DotFillProperty, value); }

    public BoxShadows Shadow { get => GetValue(ShadowProperty); set => SetValue(ShadowProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StrokeProperty)
        {
            _pen = null;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        if (Dock is not { } dock || dock.Model.Count == 0)
        {
            return;
        }
        var model = dock.Model;
        var padding = dock.ShelfPadding;
        var origin = dock.RowOrigin(Bounds.Width);
        var left = origin + dock.RowLeft - padding;
        var width = model.Width + padding * 2;
        var height = model.ItemSize + padding * 2;
        var shelf = new Rect(left, Bounds.Height - height, width, height);
        var radius = Math.Min(CornerRadius.TopLeft, height / 2);
        var pen = _pen ??= Stroke is { } stroke ? new ImmutablePen(stroke.ToImmutable(), 1) : null;
        context.DrawRectangle(Fill, pen, new RoundedRect(shelf, radius), Shadow);
        var dot = Math.Max(1.5, padding * 0.25);
        var y = Bounds.Height - padding / 2;
        for (var i = 0; i < model.Count; i++)
        {
            if (dock.IsItemRunning(i))
            {
                context.DrawEllipse(DotFill, null, new Point(origin + model.Center(i), y), dot, dot);
            }
        }
    }
}

/// <summary>
/// The label above a <see cref="MagnifyDock"/>'s active icon (under the pointer or keyboard focus): a small pill that
/// follows the icon as it grows and hops. Label layouts are built once per label text.
/// </summary>
internal sealed class MagnifyDockLabels : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<MagnifyDockLabels, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<MagnifyDockLabels>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<MagnifyDockLabels>();

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<MagnifyDockLabels>();

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<MagnifyDockLabels, CornerRadius>(nameof(CornerRadius));

    public static readonly StyledProperty<BoxShadows> ShadowProperty =
        AvaloniaProperty.Register<MagnifyDockLabels, BoxShadows>(nameof(Shadow));

    private readonly List<TextLayout?> _layouts = [];
    private bool _dirty = true;

    static MagnifyDockLabels()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<MagnifyDockLabels>(false);
        AffectsRender<MagnifyDockLabels>(FillProperty, CornerRadiusProperty, ShadowProperty);
    }

    internal MagnifyDock? Dock { get; set; }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public BoxShadows Shadow { get => GetValue(ShadowProperty); set => SetValue(ShadowProperty, value); }

    /// <summary>The labels changed: rebuild their layouts before the next frame.</summary>
    public void Reset()
    {
        _dirty = true;
        InvalidateArrange();
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == FontFamilyProperty || change.Property == FontSizeProperty)
        {
            Reset();
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_dirty)
        {
            Build();
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (Dock is not { } dock)
        {
            return;
        }
        var index = dock.LabelIndex;
        var opacity = dock.LabelOpacity;
        if (index < 0 || index >= _layouts.Count || opacity <= 0.01 || _layouts[index] is not { } layout)
        {
            return;
        }
        var model = dock.Model;
        var origin = dock.RowOrigin(Bounds.Width);
        var x = origin + model.Center(index);
        var top = Bounds.Height - dock.ShelfPadding - model.ItemSize * model.Scale(index) - model.Lift(index);
        const double padX = 10;
        const double padY = 4;
        var width = layout.WidthIncludingTrailingWhitespace + padX * 2;
        var height = layout.Height + padY * 2;
        var gap = 8 + (1 - opacity) * 4;
        var box = new Rect(x - width / 2, top - gap - height, width, height);
        var radius = Math.Min(CornerRadius.TopLeft, height / 2);
        using (context.PushOpacity(opacity))
        {
            context.DrawRectangle(Fill, null, new RoundedRect(box, radius), Shadow);
            layout.Draw(context, new Point(box.X + padX, box.Y + padY));
        }
    }

    private void Build()
    {
        _dirty = false;
        foreach (var layout in _layouts)
        {
            layout?.Dispose();
        }
        _layouts.Clear();
        if (Dock is not { } dock)
        {
            return;
        }
        var typeface = new Typeface(FontFamily);
        for (var i = 0; i < dock.Model.Count; i++)
        {
            var text = dock.LabelOf(i);
            _layouts.Add(string.IsNullOrEmpty(text) ? null : new TextLayout(text, typeface, FontSize, Foreground));
        }
    }
}
