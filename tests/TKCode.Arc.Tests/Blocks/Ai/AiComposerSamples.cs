using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Invented sample data for the AI composer (spec section 10; no Arc sample data, no vendor names or marks): the four
/// commands, the six mentions in two groups, the two tools, the five models of the model menu samples, the three
/// suggested prompts and the simulated sources of the plus menu. The Gallery reuses it.
/// </summary>
internal static class AiComposerSamples
{
    public const string Placeholder = "Ask a question — type / for actions, @ to pull in a file";

    /// <summary>The three suggestion chips a host might show above the composer: label, icon and the prompt they fill in.</summary>
    public static readonly (string Label, Geometry Icon, string Prompt)[] Suggestions =
    [
        ("Compare the last two sprint reviews", ArcIcons.FileText, "/recap @Sprint 15 review and list what slipped since the previous sprint"),
        ("Tidy up the tester welcome email", ArcIcons.PenLine, "/rewrite our tester welcome email so it fits on one screen"),
        ("Find why big exports stall", ArcIcons.Bug, "/debug @export-worker.ts large CSV exports stall at the last chunk")
    ];

    public static AiCommand[] Commands() =>
    [
        new() { Id = "recap", Name = "recap", Description = "Boil a doc or file down to its main points", Icon = ArcIcons.FileText },
        new() { Id = "rewrite", Name = "rewrite", Description = "Rewrite in a clearer or friendlier tone", Icon = ArcIcons.PenLine },
        new() { Id = "outline", Name = "outline", Description = "Turn loose notes into a structured outline", Icon = ArcIcons.Lightbulb },
        new() { Id = "debug", Name = "debug", Description = "Track down and fix a problem in the code", Icon = ArcIcons.Wrench }
    ];

    public static AiContextItem[] ContextItems() =>
    [
        new() { Id = "file-export-worker", Label = "export-worker.ts", Hint = "services/export", Group = "Files", Icon = ArcIcons.FileCode },
        new() { Id = "file-usage-oct", Label = "usage-oct.csv", Hint = "analytics/monthly", Group = "Files", Icon = ArcIcons.FileSpreadsheet },
        new() { Id = "file-style-guide", Label = "style-guide.pdf", Hint = "Brand, 3.1 MB", Group = "Files", Icon = ArcIcons.FileText },
        new() { Id = "doc-sprint-15", Label = "Sprint 15 review", Hint = "Wiki, edited Mar 3", Group = "Docs", Icon = ArcIcons.BookOpen },
        new() { Id = "doc-export-timeouts", Label = "Export timeouts", Hint = "Tracker, OPS-318", Group = "Docs", Icon = ArcIcons.ListTodo },
        new() { Id = "doc-onboarding-v2", Label = "Onboarding flow v2", Hint = "Design board, edited Mar 1", Group = "Docs", Icon = ArcIcons.LayoutTemplate }
    ];

    public static AiComposerTool[] Tools() =>
    [
        new() { Id = "web-search", Label = "Web search", Icon = ArcIcons.Globe },
        new() { Id = "extended-thinking", Label = "Extended thinking", Icon = ArcIcons.Brain }
    ];

    /// <summary>The plus menu's simulated sources: each adds a ready "Source" attachment to the composer it runs on.</summary>
    public static AiComposerAction[] SourceActions() =>
    [
        new()
        {
            Label = "Add from cloud drive", Icon = ArcIcons.Cloud,
            Command = new AddSourceCommand("Budget forecast", "Spreadsheet, 380 KB", ArcIcons.FileSpreadsheet)
        },
        new()
        {
            Label = "Add from repository", Icon = ArcIcons.GitBranch,
            Command = new AddSourceCommand("teamspace/web-app", "main, 186 files", ArcIcons.GitBranch)
        },
        new()
        {
            Label = "Capture screen", Icon = ArcIcons.Camera,
            Command = new AddSourceCommand("Capture 2026-03-04 at 09.15.png", "Image, 240 KB", null, thumbnail: true)
        }
    ];

    /// <summary>A composer with every sample: commands, mentions, tools, models and sources.</summary>
    public static AiComposer Create(Action<AiComposer>? configure = null)
    {
        var composer = new AiComposer { Placeholder = Placeholder };
        Fill(composer);
        configure?.Invoke(composer);
        return composer;
    }

    public static void Fill(AiComposer composer)
    {
        composer.Commands.AddRange(Commands());
        composer.ContextItems.AddRange(ContextItems());
        composer.Tools.AddRange(Tools());
        composer.AttachActions.AddRange(SourceActions());
        composer.Providers.AddRange(ModelMenuSamples.Providers());
        composer.Models.AddRange(ModelMenuSamples.ComposerModels());
    }

    /// <summary>A ready attachment in the composer's states, for snapshots and send tests.</summary>
    public static AiAttachment Attachment(string name, long? size, AiAttachmentStatus status = AiAttachmentStatus.Ready, double progress = 0,
        string? error = null, AiAttachmentKind kind = AiAttachmentKind.File) =>
        AiAttachmentSamples.Sample(name, size, status, progress, error, kind);

    /// <summary>A long paste (24 lines of release notes), long enough to become a "Pasted text" chip.</summary>
    public static string LongPaste() =>
        string.Join("\n", Enumerable.Range(1, 24).Select(i => $"- Item {i}: tidy up the export dialog copy and recheck the empty states."));

    /// <summary>Adds a ready source attachment (the plus menu's simulated cloud drive, repository and screen capture).</summary>
    private sealed class AddSourceCommand(string name, string meta, Geometry? icon, bool thumbnail = false) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => parameter is AiComposer;

        public void Execute(object? parameter)
        {
            if (parameter is not AiComposer composer)
            {
                return;
            }
            composer.Attachments.Add(new AiAttachment
            {
                Id = "source-" + Guid.NewGuid().ToString("N"),
                Name = name,
                Kind = thumbnail ? AiAttachmentKind.Image : AiAttachmentKind.Source,
                Meta = meta,
                Icon = icon,
                Thumbnail = thumbnail ? Swatch() : null,
                Status = AiAttachmentStatus.Ready
            });
        }

        /// <summary>A flat geometric stand-in for a screenshot.</summary>
        private static IImage Swatch()
        {
            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing
            {
                Brush = new ImmutableSolidColorBrush(Color.Parse("#DCE3F2")),
                Geometry = new RectangleGeometry(new Avalonia.Rect(0, 0, 76, 76))
            });
            group.Children.Add(new GeometryDrawing
            {
                Brush = new ImmutableSolidColorBrush(Color.Parse("#8FA3D6")),
                Geometry = new RectangleGeometry(new Avalonia.Rect(10, 14, 56, 10))
            });
            group.Children.Add(new GeometryDrawing
            {
                Brush = new ImmutableSolidColorBrush(Color.Parse("#B9C6E6")),
                Geometry = new RectangleGeometry(new Avalonia.Rect(10, 32, 36, 30))
            });
            return new DrawingImage(group);
        }
    }
}
