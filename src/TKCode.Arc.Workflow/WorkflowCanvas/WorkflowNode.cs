using System.Collections.Specialized;
using System.Threading;
using Avalonia;
using Avalonia.Collections;

namespace TKCode.Arc;

/// <summary>
/// One step of a <see cref="WorkflowGraph"/>: what it is (title, description, icon), where it sits on the canvas, and
/// while a run goes through the workflow its state, result and time. Every property is observed, so changing a step
/// updates the canvas.
/// </summary>
public sealed class WorkflowNode : AvaloniaObject
{
    private static long s_nextId;

    /// <summary>What the step does, on one line (it is cut with an ellipsis when it does not fit).</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<WorkflowNode, string>(nameof(Title), string.Empty);

    /// <summary>
    /// The line under the title while nothing has run yet: what the step works on ("Plan, seats and owner").
    /// </summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<WorkflowNode, string?>(nameof(Description));

    /// <summary>The line under the title once the step is done: what it found ("Enterprise, 212 seats"); null keeps the description.</summary>
    public static readonly StyledProperty<string?> ResultProperty =
        AvaloniaProperty.Register<WorkflowNode, string?>(nameof(Result));

    /// <summary>Your own category of the step (trigger, action, condition); screen readers read it after the title.</summary>
    public static readonly StyledProperty<string?> KindProperty =
        AvaloniaProperty.Register<WorkflowNode, string?>(nameof(Kind));

    /// <summary>
    /// The 20 × 20 mark at the start of the card: a Lucide geometry from <see cref="ArcIcons"/> (drawn as a stroke), an
    /// image, or any control.
    /// </summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<WorkflowNode, object?>(nameof(Icon));

    /// <summary>
    /// The card's top-left corner on the canvas. NaN (the default) lets the canvas place the step by arranging the
    /// whole workflow, and writes the place it chose back here.
    /// </summary>
    public static readonly StyledProperty<Point> PositionProperty =
        AvaloniaProperty.Register<WorkflowNode, Point>(nameof(Position), new Point(double.NaN, double.NaN));

    /// <summary>The card's size; null uses the size of its <see cref="Appearance"/> (300 × 60 or 200 × 84).</summary>
    public static readonly StyledProperty<Size?> SizeProperty =
        AvaloniaProperty.Register<WorkflowNode, Size?>(nameof(Size));

    /// <summary>One row (<see cref="WorkflowNodeAppearance.Standard"/>) or two (<see cref="WorkflowNodeAppearance.Compact"/>).</summary>
    public static readonly StyledProperty<WorkflowNodeAppearance> AppearanceProperty =
        AvaloniaProperty.Register<WorkflowNode, WorkflowNodeAppearance>(nameof(Appearance));

    /// <summary>Where the step stands in the current run (<see cref="WorkflowNodeState.Idle"/> when there is no run).</summary>
    public static readonly StyledProperty<WorkflowNodeState> StateProperty =
        AvaloniaProperty.Register<WorkflowNode, WorkflowNodeState>(nameof(State));

    /// <summary>How long the step took; shown next to the check once it is done ("118 ms").</summary>
    public static readonly StyledProperty<TimeSpan?> DurationProperty =
        AvaloniaProperty.Register<WorkflowNode, TimeSpan?>(nameof(Duration));

    /// <summary>Why the step failed; shown under the title in the failed state and read by screen readers.</summary>
    public static readonly StyledProperty<string?> ErrorProperty =
        AvaloniaProperty.Register<WorkflowNode, string?>(nameof(Error));

    /// <summary>
    /// A locked step stays where it is and cannot be deleted on an editable canvas; it can still be selected and opened.
    /// </summary>
    public static readonly StyledProperty<bool> IsLockedProperty =
        AvaloniaProperty.Register<WorkflowNode, bool>(nameof(IsLocked));

    /// <summary>
    /// Whether the step has an implicit input when <see cref="Inputs"/> is empty (default true). Turn it off for a step
    /// nothing leads into, such as a trigger.
    /// </summary>
    public static readonly StyledProperty<bool> HasDefaultInputProperty =
        AvaloniaProperty.Register<WorkflowNode, bool>(nameof(HasDefaultInput), true);

    /// <summary>Creates a step with a generated id and an empty title.</summary>
    public WorkflowNode()
        : this(null, string.Empty)
    {
    }

    /// <summary>Creates a step.</summary>
    /// <param name="id">A stable id (for saving and finding the step); null generates one.</param>
    /// <param name="title">What the step does, short ("Find account", "Amount over $1,000").</param>
    public WorkflowNode(string? id, string title)
    {
        Id = string.IsNullOrEmpty(id) ? "node-" + Interlocked.Increment(ref s_nextId) : id;
        Title = title;
        Inputs.CollectionChanged += (_, e) => Check(e, WorkflowPortDirection.Input);
        Outputs.CollectionChanged += (_, e) => Check(e, WorkflowPortDirection.Output);
    }

    /// <summary>
    /// The step's named inputs (a condition's single input needs none: an empty list is one implicit input, see
    /// <see cref="HasDefaultInput"/>).
    /// </summary>
    public AvaloniaList<WorkflowPort> Inputs { get; } = new();

    /// <summary>The step's named outputs, such as "Yes" and "No"; an empty list is one implicit output.</summary>
    public AvaloniaList<WorkflowPort> Outputs { get; } = new();

    /// <inheritdoc cref="HasDefaultInputProperty"/>
    public bool HasDefaultInput
    {
        get => GetValue(HasDefaultInputProperty);
        set => SetValue(HasDefaultInputProperty, value);
    }

    /// <summary>Whether the step takes data in: a named input, or the implicit one.</summary>
    internal bool AcceptsInput => Inputs.Count > 0 || HasDefaultInput;

    private static void Check(NotifyCollectionChangedEventArgs e, WorkflowPortDirection direction)
    {
        if (e.NewItems is null)
        {
            return;
        }
        foreach (WorkflowPort port in e.NewItems)
        {
            if (port.Direction != direction)
            {
                throw new ArgumentException($"The port {port} is an {port.Direction.ToString().ToLowerInvariant()} port.");
            }
        }
    }

    /// <summary>The step's stable id.</summary>
    public string Id { get; }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <inheritdoc cref="ResultProperty"/>
    public string? Result
    {
        get => GetValue(ResultProperty);
        set => SetValue(ResultProperty, value);
    }

    /// <inheritdoc cref="KindProperty"/>
    public string? Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <inheritdoc cref="IconProperty"/>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="PositionProperty"/>
    public Point Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    /// <inheritdoc cref="SizeProperty"/>
    public Size? Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <inheritdoc cref="AppearanceProperty"/>
    public WorkflowNodeAppearance Appearance
    {
        get => GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    /// <inheritdoc cref="StateProperty"/>
    public WorkflowNodeState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan? Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <inheritdoc cref="ErrorProperty"/>
    public string? Error
    {
        get => GetValue(ErrorProperty);
        set => SetValue(ErrorProperty, value);
    }

    /// <inheritdoc cref="IsLockedProperty"/>
    public bool IsLocked
    {
        get => GetValue(IsLockedProperty);
        set => SetValue(IsLockedProperty, value);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Id}: {Title}";
}
