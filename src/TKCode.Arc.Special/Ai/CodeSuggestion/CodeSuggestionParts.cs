using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The focusable code area of a <see cref="CodeSuggestion"/>: the region named after the file where the shortcuts work and
/// Arrow left / right scroll long lines. Keyboard focus draws Arc's shared ring inside its edge; pointer focus draws none.
/// </summary>
internal sealed class CodeSuggestionCodeArea : ContentControl
{
    static CodeSuggestionCodeArea()
    {
        FocusableProperty.OverrideDefaultValue<CodeSuggestionCodeArea>(true);
    }

    protected override Type StyleKeyOverride => typeof(CodeSuggestionCodeArea);
}

/// <summary>
/// The review bar under a pending hunk: its summary on the left (hidden below a 440px card; the buttons keep their names),
/// Reject and Accept on the right. It is placed and pinned by <see cref="CodeSuggestionView"/>, which folds it by clipping
/// to <see cref="SetVisibleHeight"/>; the bar itself is always laid out at its full height, so nothing is measured while it
/// folds.
/// </summary>
[TemplatePart(AcceptPart, typeof(AvaloniaButton))]
[TemplatePart(RejectPart, typeof(AvaloniaButton))]
[PseudoClasses(CompactPseudoClass)]
internal sealed class CodeSuggestionReviewBar : TemplatedControl
{
    internal const string AcceptPart = "PART_Accept";
    internal const string RejectPart = "PART_Reject";
    internal const string CompactPseudoClass = ":compact";

    public static readonly StyledProperty<string?> SummaryProperty =
        AvaloniaProperty.Register<CodeSuggestionReviewBar, string?>(nameof(Summary));

    public static readonly StyledProperty<string> AcceptTextProperty =
        AvaloniaProperty.Register<CodeSuggestionReviewBar, string>(nameof(AcceptText), "Accept");

    public static readonly StyledProperty<string> RejectTextProperty =
        AvaloniaProperty.Register<CodeSuggestionReviewBar, string>(nameof(RejectText), "Reject");

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<CodeSuggestionReviewBar, bool>(nameof(IsCompact));

    private readonly RectangleGeometry _clip = new();
    private AvaloniaButton? _accept;
    private AvaloniaButton? _reject;
    private string? _acceptName;
    private string? _rejectName;

    public string? Summary { get => GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }

    public string AcceptText { get => GetValue(AcceptTextProperty); set => SetValue(AcceptTextProperty, value); }

    public string RejectText { get => GetValue(RejectTextProperty); set => SetValue(RejectTextProperty, value); }

    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    /// <summary>The Accept button (once the template is applied).</summary>
    public AvaloniaButton? AcceptButton => _accept;

    /// <summary>The Reject button (once the template is applied).</summary>
    public AvaloniaButton? RejectButton => _reject;

    /// <summary>Raised when Accept is pressed.</summary>
    public event EventHandler? AcceptPressed;

    /// <summary>Raised when Reject is pressed.</summary>
    public event EventHandler? RejectPressed;

    /// <summary>Names the two buttons after their hunk ("Accept Add retry limits").</summary>
    public void SetNames(string accept, string reject)
    {
        _acceptName = accept;
        _rejectName = reject;
        ApplyNames();
    }

    /// <summary>How much of the bar shows: the rest is clipped (the bar folds into the code).</summary>
    public void SetVisibleHeight(double height)
    {
        var full = Bounds.Height;
        if (height >= full - 0.01 && full > 0)
        {
            if (Clip is not null)
            {
                Clip = null;
                InvalidateVisual();
            }
            return;
        }
        _clip.Rect = new Rect(0, 0, Math.Max(0, Bounds.Width), Math.Max(0, height));
        Clip = _clip;
        InvalidateVisual();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_accept is not null)
        {
            _accept.Click -= OnAccept;
        }
        if (_reject is not null)
        {
            _reject.Click -= OnReject;
        }
        _accept = e.NameScope.Find<AvaloniaButton>(AcceptPart);
        _reject = e.NameScope.Find<AvaloniaButton>(RejectPart);
        if (_accept is not null)
        {
            _accept.Click += OnAccept;
        }
        if (_reject is not null)
        {
            _reject.Click += OnReject;
        }
        ApplyNames();
        PseudoClasses.Set(CompactPseudoClass, IsCompact);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCompactProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, IsCompact);
        }
    }

    private void ApplyNames()
    {
        if (_accept is not null && _acceptName is not null)
        {
            AutomationProperties.SetName(_accept, _acceptName);
        }
        if (_reject is not null && _rejectName is not null)
        {
            AutomationProperties.SetName(_reject, _rejectName);
        }
    }

    private void OnAccept(object? sender, RoutedEventArgs e) => AcceptPressed?.Invoke(this, EventArgs.Empty);

    private void OnReject(object? sender, RoutedEventArgs e) => RejectPressed?.Invoke(this, EventArgs.Empty);
}

