using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActionMorphPreviewDemo : UserControl
{
    private readonly List<Entry> _entries =
    [
        new(EntryKind.Todo, "Draft the onboarding checklist", "Today", false),
        new(EntryKind.Event, "Design review with the mobile team", "2:00 PM", false),
        new(EntryKind.Todo, "Renew the studio's domain name", "This week", true),
        new(EntryKind.Note, "Ask for a quieter room for interviews", null, false)
    ];

    public ActionMorphPreviewDemo()
    {
        InitializeComponent();
        Morph.SubmitHandler = SaveAsync;
        Morph.Submitted += (_, e) => Add(e.Submission);
        Render();
    }

    private enum EntryKind
    {
        Todo,
        Note,
        Event
    }

    private sealed record Entry(EntryKind Kind, string Text, string? Meta, bool Done);

    // Stands in for a request to a server: it takes a moment, and fails when "Fails" is picked.
    private async Task SaveAsync(ActionMorphSubmission submission, CancellationToken token)
    {
        await Task.Delay(700, token);
        if (Outcome.SelectedValue as string == "fails")
        {
            throw new InvalidOperationException("The plan could not be saved.");
        }
    }

    private void Add(ActionMorphSubmission submission)
    {
        var kind = submission.Action.Id switch
        {
            "note" => EntryKind.Note,
            "event" => EntryKind.Event,
            _ => EntryKind.Todo
        };
        _entries.Insert(0, new Entry(kind, submission.Text.ReplaceLineEndings(" "), submission.Choice, false));
        Render();
    }

    private void Render()
    {
        Entries.Children.Clear();
        foreach (var entry in _entries.Take(5))
        {
            Entries.Children.Add(Row(entry));
        }
        var open = _entries.Count(e => e.Kind == EntryKind.Todo && !e.Done);
        Summary.Text = open == 1 ? "1 open to-do" : $"{open} open to-dos";
    }

    private static Control Row(Entry entry)
    {
        var text = new TextBlock
        {
            Text = entry.Text,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextDecorations = entry.Done ? TextDecorations.Strikethrough : null
        };
        ArcTokens.Bind(text, TextBlock.ForegroundProperty,
            entry.Done ? ArcDesignTokenKind.TextMuted : ArcDesignTokenKind.Foreground);
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 12,
            Height = 48,
            Margin = new Thickness(12, 0)
        };
        var lead = Lead(entry);
        grid.Children.Add(lead);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        if (entry.Meta is { } meta)
        {
            var label = new TextBlock { Text = meta, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(label, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
            ArcTokens.Bind(label, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            Grid.SetColumn(label, 2);
            grid.Children.Add(label);
        }
        var row = new Border { Child = grid };
        ArcTokens.Bind(row, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessBottom);
        ArcTokens.Bind(row, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return row;
    }

    private static Control Lead(Entry entry)
    {
        if (entry.Kind == EntryKind.Todo)
        {
            var circle = new Border
            {
                Width = 20,
                Height = 20,
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center
            }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
            if (entry.Done)
            {
                ArcTokens.Bind(circle, Border.BackgroundProperty, ArcDesignTokenKind.Foreground);
                ArcTokens.Bind(circle, Border.BorderBrushProperty, ArcDesignTokenKind.Foreground);
                var check = new ArcGlyph
                {
                    Data = ArcIcons.Check,
                    Size = 12,
                    StrokeThickness = 2.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                ArcTokens.Bind(check, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Background);
                circle.Child = check;
            }
            else
            {
                ArcTokens.Bind(circle, Border.BorderBrushProperty, ArcDesignTokenKind.BorderStrong);
            }
            return circle;
        }
        var glyph = new ArcGlyph
        {
            Data = entry.Kind == EntryKind.Note ? ArcIcons.FileText : ArcIcons.Calendar,
            Size = 16,
            Width = 20,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        return glyph;
    }
}
