using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc.Mobile;

/// <summary>
/// A mobile page: an optional <see cref="Header"/> (usually a <see cref="Navbar"/>), the content, and an optional
/// <see cref="Footer"/> (usually a <see cref="Tabbar"/>), laid out inside the device's safe area. The safe area is
/// the space the notch, rounded corners and gesture bar take; on desktop and in tests it is zero unless set by hand.
/// </summary>
public class Page : ContentControl
{
    /// <summary>The slot above the content, usually a <see cref="Navbar"/>.</summary>
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<Page, object?>(nameof(Header));

    /// <summary>The slot below the content, usually a <see cref="Tabbar"/>.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<Page, object?>(nameof(Footer));

    /// <summary>
    /// The insets the page keeps free on each edge. With <see cref="UsePlatformSafeArea"/> the platform sets it
    /// (zero where there is no insets manager); otherwise set it yourself.
    /// </summary>
    public static readonly StyledProperty<Thickness> SafeAreaProperty =
        AvaloniaProperty.Register<Page, Thickness>(nameof(SafeArea));

    /// <summary>Whether <see cref="SafeArea"/> follows the platform's safe area padding.</summary>
    public static readonly StyledProperty<bool> UsePlatformSafeAreaProperty =
        AvaloniaProperty.Register<Page, bool>(nameof(UsePlatformSafeArea), true);

    private TopLevel? _topLevel;
    private Avalonia.Controls.Platform.IInsetsManager? _insets;

    /// <inheritdoc cref="HeaderProperty"/>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <inheritdoc cref="FooterProperty"/>
    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <inheritdoc cref="SafeAreaProperty"/>
    public Thickness SafeArea
    {
        get => GetValue(SafeAreaProperty);
        set => SetValue(SafeAreaProperty, value);
    }

    /// <inheritdoc cref="UsePlatformSafeAreaProperty"/>
    public bool UsePlatformSafeArea
    {
        get => GetValue(UsePlatformSafeAreaProperty);
        set => SetValue(UsePlatformSafeAreaProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(Page);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Track(TopLevel.GetTopLevel(this));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Track(null);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UsePlatformSafeAreaProperty && _topLevel is not null)
        {
            Track(_topLevel);
        }
    }

    /// <summary>Follows the top level's safe area while <see cref="UsePlatformSafeArea"/> is on.</summary>
    private void Track(TopLevel? topLevel)
    {
        if (_insets is not null)
        {
            _insets.SafeAreaChanged -= OnSafeAreaChanged;
            _insets = null;
        }
        _topLevel = topLevel;
        if (topLevel is null || !UsePlatformSafeArea)
        {
            return;
        }
        _insets = topLevel.InsetsManager;
        if (_insets is not null)
        {
            _insets.SafeAreaChanged += OnSafeAreaChanged;
        }
        // No insets manager (desktop, headless): the platform reports no safe area.
        SafeArea = _insets?.SafeAreaPadding ?? default;
    }

    private void OnSafeAreaChanged(object? sender, Avalonia.Controls.Platform.SafeAreaChangedArgs e) => SafeArea = e.SafeAreaPadding;
}