/// <summary>Which buttons the footer of a <see cref="CodeSuggestion"/> offers.</summary>
internal enum CodeSuggestionActionsKind
{
    /// <summary>No buttons.</summary>
    None,

    /// <summary>Stop, while the model reads or writes.</summary>
    Stop,

    /// <summary>Reject all and Accept all (with the shortcut hint), while hunks wait.</summary>
    Review,

    /// <summary>Regenerate, once the review is done.</summary>
    Regenerate,

    /// <summary>Retry, after an error.</summary>
    Retry
}

/// <summary>
/// One set of footer buttons. The footer swaps whole sets with a short crossfade (a new set per kind), so only the
/// buttons of the current kind are visible here; the others stay out of the tab order.
/// </summary>
[TemplatePart(StopPart, typeof(AvaloniaButton))]
[TemplatePart(RejectAllPart, typeof(AvaloniaButton))]
[TemplatePart(AcceptAllPart, typeof(AvaloniaButton))]
[TemplatePart(RegeneratePart, typeof(AvaloniaButton))]
[TemplatePart(RetryPart, typeof(AvaloniaButton))]
internal sealed class CodeSuggestionActions : TemplatedControl
{
    internal const string StopPart = "PART_Stop";
    internal const string RejectAllPart = "PART_RejectAll";
    internal const string AcceptAllPart = "PART_AcceptAll";
    internal const string RegeneratePart = "PART_Regenerate";
    internal const string RetryPart = "PART_Retry";

    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<CodeSuggestionActions, bool>(nameof(IsStacked));

    public static readonly StyledProperty<string?> StopTextProperty =
        AvaloniaProperty.Register<CodeSuggestionActions, string?>(nameof(StopText));

    public static readonly StyledProperty<string?> RejectAllTextProperty =
        AvaloniaProperty.Register<CodeSuggestionActions, string?>(nameof(RejectAllText));

    public static readonly StyledProperty<string?> AcceptAllTextProperty =
        AvaloniaProperty.Register<CodeSuggestionActions, string?>(nameof(AcceptAllText));

    public static readonly StyledProperty<string?> HintTextProperty =
        AvaloniaProperty.Register<CodeSuggestionActions, string?>(nameof(HintText));

    public static readonly StyledProperty<bool> ShowsHintProperty =
        AvaloniaProperty.Register<CodeSuggestionActions, bool>(nameof(ShowsHint));

    public static readonly StyledProperty<string?> RegenerateTextProperty =
        AvaloniaProperty.Register<CodeSuggestionActions, string?>(nameof(RegenerateText));

    public static readonly StyledProperty<string?> RetryTextProperty =
        AvaloniaProperty.Register<CodeSuggestionActions, string?>(nameof(RetryText));

    private readonly Dictionary<string, AvaloniaButton> _buttons = new();

    public CodeSuggestionActions(CodeSuggestionActionsKind kind)
    {
        Kind = kind;
    }

    /// <summary>The set this instance shows (fixed: the footer makes a new set for another kind).</summary>
    public CodeSuggestionActionsKind Kind { get; }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    public string? StopText { get => GetValue(StopTextProperty); set => SetValue(StopTextProperty, value); }

    public string? RejectAllText { get => GetValue(RejectAllTextProperty); set => SetValue(RejectAllTextProperty, value); }

    public string? AcceptAllText { get => GetValue(AcceptAllTextProperty); set => SetValue(AcceptAllTextProperty, value); }

    public string? HintText { get => GetValue(HintTextProperty); set => SetValue(HintTextProperty, value); }

    public bool ShowsHint { get => GetValue(ShowsHintProperty); set => SetValue(ShowsHintProperty, value); }

    public string? RegenerateText { get => GetValue(RegenerateTextProperty); set => SetValue(RegenerateTextProperty, value); }

