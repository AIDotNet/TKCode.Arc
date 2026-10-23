using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>How <see cref="AiRelatedQuestions"/> draws its rows.</summary>
internal enum AiRelatedQuestionsAppearance
{
    /// <summary>A hairline above the first row and under every row (the related questions, the empty page's suggestions).</summary>
    List,

    /// <summary>The same rows without hairlines.</summary>
    Plain
}

/// <summary>Arguments of <see cref="AiRelatedQuestions.QuestionInvokedEvent"/>: the question the person picked.</summary>
internal sealed class AiRelatedQuestionEventArgs : RoutedEventArgs
{
    public AiRelatedQuestionEventArgs(RoutedEvent routedEvent, object source, string question, int index) : base(routedEvent, source)
    {
        Question = question;
        Index = index;
    }

    /// <summary>The question, as shown.</summary>
    public string Question { get; }

    /// <summary>Its position in the list.</summary>
    public int Index { get; }
}

/// <summary>
/// Questions to ask next under an AI answer (Arc answer "Related"), or the suggestions of the empty page: an optional
/// <see cref="Heading"/> (display font 14 / 15.4, medium, tracked −0.03em, 6px above the list), then one row per question
/// between hairlines (<see cref="Appearance"/>). A row is a 48px button (padding 12 2, radius 10): the question in 16 / 22.4
/// secondary text, wrapping, and an ArrowRight 16 in the muted color 16px after it at the right edge. Hover turns both to
/// the text color and slides the arrow 3px right on Arc's CSS spring (no fill). Rows are tab stops; Enter, Space or a click
/// raises <see cref="QuestionInvoked"/>. The keyboard ring is 2px inside the row. Reduced motion keeps the arrow in place
/// and changes colors in 120ms; Off at once.
/// </summary>
[TemplatePart(ListPart, typeof(StackPanel))]
[PseudoClasses(PlainPseudoClass, HeadingPseudoClass)]
internal sealed class AiRelatedQuestions : TemplatedControl
{
    public const string ListPart = "PART_List";

    /// <summary>Set for <see cref="AiRelatedQuestionsAppearance.Plain"/>.</summary>
    public const string PlainPseudoClass = ":plain";

    /// <summary>Set while a heading shows.</summary>
    public const string HeadingPseudoClass = ":heading";

    /// <summary>The questions; collection changes are followed.</summary>
    public static readonly StyledProperty<IList<string>?> QuestionsProperty =
        AvaloniaProperty.Register<AiRelatedQuestions, IList<string>?>(nameof(Questions));

    /// <summary>The heading ("Related"); null or empty shows none.</summary>
    public static readonly StyledProperty<string?> HeadingProperty =
        AvaloniaProperty.Register<AiRelatedQuestions, string?>(nameof(Heading));

    /// <summary>The accessible name of the list when there is no heading (the empty page's suggestions).</summary>
    public static readonly StyledProperty<string?> ListNameProperty =
        AvaloniaProperty.Register<AiRelatedQuestions, string?>(nameof(ListName));

    /// <summary>Hairlines between the rows (<see cref="AiRelatedQuestionsAppearance.List"/>, default) or none.</summary>
    public static readonly StyledProperty<AiRelatedQuestionsAppearance> AppearanceProperty =
        AvaloniaProperty.Register<AiRelatedQuestions, AiRelatedQuestionsAppearance>(nameof(Appearance));

    /// <summary>Raised (bubbling) when the person picks a question.</summary>
    public static readonly RoutedEvent<AiRelatedQuestionEventArgs> QuestionInvokedEvent =
        RoutedEvent.Register<AiRelatedQuestions, AiRelatedQuestionEventArgs>(nameof(QuestionInvoked), RoutingStrategies.Bubble);

    private readonly List<AiRelatedQuestionRow> _rows = new();
    private StackPanel? _list;
    private INotifyCollectionChanged? _observed;

    static AiRelatedQuestions()
    {
        FocusableProperty.OverrideDefaultValue<AiRelatedQuestions>(false);
    }

    /// <inheritdoc cref="QuestionsProperty"/>
    public IList<string>? Questions { get => GetValue(QuestionsProperty); set => SetValue(QuestionsProperty, value); }

    /// <inheritdoc cref="HeadingProperty"/>
    public string? Heading { get => GetValue(HeadingProperty); set => SetValue(HeadingProperty, value); }

    /// <inheritdoc cref="ListNameProperty"/>
    public string? ListName { get => GetValue(ListNameProperty); set => SetValue(ListNameProperty, value); }

    /// <inheritdoc cref="AppearanceProperty"/>
    public AiRelatedQuestionsAppearance Appearance { get => GetValue(AppearanceProperty); set => SetValue(AppearanceProperty, value); }

    /// <inheritdoc cref="QuestionInvokedEvent"/>
    public event EventHandler<AiRelatedQuestionEventArgs>? QuestionInvoked
    {
        add => AddHandler(QuestionInvokedEvent, value);
        remove => RemoveHandler(QuestionInvokedEvent, value);
    }

