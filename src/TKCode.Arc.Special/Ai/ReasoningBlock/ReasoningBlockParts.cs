using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The head button of a <see cref="ReasoningBlock"/>: the whole head row, transparent at rest, SurfaceMuted under the
/// pointer, and a keyboard focus ring drawn inside its edge. Screen readers get its expanded state (Arc's
/// <c>aria-expanded</c>).
/// </summary>
internal sealed class ReasoningBlockToggle : ToolCallDisclosureButton
{
    protected override Type StyleKeyOverride => typeof(ReasoningBlockToggle);
}

/// <summary>One step on screen: a dot on the rail, its heading over its text.</summary>
internal sealed class ReasoningStepView : TemplatedControl
{
    internal const string FirstPseudoClass = ":first";
    internal const string LastPseudoClass = ":last";

    /// <summary>The step's heading.</summary>
    public static readonly StyledProperty<string?> HeadingProperty =
        AvaloniaProperty.Register<ReasoningStepView, string?>(nameof(Heading));

    /// <summary>The step's text.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ReasoningStepView, string?>(nameof(Text));

    /// <summary>Whether the step has a heading to show.</summary>
    public static readonly DirectProperty<ReasoningStepView, bool> HasHeadingProperty =
        AvaloniaProperty.RegisterDirect<ReasoningStepView, bool>(nameof(HasHeading), o => o.HasHeading);

    private bool _hasHeading;

    /// <inheritdoc cref="HeadingProperty"/>
    public string? Heading { get => GetValue(HeadingProperty); set => SetValue(HeadingProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="HasHeadingProperty"/>
    public bool HasHeading => _hasHeading;

    /// <summary>Sets where the step sits in the rail: the first starts its line at the dot, the last ends it there.</summary>
    internal void SetPosition(bool first, bool last)
    {
        PseudoClasses.Set(FirstPseudoClass, first);
        PseudoClasses.Set(LastPseudoClass, last);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeadingProperty)
        {
            SetAndRaise(HasHeadingProperty, ref _hasHeading, !string.IsNullOrEmpty(Heading));
        }
    }
}
