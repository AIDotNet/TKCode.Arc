using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The code-built pieces of <see cref="AiWorkflow"/>: text in the card's type styles, an app's mark on the rail, the
/// status of a step, and the inline output previews (email, label, route, message, record, text). Every color is bound
/// to an ArcDesign token so the pieces follow theme changes.
/// </summary>
internal static class AiWorkflowViews
{
    public static TextBlock Text(string? text, double size, ArcDesignTokenKind color, bool medium = false, bool mono = false,
                                 bool center = false, bool wrap = false, double lineHeight = double.NaN)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            LineHeight = lineHeight,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Left
        };
        Ink(block, color);
        if (medium)
        {
            ArcTokens.Bind(block, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        }
        if (mono)
        {
            ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        }
        return block;
    }

    public static void Ink(Control control, ArcDesignTokenKind color) =>
        ArcTokens.Bind(control, TextElement.ForegroundProperty, color);

    public static ArcGlyph Glyph(Geometry? data, double size, ArcDesignTokenKind color, double stroke = 2)
    {
        var glyph = new ArcGlyph { Data = data, Size = size, StrokeThickness = stroke, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, color);
        return glyph;
    }

    /// <summary>An app's mark: its glyph (12px) in a 20px hairline square on Surface, so the rail passes behind it.</summary>
    public static Border Mark(AiWorkflowApp? app, AiWorkflowStepKind kind)
    {
        var data = app?.Mark ?? kind switch
        {
            AiWorkflowStepKind.Trigger => ArcIcons.Zap,
            AiWorkflowStepKind.Ai => ArcIcons.Sparkles,
            AiWorkflowStepKind.Branch => ArcIcons.GitBranch,
            _ => ArcIcons.Circle
        };
        var accent = app?.IsAccent ?? kind == AiWorkflowStepKind.Ai;
        var glyph = Glyph(data, AiWorkflowMetrics.MarkGlyphSize, accent ? ArcDesignTokenKind.Accent : ArcDesignTokenKind.Foreground, 2);
        glyph.HorizontalAlignment = HorizontalAlignment.Center;
        var mark = new Border
        {
            Width = AiWorkflowMetrics.MarkSize,
            Height = AiWorkflowMetrics.MarkSize,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = glyph
        }.BindCornerRadius(ArcDesignTokenKind.RadiusStep6);
        ArcTokens.Bind(mark, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(mark, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(mark, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        return mark;
    }

    /// <summary>
    /// The status slot's content: an icon and a short label, never color alone. Idle shows nothing; Running a spinner and
    /// "Running"; Done a check and the duration; Failed an alert and "Failed"; Skipped a dash and "Skipped".
    /// </summary>
    public static Control? Status(AiWorkflowStepStatus status, TimeSpan? duration, AiWorkflowLabels labels)
    {
        if (status == AiWorkflowStepStatus.Idle)
        {
            return null;
        }
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Control icon;
        string text;
        ArcDesignTokenKind ink;
        switch (status)
        {
            case AiWorkflowStepStatus.Running:
                var spinner = new ArcSpinner { Width = 12, Height = 12, Thickness = 1.75, VerticalAlignment = VerticalAlignment.Center };
                ArcTokens.Bind(spinner, ArcSpinner.ForegroundProperty, ArcDesignTokenKind.Accent);
                icon = spinner;
                text = labels.Running;
                ink = ArcDesignTokenKind.TextSecondary;
                break;
            case AiWorkflowStepStatus.Done:
                icon = Glyph(ArcIcons.Check, 13, ArcDesignTokenKind.Success, 2.25);
                text = duration is { } d ? labels.Duration(d) : string.Empty;
                ink = ArcDesignTokenKind.TextMuted;
                break;
            case AiWorkflowStepStatus.Failed:
                icon = Glyph(ArcIcons.CircleAlert, 13, ArcDesignTokenKind.Danger);
                text = labels.Failed;
                ink = ArcDesignTokenKind.Danger;
                break;
            default:
                icon = Glyph(ArcIcons.Minus, 13, ArcDesignTokenKind.TextMuted);
                text = labels.Skipped;
                ink = ArcDesignTokenKind.TextMuted;
                break;
        }
        panel.Children.Add(icon);
        panel.Children.Add(Text(text, 12, ink));
        return panel;
    }

    /// <summary>
    /// The inline preview of a step's output, in small muted text aligned with the title. Reasons and drafts are
    /// <see cref="AiWorkflowStreamText"/>s, returned in <paramref name="streams"/> so the row can play them.
    /// </summary>
    public static Control Output(AiWorkflowOutput output, AiWorkflowStep step, AiWorkflowLabels labels, List<AiWorkflowStreamText> streams)
    {
        var size = AiWorkflowMetrics.OutputFontSize;
        var line = AiWorkflowMetrics.OutputLineHeight;
        var panel = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 8) };
        switch (output)
        {
            case AiWorkflowEmailOutput email:
            {
                var head = new TextBlock { FontSize = size, LineHeight = line, TextTrimming = TextTrimming.CharacterEllipsis };
                var from = new Run(email.From);
                var sender = new Run(string.IsNullOrEmpty(email.Address) ? "  " : " <" + email.Address + ">  ");
                var subject = new Run(email.Subject);
                ArcTokens.Bind(from, TextElement.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
                ArcTokens.Bind(from, TextElement.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
                ArcTokens.Bind(sender, TextElement.ForegroundProperty, ArcDesignTokenKind.TextMuted);
                ArcTokens.Bind(subject, TextElement.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
                head.Inlines = [from, sender, subject];
                panel.Children.Add(head);
                var preview = Text(email.Preview, size, ArcDesignTokenKind.TextMuted, wrap: true, lineHeight: line);
                preview.MaxLines = 2;
                preview.TextTrimming = TextTrimming.CharacterEllipsis;
                panel.Children.Add(preview);
                break;
            }
            case AiWorkflowLabelOutput label:
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                var chip = new Border
                {
                    Height = 20,
                    Padding = new Thickness(7, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 6,
                        Children =
                        {
                            Dot(ArcDesignTokenKind.Accent),
                            Text(label.Label, size, ArcDesignTokenKind.Foreground, medium: true)
                        }
                    }
                }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
                ArcTokens.Bind(chip, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
                ArcTokens.Bind(chip, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
                row.Children.Add(chip);
                if (label.Confidence is { } confidence)
                {
                    row.Children.Add(Text(AiWorkflowFormat.Confidence(confidence), 11.5, ArcDesignTokenKind.TextMuted, mono: true));
                }
                panel.Children.Add(row);
                if (!string.IsNullOrEmpty(label.Reason))
                {
                    var reason = Stream(label.Reason, streams);
                    reason.Margin = new Thickness(0, 4, 0, 0);
                    panel.Children.Add(reason);
                }
                break;
            }
            case AiWorkflowRouteOutput route:
            {
                var path = step.Paths.FirstOrDefault(p => p.Id == route.PathId)?.Label ?? route.PathId;
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                row.Children.Add(Glyph(ArcIcons.CornerDownRight, 12, ArcDesignTokenKind.TextMuted));
                var text = new TextBlock { FontSize = size, LineHeight = line, TextTrimming = TextTrimming.CharacterEllipsis };
                var took = new Run(labels.Route(path));
                ArcTokens.Bind(took, TextElement.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
                text.Inlines = [took];
                if (!string.IsNullOrEmpty(route.Note))
                {
                    var note = new Run("  " + route.Note);
                    ArcTokens.Bind(note, TextElement.ForegroundProperty, ArcDesignTokenKind.TextMuted);
                    text.Inlines.Add(note);
                }
                row.Children.Add(text);
                panel.Children.Add(row);
                break;
            }
            case AiWorkflowMessageOutput message:
            {
                var head = new TextBlock { FontSize = size, LineHeight = line, TextTrimming = TextTrimming.CharacterEllipsis };
                var channel = new Run(message.Channel);
                var author = new Run("  " + message.Author);
                ArcTokens.Bind(channel, TextElement.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
                ArcTokens.Bind(channel, TextElement.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
                ArcTokens.Bind(author, TextElement.ForegroundProperty, ArcDesignTokenKind.TextMuted);
                head.Inlines = [channel, author];
                panel.Children.Add(head);
                var body = Text(message.Text, size, ArcDesignTokenKind.TextMuted, wrap: true, lineHeight: line);
                body.MaxLines = 2;
                body.TextTrimming = TextTrimming.CharacterEllipsis;
                panel.Children.Add(body);
                break;
            }
            case AiWorkflowRecordOutput record:
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
                var key = Text(record.Id, 11.5, ArcDesignTokenKind.TextSecondary, mono: true);
                row.Children.Add(key);
                var title = Text(record.Title, size, ArcDesignTokenKind.TextSecondary);
                Grid.SetColumn(title, 1);
                row.Children.Add(title);
                panel.Children.Add(row);
                if (!string.IsNullOrEmpty(record.Meta))
                {
                    panel.Children.Add(Text(record.Meta, size, ArcDesignTokenKind.TextMuted, lineHeight: line));
                }
                break;
            }
            case AiWorkflowTextOutput text:
                panel.Children.Add(Stream(text.Text, streams));
                break;
        }
        return panel;
    }

    private static AiWorkflowStreamText Stream(string text, List<AiWorkflowStreamText> streams)
    {
        var stream = new AiWorkflowStreamText(text)
        {
            FontSize = AiWorkflowMetrics.OutputFontSize,
            LineHeight = AiWorkflowMetrics.OutputLineHeight
        };
        Ink(stream, ArcDesignTokenKind.TextMuted);
        streams.Add(stream);
        return stream;
    }

    private static Border Dot(ArcDesignTokenKind color)
    {
        var dot = new Border { Width = 6, Height = 6, VerticalAlignment = VerticalAlignment.Center }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(dot, Border.BackgroundProperty, color);
        return dot;
    }
}