    public string? RetryText { get => GetValue(RetryTextProperty); set => SetValue(RetryTextProperty, value); }

    /// <summary>Raised with the part name of the pressed button.</summary>
    public event EventHandler<string>? Pressed;

    /// <summary>The button of a part (once the template is applied).</summary>
    public AvaloniaButton? Button(string part) => _buttons.GetValueOrDefault(part);

    /// <summary>The first visible button: where focus goes when the previous set left with the focused button.</summary>
    public AvaloniaButton? FirstButton =>
        Kind switch
        {
            CodeSuggestionActionsKind.Stop => Button(StopPart),
            CodeSuggestionActionsKind.Review => Button(AcceptAllPart),
            CodeSuggestionActionsKind.Regenerate => Button(RegeneratePart),
            CodeSuggestionActionsKind.Retry => Button(RetryPart),
            _ => null
        };

    protected override Type StyleKeyOverride => typeof(CodeSuggestionActions);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        foreach (var (part, button) in _buttons)
        {
            button.Click -= OnClick;
        }
        _buttons.Clear();
        foreach (var part in new[] { StopPart, RejectAllPart, AcceptAllPart, RegeneratePart, RetryPart })
        {
            if (e.NameScope.Find<AvaloniaButton>(part) is { } button)
            {
                _buttons[part] = button;
                button.Click += OnClick;
            }
        }
        Show(StopPart, Kind == CodeSuggestionActionsKind.Stop);
        Show(RejectAllPart, Kind == CodeSuggestionActionsKind.Review);
        Show(AcceptAllPart, Kind == CodeSuggestionActionsKind.Review);
        Show(RegeneratePart, Kind == CodeSuggestionActionsKind.Regenerate);
        Show(RetryPart, Kind == CodeSuggestionActionsKind.Retry);
    }

    private void Show(string part, bool visible)
    {
        if (Button(part) is { } button)
        {
            button.IsVisible = visible;
        }
    }

    private void OnClick(object? sender, RoutedEventArgs e)
    {
        foreach (var (part, button) in _buttons)
        {
            if (ReferenceEquals(button, sender))
            {
                Pressed?.Invoke(this, part);
                return;
            }
        }
    }
}

/// <summary>
/// The footer's button row: right-aligned with a gap, or, stacked under the status below a 440px card, the visible
/// buttons share the full width.
/// </summary>
internal sealed class CodeSuggestionActionsPanel : Panel
{
    public static readonly StyledProperty<bool> StretchesProperty =
        AvaloniaProperty.Register<CodeSuggestionActionsPanel, bool>(nameof(Stretches));

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<CodeSuggestionActionsPanel, double>(nameof(Spacing), 8);

    static CodeSuggestionActionsPanel()
    {
        AffectsMeasure<CodeSuggestionActionsPanel>(StretchesProperty, SpacingProperty);
    }

    public bool Stretches { get => GetValue(StretchesProperty); set => SetValue(StretchesProperty, value); }

    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        var count = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
            count++;
        }
        width += Math.Max(0, count - 1) * Spacing;
        if (Stretches && double.IsFinite(availableSize.Width))
        {
            width = availableSize.Width;
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var visible = new List<Control>(Children.Count);
        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                visible.Add(child);
            }
        }
        if (visible.Count == 0)
        {
            return finalSize;
        }
        var spacing = Spacing;
        if (Stretches)
        {
            var each = Math.Max(0, (finalSize.Width - spacing * (visible.Count - 1)) / visible.Count);
            var x = 0.0;
            foreach (var child in visible)
            {
                child.Arrange(new Rect(x, 0, each, finalSize.Height));
                x += each + spacing;
            }
            return finalSize;
        }
        var total = 0.0;
        foreach (var child in visible)
        {
            total += child.DesiredSize.Width;
        }
        total += spacing * (visible.Count - 1);
        var left = Math.Max(0, finalSize.Width - total);
        foreach (var child in visible)
        {
            child.Arrange(new Rect(left, 0, child.DesiredSize.Width, finalSize.Height));
            left += child.DesiredSize.Width + spacing;
        }
        return finalSize;
    }
}

/// <summary>The language mark of the file tab: two letters in a tinted rounded square (no brand logo is drawn).</summary>
internal sealed class CodeSuggestionFileMark : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<CodeSuggestionFileMark, string?>(nameof(Text));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    protected override Type StyleKeyOverride => typeof(CodeSuggestionFileMark);
}
