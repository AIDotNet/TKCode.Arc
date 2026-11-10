// Ported from Arc's free hero-section block (MIT, Copyright (c) 2026 Elia Kuratli): hero-lumen.tsx, hero-lumen-data.ts, hero-cadence.tsx,
// hero-content.tsx and their CSS modules. The products, copy and sample numbers are this library's own; customer marks are generic icons.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The dashboard's sample numbers: a fictional revenue tool.</summary>
internal static class HeroDashboardData
{
    public sealed record Kpi(string Label, ChartTone Tone, (string Value, string Change, double[] Data)[] ByRange);

    public sealed record Point(string Label, double Current, double Previous);

    public static readonly (string Value, string Label)[] Ranges = [("30d", "30D"), ("90d", "90D"), ("12m", "12M")];

    public static readonly (string Lead, string Detail)[] Insights =
    [
        ("Revenue grew $31.2K this month.", "Most of it came from nine upgrades to the annual plan."),
        ("Revenue grew $74.8K this quarter.", "Upgrades passed new business for the first time this year."),
        ("Revenue grew $149K in twelve months.", "Customers who stayed did more than new ones did.")
    ];

    public static readonly (string Name, string Change, int Amount)[] Movers =
    [
        ("Harbor", "Moved to annual", 3_900),
        ("Quill", "Added 80 seats", 2_640),
        ("Beacon", "Prepaid a year", 1_720),
        ("Fennel", "Removed 25 seats", -1_180),
        ("Orbit", "Added reporting", 910),
        ("Juniper", "Upgraded plan", 680)
    ];

    /// <summary>A smooth, repeatable walk: a trend, one slow wave and a little noise from the seed.</summary>
    private static double[] Walk(int count, double start, double end, double wave, int seed)
    {
        long s = seed;
        double Next()
        {
            s = s * 16807 % 2147483647;
            return (double)s / 2147483647 - 0.5;
        }
        var values = new double[count];
        for (var i = 0; i < count; i++)
        {
            var t = (double)i / (count - 1);
            values[i] = Math.Round(start + (end - start) * (t * t * 0.35 + t * 0.65) + Math.Sin(t * Math.PI * 2.3) * wave + Next() * wave * 0.6);
        }
        return values;
    }

    public static readonly Kpi[] Kpis =
    [
        new("Recurring revenue", ChartTone.Accent, [("$418.6K", "+8.1%", Walk(12, 387, 419, 4, 5)), ("$418.6K", "+21.7%", Walk(12, 344, 419, 6, 9)), ("$418.6K", "+55.2%", Walk(12, 270, 419, 8, 13))]),
        new("Net retention", ChartTone.Success, [("116%", "+2 pts", Walk(12, 111, 116, 1.5, 17)), ("115%", "+4 pts", Walk(12, 108, 115, 1.8, 21)), ("112%", "+8 pts", Walk(12, 102, 112, 2.4, 23))]),
        new("New customers", ChartTone.Success, [("186", "+11%", Walk(12, 4, 8, 2, 31)), ("541", "+16%", Walk(12, 34, 51, 6, 37)), ("1,906", "+31%", Walk(12, 110, 196, 16, 41))]),
        new("Lost revenue", ChartTone.Success, [("$5.4K", "−12%", Walk(12, 8, 5, 1.2, 43)), ("$18.9K", "−8%", Walk(12, 23, 19, 2, 47)), ("$81.3K", "−19%", Walk(12, 11, 6, 1.4, 53))])
    ];

    public static Point[] Series(int range)
    {
        string[] months = ["Dec", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov"];
        switch (range)
        {
            case 2:
            {
                var now = Walk(12, 8_400, 31_200, 2_400, 7);
                var before = Walk(12, 5_600, 19_400, 2_000, 19);
                return months.Select((m, i) => new Point(m, now[i], before[i])).ToArray();
            }
            case 1:
            {
                var now = Walk(13, 3_900, 8_600, 800, 11);
                var before = Walk(13, 3_200, 5_400, 620, 29);
                return now.Select((v, i) => new Point($"W{i + 1}", v, before[i])).ToArray();
            }
            default:
            {
                var now = Walk(30, 560, 1_740, 240, 3);
                var before = Walk(30, 470, 1_060, 200, 29);
                return now.Select((v, i) => new Point($"Day {i + 1}", v, before[i])).ToArray();
            }
        }
    }
}

/// <summary>The dashboard window. Everything is content except the range control, which changes every figure on screen.</summary>
internal sealed class HeroDashboard : Border
{
    private static readonly (string Label, Geometry Icon, bool Active)[] s_nav =
    [
        ("Overview", ArcIcons.Gauge, true),
        ("Revenue", ArcIcons.ChartLine, false),
        ("Customers", ArcIcons.Users, false),
        ("Forecasts", ArcIcons.Sparkles, false),
        ("Reports", ArcIcons.BookOpen, false)
    ];

