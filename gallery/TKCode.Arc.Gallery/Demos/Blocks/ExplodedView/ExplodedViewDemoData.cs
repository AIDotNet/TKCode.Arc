using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>One generated week of the sample running app: daily bars, the total, the change and three sessions.</summary>
public sealed record ExplodedViewDemoWeek(int[] Bars, double TotalKm, int ChangePercent, double[] SessionKm);

/// <summary>
/// The sample for the Exploded view demos: the week page of an invented running app, "Stride Ledger", drawn as five
/// 280 × 620 layers with Arc parts (Avatar, SegmentedControl, ArcGlyph). The numbers come from a seeded generator, so
/// every run shows the same week.
/// </summary>
public static class ExplodedViewDemoData
{
    /// <summary>The demos' seed.</summary>
    public const uint Seed = 0x5EED;

    // Lucide map, ISC (Core ArcIcons has no map glyph).
    private const string MapData =
        "M14.106 5.553a2 2 0 0 0 1.788 0l3.659-1.83A1 1 0 0 1 21 4.619v12.764a1 1 0 0 1-.553.894l-4.553 2.277a2 2 0 0 1-1.788 0" +
        "l-4.212-2.106a2 2 0 0 0-1.788 0l-3.659 1.83A1 1 0 0 1 3 19.381V6.618a1 1 0 0 1 .553-.894l4.553-2.277a2 2 0 0 1 1.788 0z " +
        "M15 5.764v15 M9 3.236v15";

    private static readonly string[] s_days = ["M", "T", "W", "T", "F", "S", "S"];

    /// <summary>mulberry32: the same small generator the web demos use, so a seed means the same numbers everywhere.</summary>
    private sealed class Mulberry32(uint seed)
    {
        private uint _state = seed;

        public double Next()
        {
            _state += 0x6D2B79F5;
            var t = _state;
            t = (t ^ (t >> 15)) * (t | 1);
            t ^= t + (t ^ (t >> 7)) * (t | 61);
            return (t ^ (t >> 14)) / 4294967296.0;
        }
    }

    /// <summary>
    /// The week: seven bars of round(14 + 78·r^1.4) px with the tallest moved to Saturday and set to 92; the total is the
    /// bars' sum / 92 × 9.4 km; the sessions take 16%, 21% and 38% of it; the change is 4 to 12%.
    /// </summary>
    public static ExplodedViewDemoWeek Generate(uint seed = Seed)
    {
        var random = new Mulberry32(seed);
        var bars = new int[7];
        for (var i = 0; i < bars.Length; i++)
        {
            bars[i] = (int)Math.Round(14 + 78 * Math.Pow(random.Next(), 1.4));
        }
        var peak = Array.IndexOf(bars, bars.Max());
        (bars[peak], bars[5]) = (bars[5], bars[peak]);
        bars[5] = 92;
        var total = Math.Round(bars.Sum() / 92.0 * 9.4, 1);
        var change = (int)Math.Round(4 + 8 * random.Next());
        double[] sessions = [Math.Round(total * 0.16, 1), Math.Round(total * 0.21, 1), Math.Round(total * 0.38, 1)];
        return new ExplodedViewDemoWeek(bars, total, change, sessions);
    }

    /// <summary>Fills a view with the sample: title, description, the two end captions and the five layers.</summary>
    public static void Fill(ExplodedView view, uint seed = Seed)
    {
        view.Title = "Made of layers";
        view.Description = "Lift the training screen apart. Every layer does one job, and together they stay quiet.";
        view.StartCaption = new ExplodedCaption { Title = "A single screen", Body = "Scroll to lift it apart, layer by layer." };
        view.EndCaption = new ExplodedCaption { Title = "Together again", Body = "Five layers, one job each, read as one calm screen." };
        view.Layers.Clear();
        view.Layers.AddRange(Layers(Generate(seed)));
    }

