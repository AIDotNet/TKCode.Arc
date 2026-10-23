using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="ModelPicker.ModelChanged"/>.</summary>
public class ModelPickerModelChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The picker.</param>
    /// <param name="oldModel">The model selected before, or null.</param>
    /// <param name="newModel">The model selected now, or null.</param>
    public ModelPickerModelChangedEventArgs(RoutedEvent routedEvent, object source, AiModel? oldModel, AiModel? newModel)
        : base(routedEvent, source)
    {
        OldModel = oldModel;
        NewModel = newModel;
    }

    /// <summary>The model selected before, or null.</summary>
    public AiModel? OldModel { get; }

    /// <summary>The model selected now, or null.</summary>
    public AiModel? NewModel { get; }
}

/// <summary>Arguments of <see cref="ModelPicker.EffortChanged"/>.</summary>
public class ModelPickerEffortChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The picker.</param>
    /// <param name="oldEffort">The effort before, or null.</param>
    /// <param name="newEffort">The effort now, or null when the model has none.</param>
    /// <param name="isSnap">True when a change of model moved the effort to a level the new model offers.</param>
    public ModelPickerEffortChangedEventArgs(RoutedEvent routedEvent, object source, ModelEffort? oldEffort, ModelEffort? newEffort, bool isSnap)
        : base(routedEvent, source)
    {
        OldEffort = oldEffort;
        NewEffort = newEffort;
        IsSnap = isSnap;
    }

    /// <summary>The effort before, or null.</summary>
    public ModelEffort? OldEffort { get; }

    /// <summary>The effort now, or null when the selected model has no effort setting.</summary>
    public ModelEffort? NewEffort { get; }

    /// <summary>True when the effort changed because another model was selected and it does not offer the old level.</summary>
    public bool IsSnap { get; }
}
