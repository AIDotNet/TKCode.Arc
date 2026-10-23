using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The header's close button (spec 3.4): 30 square, radius 10, a 16px glyph in TextMuted (PanelRightClose docked, X in the
/// sheet); under a mouse or pen SurfaceMuted with the glyph in Foreground (160ms standard); the keyboard ring 2px out.
/// </summary>
internal sealed class AiPanelIconButton : AvaloniaButton
{
    /// <summary>The glyph (a 24 × 24 Lucide geometry).</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<AiPanelIconButton, Geometry?>(nameof(Glyph));

    protected override Type StyleKeyOverride => typeof(AiPanelIconButton);

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
}

/// <summary>
/// The "This page" context pill (spec 3.4): 24 high, padding 0 9, a 13px FileText in TextMuted 5px from the text (12 /
/// 16.8, TextSecondary), a 1px Border pill with no fill. It is not removable and takes no input.
/// </summary>
internal sealed class AiPanelContextPill : TemplatedControl
{
    /// <summary>The text ("This page").</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiPanelContextPill, string?>(nameof(Text));

    /// <summary>The glyph.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<AiPanelContextPill, Geometry?>(nameof(Glyph));

    static AiPanelContextPill()
    {
        FocusableProperty.OverrideDefaultValue<AiPanelContextPill>(false);
    }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, Text);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);
}

/// <summary>
/// The selection pill (spec 3.4): "Selected: Summary" with a 13px TextSelect in Accent, on Accent 7% over Surface with an
/// edge of Accent 28% over Border, the text (12, Foreground) cut with an ellipsis when the header is narrow, and an 18px
/// round remove button (X 12, TextMuted; under a mouse or pen SurfaceMuted and Foreground). The tooltip is the whole
/// selected text (<see cref="FullText"/>). The remove button raises <see cref="RemoveRequested"/>.
/// </summary>
[TemplatePart(RemovePart, typeof(AvaloniaButton))]
internal sealed class AiPanelSelectionPill : TemplatedControl
{
    public const string RemovePart = "PART_Remove";

    /// <summary>The text ("Selected: Summary").</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiPanelSelectionPill, string?>(nameof(Text));

    /// <summary>The whole selected text (the tooltip).</summary>
    public static readonly StyledProperty<string?> FullTextProperty =
        AvaloniaProperty.Register<AiPanelSelectionPill, string?>(nameof(FullText));

    /// <summary>The accessible name of the remove button.</summary>
    public static readonly StyledProperty<string?> RemoveNameProperty =
        AvaloniaProperty.Register<AiPanelSelectionPill, string?>(nameof(RemoveName));

    /// <summary>Raised (bubbling) when the remove button is pressed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RemoveRequestedEvent =
        RoutedEvent.Register<AiPanelSelectionPill, RoutedEventArgs>(nameof(RemoveRequested), RoutingStrategies.Bubble);

    private AvaloniaButton? _remove;

    static AiPanelSelectionPill()
    {
        FocusableProperty.OverrideDefaultValue<AiPanelSelectionPill>(false);
    }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="FullTextProperty"/>
    public string? FullText { get => GetValue(FullTextProperty); set => SetValue(FullTextProperty, value); }

    /// <inheritdoc cref="RemoveNameProperty"/>
    public string? RemoveName { get => GetValue(RemoveNameProperty); set => SetValue(RemoveNameProperty, value); }

    /// <inheritdoc cref="RemoveRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? RemoveRequested
    {
        add => AddHandler(RemoveRequestedEvent, value);
        remove => RemoveHandler(RemoveRequestedEvent, value);
    }

    /// <summary>The remove button (once the template is applied).</summary>
    internal AvaloniaButton? RemoveButton => _remove;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_remove is not null)
        {
            _remove.Click -= OnRemoveClick;
        }
        _remove = e.NameScope.Find<AvaloniaButton>(RemovePart);
        if (_remove is not null)
        {
            _remove.Click += OnRemoveClick;
        }
        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == FullTextProperty || change.Property == RemoveNameProperty)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        AutomationProperties.SetName(this, Text);
        AutomationProperties.SetHelpText(this, FullText);
        Tooltip.SetTip(this, string.IsNullOrEmpty(FullText) ? null : FullText);
        if (_remove is not null)
        {
            AutomationProperties.SetName(_remove, RemoveName);
        }
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(RemoveRequestedEvent, this));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);
}

/// <summary>
/// A suggestion (spec 3.5): a 30px pill with a 1px Border on Surface, padding 12, the prompt in 13 / 18.2 TextSecondary;
/// under a mouse or pen SurfaceMuted with Foreground text (160ms standard); the keyboard ring 2px out. A press sends
/// <see cref="Text"/>.
/// </summary>
internal sealed class AiPanelSuggestionPill : AvaloniaButton
{
    /// <summary>The prompt.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiPanelSuggestionPill, string?>(nameof(Text));

    protected override Type StyleKeyOverride => typeof(AiPanelSuggestionPill);

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, Text);
        }
    }
}

