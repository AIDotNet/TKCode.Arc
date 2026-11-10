using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>How a <see cref="OnboardingSurveyQuestion"/> is answered.</summary>
public enum OnboardingSurveyQuestionKind
{
    /// <summary>One option, as cards (the arrow keys move between them).</summary>
    SingleChoice,

    /// <summary>Any number of options (up to <see cref="OnboardingSurveyQuestion.MaxSelections"/>), as checkboxes.</summary>
    MultipleChoice,

    /// <summary>A few sentences of free text.</summary>
    Text
}

/// <summary>One option of a choice question.</summary>
public class OnboardingSurveyOption : AvaloniaObject
{
    /// <summary>The value recorded in the answer; falls back to <see cref="Label"/>.</summary>
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<OnboardingSurveyOption, string?>(nameof(Value));

    /// <summary>The option's text.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<OnboardingSurveyOption, string>(nameof(Label), string.Empty);

    /// <summary>A short line under the label.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<OnboardingSurveyOption, string?>(nameof(Description));

    /// <summary>An 18px icon beside the label of a single-choice card (a Lucide geometry such as an <c>ArcIcons</c> glyph).</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<OnboardingSurveyOption, Geometry?>(nameof(Icon));

    public OnboardingSurveyOption()
    {
    }

    public OnboardingSurveyOption(string label, string? description = null, Geometry? icon = null, string? value = null)
    {
        Label = label;
        Description = description;
        Icon = icon;
        Value = value;
    }

    /// <inheritdoc cref="ValueProperty"/>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>The value that goes into the answer.</summary>
    public string EffectiveValue => Value ?? Label;

    public override string ToString() => Label;
}

/// <summary>One question of an <see cref="OnboardingSurvey"/>.</summary>
public class OnboardingSurveyQuestion : AvaloniaObject
{
    /// <summary>A stable id; the answers are keyed by it (falls back to the title).</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<OnboardingSurveyQuestion, string?>(nameof(Id));

    /// <summary>The question.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<OnboardingSurveyQuestion, string>(nameof(Title), string.Empty);

    /// <summary>A sentence under the question (why you ask, or how the answer is used).</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<OnboardingSurveyQuestion, string?>(nameof(Description));

    /// <summary>Single choice (default), multiple choice or text.</summary>
    public static readonly StyledProperty<OnboardingSurveyQuestionKind> KindProperty =
        AvaloniaProperty.Register<OnboardingSurveyQuestion, OnboardingSurveyQuestionKind>(nameof(Kind));

    /// <summary>Whether the question can be skipped (it shows "Skip question" and Continue works without an answer).</summary>
    public static readonly StyledProperty<bool> IsOptionalProperty =
        AvaloniaProperty.Register<OnboardingSurveyQuestion, bool>(nameof(IsOptional));

    /// <summary>The most options a multiple-choice question accepts; 0 means no limit.</summary>
    public static readonly StyledProperty<int> MaxSelectionsProperty =
        AvaloniaProperty.Register<OnboardingSurveyQuestion, int>(nameof(MaxSelections));

    /// <summary>The placeholder of a text question.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<OnboardingSurveyQuestion, string?>(nameof(Placeholder));

    /// <summary>The longest answer a text question accepts; 0 means no limit.</summary>
    public static readonly StyledProperty<int> MaxLengthProperty =
        AvaloniaProperty.Register<OnboardingSurveyQuestion, int>(nameof(MaxLength), 280);

    /// <inheritdoc cref="IdProperty"/>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="KindProperty"/>
    public OnboardingSurveyQuestionKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="IsOptionalProperty"/>
    public bool IsOptional { get => GetValue(IsOptionalProperty); set => SetValue(IsOptionalProperty, value); }

    /// <inheritdoc cref="MaxSelectionsProperty"/>
    public int MaxSelections { get => GetValue(MaxSelectionsProperty); set => SetValue(MaxSelectionsProperty, value); }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <inheritdoc cref="MaxLengthProperty"/>
    public int MaxLength { get => GetValue(MaxLengthProperty); set => SetValue(MaxLengthProperty, value); }

    /// <summary>The options of a choice question (the XAML content).</summary>
    [Content]
    public AvaloniaList<OnboardingSurveyOption> Options { get; } = new();

