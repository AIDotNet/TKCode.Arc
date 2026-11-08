using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace TKCode.Arc;

/// <summary>
/// A text filter condition: an enable checkbox with the label, an operator (∋ ∌ = ≠ ^ $ .*) and an <see cref="Input"/>
/// for the filter text. It holds the condition (<see cref="FilterBox.IsActive"/>, <see cref="Operator"/>,
/// <see cref="Text"/>, <see cref="IsCaseSensitive"/>) and never filters data itself; read the properties in your view
/// model, or test texts with <see cref="Matches"/> or <see cref="Condition"/>.
/// <para>With <see cref="TextOperator.Regex"/> the text is a regular expression: a pattern that does not parse shows its
/// error under the field, sets <see cref="IsPatternValid"/> to false and the <c>:invalid</c> pseudo-class.</para>
/// </summary>
[TemplatePart(InputPart, typeof(Input))]
[PseudoClasses(InvalidPseudoClass)]
public class TextFilterBox : FilterBox
{
    public const string InputPart = "PART_Input";

    /// <summary>The regular expression does not parse.</summary>
    public const string InvalidPseudoClass = ":invalid";

    #region 公共属性定义

    /// <summary>The comparison (two-way, default <see cref="TextOperator.Contains"/>).</summary>
    public static readonly StyledProperty<TextOperator> OperatorProperty =
        AvaloniaProperty.Register<TextFilterBox, TextOperator>(nameof(Operator), TextOperator.Contains,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The filter text, or the pattern with <see cref="TextOperator.Regex"/> (two-way, default empty: matches everything).</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TextFilterBox, string?>(nameof(Text), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Muted text shown while the field is empty.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<TextFilterBox, string?>(nameof(Placeholder));

    /// <summary>Most characters the field accepts (default 0: no limit).</summary>
    public static readonly StyledProperty<int> MaxLengthProperty =
        AvaloniaProperty.Register<TextFilterBox, int>(nameof(MaxLength));

    /// <summary>Whether letter case matters (two-way, default false).</summary>
    public static readonly StyledProperty<bool> IsCaseSensitiveProperty =
        AvaloniaProperty.Register<TextFilterBox, bool>(nameof(IsCaseSensitive), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How long one regular expression match may run before it counts as no match (default 200 ms).</summary>
    public static readonly StyledProperty<TimeSpan> MatchTimeoutProperty =
        AvaloniaProperty.Register<TextFilterBox, TimeSpan>(nameof(MatchTimeout), TextFilterCondition.DefaultMatchTimeout);

    /// <summary>The operators the menu offers (default null: all seven).</summary>
    public static readonly StyledProperty<IReadOnlyList<TextOperator>?> OperatorsProperty =
        AvaloniaProperty.Register<TextFilterBox, IReadOnlyList<TextOperator>?>(nameof(Operators));

    /// <summary>The current condition as plain data; a new instance after every change.</summary>
    public static readonly DirectProperty<TextFilterBox, TextFilterCondition> ConditionProperty =
        AvaloniaProperty.RegisterDirect<TextFilterBox, TextFilterCondition>(nameof(Condition), o => o.Condition);

    /// <summary>False while <see cref="Operator"/> is <see cref="TextOperator.Regex"/> and <see cref="Text"/> does not parse.</summary>
    public static readonly DirectProperty<TextFilterBox, bool> IsPatternValidProperty =
        AvaloniaProperty.RegisterDirect<TextFilterBox, bool>(nameof(IsPatternValid), o => o.IsPatternValid);

    /// <summary>Why the pattern does not parse (shown under the field), or null.</summary>
    public static readonly DirectProperty<TextFilterBox, string?> PatternErrorProperty =
        AvaloniaProperty.RegisterDirect<TextFilterBox, string?>(nameof(PatternError), o => o.PatternError);

    /// <inheritdoc cref="OperatorProperty"/>
    public TextOperator Operator { get => GetValue(OperatorProperty); set => SetValue(OperatorProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <inheritdoc cref="MaxLengthProperty"/>
    public int MaxLength { get => GetValue(MaxLengthProperty); set => SetValue(MaxLengthProperty, value); }

    /// <inheritdoc cref="IsCaseSensitiveProperty"/>
    public bool IsCaseSensitive { get => GetValue(IsCaseSensitiveProperty); set => SetValue(IsCaseSensitiveProperty, value); }

    /// <inheritdoc cref="MatchTimeoutProperty"/>
    public TimeSpan MatchTimeout { get => GetValue(MatchTimeoutProperty); set => SetValue(MatchTimeoutProperty, value); }

    /// <inheritdoc cref="OperatorsProperty"/>
    public IReadOnlyList<TextOperator>? Operators { get => GetValue(OperatorsProperty); set => SetValue(OperatorsProperty, value); }

    /// <inheritdoc cref="ConditionProperty"/>
    public TextFilterCondition Condition
    {
        get => _condition;
        private set => SetAndRaise(ConditionProperty, ref _condition, value);
    }

    /// <inheritdoc cref="IsPatternValidProperty"/>
    public bool IsPatternValid
    {
        get => _isPatternValid;
        private set => SetAndRaise(IsPatternValidProperty, ref _isPatternValid, value);
    }

    /// <inheritdoc cref="PatternErrorProperty"/>
    public string? PatternError
    {
        get => _patternError;
        private set => SetAndRaise(PatternErrorProperty, ref _patternError, value);
    }

    #endregion

    private TextFilterCondition _condition = new(TextOperator.Contains, string.Empty);
    private bool _isPatternValid = true;
    private string? _patternError;
    private Input? _input;

    /// <summary>
    /// Whether <paramref name="candidate"/> passes this filter: always true while the filter is off
    /// (<see cref="FilterBox.IsActive"/> false), otherwise <see cref="Condition"/>'s answer (an empty text or an invalid
    /// pattern constrains nothing).
    /// </summary>
    public bool Matches(string? candidate) => !IsActive || Condition.Matches(candidate);

    /// <summary>Empties the filter text (the operator and the options stay).</summary>
    public void Clear() => SetCurrentValue(TextProperty, string.Empty);

    protected override IReadOnlyList<object> OperatorChoices()
    {
        var operators = Operators is { Count: > 0 } chosen ? chosen : FilterOperators.TextOperators;
        var boxed = new object[operators.Count];
        for (var i = 0; i < operators.Count; i++)
        {
            boxed[i] = operators[i];
        }
        return boxed;
    }

    protected override object CurrentOperator() => Operator;

    protected override void PickOperator(object choice)
    {
        if (choice is TextOperator op)
        {
            SetCurrentValue(OperatorProperty, op);
        }
    }

    protected override string SymbolOf(object choice) => choice is TextOperator op ? FilterOperators.Symbol(op) : choice.ToString() ?? string.Empty;

    protected override string NameOf(object choice) => choice is TextOperator op ? FilterOperators.Name(op) : choice.ToString() ?? string.Empty;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        _input = e.NameScope.Find<Input>(InputPart);
        base.OnApplyTemplate(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OperatorProperty)
        {
            SyncOperator();
            UpdateCondition();
        }
        else if (change.Property == TextProperty || change.Property == IsCaseSensitiveProperty
                 || change.Property == MatchTimeoutProperty)
        {
            UpdateCondition();
        }
        else if (change.Property == OperatorsProperty)
        {
            RefreshOperatorChoices();
        }
        else if (change.Property == IsPatternValidProperty)
        {
            PseudoClasses.Set(InvalidPseudoClass, !IsPatternValid);
        }
    }

    protected override void UpdateAutomation()
    {
        base.UpdateAutomation();
        if (_input is not null)
        {
            _input.Label = LabelText ?? "Filter text";
        }
    }

    private void UpdateCondition()
    {
        var next = new TextFilterCondition(Operator, Text, IsCaseSensitive, MatchTimeout);
        var changed = next.Operator != Condition.Operator || next.Text != Condition.Text
                      || next.IsCaseSensitive != Condition.IsCaseSensitive || next.MatchTimeout != Condition.MatchTimeout;
        if (!changed)
        {
            return;
        }
        Condition = next;
        IsPatternValid = next.IsValid;
        PatternError = next.Error;
        RaiseFilterChanged();
    }
}
