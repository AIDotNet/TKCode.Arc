using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The floating "Ask about selection" button under the end of a page selection (spec 3.9): 30 high, padding 0 12 0 10,
/// the assistant's 14px mark and the text 6px apart (12 / 16.8, weight 500, Foreground), a pill with a 1px Border on
/// SurfaceRaised and ShadowRaised; hover SurfaceMuted (160ms standard). <see cref="Position"/> is where the page places it
/// in the article. Its accessible name is <see cref="Text"/>.
/// </summary>
internal sealed class AiPanelAskButton : AvaloniaButton
{
    public const string HoverPseudoClass = ":hover";

    /// <summary>The label ("Ask about selection").</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiPanelAskButton, string?>(nameof(Text));

    /// <summary>The assistant's mark; null draws the default spark.</summary>
    public static readonly StyledProperty<object?> MarkProperty =
        AvaloniaProperty.Register<AiPanelAskButton, object?>(nameof(Mark));

    /// <summary>The mark drawn (<see cref="Mark"/> or the spark).</summary>
    public static readonly DirectProperty<AiPanelAskButton, object> ShownMarkProperty =
        AvaloniaProperty.RegisterDirect<AiPanelAskButton, object>(nameof(ShownMark), o => o.ShownMark);

    private object _shownMark = AiAssistantSpark.Geometry;

    protected override Type StyleKeyOverride => typeof(AiPanelAskButton);

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="MarkProperty"/>
    public object? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    /// <inheritdoc cref="ShownMarkProperty"/>
    public object ShownMark { get => _shownMark; private set => SetAndRaise(ShownMarkProperty, ref _shownMark, value); }

    /// <summary>The top-left corner in the article.</summary>
    public Point Position { get; set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, Text);
        }
        else if (change.Property == MarkProperty)
        {
            ShownMark = AiAssistantSpark.Resolve(Mark);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }
}
