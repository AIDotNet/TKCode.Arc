using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The built-in picture a <see cref="ScrollStory"/> step shows on the pinned stage.</summary>
public enum ScrollStoryScene
{
    /// <summary>An inbox whose newest row slides in and lights up (collecting input).</summary>
    Capture,

    /// <summary>A board of three columns with one card picked up (organizing work).</summary>
    Arrange,

    /// <summary>A document with a comment and a check mark (review and approval).</summary>
    Review,

    /// <summary>A rising chart with a "live" badge (shipping and results).</summary>
    Launch
}

/// <summary>
/// One step of a <see cref="ScrollStory"/>: a short label, a title, a paragraph and what the stage shows while the step
/// is the active one (a built-in <see cref="Scene"/>, or an <see cref="Image"/>). Set the properties before the step is
/// added; later changes are not observed.
/// </summary>
public sealed class ScrollStoryStep
{
    /// <summary>The small label above the title ("Step one", "Capture"); null shows the step number.</summary>
    public string? Kicker { get; set; }

    /// <summary>The step's title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The paragraph under the title.</summary>
    public string? Body { get; set; }

    /// <summary>The built-in picture on the stage while this step is active.</summary>
    public ScrollStoryScene Scene { get; set; }

    /// <summary>A picture that replaces the built-in scene, cropped to fill the stage.</summary>
    public IImage? Image { get; set; }
}

/// <summary>Arguments of <see cref="ScrollStory.ActiveStepChanged"/>.</summary>
public sealed class ScrollStoryStepChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ScrollStoryStepChangedEventArgs(RoutedEvent routedEvent, int index, ScrollStoryStep step) : base(routedEvent)
    {
        Index = index;
        Step = step;
    }

    /// <summary>The index of the step that became active.</summary>
    public int Index { get; }

    /// <summary>The step that became active.</summary>
    public ScrollStoryStep Step { get; }
}

/// <summary>The invented story <see cref="ScrollStory"/> tells with no configuration.</summary>
internal static class ScrollStorySamples
{
    public static ScrollStoryStep[] Create() =>
    [
        new() { Kicker = "Capture", Title = "Every request lands in one inbox", Scene = ScrollStoryScene.Capture,
            Body = "Forms, email and chat all feed the same list, already tagged by product area, so nothing waits in a side channel." },
        new() { Kicker = "Arrange", Title = "Shape the week in minutes", Scene = ScrollStoryScene.Arrange,
            Body = "Drag work onto the board, and the plan adjusts owners and dates around what is already in flight." },
        new() { Kicker = "Review", Title = "Feedback where the work is", Scene = ScrollStoryScene.Review,
            Body = "Comments sit on the exact line they are about, and an approval is one click once every thread is closed." },
        new() { Kicker = "Launch", Title = "Ship it and watch it land", Scene = ScrollStoryScene.Launch,
            Body = "Release with a note to the people who asked, then follow adoption on the same page you planned it on." },
    ];
}
