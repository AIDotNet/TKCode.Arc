using Avalonia.Collections;
using Avalonia.Media;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Invented sample data for the answer's action parts (spec section 10: the CI migration answer). The model marks are plain
/// shapes drawn here (six-ray stars and concentric rings), not any vendor's logo; every word is our own. Shared by the tests
/// and the snapshot tool.
/// </summary>
internal static class AiAnswerActionsSamples
{
    /// <summary>A six-ray star (the sample "Lumen" models).</summary>
    public static readonly Geometry StarMark =
        Geometry.Parse("M12 1 L10.7 9.75 L2.47 6.5 L9.4 12 L2.47 17.5 L10.7 14.25 L12 23 L13.3 14.25 L21.53 17.5 L14.6 12 L21.53 6.5 L13.3 9.75Z");

    /// <summary>The same star with shorter rays ("Lumen Mini").</summary>
    public static readonly Geometry ShortStarMark =
        Geometry.Parse("M12 3.5 L10.8 9.92 L4.64 7.75 L9.6 12 L4.64 16.25 L10.8 14.08 L12 20.5 L13.2 14.08 L19.36 16.25 L14.4 12 L19.36 7.75 L13.2 9.92Z");

    /// <summary>Concentric rings ("Atlas").</summary>
    public static readonly Geometry RingsMark =
        Geometry.Parse("M12 2a10 10 0 1 0 0 20a10 10 0 1 0 0-20z M12 5a7 7 0 1 1 0 14a7 7 0 1 1 0-14z M12 8.5a3.5 3.5 0 1 0 0 7a3.5 3.5 0 1 0 0-7z");

    public static readonly IBrush Orange = new SolidColorBrush(Color.Parse("#E8714F"));
    public static readonly IBrush Blue = new SolidColorBrush(Color.Parse("#3B6FD8"));
    public static readonly IBrush Green = new SolidColorBrush(Color.Parse("#2F9E6A"));
    public static readonly IBrush Purple = new SolidColorBrush(Color.Parse("#8A5CD6"));

    public static AiModel LumenPro() => new() { Id = "lumen-pro", Name = "Lumen Pro 4", Mark = StarMark, MarkColor = Orange };

    public static AiModel Lumen() => new() { Id = "lumen", Name = "Lumen 4", Mark = StarMark, MarkColor = Blue };

    public static AiModel LumenMini() => new() { Id = "lumen-mini", Name = "Lumen Mini 4", Mark = ShortStarMark, MarkColor = Green };

    public static AiModel Atlas() => new() { Id = "atlas", Name = "Atlas 2.1", Mark = RingsMark, MarkColor = Purple };

    /// <summary>The models of "Switch model" when Lumen Pro 4 answered (the current one left out).</summary>
    public static AvaloniaList<AiModel> OtherModels() => new() { Lumen(), LumenMini(), Atlas() };

    public static readonly string[] Reasons = ["Not correct", "Out of date", "Needs a source", "Too long", "Didn't answer my question"];

    public static readonly string[] Related =
    [
        "Which checks should block a merge during the move?",
        "How do we keep build times down after switching runners?",
        "What should we do with flaky tests before cutting over?"
    ];

    public static readonly string[] Suggestions =
    [
        "How do we move our builds to a new CI system without slowing releases?",
        "Which checks should block a merge during the move?",
        "How do we keep build times down after switching runners?",
        "What should we do with flaky tests before cutting over?"
    ];

    /// <summary>A bar with every word set (as the answer block will set them).</summary>
    public static AiAnswerActions Bar(Action<AiAnswerActions>? configure = null)
    {
        var bar = new AiAnswerActions
        {
            ToolbarName = "Answer actions",
            CopyText = "Copy answer",
            CopiedText = "Copied",
            RegenerateText = "Answer again",
            GoodText = "Good answer",
            BadText = "Bad answer",
            RemoveFeedbackText = "Remove feedback",
            LinkText = "Copy link to this answer",
            LinkCopiedText = "Link copied",
            TryAgainText = "Try again",
            MoreConciseText = "More concise",
            MoreDetailedText = "More detailed",
            SwitchModelText = "Switch model",
            AnswerAgainWithText = "Answer again with",
            WhatWentWrongText = "What went wrong?",
            ThanksTitle = "Thanks for the feedback",
            ThanksDescriptionFor = reason => $"Noted: {reason.ToLowerInvariant()}",
            ThanksDescription = "It helps make answers better.",
            PreviousVersionText = "Previous version",
            NextVersionText = "Next version",
            VersionName = (n, count) => $"Version {n} of {count}",
            FeedbackReasons = Reasons,
            Models = OtherModels(),
            HasShare = true
        };
        configure?.Invoke(bar);
        return bar;
    }

    /// <summary>The model line of a complete answer by Lumen Pro 4.</summary>
    public static AiModelLine ModelLine(Action<AiModelLine>? configure = null)
    {
        var line = new AiModelLine { Model = LumenPro(), TokensText = "812 tokens", DurationText = "18.6s" };
        configure?.Invoke(line);
        return line;
    }

    public static AiRelatedQuestions RelatedQuestions(Action<AiRelatedQuestions>? configure = null)
    {
        var related = new AiRelatedQuestions { Heading = "Related", Questions = new AvaloniaList<string>(Related) };
        configure?.Invoke(related);
        return related;
    }
}
