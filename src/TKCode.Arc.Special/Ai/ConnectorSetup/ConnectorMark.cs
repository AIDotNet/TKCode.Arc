using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// An app's mark as <see cref="ConnectorSetup"/> draws it: a neutral Lucide glyph for the kind of app (or your own glyph, or
/// the first letter of the name) in a quiet rounded tile. No brand logos are drawn; pass the app's own mark as a 24 × 24
/// stroked <see cref="Glyph"/>. Use it anywhere else in your settings UI to match the card.
/// </summary>
[TemplatePart(GlyphPart, typeof(ArcGlyph))]
[TemplatePart(MonogramPart, typeof(TextBlock))]
public class ConnectorMark : TemplatedControl
{
    internal const string GlyphPart = "PART_Glyph";
    internal const string MonogramPart = "PART_Monogram";

    /// <summary>The built-in glyph for the kind of app.</summary>
    public static readonly StyledProperty<ConnectorMarkKind> KindProperty =
        AvaloniaProperty.Register<ConnectorMark, ConnectorMarkKind>(nameof(Kind));

    /// <summary>Your own 24 × 24 stroked glyph; it takes the place of the built-in one.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<ConnectorMark, Geometry?>(nameof(Glyph));

    /// <summary>The name whose first letter is shown when there is no glyph.</summary>
    public static readonly StyledProperty<string?> MonogramProperty =
        AvaloniaProperty.Register<ConnectorMark, string?>(nameof(Monogram));

    /// <summary>The glyph's size in pixels; the tile adds its padding around it.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<ConnectorMark, double>(nameof(Size), 20);

    /// <summary>The glyph's color; the foreground by default.</summary>
    public static readonly StyledProperty<IBrush?> GlyphBrushProperty =
        AvaloniaProperty.Register<ConnectorMark, IBrush?>(nameof(GlyphBrush));

    private ArcGlyph? _glyph;
    private TextBlock? _monogram;

    /// <inheritdoc cref="KindProperty"/>
    public ConnectorMarkKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <inheritdoc cref="MonogramProperty"/>
    public string? Monogram { get => GetValue(MonogramProperty); set => SetValue(MonogramProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="GlyphBrushProperty"/>
    public IBrush? GlyphBrush { get => GetValue(GlyphBrushProperty); set => SetValue(GlyphBrushProperty, value); }

    /// <summary>The Lucide glyph of a built-in kind, or null for <see cref="ConnectorMarkKind.None"/>.</summary>
    public static Geometry? GlyphOf(ConnectorMarkKind kind) => kind switch
    {
        ConnectorMarkKind.Chat => ArcIcons.MessageSquare,
        ConnectorMarkKind.Code => ArcIcons.GitBranch,
        ConnectorMarkKind.Docs => ArcIcons.FileText,
        ConnectorMarkKind.Drive => ArcIcons.Folder,
        ConnectorMarkKind.Issues => ArcIcons.CircleDot,
        ConnectorMarkKind.Calendar => ArcIcons.Calendar,
        ConnectorMarkKind.Mail => ArcIcons.Mail,
        ConnectorMarkKind.Crm => ArcIcons.Users,
        ConnectorMarkKind.Database => ArcIcons.Database,
        ConnectorMarkKind.Analytics => ArcIcons.ChartBar,
        _ => null,
    };

    /// <summary>The glyph drawn now: your own, the built-in one, or null when the monogram shows.</summary>
    internal Geometry? EffectiveGlyph => Glyph ?? GlyphOf(Kind);

    /// <summary>The letter drawn when there is no glyph.</summary>
    internal string MonogramText =>
        string.IsNullOrWhiteSpace(Monogram) ? "?" : Monogram.Trim()[..1].ToUpperInvariant();

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _glyph = e.NameScope.Find<ArcGlyph>(GlyphPart);
        _monogram = e.NameScope.Find<TextBlock>(MonogramPart);
        Update();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty || change.Property == GlyphProperty || change.Property == MonogramProperty
            || change.Property == SizeProperty || change.Property == GlyphBrushProperty)
        {
            Update();
        }
    }

    private void Update()
    {
        var glyph = EffectiveGlyph;
        var size = Math.Max(1, Size);
        if (_glyph is not null)
        {
            _glyph.Data = glyph;
            _glyph.Size = size;
            _glyph.IsVisible = glyph is not null;
            if (GlyphBrush is { } brush)
            {
                _glyph.Foreground = brush;
            }
            else
            {
                _glyph.ClearValue(ArcGlyph.ForegroundProperty);
            }
        }
        if (_monogram is not null)
        {
            _monogram.Text = MonogramText;
            _monogram.FontSize = Math.Round(size * 0.7);
            _monogram.Width = size;
            _monogram.Height = size;
            _monogram.LineHeight = size;
            _monogram.IsVisible = glyph is null;
        }
    }
}
