using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// A workspace's avatar on its own: the app's logo image (<see cref="TeamSwitcherWorkspace.LogoImage"/>), the brand mark
/// (<see cref="TeamSwitcherWorkspace.Logo"/>) on its brand color (<see cref="TeamSwitcherWorkspace.TileBackground"/>),
/// or the first letter of the name in a square tinted from the theme's
/// accent and series colors (the tint is picked from the name, so a workspace keeps its color). The corner radius and
/// the mark scale with <see cref="TileSize"/>. Drawn in one pass; nothing loads.
/// </summary>
public class TeamSwitcherTile : Control
{
    /// <summary>The workspace to draw.</summary>
    public static readonly StyledProperty<TeamSwitcherWorkspace?> WorkspaceProperty =
        AvaloniaProperty.Register<TeamSwitcherTile, TeamSwitcherWorkspace?>(nameof(Workspace));

    /// <summary>The tile's width and height in pixels; the corner radius, letter and mark scale with it.</summary>
    public static readonly StyledProperty<double> TileSizeProperty =
        AvaloniaProperty.Register<TeamSwitcherTile, double>(nameof(TileSize), 28);

    /// <summary>The tint of a lettered tile (bound to the token the name picks).</summary>
    internal static readonly StyledProperty<IBrush?> TintProperty =
        AvaloniaProperty.Register<TeamSwitcherTile, IBrush?>(nameof(Tint));

    /// <summary>How strongly the tint fills a lettered tile.</summary>
    internal static readonly StyledProperty<double> TintOpacityProperty =
        AvaloniaProperty.Register<TeamSwitcherTile, double>(nameof(TintOpacity), 0.16);

    /// <summary>The light ink for a letter or mark on a dark brand color (token).</summary>
    internal static readonly StyledProperty<Color> InkLightProperty =
        AvaloniaProperty.Register<TeamSwitcherTile, Color>(nameof(InkLight), Colors.White);

    /// <summary>The dark ink for a letter or mark on a light brand color (token; the default matches it for a tile shown on its own).</summary>
    internal static readonly StyledProperty<Color> InkDarkProperty =
        AvaloniaProperty.Register<TeamSwitcherTile, Color>(nameof(InkDark), Color.FromRgb(0x18, 0x18, 0x1B));

    /// <summary>The radius of a 28px tile; other sizes scale it.</summary>
    internal static readonly StyledProperty<CornerRadius> BaseRadiusProperty =
        AvaloniaProperty.Register<TeamSwitcherTile, CornerRadius>(nameof(BaseRadius), new CornerRadius(8));

    /// <summary>The font of the letter (inherited).</summary>
    internal static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<TeamSwitcherTile>();

    private static readonly ArcDesignTokenKind[] s_tints =
    [
        ArcDesignTokenKind.Accent, ArcDesignTokenKind.Series1, ArcDesignTokenKind.Series2, ArcDesignTokenKind.Series3,
        ArcDesignTokenKind.Series4
    ];

    private IDisposable? _tintBinding;
    private int _tint = -1;
    private TeamSwitcherWorkspace? _watched;
    private FormattedText? _letter;
    private string? _letterKey;
    private FontFamily? _letterFont;
    private IBrush? _inkLight;
    private IBrush? _inkDark;

    static TeamSwitcherTile()
    {
        AffectsMeasure<TeamSwitcherTile>(TileSizeProperty);
        AffectsRender<TeamSwitcherTile>(WorkspaceProperty, TintProperty, TintOpacityProperty, InkLightProperty, InkDarkProperty,
            BaseRadiusProperty, FontFamilyProperty);
        FocusableProperty.OverrideDefaultValue<TeamSwitcherTile>(false);
    }

    /// <summary>Creates a tile.</summary>
    public TeamSwitcherTile()
    {
        ArcTokens.Bind(this, BaseRadiusProperty, ArcDesignTokenKind.RadiusStep8, Avalonia.Data.BindingPriority.Style);
        this.Bind(TintOpacityProperty, this.GetResourceObservable(TeamSwitcherTokenKind.TileTintOpacity), Avalonia.Data.BindingPriority.Style);
        this.Bind(InkLightProperty, this.GetResourceObservable(TeamSwitcherTokenKind.TileInkLight), Avalonia.Data.BindingPriority.Style);
        this.Bind(InkDarkProperty, this.GetResourceObservable(TeamSwitcherTokenKind.TileInkDark), Avalonia.Data.BindingPriority.Style);
        UpdateTint();
    }

    /// <inheritdoc cref="WorkspaceProperty"/>
    public TeamSwitcherWorkspace? Workspace { get => GetValue(WorkspaceProperty); set => SetValue(WorkspaceProperty, value); }

    /// <inheritdoc cref="TileSizeProperty"/>
    public double TileSize { get => GetValue(TileSizeProperty); set => SetValue(TileSizeProperty, value); }

    internal IBrush? Tint => GetValue(TintProperty);

    internal double TintOpacity => GetValue(TintOpacityProperty);

    internal Color InkLight => GetValue(InkLightProperty);

    internal Color InkDark => GetValue(InkDarkProperty);

    internal CornerRadius BaseRadius => GetValue(BaseRadiusProperty);

