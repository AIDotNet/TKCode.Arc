using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The small glyph buttons of <see cref="Tabs"/>: a tab's close button (class <c>close</c>, not focusable: Delete on the
/// focused tab closes it) and the add button at the end of the list (class <c>add</c>, plus <c>card</c> in the card
/// variant). Their own type, so the tabs build them only when they need one and the theme sizes them by class.
/// </summary>
internal sealed class TabGlyphButton : AvaloniaButton
{
    public const string CloseClass = "close";
    public const string AddClass = "add";
    public const string CardClass = "card";

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<TabGlyphButton, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<TabGlyphButton, double>(nameof(GlyphSize), 12);

    public Geometry? Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public double GlyphSize
    {
        get => GetValue(GlyphSizeProperty);
        set => SetValue(GlyphSizeProperty, value);
    }

    private LazyFocusRing? _focusRing;

    protected override Type StyleKeyOverride => typeof(TabGlyphButton);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        // Only the add button takes keyboard focus; the ring is built the first time it shows.
        _focusRing ??= new LazyFocusRing(this, TabsTokens.Key(TabsTokenKind.AddFocusRingMargin), TabsTokens.Key(TabsTokenKind.AddFocusRingRadius));
        _focusRing.Attach(e.NameScope.Find<Panel>("PART_Host"));
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }
}
