using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One step of an <see cref="AiWorkflow"/> on the rail. Its head is one 32px row (44px under 480px, with the detail
/// stacked under the title): the app's mark on the rail, the title with its detail inline, the status in a fixed 72px
/// slot (64px narrow) so titles never shift, and the remove button. Under it, aligned with the title: a branch's path
/// tabs, the output preview and the error with Retry, each opening on its measured height. While the step runs, its
/// detail crossfades to a shimmering activity line.
/// </summary>
internal sealed class AiWorkflowStepRow : Decorator
{
    private readonly Grid _head = new();
    private readonly Grid _text = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,Auto"), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _title;
    private readonly ToolCallText _detail = new() { Swap = ToolCallTextSwap.Fade, FontSize = AiWorkflowMetrics.TitleFontSize, LineHeight = AiWorkflowMetrics.TitleLineHeight };
    private readonly ArcContentSwap _status = new()
    {
        Mode = ContentSwapMode.Crossfade,
        HorizontalContentAlignment = HorizontalAlignment.Right,
        VerticalContentAlignment = VerticalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        ClipToBounds = false
    };
    private readonly AiWorkflowIconButton _remove = new();
    private Border _mark;
    private readonly AiHeightBox _output = Collapse();
    private readonly AiHeightBox _error = Collapse();
    private readonly TextBlock _errorText;
    private readonly Button _retry = new() { Variant = ButtonVariant.Secondary, Size = ControlSize.Small, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly StackPanel _body = new() { Margin = new Thickness(AiWorkflowMetrics.BodyIndent, 0, 0, 0) };
    private readonly List<AiWorkflowStreamText> _streams = [];
    private AiWorkflowOutput? _shownOutput;
    private AiWorkflowStepStatus _shownStatus = (AiWorkflowStepStatus)(-1);
    private TimeSpan? _shownDuration;
    private bool _narrow;
    private bool _hovered;
    private bool _touch;

    public AiWorkflowStepRow(AiWorkflowStep step)
    {
        Step = step;
        _mark = AiWorkflowViews.Mark(step.App, step.Kind);
        _title = AiWorkflowViews.Text(step.Title, AiWorkflowMetrics.TitleFontSize, ArcDesignTokenKind.Foreground, medium: true);
        _title.LineHeight = AiWorkflowMetrics.TitleLineHeight;
        ArcTokens.Bind(_detail, ToolCallText.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_detail, ToolCallText.MutedForegroundProperty, ArcDesignTokenKind.TextMuted);
        _detail.Margin = new Thickness(6, 0, 0, 0);
        _text.Children.Add(_title);
        _text.Children.Add(_detail);

        _remove.Content = AiWorkflowViews.Glyph(ArcIcons.X, 14, ArcDesignTokenKind.TextMuted);
        _remove.Click += (_, _) => RemoveRequested?.Invoke(this, EventArgs.Empty);
        _remove.GotFocus += (_, _) => UpdateReveal();
        _remove.LostFocus += (_, _) => UpdateReveal();

        _head.ColumnDefinitions = new ColumnDefinitions($"{AiWorkflowMetrics.MarkSize},*,{AiWorkflowMetrics.StatusWidth},{AiWorkflowMetrics.RemoveSize}");
        _head.Height = AiWorkflowMetrics.RowHeight;
        _mark.Margin = default;
        _head.Children.Add(_mark);
        _text.Margin = new Thickness(AiWorkflowMetrics.MarkGap, 0, 8, 0);
        Grid.SetColumn(_text, 1);
        _head.Children.Add(_text);
        Grid.SetColumn(_status, 2);
        _head.Children.Add(_status);
        _remove.Margin = new Thickness(4, 0, 0, 0);
        _remove.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(_remove, 3);
        _head.Children.Add(_remove);

        _errorText = AiWorkflowViews.Text(null, AiWorkflowMetrics.OutputFontSize, ArcDesignTokenKind.Danger, wrap: true,
            lineHeight: AiWorkflowMetrics.OutputLineHeight);
        _retry.Height = 26;
        _retry.MinHeight = 26;
        _retry.Padding = new Thickness(10, 0);
        _retry.Click += (_, _) => RetryRequested?.Invoke(this, EventArgs.Empty);
        _error.Content = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 0, 8), Children = { _errorText, _retry } };

        // Transparent fills so the whole row reports hover (the remove button shows while the row is hovered).
        _head.Background = Brushes.Transparent;
        var stack = new StackPanel { Background = Brushes.Transparent };
        stack.Children.Add(_head);
        _body.Children.Add(_output);
        _body.Children.Add(_error);
        stack.Children.Add(_body);
        Child = stack;
        Shell = Collapse();
        Shell.Content = this;
        Update(step);
        ApplyNarrow();
    }

    /// <summary>The step shown (replaced in place when the list is replaced).</summary>
    public AiWorkflowStep Step { get; private set; }

    /// <summary>The box the row opens and closes in when it is added or removed.</summary>
    public AiHeightBox Shell { get; }

    /// <summary>True while the row is closing on its way out.</summary>
    public bool IsLeaving { get; set; }

    /// <summary>The branch's tabs, placed before the output (null for other steps).</summary>
    public Control? Tabs { get; private set; }

    public AiWorkflowIconButton RemoveButton => _remove;

    public Button RetryButton => _retry;

    public AiHeightBox OutputBox => _output;

    public AiHeightBox ErrorBox => _error;

    public ToolCallText DetailText => _detail;

    public ArcContentSwap StatusSlot => _status;

    public AiWorkflowStepStatus ShownStatus => _shownStatus;

    public IReadOnlyList<AiWorkflowStreamText> Streams => _streams;

    /// <summary>The vertical center of the mark in the row (the rail runs through it).</summary>
    public double MarkCenterY => (_narrow ? AiWorkflowMetrics.NarrowRowHeight : AiWorkflowMetrics.RowHeight) / 2;

    public event EventHandler? RemoveRequested;

    public event EventHandler? RetryRequested;

    public void SetTabs(Control? tabs)
    {
        if (ReferenceEquals(Tabs, tabs))
        {
            return;
        }
        if (Tabs is not null)
        {
            _body.Children.Remove(Tabs);
        }
        Tabs = tabs;
        if (tabs is not null)
        {
            _body.Children.Add(tabs);
        }
    }

    /// <summary>Shows a new copy of the step (the list was replaced): title, detail and mark.</summary>
    public void Update(AiWorkflowStep step)
    {
        var before = Step;
        Step = step;
        if (_title.Text != step.Title)
        {
            _title.Text = step.Title;
        }
        if (!ReferenceEquals(before, step) && (!ReferenceEquals(before.App, step.App) || before.Kind != step.Kind))
        {
            var mark = AiWorkflowViews.Mark(step.App, step.Kind);
            mark.Opacity = _mark.Opacity;
            _head.Children[0] = mark;
            _mark = mark;
        }
    }

    public void SetNarrow(bool narrow)
    {
        if (_narrow != narrow)
        {
            _narrow = narrow;
            ApplyNarrow();
        }
    }

    private void ApplyNarrow()
    {
        _head.Height = _narrow ? AiWorkflowMetrics.NarrowRowHeight : AiWorkflowMetrics.RowHeight;
        _head.ColumnDefinitions[2].Width = new GridLength(_narrow ? AiWorkflowMetrics.NarrowStatusWidth : AiWorkflowMetrics.StatusWidth);
        // Above 480px the detail follows the title on its line; below it, it stacks under the title.
        Grid.SetRow(_detail, _narrow ? 1 : 0);
        Grid.SetColumn(_detail, _narrow ? 0 : 1);
        Grid.SetColumnSpan(_detail, _narrow ? 2 : 1);
        _detail.Margin = _narrow ? default : new Thickness(6, 0, 0, 0);
        _detail.HorizontalAlignment = HorizontalAlignment.Left;
    }

    /// <summary>Touch screens always show the remove button; a fine pointer shows it on hover or focus.</summary>
    public void SetTouch(bool touch)
    {
        _touch = touch;
        UpdateReveal();
    }

    /// <summary>Brings the row in line with the step's run state.</summary>
    public void Apply(AiWorkflowStepState? state, AiWorkflowLabels labels, bool editable, bool running, bool animate)
    {
        var step = Step;
        var status = state?.Status ?? AiWorkflowStepStatus.Idle;

        // Detail line: crossfades to the shimmering activity while the step runs.
        var activity = status == AiWorkflowStepStatus.Running && !string.IsNullOrEmpty(step.Activity);
        var detail = activity ? step.Activity : step.Detail ?? string.Empty;
        if (_detail.Text != detail)
        {
            _detail.Text = detail;
        }
        _detail.IsShimmering = status == AiWorkflowStepStatus.Running;
        _detail.IsVisible = !string.IsNullOrEmpty(detail);

        // Status slot: icon and label crossfade in a fixed slot.
        if (_shownStatus != status || _shownDuration != state?.Duration)
        {
            _shownStatus = status;
            _shownDuration = state?.Duration;
            _status.Content = AiWorkflowViews.Status(status, state?.Duration, labels);
        }

        _title.Opacity = status == AiWorkflowStepStatus.Skipped ? 0.6 : 1;
        _mark.Opacity = status == AiWorkflowStepStatus.Skipped ? 0.6 : 1;

        // Output preview.
        var output = status == AiWorkflowStepStatus.Done ? state?.Output : null;
        if (!ReferenceEquals(output, _shownOutput))
        {
            var fresh = output is not null;
            if (output is not null)
            {
                _streams.Clear();
                _output.Content = AiWorkflowViews.Output(output, step, labels, _streams);
            }
            _shownOutput = output;
            Toggle(_output, output is not null, animate);
            if (fresh && animate)
            {
                foreach (var stream in _streams)
                {
                    stream.Play();
                }
            }
        }

        // Error and Retry.
        var failed = status == AiWorkflowStepStatus.Failed;
        _errorText.Text = state?.Error;
        _retry.Content = labels.Retry;
        AutomationProperties.SetName(_retry, labels.Retry + " " + step.Title);
        Toggle(_error, failed, animate);

        // Remove: never a trigger; not while a run is in progress.
        _remove.IsVisible = editable && step.Kind != AiWorkflowStepKind.Trigger;
        _remove.IsEnabled = !running;
        AutomationProperties.SetName(_remove, labels.Remove(step.Title));

        var spoken = status switch
        {
            AiWorkflowStepStatus.Running => labels.Running,
            AiWorkflowStepStatus.Done => labels.DoneSpoken(state?.Duration),
            AiWorkflowStepStatus.Failed => labels.Failed + ": " + state?.Error,
            AiWorkflowStepStatus.Skipped => labels.Skipped,
            _ => labels.NotRun
        };
        AutomationProperties.SetName(this, labels.StepName(step.Title, step.App?.Name, spoken));
    }

    /// <summary>
    /// A measured-height box of the card (an output preview, an error, a step that is added or removed): the 0.22s spring,
    /// the content fades in over 160ms and out over 120ms, and a closed box keeps its content, hidden.
    /// </summary>
    internal static AiHeightBox Collapse() => new()
    {
        KeepsContent = true,
        HeightSpring = AiWorkflowMetrics.Spring,
        FadeOutDuration = ArcMotionPresets.Instant,
        CrossClipOverflow = 8,
    };

    private static void Toggle(AiHeightBox box, bool open, bool animate)
    {
        if (box.IsExpanded == open)
        {
            return;
        }
        if (animate)
        {
            box.IsExpanded = open;
        }
        else if (open)
        {
            box.OpenInstantly();
        }
        else
        {
            box.CloseInstantly();
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _hovered = true;
            UpdateReveal();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = false;
        UpdateReveal();
    }

    private void UpdateReveal() => _remove.Classes.Set("revealed", _touch || _hovered || _remove.IsKeyboardFocusWithin);
}
