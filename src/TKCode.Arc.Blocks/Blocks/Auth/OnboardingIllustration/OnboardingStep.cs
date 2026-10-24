using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The four drawings of <see cref="OnboardingIllustration"/>; the same line shapes morph from one to the next.</summary>
public enum OnboardingScene
{
    /// <summary>An empty open box with a plus sign: a new, empty space.</summary>
    Folder,

    /// <summary>Pages dropping into the box while a note flies in along a dotted trail: bringing content in.</summary>
    Files,

    /// <summary>Three people under one shared board, a dotted comment pointing at a highlighted line: working together.</summary>
    Team,

    /// <summary>A round badge with two ribbons, three rays and a check mark that draws in: done.</summary>
    Badge
}

/// <summary>
/// One step of <see cref="OnboardingIllustration"/>: its drawing, its title and sentence, and the accessible description
/// of the drawing. A change to any of them shows at once in the block.
/// </summary>
public class OnboardingStep : AvaloniaObject
{
    /// <summary>The drawing shown for this step.</summary>
    public static readonly StyledProperty<OnboardingScene> SceneProperty =
        AvaloniaProperty.Register<OnboardingStep, OnboardingScene>(nameof(Scene));

    /// <summary>The step's title (one short line).</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<OnboardingStep, string>(nameof(Title), string.Empty);

    /// <summary>The sentence under the title (two lines at most fit without moving the buttons).</summary>
    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<OnboardingStep, string>(nameof(Description), string.Empty);

    /// <summary>What the drawing shows, read by screen readers as the image's name.</summary>
    public static readonly StyledProperty<string> SceneDescriptionProperty =
        AvaloniaProperty.Register<OnboardingStep, string>(nameof(SceneDescription), string.Empty);

    /// <summary>Creates an empty step (Folder scene, no text).</summary>
    public OnboardingStep()
    {
    }

    /// <summary>Creates a step with its drawing and text.</summary>
    public OnboardingStep(OnboardingScene scene, string title, string description, string sceneDescription)
    {
        Scene = scene;
        Title = title;
        Description = description;
        SceneDescription = sceneDescription;
    }

    /// <inheritdoc cref="SceneProperty"/>
    public OnboardingScene Scene { get => GetValue(SceneProperty); set => SetValue(SceneProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="SceneDescriptionProperty"/>
    public string SceneDescription { get => GetValue(SceneDescriptionProperty); set => SetValue(SceneDescriptionProperty, value); }
}

/// <summary>What moved <see cref="OnboardingIllustration.CurrentIndex"/>.</summary>
public enum OnboardingStepChangeReason
{
    /// <summary>The Next button (or <see cref="OnboardingIllustration.Next"/>).</summary>
    Next,

    /// <summary>The Back button (or <see cref="OnboardingIllustration.Back"/>).</summary>
    Back,

    /// <summary>A step dot.</summary>
    Dot,

    /// <summary>The Left or Right arrow key.</summary>
    Keyboard,

    /// <summary>The Skip button (or <see cref="OnboardingIllustration.Skip"/>): straight to the last step.</summary>
    Skip,

    /// <summary>Replay intro (or <see cref="OnboardingIllustration.Replay"/>): back to the first step.</summary>
    Replay,

    /// <summary>Code set <see cref="OnboardingIllustration.CurrentIndex"/> or called <see cref="OnboardingIllustration.GoTo"/>.</summary>
    Programmatic
}

/// <summary>Arguments of <see cref="OnboardingIllustration.StepChanged"/>.</summary>
public sealed class OnboardingStepChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public OnboardingStepChangedEventArgs(RoutedEvent routedEvent, int oldIndex, int newIndex, OnboardingStepChangeReason reason)
        : base(routedEvent)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
        Reason = reason;
    }

    /// <summary>The step shown before (0-based).</summary>
    public int OldIndex { get; }

    /// <summary>The step shown now (0-based).</summary>
    public int NewIndex { get; }

    /// <summary>What moved the step.</summary>
    public OnboardingStepChangeReason Reason { get; }
}

/// <summary>
/// Every word <see cref="OnboardingIllustration"/> shows or announces besides the steps themselves, in English by
/// default. Replace the instance to translate or reword; texts with numbers are functions so the word order can change.
/// </summary>
public sealed class OnboardingIllustrationLabels
{
    /// <summary>The main button on every step but the last.</summary>
    public string Next { get; init; } = "Next";

    /// <summary>The button that goes back one step (its accessible name when only the arrow shows).</summary>
    public string Back { get; init; } = "Back";

    /// <summary>The button in the corner of the drawing that jumps to the last step.</summary>
    public string Skip { get; init; } = "Skip";

    /// <summary>The main button on the last step.</summary>
    public string GetStarted { get; init; } = "Get started";

    /// <summary>The main button once the onboarding is complete.</summary>
    public string ReplayIntro { get; init; } = "Replay intro";

    /// <summary>The accessible name of the row of step dots.</summary>
    public string StepsGroupName { get; init; } = "Onboarding steps";

    /// <summary>The position announced with each step's text; the arguments are the step (1-based) and the step count.</summary>
    public Func<int, int, string> StepStatus { get; init; } = (step, count) => $"Step {step} of {count}";

    /// <summary>Announced with the completion text.</summary>
    public string CompletedStatus { get; init; } = "Onboarding complete";

    /// <summary>The accessible name of a step dot; the arguments are the step (1-based) and its title.</summary>
    public Func<int, string, string> DotName { get; init; } = (step, title) => $"Step {step}: {title}";

    /// <summary>The status a screen reader reads on the dot of the current step.</summary>
    public string CurrentDotStatus { get; init; } = "Current step";
}

/// <summary>
/// The sample steps <see cref="OnboardingIllustration"/> shows while <see cref="OnboardingIllustration.Steps"/> is empty:
/// a first run of an invented workspace product, one step per scene.
/// </summary>
public static class OnboardingIllustrationSamples
{
    /// <summary>The accessible name of the sample card's region.</summary>
    public const string Label = "Northdesk onboarding";

    /// <summary>Creates the four sample steps (new objects on every call).</summary>
    public static OnboardingStep[] Steps() =>
    [
        new(OnboardingScene.Folder,
            "Set up your first space",
            "Each project keeps one home. Begin with the spring launch notes and add files when you are ready.",
            "An empty open box with a plus sign beside it"),
        new(OnboardingScene.Files,
            "Bring the files you already use",
            "Drag in slides, sheets, and PDFs. Northdesk sorts them by kind and keeps every earlier version.",
            "Pages dropping into the open box while one more page flies in"),
        new(OnboardingScene.Team,
            "Review it together in one place",
            "Invite Ana and Ruben to the same draft. Notes stay attached to the exact line they mention.",
            "Three people gathered around one shared board"),
        new(OnboardingScene.Badge,
            "You are all set",
            "Spring launch notes now has 5 files and 2 teammates. You can change any of it later in settings.",
            "A ribbon badge with a check mark")
    ];
}