    /// <summary>The five layers, bottom up.</summary>
    public static IEnumerable<ExplodedLayer> Layers(ExplodedViewDemoWeek week)
    {
        yield return new ExplodedLayer
        {
            Name = "Base",
            Note = "Two neutral tokens",
            Body = "One quiet base layer. Every fill above it is a token, so the dark theme is a swap, not a rebuild.",
            Kind = ExplodedLayerKind.Plate,
            Content = BaseLayer()
        };
        yield return new ExplodedLayer
        {
            Name = "Grid",
            Note = "4 columns, 8pt steps",
            Body = "Columns and baselines line up the header, the chart and the tab bar on the same edges.",
            Kind = ExplodedLayerKind.Grid,
            Content = GridLayer()
        };
        yield return new ExplodedLayer
        {
            Name = "Data",
            Note = "Fixed-width numbers",
            Body = "Distance, the daily bars and the session log. Digits keep their width, so updates never shift a row.",
            Content = DataLayer(week)
        };
        yield return new ExplodedLayer
        {
            Name = "Controls",
            Note = "Thumb-sized targets",
            Body = "A range switch and the tab bar, each target at least 44 tall.",
            Content = ControlsLayer()
        };
        yield return new ExplodedLayer
        {
            Name = "Overlay",
            Note = "One shadow only",
            Body = "Alerts sit above everything, so this is the only layer allowed to cast a shadow.",
            Content = OverlayLayer(week)
        };
    }

    // Building blocks -----------------------------------------------------------------------------------------------------

    private static T Token<T>(T element, AvaloniaProperty property, ArcDesignTokenKind kind) where T : StyledElement
    {
        ArcTokens.Bind(element, property, kind);
        return element;
    }

    private static TextBlock Text(string text, double size, ArcDesignTokenKind color, FontWeight weight = FontWeight.Normal,
                                  bool tabular = false, bool display = false)
    {
        var block = new TextBlock { Text = text, FontSize = size, FontWeight = weight };
        Token(block, TextBlock.ForegroundProperty, color);
        Token(block, TextBlock.FontFamilyProperty, display ? ArcDesignTokenKind.DisplayFontFamily : ArcDesignTokenKind.BodyFontFamily);
        if (tabular)
        {
            block.FontFeatures = FontFeatureCollection.Parse("+tnum");
        }
        return block;
    }

