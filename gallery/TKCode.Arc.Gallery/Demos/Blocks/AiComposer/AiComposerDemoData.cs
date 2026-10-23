using Avalonia.Controls;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Invented commands, mentions, tools and a simulated reply for the AI composer demos.</summary>
internal static class AiComposerDemoData
{
    public const string Placeholder = "Ask a question, type / for actions or @ for a file";

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

    /// <summary>Fills a composer with the commands, mentions, tools and the picker demo's models.</summary>
    public static void Fill(AiComposer composer, bool models = true, bool tools = true)
    {
        composer.Commands.AddRange(Commands());
        composer.ContextItems.AddRange(ContextItems());
        if (tools)
        {
            composer.Tools.AddRange(Tools());
        }

        if (models)
        {
            composer.Providers.AddRange(ModelPickerDemoData.Providers());
            composer.Models.AddRange(ModelPickerDemoData.Models());
        }
    }

    /// <summary>
    /// Plays a reply for a sent message: the composer shows its stop button and a progress ring for about three seconds, then
    /// the reply text appears. Returns a handle that stops it.
    /// </summary>
    public static IDisposable SimulateReply(AiComposer composer, TextBlock output, AiComposerMessage message)
    {
        const int Steps = 30;
        var step = 0;
        output.Text = string.Empty;
        composer.GenerationProgress = 0;
        composer.IsGenerating = true;
        var reply = $"You asked: “{message.Text.Trim()}”. Here is a quick plan: pin down the goal, sort the open questions by risk, and give each one an owner.";
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (_, _) =>
        {
            step++;
            composer.GenerationProgress = Math.Min(1, step / (double)Steps);
            output.Text = reply[..Math.Min(reply.Length, reply.Length * step / Steps)];
            if (step >= Steps)
            {
                timer.Stop();
                composer.GenerationProgress = 1;
                composer.IsGenerating = false;
            }
        };
        timer.Start();
        return new Stop(timer, composer, output);
    }

    private sealed class Stop(DispatcherTimer timer, AiComposer composer, TextBlock output) : IDisposable
    {
        public void Dispose()
        {
            timer.Stop();
            composer.IsGenerating = false;
            output.Text += output.Text is { Length: > 0 } ? " (stopped)" : "(stopped)";
        }
    }
}