    /// <summary>The rows, in order.</summary>
    internal IReadOnlyList<AiRelatedQuestionRow> Rows => _rows;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _list?.Children.Clear();
        _list = e.NameScope.Find<StackPanel>(ListPart);
        _rows.Clear();
        Rebuild();
        UpdateNames();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == QuestionsProperty)
        {
            Observe(TopLevel.GetTopLevel(this) is not null ? change.GetNewValue<IList<string>?>() : null);
            Rebuild();
        }
        else if (change.Property == HeadingProperty || change.Property == ListNameProperty)
        {
            UpdateNames();
        }
        else if (change.Property == AppearanceProperty)
        {
            PseudoClasses.Set(PlainPseudoClass, change.GetNewValue<AiRelatedQuestionsAppearance>() == AiRelatedQuestionsAppearance.Plain);
            foreach (var row in _rows)
            {
                row.ShowsLine = Appearance == AiRelatedQuestionsAppearance.List;
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(Questions);
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // The questions are app data that can outlive the list: they are followed only while it is shown.
        Observe(null);
    }

    private void Observe(IList<string>? questions)
    {
        var next = questions as INotifyCollectionChanged;
        if (ReferenceEquals(next, _observed))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnQuestionsChanged;
        }
        _observed = next;
        if (next is not null)
        {
            next.CollectionChanged += OnQuestionsChanged;
        }
    }

    private void OnQuestionsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void UpdateNames()
    {
        var hasHeading = !string.IsNullOrEmpty(Heading);
        PseudoClasses.Set(HeadingPseudoClass, hasHeading);
        var name = hasHeading ? Heading : ListName;
        AutomationProperties.SetName(this, name ?? string.Empty);
        if (_list is not null)
        {
            AutomationProperties.SetName(_list, name ?? string.Empty);
        }
    }

    /// <summary>One row per question; rows are reused by position, so a row with focus keeps it when the words change.</summary>
    private void Rebuild()
    {
        if (_list is null)
        {
            return;
        }
        var questions = new List<string>();
        if (Questions is { } source)
        {
            foreach (var question in source)
            {
                if (!string.IsNullOrWhiteSpace(question))
                {
                    questions.Add(question);
                }
            }
        }
        while (_rows.Count > questions.Count)
        {
            var last = _rows[^1];
            last.Click -= OnRowClick;
            _rows.RemoveAt(_rows.Count - 1);
            _list.Children.Remove(last);
        }
        for (var i = 0; i < questions.Count; i++)
        {
            if (i < _rows.Count)
            {
                _rows[i].Question = questions[i];
                _rows[i].Index = i;
                continue;
            }
            var row = new AiRelatedQuestionRow
            {
                Question = questions[i],
                Index = i,
                ShowsLine = Appearance == AiRelatedQuestionsAppearance.List
            };
            row.Click += OnRowClick;
            _rows.Add(row);
            _list.Children.Add(row);
        }
    }

    private void OnRowClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is AiRelatedQuestionRow { Question: { } question } row)
        {
            RaiseEvent(new AiRelatedQuestionEventArgs(QuestionInvokedEvent, this, question, row.Index));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SectionPeer(this);

    /// <summary>A named group (Arc: a section labelled by its heading, or a list with an <c>aria-label</c>).</summary>
    private sealed class SectionPeer(AiRelatedQuestions owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// A row of <see cref="AiRelatedQuestions"/>: a button of at least 48px (padding 12 2, radius 10) with the question and an
/// arrow at its right edge, and a hairline under it (<see cref="ShowsLine"/>). Its name is the question; the arrow is not read.
/// </summary>
[PseudoClasses(HoverPseudoClass, LinePseudoClass)]
internal sealed class AiRelatedQuestionRow : AvaloniaButton
{
    /// <summary>Mouse or pen over the row.</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>Set while the row draws its hairline.</summary>
    public const string LinePseudoClass = ":line";

    /// <summary>The question.</summary>
    public static readonly StyledProperty<string?> QuestionProperty =
        AvaloniaProperty.Register<AiRelatedQuestionRow, string?>(nameof(Question));

    /// <summary>Whether a hairline runs under the row.</summary>
    public static readonly StyledProperty<bool> ShowsLineProperty =
        AvaloniaProperty.Register<AiRelatedQuestionRow, bool>(nameof(ShowsLine), true);

    public AiRelatedQuestionRow()
    {
        PseudoClasses.Set(LinePseudoClass, true);
    }

    /// <inheritdoc cref="QuestionProperty"/>
    public string? Question { get => GetValue(QuestionProperty); set => SetValue(QuestionProperty, value); }

    /// <inheritdoc cref="ShowsLineProperty"/>
    public bool ShowsLine { get => GetValue(ShowsLineProperty); set => SetValue(ShowsLineProperty, value); }

    /// <summary>The row's position in the list.</summary>
    public int Index { get; set; }

    protected override Type StyleKeyOverride => typeof(AiRelatedQuestionRow);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == QuestionProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>() ?? string.Empty);
        }
        else if (change.Property == ShowsLineProperty)
        {
            PseudoClasses.Set(LinePseudoClass, change.GetNewValue<bool>());
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover is (hover: hover) and (pointer: fine): touch never leaves a hover look behind.
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch && IsEffectivelyEnabled);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    /// <summary>A button named by its question; the arrow is not read.</summary>
    private sealed class RowPeer(AiRelatedQuestionRow owner) : ButtonAutomationPeer(owner)
    {
        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
