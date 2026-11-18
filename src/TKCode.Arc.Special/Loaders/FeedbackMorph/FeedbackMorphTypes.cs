using Avalonia.Collections;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Which way a <see cref="FeedbackMorph"/>'s panel grows from the button.</summary>
public enum FeedbackMorphSide
{
    /// <summary>The panel grows downward; the button's top edge stays where it is.</summary>
    Bottom,

    /// <summary>The panel grows upward; the button's bottom edge stays where it is (a button near the bottom of a page).</summary>
    Top
}

/// <summary>Which horizontal point of the button a <see cref="FeedbackMorph"/>'s panel and thank-you pill stay anchored to.</summary>
public enum FeedbackMorphAlign
{
    /// <summary>The left edge (in a left-to-right layout): the panel grows to the right.</summary>
    Start,

    /// <summary>The centre: the panel grows to both sides.</summary>
    Center,

    /// <summary>The right edge: the panel grows to the left.</summary>
    End
}

/// <summary>Where a <see cref="FeedbackMorph"/> is in its open, send and thank-you cycle.</summary>
public enum FeedbackMorphState
{
    /// <summary>The button.</summary>
    Closed,

    /// <summary>The panel with the faces, the note and the send button.</summary>
    Open,

    /// <summary>The panel while <see cref="FeedbackMorph.SubmitHandler"/> runs.</summary>
    Sending,

    /// <summary>The thank-you pill, until <see cref="FeedbackMorph.ResetAfter"/> folds it back into the button.</summary>
    Sent
}

/// <summary>The built-in face drawings of a <see cref="FeedbackMood"/>, from unhappy to delighted.</summary>
public enum FeedbackFace
{
    /// <summary>Lowered brows and a deep frown.</summary>
    Upset,

    /// <summary>A gentle frown.</summary>
    Sad,

    /// <summary>A straight mouth.</summary>
    Neutral,

    /// <summary>A soft smile.</summary>
    Happy,

    /// <summary>Smiling eyes and an open grin.</summary>
    Delighted
}

/// <summary>
/// One point of a <see cref="FeedbackMorph"/>'s mood scale: a face button with a name, and the question the note asks once
/// it is picked. Change the scale by replacing <see cref="FeedbackMorph.Moods"/>; edits to a mood on screen are not picked up.
/// </summary>
public sealed class FeedbackMood
{
    /// <summary>What <see cref="FeedbackMorphSubmission.Mood"/> carries when this face is picked, such as "happy".</summary>
    public string? Value { get; set; }

    /// <summary>The face's name: the caption under the faces and the button's accessible name.</summary>
    public string? Label { get; set; }

    /// <summary>Replaces the note's placeholder while this face is picked; null keeps <see cref="FeedbackMorph.Placeholder"/>.</summary>
    public string? Prompt { get; set; }

    /// <summary>The built-in drawing of the face.</summary>
    public FeedbackFace Face { get; set; } = FeedbackFace.Neutral;

    /// <summary>Your own 24 × 24 glyph (a geometry drawn as a stroke) instead of <see cref="Face"/>; null draws the face.</summary>
    public Avalonia.Media.Geometry? Glyph { get; set; }

    /// <summary>Anything else you need to keep with the mood; the control does not read it.</summary>
    public object? Tag { get; set; }

    /// <inheritdoc />
    public override string ToString() => Label ?? Value ?? string.Empty;

    /// <summary>The five moods a <see cref="FeedbackMorph"/> starts with, from upset to delighted.</summary>
    public static IReadOnlyList<FeedbackMood> CreateDefaults() =>
    [
        new() { Value = "upset", Label = "Upset", Face = FeedbackFace.Upset, Prompt = "What went wrong? Steps help us fix it" },
        new() { Value = "meh", Label = "Meh", Face = FeedbackFace.Sad, Prompt = "Where did it slow you down?" },
        new() { Value = "fine", Label = "Fine", Face = FeedbackFace.Neutral, Prompt = "What one change would help most?" },
        new() { Value = "happy", Label = "Happy", Face = FeedbackFace.Happy, Prompt = "What should we keep doing?" },
        new() { Value = "thrilled", Label = "Thrilled", Face = FeedbackFace.Delighted, Prompt = "Tell us what made your day" }
    ];
}

/// <summary>A list of <see cref="FeedbackMood"/>s, for setting <see cref="FeedbackMorph.Moods"/> in XAML.</summary>
public sealed class FeedbackMoodCollection : AvaloniaList<FeedbackMood>
{
    /// <summary>Creates an empty list.</summary>
    public FeedbackMoodCollection()
    {
    }

    /// <summary>Creates a list with <paramref name="moods"/>.</summary>
    public FeedbackMoodCollection(IEnumerable<FeedbackMood> moods) : base(moods)
    {
    }
}

/// <summary>What a <see cref="FeedbackMorph"/> sends.</summary>
public sealed class FeedbackMorphSubmission
{
    /// <summary>Creates a submission.</summary>
    public FeedbackMorphSubmission(FeedbackMood? mood, string message)
    {
        MoodItem = mood;
        Message = message;
    }

    /// <summary>The picked mood's <see cref="FeedbackMood.Value"/>, or null when no face was picked.</summary>
    public string? Mood => MoodItem?.Value;

    /// <summary>The picked mood, or null when no face was picked.</summary>
    public FeedbackMood? MoodItem { get; }

    /// <summary>The note with surrounding white space removed (may be empty when a face was picked).</summary>
    public string Message { get; }
}

/// <summary>Carries what a <see cref="FeedbackMorph"/> sent.</summary>
public sealed class FeedbackMorphSubmittedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="submission"/>.</summary>
    public FeedbackMorphSubmittedEventArgs(RoutedEvent routedEvent, FeedbackMorphSubmission submission) : base(routedEvent)
    {
        Submission = submission;
    }

    /// <summary>The mood and the note.</summary>
    public FeedbackMorphSubmission Submission { get; }
}

/// <summary>Carries a change of <see cref="FeedbackMorph.SelectedMood"/>.</summary>
public sealed class FeedbackMorphSelectionChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public FeedbackMorphSelectionChangedEventArgs(RoutedEvent routedEvent, FeedbackMood? oldItem, FeedbackMood? newItem)
        : base(routedEvent)
    {
        OldItem = oldItem;
        NewItem = newItem;
    }

    /// <summary>The mood picked before (null when none was).</summary>
    public FeedbackMood? OldItem { get; }

    /// <summary>The mood picked now (null when the pick was cleared).</summary>
    public FeedbackMood? NewItem { get; }
}

/// <summary>Carries a change of <see cref="FeedbackMorph.State"/>.</summary>
public sealed class FeedbackMorphStateChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public FeedbackMorphStateChangedEventArgs(RoutedEvent routedEvent, FeedbackMorphState oldState, FeedbackMorphState newState,
        Exception? error) : base(routedEvent)
    {
        OldState = oldState;
        NewState = newState;
        Error = error;
    }

    /// <summary>The state before.</summary>
    public FeedbackMorphState OldState { get; }

    /// <summary>The state now.</summary>
    public FeedbackMorphState NewState { get; }

    /// <summary>What <see cref="FeedbackMorph.SubmitHandler"/> threw when a send failed (the state is back to Open); null otherwise.</summary>
    public Exception? Error { get; }
}
