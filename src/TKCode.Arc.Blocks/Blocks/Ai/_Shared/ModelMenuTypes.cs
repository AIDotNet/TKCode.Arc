using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where the keyboard position starts when a model menu opens.</summary>
public enum ModelMenuOpenFocus
{
    /// <summary>The menu takes focus without a highlighted row (a pointer opened it).</summary>
    Surface,

    /// <summary>The row of the selected model is highlighted; the first row when the selected model is in "More models".</summary>
    Selected,

    /// <summary>The first row is highlighted.</summary>
    First,

    /// <summary>The last row is highlighted.</summary>
    Last
}

/// <summary>Arguments of <c>ModelPill.OpenRequested</c>: the pill asks its owner to open (or, when open, close) the menu.</summary>
public class ModelPillOpenRequestedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The pill.</param>
    /// <param name="focus">Where the keyboard position starts.</param>
    public ModelPillOpenRequestedEventArgs(RoutedEvent routedEvent, object source, ModelMenuOpenFocus focus)
        : base(routedEvent, source)
    {
        Focus = focus;
    }

    /// <summary>Where the keyboard position starts: <see cref="ModelMenuOpenFocus.Surface"/> for a pointer, <see cref="ModelMenuOpenFocus.Selected"/> for a key.</summary>
    public ModelMenuOpenFocus Focus { get; }
}

/// <summary>Arguments of <c>ModelMenu.ModelChosen</c>: a model was picked (also when it is the one already selected).</summary>
public class ModelMenuModelEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The menu.</param>
    /// <param name="model">The picked model.</param>
    /// <param name="previousModel">The model selected before, or null.</param>
    public ModelMenuModelEventArgs(RoutedEvent routedEvent, object source, AiModel model, AiModel? previousModel)
        : base(routedEvent, source)
    {
        Model = model;
        PreviousModel = previousModel;
    }

    /// <summary>The picked model.</summary>
    public AiModel Model { get; }

    /// <summary>The model selected before the pick, or null.</summary>
    public AiModel? PreviousModel { get; }

    /// <summary>True when <see cref="Model"/> differs from <see cref="PreviousModel"/>.</summary>
    public bool IsChange => !ReferenceEquals(Model, PreviousModel);
}

/// <summary>Arguments of <c>ModelMenu.EffortChosen</c>: the effort changed, by a pick in the menu or by snapping to a level the new model offers.</summary>
public class ModelMenuEffortEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The menu.</param>
    /// <param name="effort">The new effort, or null when the model has none.</param>
    /// <param name="previousEffort">The effort before, or null.</param>
    /// <param name="isSnap">True when a change of model moved the effort to a level the model offers.</param>
    public ModelMenuEffortEventArgs(RoutedEvent routedEvent, object source, ModelEffort? effort, ModelEffort? previousEffort, bool isSnap)
        : base(routedEvent, source)
    {
        Effort = effort;
        PreviousEffort = previousEffort;
        IsSnap = isSnap;
    }

    /// <summary>The new effort, or null when the selected model has no effort setting.</summary>
    public ModelEffort? Effort { get; }

    /// <summary>The effort before the change, or null.</summary>
    public ModelEffort? PreviousEffort { get; }

    /// <summary>True when the effort changed because a different model was selected and it does not offer the old level.</summary>
    public bool IsSnap { get; }
}
