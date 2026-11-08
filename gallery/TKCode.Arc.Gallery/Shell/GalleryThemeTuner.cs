using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Localization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The home page's first screen: a sample app built from Arc components beside the theme panel. The panel changes the real
/// theme, so the sample, this page and the rest of the gallery all follow; the code under it does the same in your app.
/// </summary>
internal sealed class GalleryThemeTuner : GalleryTwoPane
{
    private readonly GalleryTheme _theme;
    private readonly TextBlock _code = GalleryUi.Mono(string.Empty, ArcDesignTokenKind.TextSecondary, 11.5);
    private readonly CopyButton _copy = new() { IsIconOnly = true, VerticalAlignment = VerticalAlignment.Top };

    public GalleryThemeTuner(GalleryTheme theme, Control owner)
    {
        _theme = theme;
        EndWidth = 320;
        StackBelow = 880;
        Children.Add(new AppSample());
        Children.Add(BuildPanel(owner));
        _theme.Changed += SyncCode;
        SyncCode();
    }

    /// <summary>The code that gives another app this theme.</summary>
    public string ThemeCode => _code.Text ?? string.Empty;

    private Control BuildPanel(Control owner)
    {
        // The Appearance popover's own panel, stretched to the column: one set of controls, one place that applies them.
        var appearance = new GalleryAppearancePanel(_theme, owner) { Width = double.NaN, BorderThickness = default, Padding = default, Background = null };

        _code.TextWrapping = TextWrapping.Wrap;
        var codeRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6 };
        Grid.SetColumn(_copy, 1);
        codeRow.Children.Add(_code);
        codeRow.Children.Add(_copy);
        var codeBox = new Border { Padding = new Thickness(12, 8, 6, 8), Child = codeRow };
        ArcTokens.Bind(codeBox, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(codeBox, Border.CornerRadiusProperty, ArcDesignTokenKind.RadiusControl);

        var stack = new StackPanel { Spacing = 14 };
        stack.Children.Add(GallerySite.Text(GalleryShellLangResourceKind.HomeTunerTitle, ArcDesignTokenKind.Foreground, 16, FontWeight.Medium));
        stack.Children.Add(appearance);
        stack.Children.Add(GallerySite.Text(GalleryShellLangResourceKind.HomeTunerCode, ArcDesignTokenKind.TextMuted, 12, FontWeight.Medium));
        stack.Children.Add(codeBox);
        return GallerySite.Panel(stack, padding: 18);
    }

    private void SyncCode()
    {
        var calls = new List<string> { $".Appearance(ArcAppearance.{(_theme.IsDark ? "Dark" : "Light")})" };
        if (_theme.IsPixel)
        {
            calls.Add(".Pixel()");
        }
        else
        {
            calls.Add($".Accent(ArcAccentPreset.{_theme.Accent})");
            switch (_theme.Corners)
            {
                case 1:
                    calls.Add(".Corners(ArcCornerStyle.Scale(0.5))");
                    break;
                case 2:
                    calls.Add(".Corners(ArcCornerStyle.Square)");
                    break;
            }
        }
        var code = $"ArcTheme.Apply(t => t\n  {string.Join("\n  ", calls)});";
        _code.Text = code;
        _copy.Value = code;
    }

    /// <summary>The sample app: real Arc controls, built when the first screen is laid out.</summary>
    private sealed class AppSample : Border
    {
        private static readonly double[] s_deploys = [34, 48, 41, 62, 55, 72, 64, 88, 70, 96, 80, 92];

        private bool _built;

        public AppSample()
        {
            BorderThickness = new Thickness(1);
            ClipToBounds = true;
            MinHeight = 420;
            ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Surface);
            ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
            ArcTokens.Bind(this, CornerRadiusProperty, ArcDesignTokenKind.RadiusPanel);
            // The hero's text draws first; the controls follow when this box first reaches the viewport.
            _ = new GalleryFirstSight(this, Build);
        }

        private void Build()
        {
            if (_built)
            {
                return;
            }
            _built = true;

            var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
            var title = GallerySite.Text(GalleryShellLangResourceKind.HomePreviewWeek, ArcDesignTokenKind.Foreground, 18, FontWeight.Medium);
            title.VerticalAlignment = VerticalAlignment.Center;
            var create = new Button { Variant = ButtonVariant.Primary, Size = ControlSize.Small, Content = GallerySite.Labelled(GalleryShellLangResourceKind.HomePreviewNew, null) };
            Grid.SetColumn(create, 1);
            head.Children.Add(title);
            head.Children.Add(create);

            var metrics = new GalleryGrid { MinItemWidth = 130, MaxColumns = 3, Spacing = 10 };
            metrics.Children.Add(Metric(GalleryShellLangResourceKind.HomePreviewProjects, 24, null, "+3"));
            metrics.Children.Add(Metric(GalleryShellLangResourceKind.HomePreviewOnTime, 92, "%", "+4%"));
            // Three in a row or two: a third alone on a phone's second row would only push the chart down.
            var review = Metric(GalleryShellLangResourceKind.HomePreviewReview, 7, null, null);
            metrics.Children.Add(review);
            SizeChanged += (_, e) => review.IsVisible = e.NewSize.Width >= 480;

            var chart = new BarChart { ShowAverage = false, PlotHeight = 150 };
            GalleryText.Bind(chart, ChartBase.LabelProperty, GalleryShellLangResourceKind.HomePreviewChart);
            GalleryText.Bind(chart, BarChart.AverageLabelProperty, GalleryShellLangResourceKind.HomePreviewAverage);
            chart.ItemsSource = s_deploys.Select((deploys, week) => new ChartPoint($"W{week + 1}", deploys)).ToArray();

            var note = GallerySite.Text(GalleryShellLangResourceKind.HomePreviewNote, ArcDesignTokenKind.TextMuted, 12);

            var main = new StackPanel { Spacing = 14, Margin = new Thickness(16) };
            main.Children.Add(head);
            main.Children.Add(metrics);
            main.Children.Add(chart);
            main.Children.Add(note);
            Child = main;
        }

        private static MetricCard Metric(GalleryShellLangResourceKind label, double value, string? suffix, string? change)
        {
            var card = new MetricCard { Value = value, Suffix = suffix, Change = change };
            GalleryText.Bind(card, MetricCard.LabelProperty, label);
            return card;
        }
    }
}
