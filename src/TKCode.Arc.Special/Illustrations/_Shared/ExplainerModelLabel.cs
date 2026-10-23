using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// The model line of an explainer header: the model's mark (a 24×24 stroked glyph; a sparkle in the accent by default)
/// and its name, one line, cut with an ellipsis. Nothing is shown for a null <see cref="Model"/>. The mark is
/// decorative; the name is real text.
/// </summary>
internal sealed class ExplainerModelLabel : Control
{
    /// <summary>Defines the <see cref="Model"/> property.</summary>
    public static readonly StyledProperty<ExplainerModel?> ModelProperty =
        AvaloniaProperty.Register<ExplainerModelLabel, ExplainerModel?>(nameof(Model));

    /// <summary>Defines the <see cref="MarkSize"/> property.</summary>
    public static readonly StyledProperty<double> MarkSizeProperty =
        AvaloniaProperty.Register<ExplainerModelLabel, double>(nameof(MarkSize), 16);

    /// <summary>Defines the <see cref="FontSize"/> property.</summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<ExplainerModelLabel>();

    /// <summary>Defines the <see cref="FontWeight"/> property.</summary>
    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        TextElement.FontWeightProperty.AddOwner<ExplainerModelLabel>();

    private readonly StackPanel _row = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    private readonly ArcGlyph _mark = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _name = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private IDisposable? _markColor;

    public ExplainerModelLabel()
    {
        _row.Children.Add(_mark);
        _row.Children.Add(_name);
        VisualChildren.Add(_row);
        LogicalChildren.Add(_row);
        ArcTokens.Bind(_name, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        Update();
    }

    /// <summary>The model to show.</summary>
    public ExplainerModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <summary>The mark size in px (default 16).</summary>
    public double MarkSize { get => GetValue(MarkSizeProperty); set => SetValue(MarkSizeProperty, value); }

    /// <summary>The name's font size (inherited).</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>The name's font weight (inherited).</summary>
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    /// <summary>The mark drawn when the model has none.</summary>
    public static Geometry DefaultMark => ArcIcons.Sparkles;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModelProperty || change.Property == MarkSizeProperty)
        {
            Update();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _row.Measure(availableSize);
        return _row.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _row.Arrange(new Rect(finalSize));
        return finalSize;
    }

    private void Update()
    {
        var model = Model;
        IsVisible = model is not null;
        if (model is null)
        {
            return;
        }
        _mark.Data = model.Mark ?? DefaultMark;
        _mark.Size = MarkSize;
        _markColor?.Dispose();
        _markColor = null;
        if (model.MarkBrush is { } brush)
        {
            _mark.Foreground = brush;
        }
        else
        {
            _markColor = ArcTokens.Bind(_mark, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Series1);
        }
        _name.Text = model.Name;
    }
}
