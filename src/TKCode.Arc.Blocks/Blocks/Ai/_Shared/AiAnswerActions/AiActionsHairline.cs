using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A 1px hairline of Arc's AI answer (menu separators, the lines between related questions): BorderSubtle in the light
/// theme and Border in the dark one, where BorderSubtle would almost vanish on the background. The theme is read from the
/// background token's lightness, so it follows any palette. It stretches along its parent and takes no input.
/// </summary>
internal sealed class AiActionsHairline : Control
{
    /// <summary>The line's color (set from the theme; read-only in practice).</summary>
    public static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<AiActionsHairline, IBrush?>(nameof(Brush));

    private IDisposable? _binding;
    private ArcDesignTokenKind? _kind;

    static AiActionsHairline()
    {
        AffectsRender<AiActionsHairline>(BrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AiActionsHairline>(false);
        FocusableProperty.OverrideDefaultValue<AiActionsHairline>(false);
        HeightProperty.OverrideDefaultValue<AiActionsHairline>(1);
    }

    public AiActionsHairline()
    {
        ActualThemeVariantChanged += (_, _) => Refresh();
        ResourcesChanged += (_, _) => Refresh();
    }

    /// <inheritdoc cref="BrushProperty"/>
    public IBrush? Brush { get => GetValue(BrushProperty); set => SetValue(BrushProperty, value); }

    /// <summary>The token the line uses now (Border in a dark theme, BorderSubtle otherwise).</summary>
    internal ArcDesignTokenKind? Kind => _kind;

    /// <summary>Whether the background token reads as dark (lightness below 0.5).</summary>
    internal static bool IsDark(Control owner) => Oklch.FromColor(ArcTokens.GetColor(owner, ArcDesignTokenKind.Background)).L < 0.5;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Refresh();
    }

    private void Refresh()
    {
        if (TopLevel.GetTopLevel(this) is null)
        {
            return;
        }
        var kind = IsDark(this) ? ArcDesignTokenKind.Border : ArcDesignTokenKind.BorderSubtle;
        if (kind == _kind)
        {
            return;
        }
        _kind = kind;
        _binding?.Dispose();
        _binding = ArcTokens.Bind(this, BrushProperty, kind);
    }

    public override void Render(DrawingContext context)
    {
        if (Brush is { } brush)
        {
            context.FillRectangle(brush, new Rect(Bounds.Size));
        }
    }
}
