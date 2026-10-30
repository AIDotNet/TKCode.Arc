using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A model or aspect chip: a radio button in a group with one tab stop (the chosen chip). Arrow keys move to the next or
/// previous chip and choose it, wrapping at the ends. A model chip shows its 16px mark (or its first letter) and springs
/// its name open only while chosen; an aspect chip shows a small outline of its shape and its label. The card owns the
/// choice, so pressing raises <see cref="Picked"/> and the card checks the chip.
/// </summary>
[TemplatePart(ShapePart, typeof(Border))]
[PseudoClasses(CollapsesPseudoClass, MonogramPseudoClass, ShapedPseudoClass)]
internal sealed class ImageGenerationChip : RadioButton
{
    internal const string ShapePart = "PART_Shape";

    /// <summary>A model chip: the name shows only while chosen.</summary>
    internal const string CollapsesPseudoClass = ":collapses";

    /// <summary>No mark: the first letter stands in.</summary>
    internal const string MonogramPseudoClass = ":monogram";

    /// <summary>An aspect chip with its shape outline.</summary>
    internal const string ShapedPseudoClass = ":shaped";

    public static readonly StyledProperty<Geometry?> MarkProperty =
        AvaloniaProperty.Register<ImageGenerationChip, Geometry?>(nameof(Mark));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ImageGenerationChip, string?>(nameof(Text));

    public static readonly StyledProperty<string?> MonogramProperty =
        AvaloniaProperty.Register<ImageGenerationChip, string?>(nameof(Monogram));

    public static readonly StyledProperty<bool> CollapsesProperty =
        AvaloniaProperty.Register<ImageGenerationChip, bool>(nameof(Collapses));

    /// <summary>The shape's outline (an aspect chip), sized to fit <see cref="ShapeSize"/>; 0 draws none.</summary>
    public static readonly StyledProperty<double> ShapeAspectProperty =
        AvaloniaProperty.Register<ImageGenerationChip, double>(nameof(ShapeAspect));

    public static readonly StyledProperty<double> ShapeSizeProperty =
        AvaloniaProperty.Register<ImageGenerationChip, double>(nameof(ShapeSize), 12);

    private Border? _shape;

    public Geometry? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public string? Monogram { get => GetValue(MonogramProperty); set => SetValue(MonogramProperty, value); }

    public bool Collapses { get => GetValue(CollapsesProperty); set => SetValue(CollapsesProperty, value); }

    public double ShapeAspect { get => GetValue(ShapeAspectProperty); set => SetValue(ShapeAspectProperty, value); }

    public double ShapeSize { get => GetValue(ShapeSizeProperty); set => SetValue(ShapeSizeProperty, value); }

    /// <summary>The id of the model or aspect.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Raised when the person presses the chip or arrows onto it.</summary>
    public event EventHandler<RoutedEventArgs>? Picked;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _shape = e.NameScope.Find<Border>(ShapePart);
        UpdateShape();
        UpdateClasses();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ShapeAspectProperty || change.Property == ShapeSizeProperty)
        {
            UpdateShape();
            UpdateClasses();
        }
        else if (change.Property == MarkProperty || change.Property == CollapsesProperty)
        {
            UpdateClasses();
        }
        else if (change.Property == IsCheckedProperty)
        {
            // One tab stop per group: the chosen chip.
            IsTabStop = IsChecked == true;
        }
    }

    /// <summary>The card owns the choice.</summary>
    protected override void Toggle() => Picked?.Invoke(this, new RoutedEventArgs());

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var step = e.Key switch
        {
            Key.Right or Key.Down => 1,
            Key.Left or Key.Up => -1,
            Key.Home => int.MinValue,
            Key.End => int.MaxValue,
            _ => 0,
        };
        if (step == 0 || e.KeyModifiers != KeyModifiers.None || Parent is not Panel panel)
        {
            base.OnKeyDown(e);
            return;
        }
        var chips = new List<ImageGenerationChip>();
        foreach (var child in panel.Children)
        {
            if (child is ImageGenerationChip { IsVisible: true, IsEnabled: true } chip)
            {
                chips.Add(chip);
            }
        }
        var index = chips.IndexOf(this);
        if (index < 0 || chips.Count == 0)
        {
            base.OnKeyDown(e);
            return;
        }
        var next = step switch
        {
            int.MinValue => 0,
            int.MaxValue => chips.Count - 1,
            _ => ((index + step) % chips.Count + chips.Count) % chips.Count,
        };
        e.Handled = true;
        var target = chips[next];
        target.Focus(NavigationMethod.Directional);
        target.Picked?.Invoke(target, new RoutedEventArgs());
    }

    private void UpdateShape()
    {
        if (_shape is null)
        {
            return;
        }
        var aspect = ShapeAspect;
        if (aspect <= 0 || !double.IsFinite(aspect))
        {
            return;
        }
        var size = ShapeSize;
        // Fit the shape in the square, at least a third of it on the short side.
        var width = aspect > 1 ? size / aspect : size;
        var height = aspect > 1 ? size : size * aspect;
        _shape.Width = Math.Max(size / 3, Math.Round(width));
        _shape.Height = Math.Max(size / 3, Math.Round(height));
    }

    private void UpdateClasses()
    {
        PseudoClasses.Set(CollapsesPseudoClass, Collapses);
        PseudoClasses.Set(MonogramPseudoClass, Mark is null && !string.IsNullOrEmpty(Monogram));
        PseudoClasses.Set(ShapedPseudoClass, ShapeAspect > 0);
    }
}
