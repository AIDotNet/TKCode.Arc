using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc.Mobile;

/// <summary>
/// The top bar of a <see cref="Page"/>: a <see cref="Leading"/> slot (usually a back button), a centered
/// <see cref="Title"/> and a <see cref="Trailing"/> slot for actions. It is 44 high, and the slots are at least 44 wide.
/// </summary>
public class Navbar : TemplatedControl
{
    /// <summary>The bar's title.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Navbar, string?>(nameof(Title));

    /// <summary>The content at the start of the bar, usually a back button.</summary>
    public static readonly StyledProperty<object?> LeadingProperty =
        AvaloniaProperty.Register<Navbar, object?>(nameof(Leading));

    /// <summary>The content at the end of the bar, usually action buttons.</summary>
    public static readonly StyledProperty<object?> TrailingProperty =
        AvaloniaProperty.Register<Navbar, object?>(nameof(Trailing));

    /// <summary>Whether the bar shows a back button (a chevron and <see cref="BackText"/>) before <see cref="Leading"/>.</summary>
    public static readonly StyledProperty<bool> ShowBackProperty =
        AvaloniaProperty.Register<Navbar, bool>(nameof(ShowBack));

    /// <summary>The back button's label, usually the previous screen's title. Empty shows the chevron alone.</summary>
    public static readonly StyledProperty<string?> BackTextProperty =
        AvaloniaProperty.Register<Navbar, string?>(nameof(BackText));

    /// <summary>The command the back button runs.</summary>
    public static readonly StyledProperty<ICommand?> BackCommandProperty =
        AvaloniaProperty.Register<Navbar, ICommand?>(nameof(BackCommand));

    /// <summary>Whether the title also shows as a large heading under the bar, like a root screen in iOS.</summary>
    public static readonly StyledProperty<bool> LargeTitleProperty =
        AvaloniaProperty.Register<Navbar, bool>(nameof(LargeTitle));

    /// <summary>
    /// How far the large title has collapsed into the bar, from 0 (fully shown) to 1 (gone, the bar's own title is shown).
    /// Bind it to the page's scroll offset divided by <see cref="LargeTitleExtent"/>. It does nothing without <see cref="LargeTitle"/>.
    /// </summary>
    public static readonly StyledProperty<double> CollapseProgressProperty =
        AvaloniaProperty.Register<Navbar, double>(nameof(CollapseProgress), coerce: (_, value) => Math.Clamp(value, 0, 1));

    /// <summary>The height of the large title row when it is fully shown.</summary>
    public const double LargeTitleExtent = 52;

    /// <summary>The current height of the large title row, between 0 and <see cref="LargeTitleExtent"/>.</summary>
    public static readonly DirectProperty<Navbar, double> LargeTitleHeightProperty =
        AvaloniaProperty.RegisterDirect<Navbar, double>(nameof(LargeTitleHeight), n => n.LargeTitleHeight);

    /// <summary>The opacity of the title in the bar: 1 normally, and with a large title it fades in as the large one collapses.</summary>
    public static readonly DirectProperty<Navbar, double> BarTitleOpacityProperty =
        AvaloniaProperty.RegisterDirect<Navbar, double>(nameof(BarTitleOpacity), n => n.BarTitleOpacity);

    static Navbar()
    {
        LargeTitleProperty.Changed.AddClassHandler<Navbar>((navbar, _) => navbar.UpdateLargeTitle());
        CollapseProgressProperty.Changed.AddClassHandler<Navbar>((navbar, _) => navbar.UpdateLargeTitle());
    }

    private double _largeTitleHeight;
    private double _barTitleOpacity = 1;

    /// <inheritdoc cref="LargeTitleProperty"/>
    public bool LargeTitle
    {
        get => GetValue(LargeTitleProperty);
        set => SetValue(LargeTitleProperty, value);
    }

    /// <inheritdoc cref="CollapseProgressProperty"/>
    public double CollapseProgress
    {
        get => GetValue(CollapseProgressProperty);
        set => SetValue(CollapseProgressProperty, value);
    }

    /// <inheritdoc cref="LargeTitleHeightProperty"/>
    public double LargeTitleHeight => _largeTitleHeight;

    /// <inheritdoc cref="BarTitleOpacityProperty"/>
    public double BarTitleOpacity => _barTitleOpacity;

    /// <inheritdoc cref="ShowBackProperty"/>
    public bool ShowBack
    {
        get => GetValue(ShowBackProperty);
        set => SetValue(ShowBackProperty, value);
    }

    /// <inheritdoc cref="BackTextProperty"/>
    public string? BackText
    {
        get => GetValue(BackTextProperty);
        set => SetValue(BackTextProperty, value);
    }

    /// <inheritdoc cref="BackCommandProperty"/>
    public ICommand? BackCommand
    {
        get => GetValue(BackCommandProperty);
        set => SetValue(BackCommandProperty, value);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="LeadingProperty"/>
    public object? Leading
    {
        get => GetValue(LeadingProperty);
        set => SetValue(LeadingProperty, value);
    }

    /// <inheritdoc cref="TrailingProperty"/>
    public object? Trailing
    {
        get => GetValue(TrailingProperty);
        set => SetValue(TrailingProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(Navbar);

    private void UpdateLargeTitle()
    {
        var progress = LargeTitle ? CollapseProgress : 1;
        SetAndRaise(LargeTitleHeightProperty, ref _largeTitleHeight, LargeTitle ? LargeTitleExtent * (1 - progress) : 0);
        SetAndRaise(BarTitleOpacityProperty, ref _barTitleOpacity, progress);
    }
}