    /// <summary>The letter on screen, or null when the tile draws a mark.</summary>
    internal string? Letter => Workspace is { Logo: null, LogoImage: null } w ? TeamSwitcherLogic.Monogram(w.Name) : null;

    /// <summary>Which tint the tile uses (0 accent, 1 to 4 the series), or -1 before it has a workspace.</summary>
    internal int TintIndex => _tint;

    /// <summary>
    /// The ink for a letter or mark on <paramref name="brand"/>: the light or the dark ink, whichever contrasts more with a
    /// solid color (WCAG relative luminance); the light ink on gradients and other brushes.
    /// </summary>
    internal IBrush InkOn(IBrush brand)
    {
        _inkLight ??= new ImmutableSolidColorBrush(InkLight);
        _inkDark ??= new ImmutableSolidColorBrush(InkDark);
        if (brand is not ISolidColorBrush { Color: var color })
        {
            return _inkLight;
        }
        var background = Luminance(color);
        var light = Contrast(Luminance(InkLight), background);
        var dark = Contrast(Luminance(InkDark), background);
        return dark > light ? _inkDark : _inkLight;
    }

    private static double Contrast(double a, double b) => (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);

    private static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == InkLightProperty)
        {
            _inkLight = null;
        }
        else if (change.Property == InkDarkProperty)
        {
            _inkDark = null;
        }
        if (change.Property == WorkspaceProperty)
        {
            if (_watched is not null)
            {
                _watched.PropertyChanged -= OnWorkspaceChanged;
            }
            _watched = Workspace;
            if (_watched is not null)
            {
                _watched.PropertyChanged += OnWorkspaceChanged;
            }
            UpdateTint();
            UpdateName();
        }
    }

    private void OnWorkspaceChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        UpdateTint();
        UpdateName();
        InvalidateVisual();
    }

    private void UpdateName() => AutomationProperties.SetName(this, Workspace?.Name ?? string.Empty);

    private void UpdateTint()
    {
        var index = TeamSwitcherLogic.TintIndex(Workspace?.Name);
        if (index == _tint)
        {
            return;
        }
        _tint = index;
        _tintBinding?.Dispose();
        _tintBinding = ArcTokens.Bind(this, TintProperty, s_tints[index], Avalonia.Data.BindingPriority.Style);
    }

    protected override Size MeasureOverride(Size availableSize) => new(TileSize, TileSize);

    protected override AutomationPeer OnCreateAutomationPeer() => new TeamSwitcherTilePeer(this);

    public override void Render(DrawingContext context)
    {
        var size = TileSize;
        var workspace = Workspace;
        if (workspace is null || size <= 0)
        {
            return;
        }
        var bounds = new Rect((Bounds.Width - size) / 2, (Bounds.Height - size) / 2, size, size);
        var scale = size / 28.0;
        var radius = BaseRadius;
        var rounded = new RoundedRect(bounds, radius.TopLeft * scale, radius.TopRight * scale, radius.BottomRight * scale,
            radius.BottomLeft * scale);
        if (workspace.LogoImage is { } image && image.Size is { Width: > 0, Height: > 0 } source)
        {
            // The app's own logo fills the tile: a centered square of the image, clipped to the tile's corners.
            if (workspace.TileBackground is { } behind)
            {
                context.DrawRectangle(behind, null, rounded);
            }
            var side = Math.Min(source.Width, source.Height);
            var crop = new Rect((source.Width - side) / 2, (source.Height - side) / 2, side, side);
            using (context.PushClip(rounded))
            {
                context.DrawImage(image, crop, bounds);
            }
            return;
        }
        IBrush? ink;
        if (workspace.TileBackground is { } brand)
        {
            context.DrawRectangle(brand, null, rounded);
            ink = workspace.TileForeground ?? InkOn(brand);
        }
        else
        {
            if (Tint is { } tint)
            {
                using (context.PushOpacity(TintOpacity))
                {
                    context.DrawRectangle(tint, null, rounded);
                }
            }
            ink = workspace.TileForeground ?? Tint;
        }
        if (workspace.Logo is { } logo)
        {
            // The mark sits in a 24 × 24 box at 60% of the tile.
            var mark = size * 0.6;
            var factor = mark / 24.0;
            var origin = new Point(bounds.X + (size - mark) / 2, bounds.Y + (size - mark) / 2);
            using (context.PushTransform(Matrix.CreateScale(factor, factor) * Matrix.CreateTranslation(origin.X, origin.Y)))
            {
                context.DrawGeometry(ink, null, logo);
            }
            return;
        }
        var letter = Letter ?? "?";
        var key = letter + "|" + size.ToString(CultureInfo.InvariantCulture);
        var font = GetValue(FontFamilyProperty);
        if (_letter is null || _letterKey != key || !ReferenceEquals(_letterFont, font))
        {
            _letterKey = key;
            _letterFont = font;
            _letter = new FormattedText(letter, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(font, FontStyle.Normal, FontWeight.SemiBold), Math.Round(size * 0.46), ink);
        }
        if (ink is not null)
        {
            _letter.SetForegroundBrush(ink);
        }
        context.DrawText(_letter, new Point(bounds.X + (size - _letter.Width) / 2, bounds.Y + (size - _letter.Height) / 2));
    }

    private sealed class TeamSwitcherTilePeer(TeamSwitcherTile owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override bool IsContentElementCore() => false;
    }
}