    private readonly Sparkline[] _sparklines;
    private readonly LineChart _chart;
    private readonly TextBlock _insightLead;
    private readonly TextBlock _insightRest;
    private readonly bool _isNarrow;
    private int _range;

    public HeroDashboard(bool isNarrow)
    {
        _isNarrow = isNarrow;
        AutomationProperties.SetName(this, "Revenue dashboard, sample data");
        BorderThickness = new Thickness(1);
        ClipToBounds = true;
        this.BindCornerRadius(ArcDesignTokenKind.RadiusStep16);
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(this, BoxShadowProperty, ArcDesignTokenKind.ShadowFloating);

        Range = new SegmentedControl { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(Range, "Date range");
        foreach (var (value, label) in HeroDashboardData.Ranges)
        {
            Range.Items.Add(new SegmentedOption(value, label));
        }
        Range.SelectedIndex = 0;
        Range.SelectionChanged += (_, _) => SetRange(Math.Max(0, Range.SelectedIndex));

        var title = HeroText.Line("Revenue overview", ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeLG, true);
        var synced = HeroText.Line("Synced 2 minutes ago", ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS);
        DockPanel.SetDock(Range, Dock.Right);
        var head = new DockPanel { Children = { Range, new StackPanel { Spacing = 2, Children = { title, synced } } } };

        _sparklines = new Sparkline[HeroDashboardData.Kpis.Length];
        var kpis = new UniformGrid { Columns = isNarrow ? 2 : 4, ColumnSpacing = 12, RowSpacing = 12 };
        for (var i = 0; i < _sparklines.Length; i++)
        {
            var kpi = HeroDashboardData.Kpis[i];
            _sparklines[i] = new Sparkline
            {
                Label = kpi.Label,
                Tone = kpi.Tone,
                DrawingHeight = 34,
                IsInteractive = false,
                YSelector = static o => (double)o
            };
            kpis.Children.Add(Tile(_sparklines[i], new Thickness(16, 14, 16, 12)));
        }

        _insightLead = HeroText.Line(null, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, true);
        _insightLead.TextWrapping = TextWrapping.Wrap;
        _insightRest = HeroText.Line(null, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeSM);
        _insightRest.TextWrapping = TextWrapping.Wrap;
        var insightIcon = HeroText.Glyph(ArcIcons.Sparkles, 15, ArcDesignTokenKind.Accent);
        insightIcon.VerticalAlignment = VerticalAlignment.Top;
        insightIcon.Margin = new Thickness(0, 2, 8, 0);
        DockPanel.SetDock(insightIcon, Dock.Left);
        var insight = new DockPanel { Children = { insightIcon, new StackPanel { Children = { _insightLead, _insightRest } } } };

        _chart = new LineChart
        {
            Label = "Net new revenue",
            PlotHeight = isNarrow ? 210 : 232,
            ShowLegend = false,
            XSelector = static o => ((HeroDashboardData.Point)o).Label,
            TickFormatter = static v => v >= 1000 ? $"${Math.Round(v / 1000, 1)}K" : $"${v}"
        };
        _chart.Series.Add(new ChartSeries { Name = "This period", HasArea = true, ValueSelector = static o => ((HeroDashboardData.Point)o).Current });
        _chart.Series.Add(new ChartSeries { Name = "Last period", IsDashed = true, ValueSelector = static o => ((HeroDashboardData.Point)o).Previous });

        var chartCard = Tile(new StackPanel { Spacing = 14, Children = { insight, _chart } }, new Thickness(18, 16));
        var cards = new Grid { ColumnSpacing = 12 };
        if (isNarrow)
        {
            cards.Children.Add(chartCard);
        }
        else
        {
            cards.ColumnDefinitions = new ColumnDefinitions("1.7*,*");
            var movers = Tile(BuildMovers(), new Thickness(18, 16));
            Grid.SetColumn(movers, 1);
            cards.Children.Add(chartCard);
            cards.Children.Add(movers);
        }

        var main = new StackPanel { Spacing = 16, Margin = new Thickness(24, 22), Children = { head, kpis, cards } };
        var app = new Grid();
        if (isNarrow)
        {
            app.Children.Add(main);
        }
        else
        {
            app.ColumnDefinitions = new ColumnDefinitions("224,*");
            Grid.SetColumn(main, 1);
            app.Children.Add(BuildSidebar());
            app.Children.Add(main);
        }
        var chrome = BuildChrome();
        DockPanel.SetDock(chrome, Dock.Top);
        Child = new DockPanel { Children = { chrome, app } };
        SetRange(0);
    }

    internal SegmentedControl Range { get; }

    internal string? FirstKpiValue => _sparklines[0].Value;

    internal string? SecondKpiValue => _sparklines[1].Value;

    internal string? InsightText => _insightLead.Text;

    internal int ChartPointCount => (_chart.ItemsSource as System.Collections.ICollection)?.Count ?? 0;

    private void SetRange(int range)
    {
        _range = range;
        for (var i = 0; i < _sparklines.Length; i++)
        {
            var (value, change, data) = HeroDashboardData.Kpis[i].ByRange[range];
            _sparklines[i].Value = value;
            _sparklines[i].Change = change;
            _sparklines[i].ItemsSource = data.Cast<object>().ToArray();
        }
        _chart.ItemsSource = HeroDashboardData.Series(range);
        var (lead, detail) = HeroDashboardData.Insights[range];
        _insightLead.Text = lead;
        _insightRest.Text = detail;
    }

    private static Border Tile(Control child, Thickness padding)
    {
        var tile = new Border { BorderThickness = new Thickness(1), Padding = padding, Child = child }.BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        ArcTokens.Bind(tile, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        ArcTokens.Bind(tile, BackgroundProperty, ArcDesignTokenKind.Surface);
        return tile;
    }

    private Control BuildChrome()
    {
        var lights = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, Margin = new Thickness(16, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        for (var i = 0; i < 3; i++)
        {
            var light = new Border { Width = 11, Height = 11 }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
            light.Bind(BackgroundProperty, light.GetResourceObservable(HeroSectionTokens.Key(HeroSectionTokenKind.WindowLight)));
            lights.Children.Add(light);
        }
        var url = HeroText.Line("app.tally.example/overview", ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS);
        var urlBox = new Border { Padding = new Thickness(14, 3), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = url }.BindCornerRadius(ArcDesignTokenKind.RadiusStep8);
        ArcTokens.Bind(urlBox, BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(urlBox, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        var chrome = new Border { Height = 40, BorderThickness = new Thickness(0, 0, 0, 1), Child = new Panel { Children = { lights, urlBox } } };
        ArcTokens.Bind(chrome, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        chrome.Bind(BackgroundProperty, chrome.GetResourceObservable(HeroSectionTokens.Key(HeroSectionTokenKind.ChromeFill)));
        return chrome;
    }

    private static Control NavRow(string label, Control lead, bool active)
    {
        var text = HeroText.Line(label, active ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeSM, active);
        text.VerticalAlignment = VerticalAlignment.Center;
        var row = new Border { Padding = new Thickness(8, 6), Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { lead, text } } }.BindCornerRadius(ArcDesignTokenKind.RadiusStep8);
        if (active)
        {
            ArcTokens.Bind(row, BackgroundProperty, ArcDesignTokenKind.AccentSubtle);
        }
        return row;
    }

    private Control BuildSidebar()
    {
        var mark = HeroText.Line("T", ArcDesignTokenKind.Background, ArcDesignTokenKind.TypeXS, true);
        mark.HorizontalAlignment = HorizontalAlignment.Center;
        mark.VerticalAlignment = VerticalAlignment.Center;
        var markBox = new Border { Width = 22, Height = 22, Child = mark }.BindCornerRadius(ArcDesignTokenKind.RadiusStep6);
        ArcTokens.Bind(markBox, BackgroundProperty, ArcDesignTokenKind.Foreground);
        var workspaceName = HeroText.Line("Tally Cloud", ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, true);
        workspaceName.VerticalAlignment = VerticalAlignment.Center;
        var chevron = HeroText.Glyph(ArcIcons.ChevronsUpDown, 14, ArcDesignTokenKind.TextMuted);
        DockPanel.SetDock(markBox, Dock.Left);
        DockPanel.SetDock(chevron, Dock.Right);
        workspaceName.Margin = new Thickness(10, 0);
        var workspace = new DockPanel { Margin = new Thickness(8, 6, 8, 10), Children = { markBox, chevron, workspaceName } };

        var search = new Border
        {
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 6),
            Margin = new Thickness(0, 0, 0, 10),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { HeroText.Glyph(ArcIcons.Search, 14, ArcDesignTokenKind.TextMuted), HeroText.Line("Search", ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeSM) }
            }
        }.BindCornerRadius(ArcDesignTokenKind.RadiusStep8);
        ArcTokens.Bind(search, BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(search, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);

        var nav = new StackPanel { Spacing = 1 };
        foreach (var (label, icon, active) in s_nav)
        {
            nav.Children.Add(NavRow(label, HeroText.Glyph(icon, 15, active ? ArcDesignTokenKind.Accent : ArcDesignTokenKind.TextMuted), active));
        }
        var viewsLabel = HeroText.Line("Saved views", ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS);
        viewsLabel.Margin = new Thickness(8, 18, 8, 6);
        var views = new StackPanel { Spacing = 1 };
        foreach (var (label, tone) in new[] { ("Annual upgrades", ArcDesignTokenKind.Accent), ("At risk", ArcDesignTokenKind.Danger), ("Europe", ArcDesignTokenKind.TextMuted) })
        {
            var dot = new Border { Width = 8, Height = 8, Margin = new Thickness(3, 0), VerticalAlignment = VerticalAlignment.Center }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
            ArcTokens.Bind(dot, BackgroundProperty, tone);
            views.Children.Add(NavRow(label, dot, false));
        }

        var name = HeroText.Line("Ada Moreno", ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, true);
        var job = HeroText.Line("Finance lead", ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS);
        var avatar = new Avatar { DisplayName = "Ada Moreno", Size = AvatarSize.Small };
        var settings = HeroText.Glyph(ArcIcons.Settings, 15, ArcDesignTokenKind.TextMuted);
        DockPanel.SetDock(avatar, Dock.Left);
        DockPanel.SetDock(settings, Dock.Right);
        var who = new StackPanel { Margin = new Thickness(10, 0), Children = { name, job } };
        var me = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(6, 12, 6, 2), Child = new DockPanel { Children = { avatar, settings, who } } };
        ArcTokens.Bind(me, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        DockPanel.SetDock(me, Dock.Bottom);

        var sidebar = new Border
        {
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(12, 14),
            Child = new DockPanel { Children = { me, new StackPanel { Spacing = 2, Children = { workspace, search, nav, viewsLabel, views } } } }
        };
        ArcTokens.Bind(sidebar, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        sidebar.Bind(BackgroundProperty, sidebar.GetResourceObservable(HeroSectionTokens.Key(HeroSectionTokenKind.SidebarFill)));
        return sidebar;
    }

    private static Control BuildMovers()
    {
        var note = HeroText.Line("Revenue change", ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS);
        note.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(note, Dock.Right);
        var head = new DockPanel { Children = { note, HeroText.Line("Biggest movers", ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, true) } };
        var list = new StackPanel { Spacing = 14, Children = { head } };
        ArcDesignTokenKind[] series = [ArcDesignTokenKind.Series1, ArcDesignTokenKind.Series2, ArcDesignTokenKind.Series3, ArcDesignTokenKind.Series4];
        for (var i = 0; i < HeroDashboardData.Movers.Length; i++)
        {
            var (name, change, amount) = HeroDashboardData.Movers[i];
            var letter = HeroText.Line(name[..1], ArcDesignTokenKind.Background, ArcDesignTokenKind.TypeXS, true);
            letter.HorizontalAlignment = HorizontalAlignment.Center;
            letter.VerticalAlignment = VerticalAlignment.Center;
            var logo = new Border { Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Center, Child = letter }.BindCornerRadius(ArcDesignTokenKind.RadiusStep6);
            ArcTokens.Bind(logo, BackgroundProperty, series[i % series.Length]);
            var value = HeroText.Line($"{(amount < 0 ? "−" : "+")}${Math.Abs(amount):N0}", amount < 0 ? ArcDesignTokenKind.Danger : ArcDesignTokenKind.Success, ArcDesignTokenKind.TypeSM, true);
            value.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(logo, Dock.Left);
            DockPanel.SetDock(value, Dock.Right);
            var who = new StackPanel
            {
                Margin = new Thickness(12, 0),
                Children =
                {
                    HeroText.Line(name, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, true),
                    HeroText.Line(change, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS)
                }
            };
            list.Children.Add(new DockPanel { Children = { logo, value, who } });
        }
        return list;
    }
}

/// <summary>
/// The crop the dashboard runs into: drawn at its design size and scaled to fit, like a screenshot, it settles from a tilt as it
/// enters and fades out at the bottom edge. Two designs: 1200 wide, or 600 on a phone, where it keeps a legible size and crops evenly.
/// </summary>
internal sealed class HeroDashboardStage : Control
{
    private const double Wide = 1200;
    private const double Narrow = 600;
    private const double WideHeight = 760;
    private const double NarrowHeight = 700;
    private const double Perspective = 2200;

    private static readonly IImmutableBrush s_mask = new ImmutableLinearGradientBrush(
        new[] { new ImmutableGradientStop(0.62, Colors.Black), new ImmutableGradientStop(1, Colors.Transparent) },
        1, null, null, GradientSpreadMethod.Pad, new RelativePoint(0, 0, RelativeUnit.Relative), new RelativePoint(0, 1, RelativeUnit.Relative));

    private readonly ScaleTransform _fit = new();
    private readonly Rotate3DTransform _tilt = new() { Depth = Perspective };
    private readonly MatrixTransform _settle = new();
    private double _centerX;
    private readonly TranslateTransform _rise = new();
    private readonly TransformGroup _transform = new();
    private HeroDashboard? _dashboard;
    private bool? _isNarrow;
    private double _halo = 96;
    private double _gutter = 40;
    private double _scale = 1;
    private double _tiltAmount;

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<HeroDashboardStage, IBrush?>(nameof(AccentBrush));

    static HeroDashboardStage()
    {
        AffectsRender<HeroDashboardStage>(AccentBrushProperty);
    }

    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    public HeroDashboardStage()
    {
        ArcTokens.Bind(this, AccentBrushProperty, ArcDesignTokenKind.Accent);
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
        ClipToBounds = true;
        OpacityMask = s_mask;
        RenderTransform = _rise;
        _transform.Children.Add(_fit);
        _transform.Children.Add(_tilt);
        _transform.Children.Add(_settle);
    }

    internal HeroDashboard? Dashboard => _dashboard;

    internal double TiltAngle => _tilt.AngleX;

    internal bool IsNarrowDesign => _isNarrow == true;

    /// <summary>Sets the room above the window and at its sides.</summary>
    public void SetMargins(double halo, double gutter)
    {
        _halo = halo;
        _gutter = gutter;
        InvalidateMeasure();
    }

    /// <summary>Sets the entrance: <paramref name="amount"/> is 1 at the full tilt and 0 in place.</summary>
    public void SetTilt(double amount)
    {
        _tiltAmount = amount;
        ApplyTilt();
    }

    /// <summary>Sets the rise of the whole stage (px).</summary>
    public void SetRise(double y) => _rise.Y = y;

    private void ApplyTilt()
    {
        _tilt.AngleX = 22 * _tiltAmount;
        var scale = 1 - 0.06 * _tiltAmount;
        _settle.Matrix = Matrix.CreateTranslation(-_centerX, 0) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(_centerX, 0);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? Wide + 2 * _gutter : availableSize.Width;
        var inner = Math.Max(1, width - 2 * _gutter);
        var narrow = inner < 720;
        if (_isNarrow != narrow)
        {
            _isNarrow = narrow;
            if (_dashboard is not null)
            {
                VisualChildren.Remove(_dashboard);
                LogicalChildren.Remove(_dashboard);
            }
            _dashboard = new HeroDashboard(narrow) { RenderTransform = _transform, RenderTransformOrigin = RelativePoint.TopLeft };
            VisualChildren.Add(_dashboard);
            LogicalChildren.Add(_dashboard);
        }
        var design = narrow ? Narrow : Wide;
        _scale = narrow ? Math.Max(0.6, Math.Min(1, inner / design)) : Math.Min(1, inner / design);
        _dashboard!.Measure(new Size(design, narrow ? NarrowHeight : WideHeight));
        var height = double.IsInfinity(availableSize.Height) ? _halo + (narrow ? NarrowHeight : WideHeight) * _scale : availableSize.Height;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var narrow = _isNarrow == true;
        var design = narrow ? Narrow : Wide;
        var height = narrow ? NarrowHeight : WideHeight;
        var shown = design * _scale;
        _fit.ScaleX = _scale;
        _fit.ScaleY = _scale;
        // perspective-origin 50% 0%, transform-origin 50% 0%.
        _tilt.CenterX = shown / 2;
        _tilt.CenterY = 0;
        _centerX = shown / 2;
        ApplyTilt();
        _dashboard?.Arrange(new Rect((finalSize.Width - shown) / 2, _halo, design, height));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        // The light behind the window: a soft ellipse above its top edge.
        var accent = AccentBrush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
        var isDark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        var tint = Oklch.MixOklab(Color.Parse(isDark ? "#6e7cff" : "#7d8cff"), accent, isDark ? 0.30 : 0.35);
        var glow = Oklch.WithAlpha(tint, isDark ? 0.58 : 0.36);
        var rect = new Rect(Bounds.Width * 0.1, _halo - 110, Bounds.Width * 0.8, 240);
        var brush = new ImmutableRadialGradientBrush(
            new[] { new ImmutableGradientStop(0, glow), new ImmutableGradientStop(0.72, Color.FromArgb(0, glow.R, glow.G, glow.B)) },
            1, null, null, GradientSpreadMethod.Pad, RelativePoint.Center, RelativePoint.Center,
            new RelativeScalar(0.5, RelativeUnit.Relative), new RelativeScalar(0.5, RelativeUnit.Relative));
        context.FillRectangle(brush, rect);
    }
}

/// <summary>The screenshot design: a headline over a drifting mesh and a live dashboard rising from the bottom edge.</summary>
internal sealed class HeroDashboardScene : HeroScene
{
    private readonly HeroEntrance _entrance;
    private readonly Grid _grid = new() { RowDefinitions = new RowDefinitions("Auto,*") };
    private readonly StackPanel _copy = new() { Spacing = 20, MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly CtaBalancedText _title;
    private readonly TextBlock _description;
    private readonly CtaFlowPanel _actions;

    public HeroDashboardScene(HeroSection section, HeroAction primary, HeroAction secondary) : base(section)
    {
        _entrance = new HeroEntrance(this);
        _title = HeroText.Title("Know why your revenue moved", 14);
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        _title.TextAlignment = TextAlignment.Center;
        _description = HeroText.Paragraph("Tally connects billing, your CRM and your warehouse, then explains every change in revenue as it happens. No SQL, no stale spreadsheets.", 590);
        _description.HorizontalAlignment = HorizontalAlignment.Center;
        _description.TextAlignment = TextAlignment.Center;
        _actions = CreateActions(primary, secondary, HorizontalAlignment.Center);
        var fine = HeroText.Line("Free for 14 days. Connects in about four minutes.", ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeSM);
        fine.HorizontalAlignment = HorizontalAlignment.Center;
        fine.TextAlignment = TextAlignment.Center;
        fine.TextWrapping = TextWrapping.Wrap;
        foreach (var item in new Control[] { _title, _description, _actions, fine })
        {
            _copy.Children.Add(item);
            _entrance.AddRise(item);
        }
        Stage = new HeroDashboardStage { MaxWidth = 1280 };
        Grid.SetRow(Stage, 1);
        _grid.Children.Add(_copy);
        _grid.Children.Add(Stage);
        Children.Add(_grid);

        _entrance.Add(1860, (time, full) =>
        {
            if (full)
            {
                var stage = ArcEasings.Enter.Ease(StatsBandTiming.Progress(time, 320, 1100));
                Stage.Opacity = stage;
                Stage.SetRise(56 * (1 - stage));
                Stage.SetTilt(1 - ArcEasings.Enter.Ease(StatsBandTiming.Progress(time, 360, 1500)));
            }
            else
            {
                // Reduced motion: the window is already in place and only fades briefly.
                Stage.Opacity = StatsBandTiming.Progress(time, 0, ArcMotionPresets.Standard.TotalMilliseconds);
                Stage.SetRise(0);
                Stage.SetTilt(0);
            }
        });
    }

    internal HeroDashboardStage Stage { get; }

    public override void PlayEntrance(bool animate) => _entrance.Play(animate);

    public override void UpdateSize(double width, double viewportHeight)
    {
        var phone = width <= 560;
        var top = phone ? 84 : Math.Clamp(viewportHeight * 0.14, 88, 144);
        _grid.Margin = new Thickness(0, top, 0, 0);
        _copy.Margin = new Thickness(phone ? 16 : 40, 0);
        _copy.Spacing = phone ? 16 : 20;
        _actions.Margin = new Thickness(0, phone ? 8 : 12, 0, 0);
        HeroText.SetTitleSize(_title, Math.Clamp(19.2 + 0.05 * width, 40, ArcTokens.Get<double>(this, ArcDesignTokenKind.Type5XL)));
        _description.FontSize = phone ? ArcTokens.Get<double>(this, ArcDesignTokenKind.TypeBase) : ArcTokens.Get<double>(this, ArcDesignTokenKind.TypeLG);
        // margin-top: space-12 (space-10 on a phone) less the halo above the window.
        var halo = phone ? 56 : 96;
        Stage.Margin = new Thickness(0, (phone ? 40 : 48) - halo, 0, 0);
        Stage.SetMargins(halo, phone ? 0 : 40);
    }
}

/// <summary>The mesh design: large type over a mesh, with customers along the bottom edge.</summary>
internal sealed class HeroEditorialScene : HeroScene
{
    private static readonly (string Name, Geometry Mark)[] s_brands =
    [
        ("Harbor", ArcIcons.Send), ("Quill", ArcIcons.Star), ("Beacon", ArcIcons.Zap), ("Fennel", ArcIcons.Globe),
        ("Orbit", ArcIcons.Circle), ("Juniper", ArcIcons.Layers), ("Tamarind", ArcIcons.Box)
    ];

    private readonly HeroEntrance _entrance;
    private readonly Grid _grid = new() { RowDefinitions = new RowDefinitions("*,Auto,Auto,*,Auto") };
    private readonly CtaBalancedText _title;
    private readonly TextBlock _description;
    private readonly Grid _row = new();
    private readonly StackPanel _cta = new() { Spacing = 12 };
    private readonly CtaFlowPanel _actions;
    private readonly TextBlock _fine;
    private readonly UniformGrid _brands = new() { RowSpacing = 16, ColumnSpacing = 16 };
    private readonly List<Control> _brandItems = new();
    private readonly List<TextBlock> _brandNames = new();
    private readonly StackPanel _brandBlock = new() { Spacing = 20 };

    public HeroEditorialScene(HeroSection section, HeroAction primary, HeroAction secondary) : base(section)
    {
        _entrance = new HeroEntrance(this);
        _title = HeroText.Title("A calm week, planned for you", 14);
        Grid.SetRow(_title, 1);
        _description = HeroText.Paragraph("Weekly reads your tasks, meetings and goals, then holds time for the work that counts. When plans change, it moves everything for you.", 520);
        _description.Opacity = 1;
        _description.VerticalAlignment = VerticalAlignment.Bottom;
        _actions = CreateActions(primary, secondary, HorizontalAlignment.Right);
        _fine = HeroText.Line("Free for one person. Teams from $7 a seat.", ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM);
        _fine.TextWrapping = TextWrapping.Wrap;
        _cta.Children.Add(_actions);
        _cta.Children.Add(_fine);
        _row.Children.Add(_description);
        _row.Children.Add(_cta);
        Grid.SetRow(_row, 2);

        var lead = HeroText.Line("Teams at these companies plan with Weekly", ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM);
        lead.TextWrapping = TextWrapping.Wrap;
        lead.Opacity = 0.62;
        foreach (var (name, mark) in s_brands)
        {
            var glyph = HeroText.Glyph(mark, 22, ArcDesignTokenKind.Foreground);
            var text = HeroText.Line(name, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeLG, true);
            text.VerticalAlignment = VerticalAlignment.Center;
            _brandNames.Add(text);
            var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9, Opacity = 0.78, Children = { glyph, text } };
            _brandItems.Add(item);
            _brands.Children.Add(item);
        }
        AutomationProperties.SetName(_brands, "Sample customers");
        _brandBlock.Children.Add(lead);
        _brandBlock.Children.Add(_brands);
        var brandLine = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 24, 0, 0), Child = _brandBlock };
        brandLine.Bind(Border.BorderBrushProperty, brandLine.GetResourceObservable(HeroSectionTokens.Key(HeroSectionTokenKind.WindowLight)));
        Grid.SetRow(brandLine, 4);

        _grid.Children.Add(_title);
        _grid.Children.Add(_row);
        _grid.Children.Add(brandLine);
        _grid.MaxWidth = 1320;
        Children.Add(_grid);

        _entrance.AddRise(_title);
        _entrance.AddRise(_description);
        _entrance.AddRise(_cta);
        _entrance.AddRise(brandLine);
    }

    public override void PlayEntrance(bool animate) => _entrance.Play(animate);

    public override void UpdateSize(double width, double viewportHeight)
    {
        var stacked = width <= 800;
        var phone = width <= 560;
        var top = Math.Clamp(viewportHeight * 0.12, 88, 128);
        _grid.Margin = phone ? new Thickness(16, 80, 16, 24) : new Thickness(40, top, 40, 40);
        _grid.RowSpacing = phone ? 24 : 40;
        HeroText.SetTitleSize(_title, Math.Clamp(16 + 0.07 * width, 44, 112));
        _description.FontSize = phone ? ArcTokens.Get<double>(this, ArcDesignTokenKind.TypeBase) : ArcTokens.Get<double>(this, ArcDesignTokenKind.TypeLG);
        _row.ColumnDefinitions = stacked ? new ColumnDefinitions("*") : new ColumnDefinitions("*,Auto");
        _row.RowDefinitions = stacked ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
        _row.ColumnSpacing = stacked ? 0 : 64;
        _row.RowSpacing = stacked ? 32 : 0;
        Grid.SetColumn(_cta, stacked ? 0 : 1);
        Grid.SetRow(_cta, stacked ? 1 : 0);
        _cta.HorizontalAlignment = stacked ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        _fine.HorizontalAlignment = stacked ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        _fine.Opacity = 0.62;
        _actions.ContentAlignment = stacked ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        _brands.Columns = phone ? 3 : stacked ? 4 : _brandItems.Count;
        for (var i = 0; i < _brandItems.Count; i++)
        {
            _brandItems[i].IsVisible = !phone || i < 6;
        }
        foreach (var name in _brandNames)
        {
            name.FontSize = phone ? ArcTokens.Get<double>(this, ArcDesignTokenKind.TypeBase) : ArcTokens.Get<double>(this, ArcDesignTokenKind.TypeLG);
        }
    }
}

/// <summary>The hero with your own content: announcement, headline, description, actions, an install line, facts and a visual beside the copy.</summary>
internal sealed class HeroContentScene : HeroScene
{
    private readonly HeroEntrance _entrance;
    private readonly HeroSectionVariant _layout;
    private readonly Grid _grid = new();
    private readonly StackPanel _copy = new() { Spacing = 20 };
    private readonly CtaBalancedText _title;
    private readonly TextBlock? _description;
    private readonly ContentControl? _media;

    public HeroContentScene(HeroSection section, HeroSectionVariant layout) : base(section)
    {
        _layout = layout;
        _entrance = new HeroEntrance(this);
        var centered = layout == HeroSectionVariant.Centered;
        var alignment = centered ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _copy.HorizontalAlignment = alignment;
        _copy.MaxWidth = layout == HeroSectionVariant.Minimal ? 860 : 760;

        void Add(Control item)
        {
            item.HorizontalAlignment = alignment;
            _copy.Children.Add(item);
            _entrance.AddRise(item);
        }

        if (section.Announcement is { } announcement)
        {
            var pill = new HeroAnnouncementButton { Content = new HeroActionContent { Text = announcement.Label, TrailingGlyph = ArcIcons.ArrowRight } };
            AutomationProperties.SetName(pill, announcement.Label ?? string.Empty);
            pill.Click += (_, _) =>
            {
                var args = new CtaActionEventArgs(HeroSection.ActionInvokedEvent, section, announcement, false, false);
                section.RaiseEvent(args);
                if (announcement.Command?.CanExecute(announcement.CommandParameter) == true)
                {
                    announcement.Command.Execute(announcement.CommandParameter);
                }
                if (!args.Handled)
                {
                    SiteLinks.TryLaunch(section, announcement.Href);
                }
            };
            Add(pill);
        }

        _title = HeroText.Title(section.Title ?? string.Empty, 16);
        _title.TextAlignment = centered ? TextAlignment.Center : TextAlignment.Left;
        Add(_title);
        if (!string.IsNullOrEmpty(section.Description))
        {
            _description = HeroText.Paragraph(section.Description, 560);
            _description.TextAlignment = centered ? TextAlignment.Center : TextAlignment.Left;
            Add(_description);
        }
        if (section.PrimaryAction is not null || section.SecondaryAction is not null)
        {
            var actions = CreateActions(section.PrimaryAction, section.SecondaryAction, alignment);
            actions.Margin = new Thickness(0, 12, 0, 0);
            Add(actions);
        }
        if (!string.IsNullOrEmpty(section.InstallCommand))
        {
            Add(BuildInstall(section.InstallCommand));
        }
        if (section.Meta is { Count: > 0 } meta)
        {
            var facts = new CtaFlowPanel { Spacing = 20, ContentAlignment = alignment };
            foreach (var entry in meta)
            {
                facts.Children.Add(HeroText.Line(entry, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeSM));
            }
            var line = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 20, 0, 0), Margin = new Thickness(0, 16, 0, 0), Child = facts };
            ArcTokens.Bind(line, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
            Add(line);
        }
        _grid.Children.Add(_copy);
        if (section.Media is not null && layout == HeroSectionVariant.Split)
        {
            _media = new ContentControl { Content = section.Media, VerticalAlignment = VerticalAlignment.Center };
            _grid.Children.Add(_media);
            _entrance.AddRise(_media);
        }
        _grid.MaxWidth = 1200;
        Children.Add(_grid);
    }

    private static Control BuildInstall(string command)
    {
        var prompt = HeroText.Line("$", ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS);
        var code = HeroText.Line(command, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeXS);
        foreach (var text in new[] { prompt, code })
        {
            ArcTokens.Bind(text, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
            text.FontSize = 13;
            text.VerticalAlignment = VerticalAlignment.Center;
        }
        var copy = new CopyButton { Value = command, Label = "Copy install command", IsIconOnly = true, Variant = CopyButtonVariant.Plain };
        DockPanel.SetDock(prompt, Dock.Left);
        DockPanel.SetDock(copy, Dock.Right);
        code.Margin = new Thickness(8, 0);
        var box = new Border
        {
            MaxWidth = 440,
            MinWidth = 280,
            Height = 44,
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(16, 0, 4, 0),
            BorderThickness = new Thickness(1),
            Child = new DockPanel { Children = { prompt, copy, code } }
        }.BindCornerRadius(ArcDesignTokenKind.RadiusControl);
        ArcTokens.Bind(box, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(box, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        return box;
    }

    public override void PlayEntrance(bool animate) => _entrance.Play(animate);

    public override void UpdateSize(double width, double viewportHeight)
    {
        var phone = width <= 560;
        var stacked = width <= 800;
        _grid.Margin = phone ? new Thickness(16, 64) : new Thickness(40, 96);
        HeroText.SetTitleSize(_title, Math.Clamp(20.8 + 0.044 * width, 36, 72));
        if (_description is not null)
        {
            _description.FontSize = phone ? ArcTokens.Get<double>(this, ArcDesignTokenKind.TypeBase) : ArcTokens.Get<double>(this, ArcDesignTokenKind.TypeLG);
        }
        if (_layout == HeroSectionVariant.Split && _media is not null)
        {
            _grid.ColumnDefinitions = stacked ? new ColumnDefinitions("*") : new ColumnDefinitions("*,1.05*");
            _grid.RowDefinitions = stacked ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
            _grid.ColumnSpacing = stacked ? 0 : 48;
            _grid.RowSpacing = stacked ? 40 : 0;
            Grid.SetColumn(_media, stacked ? 0 : 1);
            Grid.SetRow(_media, stacked ? 1 : 0);
            _copy.VerticalAlignment = VerticalAlignment.Center;
        }
    }
}
