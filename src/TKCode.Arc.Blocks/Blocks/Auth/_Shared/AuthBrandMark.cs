using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The default brand marks of the Auth blocks that draw a replaceable outline.</summary>
internal static class AuthBrandMarks
{
    private static Geometry? s_notebook24;
    private static Geometry? s_notebook20;

    /// <summary>
    /// <see cref="LoginSplit"/>'s notebook on a 24-unit box: two curved pages and the spine between them.
    /// </summary>
    public static Geometry Notebook24 => s_notebook24 ??=
        Geometry.Parse("M12 6.6C9.9 5.2 7 4.7 3.75 5V18.4C7 18.1 9.9 18.6 12 20 M12 6.6C14.1 5.2 17 4.7 20.25 5V18.4C17 18.1 14.1 18.6 12 20 M12 6.6V20");

    /// <summary><see cref="LoginImmersive"/>'s notebook on a 20-unit box: two pages bowed out from a spine.</summary>
    public static Geometry Notebook20 => s_notebook20 ??=
        Geometry.Parse("M10 5.6C8.2 4.4 5.6 3.9 3 4.3v10.6c2.6-.4 5.2.1 7 1.3 M10 5.6c1.8-1.2 4.4-1.7 7-1.3v10.6c-2.6-.4-5.2.1-7 1.3 M10 5.6v10.6");
}

/// <summary>
/// A product mark outlined in <see cref="Foreground"/> (2.25 stroke, round caps and joins) on a
/// <see cref="ViewBox"/>-unit box, scaled to the smaller side: <see cref="Data"/>, or <see cref="DefaultData"/> when that is
/// null. Sized by the theme (Width and Height).
/// </summary>
internal sealed class AuthBrandMark : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<AuthBrandMark>();

    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<AuthBrandMark, Geometry?>(nameof(Data));

    public static readonly StyledProperty<Geometry?> DefaultDataProperty =
        AvaloniaProperty.Register<AuthBrandMark, Geometry?>(nameof(DefaultData));

    public static readonly StyledProperty<double> ViewBoxProperty =
        AvaloniaProperty.Register<AuthBrandMark, double>(nameof(ViewBox), 24);

    private Pen? _pen;
    private IBrush? _penBrush;

    static AuthBrandMark()
    {
        AffectsRender<AuthBrandMark>(ForegroundProperty, DataProperty, DefaultDataProperty, ViewBoxProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AuthBrandMark>(false);
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>The outline on a <see cref="ViewBox"/>-unit box; null draws <see cref="DefaultData"/>.</summary>
    public Geometry? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }

    /// <summary>The block's own mark, drawn while <see cref="Data"/> is null.</summary>
    public Geometry? DefaultData { get => GetValue(DefaultDataProperty); set => SetValue(DefaultDataProperty, value); }

    /// <summary>The side of the box the outline is drawn on (24 or 20).</summary>
    public double ViewBox { get => GetValue(ViewBoxProperty); set => SetValue(ViewBoxProperty, value); }

    public override void Render(DrawingContext context)
    {
        if (Foreground is not { } brush)
        {
            return;
        }
        if (_pen is null || !ReferenceEquals(_penBrush, brush))
        {
            _pen = new Pen(brush, 2.25, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _penBrush = brush;
        }
        var scale = Math.Min(Bounds.Width, Bounds.Height) / ViewBox;
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            if ((Data ?? DefaultData) is { } data)
            {
                context.DrawGeometry(null, _pen, data);
            }
        }
    }
}
