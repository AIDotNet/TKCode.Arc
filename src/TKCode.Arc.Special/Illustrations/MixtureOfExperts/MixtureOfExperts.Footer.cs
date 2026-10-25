using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

public partial class MixtureOfExperts
{
    private (MixtureOfExpertsFooterKind Kind, int Subject, int Plan, int Palette, bool Compact, MixtureOfExpertsLabels? Labels) _footerKey;

    /// <summary>The footer's route line and note as shown (tests read them).</summary>
    internal (string Line, string Note) FooterText { get; private set; }

    /// <summary>
    /// Rebuilds the footer's two lines when what they describe changes. Both swap like Arc's: the old line fades out in
    /// about 100 ms while the new one fades in from 2 px below in 160 ms.
    /// </summary>
    private void UpdateFooter(MixtureOfExpertsScene scene)
    {
        var key = (scene.Footer, scene.FooterSubject, _planVersion, _palette.Version, _isCompact, (MixtureOfExpertsLabels?)Labels);
        if (key == _footerKey || _routeLine is null || _note is null || !_palette.IsReady)
        {
            return;
        }
        _footerKey = key;
        var labels = Labels;
        var p = _palette;
        string lineText;
        string noteText;
        Control line;
        Control note;
        switch (scene.Footer)
        {
            case MixtureOfExpertsFooterKind.Empty:
                lineText = string.Empty;
                noteText = labels.NoTokens;
                line = new Panel();
                note = NoteText(noteText, null, warning: false);
                break;
            case MixtureOfExpertsFooterKind.Summary:
            {
                var plan = _plan;
                var active = FormatBillions(Parameters.Active(plan.K));
                lineText = labels.Summary(plan.Routes.Length);
                noteText = labels.SummaryNote(plan.K, plan.ExpertCount, active, FormatBillions(Parameters.Total));
                line = Row(8, Glyph(ArcIcons.CircleCheck, p.Success, 14), Label(lineText, p.Foreground, medium: true));
                note = NoteText(noteText, null, warning: false);
                break;
            }
            case MixtureOfExpertsFooterKind.Expert:
            {
                var e = scene.FooterSubject;
                var (name, handles, icon) = _experts[e];
                var load = _plan.LoadsAfter(scene.Counted)[e];
                var loadText = labels.ExpertLoad(load, _plan.IsUnlimited ? null : _plan.Capacity);
                lineText = name + " " + loadText;
                noteText = handles ?? string.Empty;
                var mark = icon is not null ? (Control)Glyph(icon, p.TextSecondary, 14) : Label(name.Length > 0 ? name[..1].ToUpperInvariant() : "?", p.TextSecondary, medium: true);
                line = Row(8, mark, Label(name, p.Foreground, medium: true), Label(loadText, p.TextMuted));
                note = NoteText(noteText, null, warning: false);
                break;
            }
            case MixtureOfExpertsFooterKind.Scoring:
            {
                var t = scene.FooterSubject;
                lineText = labels.Scoring(_plan.ExpertCount);
                noteText = labels.ScoringNote(_plan.K, _plan.ExpertCount);
                line = RouteLine(t, Label(lineText, p.Foreground));
                note = NoteText(noteText, null, warning: false);
                break;
            }
            default:
            {
                var t = scene.FooterSubject;
                var route = _plan.Routes[t];
                var items = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
                var spoken = new List<string>();
                for (var r = 0; r < route.Experts.Count; r++)
                {
                    var e = route.Experts[r];
                    var name = _experts[e].Name;
                    if (route.IsDropped(e))
                    {
                        items.Children.Add(Row(6, Glyph(ArcIcons.TriangleAlert, p.Warning, 12), Label(name, p.TextSecondary), Label(labels.Full, p.TextMuted)));
                        spoken.Add(name + " " + labels.Full);
                    }
                    else
                    {
                        var percent = MixtureOfExpertsPlanner.Percent(route.Weights[r]) + "%";
                        items.Children.Add(Row(6, Dot(p.Rank[Math.Min(r, 1)]), Label(name, p.Foreground), Label(percent, p.TextSecondary)));
                        spoken.Add(name + " " + percent);
                    }
                }
                lineText = string.Join(", ", spoken);
                line = RouteLine(t, items);
                (noteText, var warning) = RouteNote(route, labels);
                note = NoteText(noteText, route.Token, warning);
                break;
            }
        }
        FooterText = (lineText, noteText.Replace("{token}", SpokenToken(scene.FooterSubject), StringComparison.Ordinal));
        _routeLine.Content = line;
        _note.Content = note;
    }