/// <summary>
/// A row of suggestion pills that wraps, 6px apart; it follows the newest answer (or sits in the empty thread). A pressed
/// pill raises <see cref="Invoked"/> with its prompt.
/// </summary>
internal sealed class AiPanelSuggestionRow : WrapPanel
{
    /// <summary>Raised (bubbling) when a pill is pressed; the argument's source is the pill.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> InvokedEvent =
        RoutedEvent.Register<AiPanelSuggestionRow, RoutedEventArgs>(nameof(Invoked), RoutingStrategies.Bubble);

    private readonly List<string> _prompts = [];

    public AiPanelSuggestionRow()
    {
        Orientation = Avalonia.Layout.Orientation.Horizontal;
    }

    protected override Type StyleKeyOverride => typeof(AiPanelSuggestionRow);

    /// <inheritdoc cref="InvokedEvent"/>
    public event EventHandler<RoutedEventArgs>? Invoked
    {
        add => AddHandler(InvokedEvent, value);
        remove => RemoveHandler(InvokedEvent, value);
    }

    /// <summary>The prompts shown.</summary>
    public IReadOnlyList<string> Prompts => _prompts;

    /// <summary>The pills shown.</summary>
    public IEnumerable<AiPanelSuggestionPill> Pills => Children.OfType<AiPanelSuggestionPill>();

    /// <summary>Shows <paramref name="prompts"/> (pills whose prompt stays are kept).</summary>
    public void SetPrompts(IReadOnlyList<string> prompts)
    {
        if (_prompts.SequenceEqual(prompts))
        {
            return;
        }
        _prompts.Clear();
        _prompts.AddRange(prompts);
        var pills = Children.OfType<AiPanelSuggestionPill>().ToList();
        Children.Clear();
        foreach (var prompt in prompts)
        {
            var pill = pills.FirstOrDefault(p => p.Text == prompt);
            if (pill is null)
            {
                pill = new AiPanelSuggestionPill { Text = prompt };
                pill.Click += OnPillClick;
            }
            else
            {
                pills.Remove(pill);
            }
            Children.Add(pill);
        }
        foreach (var pill in pills)
        {
            pill.Click -= OnPillClick;
        }
    }

    private void OnPillClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is AiPanelSuggestionPill pill)
        {
            pill.RaiseEvent(new RoutedEventArgs(InvokedEvent, pill));
        }
    }
}

/// <summary>
/// The card of a failed answer (spec 3.7): a 1px Border box, radius 14, padding 10 10 10 12, no fill; a 16px CircleAlert in
/// Danger, 10px, two lines (<see cref="Title"/> 13 / 18.2 Foreground, which may wrap; <see cref="Detail"/> 12 / 16.8
/// TextMuted), and at the right Retry (a small secondary button with a 13px RotateCcw) raising <see cref="RetryRequested"/>.
/// </summary>
[TemplatePart(RetryPart, typeof(Button))]
internal sealed class AiPanelErrorCard : TemplatedControl
{
    public const string RetryPart = "PART_Retry";

    /// <summary>The first line ("Helper could not answer").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AiPanelErrorCard, string?>(nameof(Title));

    /// <summary>The second line (the error).</summary>
    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<AiPanelErrorCard, string?>(nameof(Detail));

    /// <summary>The retry button's text.</summary>
    public static readonly StyledProperty<string?> RetryTextProperty =
        AvaloniaProperty.Register<AiPanelErrorCard, string?>(nameof(RetryText));

    /// <summary>Raised (bubbling) when Retry is pressed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RetryRequestedEvent =
        RoutedEvent.Register<AiPanelErrorCard, RoutedEventArgs>(nameof(RetryRequested), RoutingStrategies.Bubble);

    private Button? _retry;

    static AiPanelErrorCard()
    {
        FocusableProperty.OverrideDefaultValue<AiPanelErrorCard>(false);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DetailProperty"/>
    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    /// <inheritdoc cref="RetryTextProperty"/>
    public string? RetryText { get => GetValue(RetryTextProperty); set => SetValue(RetryTextProperty, value); }

    /// <inheritdoc cref="RetryRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? RetryRequested
    {
        add => AddHandler(RetryRequestedEvent, value);
        remove => RemoveHandler(RetryRequestedEvent, value);
    }

    /// <summary>The retry button (once the template is applied).</summary>
    internal Button? RetryButton => _retry;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_retry is not null)
        {
            _retry.Click -= OnRetryClick;
        }
        _retry = e.NameScope.Find<Button>(RetryPart);
        if (_retry is not null)
        {
            _retry.Click += OnRetryClick;
            AutomationProperties.SetName(_retry, RetryText);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RetryTextProperty && _retry is not null)
        {
            AutomationProperties.SetName(_retry, RetryText);
        }
        else if (change.Property == TitleProperty || change.Property == DetailProperty)
        {
            AutomationProperties.SetName(this, string.IsNullOrEmpty(Detail) ? Title : $"{Title}. {Detail}");
        }
    }

    private void OnRetryClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(RetryRequestedEvent, this));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);
}
