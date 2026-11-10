// Ported from Arc's free faq-section block (MIT, Copyright (c) 2026 Elia Kuratli): Question, Contact and the category button
// of faq-section.tsx.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>The button of one question: the text and a plus that turns into a close mark.</summary>
internal sealed class FaqTrigger : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(FaqTrigger);
}

/// <summary>A topic in the rail. The gliding highlight behind the rail marks the chosen one.</summary>
[PseudoClasses(ActivePseudoClass)]
internal sealed class FaqCategoryButton : AvaloniaButton
{
    public const string ActivePseudoClass = ":active";

    public static readonly StyledProperty<string?> TopicProperty =
        AvaloniaProperty.Register<FaqCategoryButton, string?>(nameof(Topic));

    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<FaqCategoryButton, int>(nameof(Count));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<FaqCategoryButton, bool>(nameof(IsActive));

    protected override Type StyleKeyOverride => typeof(FaqCategoryButton);

    public string? Topic { get => GetValue(TopicProperty); set => SetValue(TopicProperty, value); }

    public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty)
        {
            PseudoClasses.Set(ActivePseudoClass, IsActive);
        }
    }
}

/// <summary>The row that offers a person: the label, an optional line under it and an arrow that leans on hover.</summary>
internal sealed class FaqContactButton : AvaloniaButton
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<FaqContactButton, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<FaqContactButton, string?>(nameof(Description));

    protected override Type StyleKeyOverride => typeof(FaqContactButton);

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
}

/// <summary>
/// One question: the trigger, the excerpt that shows a match inside a closed answer, and the answer that opens on a height
/// spring. A row that a search filters out collapses on the same spring and comes back, instead of being rebuilt.
/// </summary>
[TemplatePart(TriggerPart, typeof(FaqTrigger))]
[TemplatePart(QuestionPart, typeof(DecoratedTextBlock))]
[TemplatePart(HintPart, typeof(DecoratedTextBlock))]
[TemplatePart(AnswerPart, typeof(DecoratedTextBlock))]
[PseudoClasses(OpenPseudoClass, NarrowPseudoClass, AbsentPseudoClass)]
internal sealed class FaqRow : TemplatedControl
{
    public const string TriggerPart = "PART_Trigger";
    public const string QuestionPart = "PART_Question";
    public const string HintPart = "PART_Hint";
    public const string AnswerPart = "PART_Answer";
    public const string OpenPseudoClass = ":open";
    public const string NarrowPseudoClass = ":narrow";
    public const string AbsentPseudoClass = ":absent";

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<FaqRow, bool>(nameof(IsOpen));

    public static readonly StyledProperty<bool> IsPresentProperty =
        AvaloniaProperty.Register<FaqRow, bool>(nameof(IsPresent), true);

    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<FaqRow, bool>(nameof(IsNarrow));

    public static readonly StyledProperty<string> QueryProperty =
        AvaloniaProperty.Register<FaqRow, string>(nameof(Query), string.Empty);

    public static readonly StyledProperty<IBrush?> MarkBrushProperty =
        AvaloniaProperty.Register<FaqRow, IBrush?>(nameof(MarkBrush));

    public static readonly DirectProperty<FaqRow, string?> HintProperty =
        AvaloniaProperty.RegisterDirect<FaqRow, string?>(nameof(Hint), o => o.Hint);

    public static readonly DirectProperty<FaqRow, bool> HasHintProperty =
        AvaloniaProperty.RegisterDirect<FaqRow, bool>(nameof(HasHint), o => o.HasHint);

    private FaqTrigger? _trigger;
    private DecoratedTextBlock? _question;
    private DecoratedTextBlock? _hintBlock;
    private DecoratedTextBlock? _answer;
    private string? _hint;
    private bool _hasHint;

    public FaqRow(FaqItem item)
    {
        Item = item;
    }

    /// <summary>The question this row shows.</summary>
    public FaqItem Item { get; }

    /// <summary>The focusable trigger, once the template is applied.</summary>
    public FaqTrigger? Trigger => _trigger;

    public event EventHandler? Toggled;

    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    public bool IsPresent { get => GetValue(IsPresentProperty); set => SetValue(IsPresentProperty, value); }

    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    public string Query { get => GetValue(QueryProperty); set => SetValue(QueryProperty, value); }

    public IBrush? MarkBrush { get => GetValue(MarkBrushProperty); set => SetValue(MarkBrushProperty, value); }

    /// <summary>The excerpt shown while the row is closed and matches only in its answer.</summary>
    public string? Hint { get => _hint; private set => SetAndRaise(HintProperty, ref _hint, value); }

    public bool HasHint { get => _hasHint; private set => SetAndRaise(HasHintProperty, ref _hasHint, value); }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_trigger is not null)
        {
            _trigger.Click -= OnTriggerClick;
        }
        _trigger = e.NameScope.Find<FaqTrigger>(TriggerPart);
        _question = e.NameScope.Find<DecoratedTextBlock>(QuestionPart);
        _hintBlock = e.NameScope.Find<DecoratedTextBlock>(HintPart);
        _answer = e.NameScope.Find<DecoratedTextBlock>(AnswerPart);
        if (_trigger is not null)
        {
            _trigger.Click += OnTriggerClick;
            AutomationProperties.SetName(_trigger, Item.Question);
            UpdateExpandedHelp();
        }
        if (_question is not null)
        {
            _question.Text = Item.Question;
        }
        if (_answer is not null)
        {
            _answer.Text = Item.Answer;
        }
        Update();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            PseudoClasses.Set(OpenPseudoClass, IsOpen);
            UpdateExpandedHelp();
            Update();
        }
        else if (change.Property == IsPresentProperty)
        {
            PseudoClasses.Set(AbsentPseudoClass, !IsPresent);
            if (_trigger is not null)
            {
                _trigger.IsTabStop = IsPresent;
            }
        }
        else if (change.Property == IsNarrowProperty)
        {
            PseudoClasses.Set(NarrowPseudoClass, IsNarrow);
        }
        else if (change.Property == QueryProperty || change.Property == MarkBrushProperty)
        {
            Update();
        }
    }

    private void OnTriggerClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Toggled?.Invoke(this, EventArgs.Empty);

    private void UpdateExpandedHelp()
    {
        if (_trigger is not null)
        {
            AutomationProperties.SetHelpText(_trigger, IsOpen ? "Expanded" : "Collapsed");
        }
    }

    private void Update()
    {
        var query = Query;
        var hint = query.Trim().Length > 0 && !IsOpen && !FaqText.Contains(Item.Question, query) ? FaqText.Snippet(Item.Answer, query) : null;
        // A hint that goes away keeps its text while its height closes.
        if (hint is not null)
        {
            Hint = hint;
        }
        HasHint = hint is not null;
        if (_hintBlock is not null && hint is not null)
        {
            _hintBlock.Text = hint;
        }
        Decorate(_question, Item.Question, query);
        Decorate(_answer, Item.Answer, query);
        Decorate(_hintBlock, Hint ?? string.Empty, query);
    }

    private void Decorate(DecoratedTextBlock? block, string text, string query)
    {
        if (block is null)
        {
            return;
        }
        block.Decorations.Clear();
        foreach (var (start, length) in FaqText.Matches(text, query))
        {
            block.Decorations.Add(new TextRangeDecoration(start, length)
            {
                Background = MarkBrush,
                CornerRadius = 4,
                Outset = new Thickness(1, 0)
            });
        }
        block.InvalidateDecorations();
    }
}