    /// <summary>The note for a route: shared, single, one skipped, or passed through.</summary>
    private (string Text, bool Warning) RouteNote(MixtureOfExpertsRoute route, MixtureOfExpertsLabels labels)
    {
        string Name(int e) => _experts[e].Name;
        if (route.IsPassThrough)
        {
            var names = route.Experts.Count == 1 ? Name(route.Experts[0]) : labels.And(Name(route.Experts[0]), Name(route.Experts[1]));
            return (labels.NotePassThrough(names), true);
        }
        if (route.Dropped.Count > 0)
        {
            var full = route.Dropped[0];
            var kept = route.Experts.First(e => !route.IsDropped(e));
            return (labels.NoteDropped(Name(full), Name(kept)), true);
        }
        if (route.Experts.Count == 1)
        {
            return (labels.NoteSingle(Name(route.Experts[0]), _plan.ExpertCount - 1), false);
        }
        return (labels.NotePair(Name(route.Experts[0]), Name(route.Experts[1]),
            MixtureOfExpertsPlanner.Percent(route.Weights[0]), MixtureOfExpertsPlanner.Percent(route.Weights[1])), false);
    }

    /// <summary>The route line: the token's chip and an arrow (hidden under 480 px), then <paramref name="rest"/>.</summary>
    private Control RouteLine(int token, Control rest)
    {
        var p = _palette;
        var row = Row(8);
        if (!_isCompact)
        {
            var chip = new Border
            {
                Height = 24,
                BorderThickness = new Thickness(1),
                BorderBrush = p.Brush(p.BorderStrong),
                Background = p.Brush(p.Surface),
                Padding = new Thickness(8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = Label(_tokens[token].Text, p.Foreground, medium: true)
            };
            chip.BindCornerRadius(ArcDesignTokenKind.RadiusStep7);
            row.Children.Add(chip);
            row.Children.Add(Glyph(ArcIcons.ArrowRight, p.TextMuted, 14));
        }
        row.Children.Add(rest);
        return row;
    }

    /// <summary>
    /// A note in secondary text that wraps; <c>{token}</c> becomes a small inline chip with the token. A warning note
    /// starts with a warning sign.
    /// </summary>
    private Control NoteText(string text, string? token, bool warning)
    {
        var p = _palette;
        var block = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 19.6,
            Foreground = p.Brush(p.TextSecondary),
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Top
        };
        var at = token is null ? -1 : text.IndexOf("{token}", StringComparison.Ordinal);
        if (at < 0)
        {
            block.Text = token is null ? text : text.Replace("{token}", token, StringComparison.Ordinal);
        }
        else
        {
            var inlines = new InlineCollection();
            if (at > 0)
            {
                inlines.Add(new Run(text[..at]));
            }
            var chip = new Border
            {
                Background = p.Brush(p.SurfaceMuted),
                Padding = new Thickness(4, 0),
                Margin = new Thickness(0, 0, 0, -3),
                Child = new TextBlock { Text = token, Foreground = p.Brush(p.Foreground), FontSize = 14 }
            };
            chip.BindCornerRadius(ArcDesignTokenKind.RadiusStep4);
            inlines.Add(new InlineUIContainer(chip) { BaselineAlignment = BaselineAlignment.Center });
            var rest = text[(at + "{token}".Length)..];
            if (rest.Length > 0)
            {
                inlines.Add(new Run(rest));
            }
            block.Inlines = inlines;
        }
        if (!warning)
        {
            return block;
        }
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var sign = Glyph(ArcIcons.TriangleAlert, p.Warning, 14);
        sign.VerticalAlignment = VerticalAlignment.Top;
        sign.Margin = new Thickness(0, 2.8, 6, 0);
        grid.Children.Add(sign);
        Grid.SetColumn(block, 1);
        grid.Children.Add(block);
        return grid;
    }

    private static StackPanel Row(double spacing, params Control[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing, VerticalAlignment = VerticalAlignment.Center };
        foreach (var child in children)
        {
            row.Children.Add(child);
        }
        return row;
    }

    private TextBlock Label(string text, Color color, bool medium = false) => new()
    {
        Text = text,
        Foreground = _palette.Brush(color),
        FontSize = 14,
        FontWeight = medium ? FontWeight.Medium : FontWeight.Normal,
        VerticalAlignment = VerticalAlignment.Center
    };

    private ArcGlyph Glyph(Geometry data, Color color, double size) => new()
    {
        Data = data,
        Size = size,
        Foreground = _palette.Brush(color),
        VerticalAlignment = VerticalAlignment.Center
    };

    private Ellipse Dot(Color color) => new()
    {
        Width = 8,
        Height = 8,
        Fill = _palette.Brush(color),
        VerticalAlignment = VerticalAlignment.Center
    };
}