    private static ArcGlyph Glyph(Geometry data, double size, double stroke, ArcDesignTokenKind color)
    {
        var glyph = new ArcGlyph
        {
            Data = data,
            Size = size,
            StrokeThickness = stroke,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        return Token(glyph, ArcGlyph.ForegroundProperty, color);
    }

    private static T At<T>(T control, double left, double top) where T : Control
    {
        Canvas.SetLeft(control, left);
        Canvas.SetTop(control, top);
        return control;
    }

    private static Canvas Screen() => new() { Width = 280, Height = 620 };

    /// <summary>Layer 1: the plate's status bar (the plate itself is drawn by the view).</summary>
    private static Control BaseLayer()
    {
        var bar = new Grid { Width = 234, Height = 22, ColumnDefinitions = new ColumnDefinitions("*,Auto,*") };
        var time = Text("8:24", 13, ArcDesignTokenKind.Foreground, FontWeight.Medium, tabular: true);
        time.VerticalAlignment = VerticalAlignment.Center;
        bar.Children.Add(time);
        var island = Token(new Border { Width = 78, Height = 22, CornerRadius = new CornerRadius(12) }, Border.BackgroundProperty,
            ArcDesignTokenKind.Foreground);
        Grid.SetColumn(island, 1);
        bar.Children.Add(island);
        var level = Token(new Border { Width = 10, Height = 6, CornerRadius = new CornerRadius(1.5), HorizontalAlignment = HorizontalAlignment.Left },
            Border.BackgroundProperty, ArcDesignTokenKind.Foreground);
        var battery = Token(new Border
        {
            Width = 22,
            Height = 11,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3.5),
            Padding = new Thickness(1.5),
            Child = level
        }, Border.BorderBrushProperty, ArcDesignTokenKind.TextSecondary);
        var icons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { Glyph(ArcIcons.Wifi, 13, 2, ArcDesignTokenKind.Foreground), battery }
        };
        Grid.SetColumn(icons, 2);
        bar.Children.Add(icons);
        var screen = Screen();
        screen.Children.Add(At(bar, 24, 14));
        return screen;
    }

    /// <summary>Layer 2: four columns and five baselines, in the accent (it only exists while the stack is apart).</summary>
    private static Control GridLayer()
    {
        var screen = Screen();
        for (var column = 0; column < 4; column++)
        {
            var left = 16 + column * 66.0;
            screen.Children.Add(At(Token(new Border { Width = 50, Height = 620, Opacity = 0.09 }, Border.BackgroundProperty,
                ArcDesignTokenKind.Accent), left, 0));
            screen.Children.Add(At(Token(new Border { Width = 50, Height = 620, Opacity = 0.3, BorderThickness = new Thickness(1, 0) },
                Border.BorderBrushProperty, ArcDesignTokenKind.Accent), left, 0));
        }
        foreach (var y in new[] { 52.0, 148, 208, 352, 548 })
        {
            screen.Children.Add(At(Token(new Border { Width = 280, Height = 1, Opacity = 0.36 }, Border.BackgroundProperty,
                ArcDesignTokenKind.Accent), 0, y));
        }
        return screen;
    }

    /// <summary>Layer 3: the week's distance, the daily bars and the session log.</summary>
    private static Control DataLayer(ExplodedViewDemoWeek week)
    {
        var screen = Screen();
        var header = new Grid { Width = 248, ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(Text("This week", 13, ArcDesignTokenKind.TextSecondary));
        var range = Text("Oct 14–20", 13, ArcDesignTokenKind.TextMuted);
        Grid.SetColumn(range, 1);
        header.Children.Add(range);
        screen.Children.Add(At(header, 16, 52));

        var total = Text($"{week.TotalKm:0.0} km", 34, ArcDesignTokenKind.Foreground, FontWeight.Medium, tabular: true, display: true);
        total.LineHeight = 37.4;
        total.LetterSpacing = -1.02;
        screen.Children.Add(At(total, 16, 74));

        var change = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children =
            {
                Glyph(ArcIcons.ArrowUpRight, 13, 1.75, ArcDesignTokenKind.TextSecondary),
                Text($"{week.ChangePercent}% more than last week", 12, ArcDesignTokenKind.TextSecondary)
            }
        };
        screen.Children.Add(At(change, 16, 118));

        var chart = new Grid { Width = 248, Height = 120, ColumnSpacing = 10, ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*,*") };
        var peak = week.Bars.Max();
        for (var day = 0; day < 7; day++)
        {
            var bar = Token(new Border { Height = week.Bars[day], CornerRadius = new CornerRadius(7) }, Border.BackgroundProperty,
                week.Bars[day] == peak ? ArcDesignTokenKind.Accent : ArcDesignTokenKind.SurfaceMuted);
            var letter = Text(s_days[day], 11, ArcDesignTokenKind.TextMuted);
            letter.HorizontalAlignment = HorizontalAlignment.Center;
            var column = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Spacing = 8, Children = { bar, letter } };
            Grid.SetColumn(column, day);
            chart.Children.Add(column);
        }
        screen.Children.Add(At(chart, 16, 208));

        var title = new Grid { Width = 248, ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        title.Children.Add(Text("Sessions", 13, ArcDesignTokenKind.Foreground, FontWeight.Medium));
        var all = Text("All", 13, ArcDesignTokenKind.TextMuted);
        Grid.SetColumn(all, 1);
        title.Children.Add(all);
        screen.Children.Add(At(title, 16, 352));

        (string Name, string Kind, Color From, Color To)[] sessions =
        [
            ("Harbor loop", "Easy run", Color.Parse("#5EB8F2"), Color.Parse("#3F6FD8")),
            ("Hill repeats", "Intervals", Color.Parse("#F4A65B"), Color.Parse("#D9534F")),
            ("Long Sunday", "Endurance", Color.Parse("#7FD8A6"), Color.Parse("#2E9C78"))
        ];
        var list = new StackPanel { Width = 248 };
        for (var i = 0; i < sessions.Length; i++)
        {
            var row = new Grid { Height = 52, ColumnSpacing = 12, ColumnDefinitions = new ColumnDefinitions("32,*,Auto") };
            var avatar = new Avatar
            {
                DisplayName = sessions[i].Name,
                Width = 32,
                Height = 32,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new LinearGradientBrush
                {
                    StartPoint = RelativePoint.TopLeft,
                    EndPoint = RelativePoint.BottomRight,
                    GradientStops = { new GradientStop(sessions[i].From, 0), new GradientStop(sessions[i].To, 1) }
                }
            };
            row.Children.Add(avatar);
            var text = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Children = { Text(sessions[i].Name, 13, ArcDesignTokenKind.Foreground), Text(sessions[i].Kind, 12, ArcDesignTokenKind.TextMuted) }
            };
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            var distance = Text($"{week.SessionKm[i]:0.0} km", 13, ArcDesignTokenKind.Foreground, tabular: true);
            distance.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(distance, 2);
            row.Children.Add(distance);
            if (i > 0)
            {
                list.Children.Add(Token(new Border { Height = 1 }, Border.BackgroundProperty, ArcDesignTokenKind.BorderSubtle));
                row.Height = 51;
            }
            list.Children.Add(row);
        }
        screen.Children.Add(At(list, 16, 380));
        return screen;
    }

    /// <summary>Layer 4: the range switch and the tab bar.</summary>
    private static Control ControlsLayer()
    {
        var screen = Screen();
        var range = new SegmentedControl
        {
            Width = 248,
            SelectedValue = "week",
            // Three equal segments across the screen's content width.
            ItemsPanel = new FuncTemplate<Panel?>(() => new UniformGrid { Rows = 1, ColumnSpacing = 2, MinWidth = 242 }),
            ItemsSource = new[]
            {
                new SegmentedOption("day", "Day"),
                new SegmentedOption("week", "Week"),
                new SegmentedOption("month", "Month")
            }
        };
        screen.Children.Add(At(range, 16, 148));

        var tabs = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };
        Geometry[] icons = [ArcIcons.House, ArcIcons.Activity, ArcIcons.Parse(MapData), ArcIcons.User];
        for (var i = 0; i < icons.Length; i++)
        {
            var cell = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(14),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = Glyph(icons[i], 20, 1.75, i == 1 ? ArcDesignTokenKind.Accent : ArcDesignTokenKind.TextMuted)
            };
            Grid.SetColumn(cell, i);
            tabs.Children.Add(cell);
        }
        var bar = new Border
        {
            Width = 278,
            Height = 72,
            Padding = new Thickness(16, 0, 16, 14),
            BorderThickness = new Thickness(0, 1, 0, 0),
            CornerRadius = new CornerRadius(0, 0, 39, 39),
            Child = tabs
        };
        Token(bar, Border.BackgroundProperty, ArcDesignTokenKind.Background);
        Token(bar, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        screen.Children.Add(At(bar, 1, 547));
        return screen;
    }

    /// <summary>Layer 5: the one floating alert, the only shadow in the stack.</summary>
    private static Control OverlayLayer(ExplodedViewDemoWeek week)
    {
        var screen = Screen();
        var mark = new Panel
        {
            Width = 32,
            Height = 32,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Token(new Ellipse { Opacity = 0.14 }, Shape.FillProperty, ArcDesignTokenKind.Success),
                Token(new ArcGlyph { Data = ArcIcons.Check, Size = 14, StrokeThickness = 2, HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center }, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Success)
            }
        };
        var heading = new DockPanel();
        var now = Text("now", 12, ArcDesignTokenKind.TextMuted);
        DockPanel.SetDock(now, Dock.Right);
        heading.Children.Add(now);
        heading.Children.Add(Text("Weekly goal reached", 13, ArcDesignTokenKind.Foreground, FontWeight.Medium));
        // The goal is the last round 5 km under the week's distance, so the alert always has some to spare.
        var goal = Math.Max(5, Math.Floor((week.TotalKm - 0.1) / 5) * 5);
        var body = Text($"{goal:0} km target, {week.TotalKm - goal:0.0} km to spare", 12, ArcDesignTokenKind.TextSecondary);
        body.TextTrimming = TextTrimming.CharacterEllipsis;
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1, Children = { heading, body } };
        Grid.SetColumn(texts, 1);
        var card = new Border
        {
            Width = 256,
            Height = 60,
            Padding = new Thickness(14, 12),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(22),
            Child = new Grid { ColumnSpacing = 12, ColumnDefinitions = new ColumnDefinitions("32,*"), Children = { mark, texts } }
        };
        Token(card, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceRaised);
        Token(card, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        Token(card, Border.BoxShadowProperty, ArcDesignTokenKind.ShadowFloating);
        screen.Children.Add(At(card, 12, 466));
        return screen;
    }
}