    /// <summary>The key of this question's answer.</summary>
    public string Key => Id ?? Title;

    public override string ToString() => Title;
}

/// <summary>The answer to one question: the chosen values or the text, or nothing when it was skipped.</summary>
/// <param name="QuestionId">The question's <see cref="OnboardingSurveyQuestion.Key"/>.</param>
/// <param name="Values">The chosen option values, in option order (empty for text and skipped questions).</param>
/// <param name="Text">The text of a text question, or null.</param>
public sealed record OnboardingSurveyAnswer(string QuestionId, IReadOnlyList<string> Values, string? Text)
{
    /// <summary>True when nothing was chosen or written.</summary>
    public bool IsEmpty => Values.Count == 0 && string.IsNullOrWhiteSpace(Text);
}

/// <summary>Arguments of <see cref="OnboardingSurvey.QuestionChanged"/>.</summary>
public sealed class OnboardingSurveyQuestionChangedEventArgs : RoutedEventArgs
{
    public OnboardingSurveyQuestionChangedEventArgs(RoutedEvent routedEvent, int oldIndex, int newIndex) : base(routedEvent)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    /// <summary>The question index that was shown.</summary>
    public int OldIndex { get; }

    /// <summary>The question index now shown; the question count when the thank-you is shown.</summary>
    public int NewIndex { get; }
}

/// <summary>Arguments of <see cref="OnboardingSurvey.Completed"/>: every answer, and whether the person skipped the rest of the survey.</summary>
public sealed class OnboardingSurveyCompletedEventArgs : RoutedEventArgs
{
    public OnboardingSurveyCompletedEventArgs(RoutedEvent routedEvent, IReadOnlyList<OnboardingSurveyAnswer> answers, bool isSkipped) : base(routedEvent)
    {
        Answers = answers;
        IsSkipped = isSkipped;
    }

    /// <summary>One answer per question, in question order (skipped and unseen questions have empty answers).</summary>
    public IReadOnlyList<OnboardingSurveyAnswer> Answers { get; }

    /// <summary>True when "Skip survey" ended it.</summary>
    public bool IsSkipped { get; }
}

/// <summary>
/// The built-in text of an <see cref="OnboardingSurvey"/>. Replace the whole object (with an object initializer) to
/// localize or reword it.
/// </summary>
public sealed class OnboardingSurveyLabels
{
    /// <summary>The culture of numbers (default en-US).</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The counter: (question, total) ⇒ "Question 2 of 4".</summary>
    public Func<int, int, string> Counter { get; init; } = static (index, total) => $"Question {index} of {total}";

    /// <summary>The button that ends the survey early.</summary>
    public string SkipSurvey { get; init; } = "Skip survey";

    /// <summary>The button that skips an optional question.</summary>
    public string SkipQuestion { get; init; } = "Skip question";

    /// <summary>The Back button.</summary>
    public string Back { get; init; } = "Back";

    /// <summary>The Continue button.</summary>
    public string Continue { get; init; } = "Continue";

    /// <summary>The button on the last question.</summary>
    public string Finish { get; init; } = "Finish";

    /// <summary>The hint under a multiple-choice question: limit (0 for none) ⇒ text.</summary>
    public Func<int, string> ChooseHint { get; init; } = static max => max > 0 ? $"Choose up to {max}." : "Choose all that apply.";

    /// <summary>The counter under a text answer: (length, limit) ⇒ "12 / 280".</summary>
    public Func<int, int, string> CharacterCount { get; init; } = static (length, max) => $"{length} / {max}";

    /// <summary>The title of the thank-you.</summary>
    public string DoneTitle { get; init; } = "Thanks, you're all set";

    /// <summary>The sentence of the thank-you.</summary>
    public string DoneDescription { get; init; } = "We'll use your answers to set up your workspace. You can change them later in Settings.";

    /// <summary>The title shown after "Skip survey".</summary>
    public string SkippedTitle { get; init; } = "No problem";

    /// <summary>The sentence shown after "Skip survey".</summary>
    public string SkippedDescription { get; init; } = "You can answer these questions any time from Settings.";

    /// <summary>The button on the thank-you that starts again.</summary>
    public string Restart { get; init; } = "Start over";

    /// <summary>The accessible name of the progress bar.</summary>
    public string ProgressName { get; init; } = "Survey progress";
}
