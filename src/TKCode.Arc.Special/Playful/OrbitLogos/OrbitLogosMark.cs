using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A filled path-data mark on a 24 by 24 viewBox, scaled to fit its box. It paints in <see cref="Fill"/> and falls back
/// to the inherited text color, so black-and-white marks follow the theme.
/// </summary>
internal sealed class OrbitLogosMark : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<OrbitLogosMark, Geometry?>(nameof(Data));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<OrbitLogosMark, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<OrbitLogosMark>();

    static OrbitLogosMark()
    {
        AffectsRender<OrbitLogosMark>(DataProperty, FillProperty, ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<OrbitLogosMark>(false);
    }

    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Parses path data; a path that fails to parse is no mark. The fill rule is non-zero, as in the format.</summary>
    public static Geometry? TryParse(string data)
    {
        try
        {
            return ArcIcons.Parse("F1 " + data.Trim());
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(0, 0);

    public override void Render(DrawingContext context)
    {
        if (Data is null)
        {
            return;
        }
        var side = Math.Min(Bounds.Width, Bounds.Height);
        if (side <= 0)
        {
            return;
        }
        var scale = side / 24;
        var x = (Bounds.Width - side) / 2;
        var y = (Bounds.Height - side) / 2;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y)))
        {
            context.DrawGeometry(Fill ?? Foreground, null, Data);
        }
    }
}
