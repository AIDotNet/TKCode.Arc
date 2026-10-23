using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// What an <see cref="AiToolChip"/> shows for one state: the 14px icon cell (a turning LoaderCircle while running, the tool's
/// icon once done, a Danger CircleAlert on error), 8px, the name (shimmering while running, trimmed when space runs out),
/// the source marks (13px, 3px apart) and the muted summary with tabular figures, 6px apart. A chip makes a new face for
/// every state so the old one can fade out while the new one comes in; within a state the summary, marks and icon change in
/// place.
/// </summary>
[TemplatePart(IconPart, typeof(ContentPresenter))]
[TemplatePart(MarksPart, typeof(StackPanel))]
[PseudoClasses(RunningPseudoClass, DonePseudoClass, ErrorPseudoClass)]
internal sealed class AiToolChipFace : TemplatedControl
{
    public const string IconPart = "PART_Icon";
    public const string MarksPart = "PART_Marks";

    public const string RunningPseudoClass = ":running";
    public const string DonePseudoClass = ":done";
    public const string ErrorPseudoClass = ":error";

    /// <summary>The state the face shows; fixed for the face's life in a chip.</summary>
    public static readonly StyledProperty<AiChatToolStatus> StatusProperty =
        AvaloniaProperty.Register<AiToolChipFace, AiChatToolStatus>(nameof(Status));

    /// <summary>The tool's name for this state.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AiToolChipFace, string?>(nameof(Label));

    /// <summary>The short result after the name; empty hides it.</summary>
    public static readonly StyledProperty<string?> SummaryProperty =
        AvaloniaProperty.Register<AiToolChipFace, string?>(nameof(Summary));

    /// <summary>The icon of a finished tool: a control (an <see cref="ArcGlyph"/>, an <see cref="Image"/> or the owner's mark).</summary>
    public static readonly StyledProperty<Control?> IconProperty =
        AvaloniaProperty.Register<AiToolChipFace, Control?>(nameof(Icon));

    /// <inheritdoc cref="StatusProperty"/>
    public AiChatToolStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="SummaryProperty"/>
    public string? Summary { get => GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Control? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    private StackPanel? _marks;
    private IReadOnlyList<AiSource> _markSources = Array.Empty<AiSource>();

    public AiToolChipFace()
    {
        UpdateStatus(Status);
    }

    /// <summary>The sources whose marks show, in order.</summary>
    internal IReadOnlyList<AiSource> MarkSources => _markSources;

    /// <summary>Shows the marks of <paramref name="sources"/>; an equal list changes nothing.</summary>
    internal void SetMarks(IReadOnlyList<AiSource> sources)
    {
        if (_markSources.SequenceEqual(sources))
        {
            return;
        }
        _markSources = sources.ToArray();
        FillMarks();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _marks = e.NameScope.Find<StackPanel>(MarksPart);
        FillMarks();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty)
        {
            UpdateStatus(change.GetNewValue<AiChatToolStatus>());
        }
    }

    private void UpdateStatus(AiChatToolStatus status)
    {
        PseudoClasses.Set(RunningPseudoClass, status == AiChatToolStatus.Running);
        PseudoClasses.Set(DonePseudoClass, status == AiChatToolStatus.Done);
        PseudoClasses.Set(ErrorPseudoClass, status == AiChatToolStatus.Error);
    }

    private void FillMarks()
    {
        if (_marks is null)
        {
            return;
        }
        _marks.Children.Clear();
        foreach (var source in _markSources)
        {
            _marks.Children.Add(new AiToolSourceMark { Source = source, Size = 13 });
        }
        _marks.IsVisible = _markSources.Count > 0;
    }
}
