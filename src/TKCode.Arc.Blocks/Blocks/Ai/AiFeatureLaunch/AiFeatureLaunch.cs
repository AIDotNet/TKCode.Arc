using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// An in-product announcement of a new AI feature: a short looping demo on top, a "New" badge, the feature's name, a
/// sentence on what it does and a few highlights, then a try button and a quiet "Maybe later", with a close button in
/// the corner. Closing it (or trying the feature, with <see cref="DismissOnTry"/>) shrinks the card away and raises
/// <see cref="Dismissed"/> with the reason, so the app can remember not to show it again.
/// <code>
/// &lt;arc:AiFeatureLaunch Title="Thread summaries" Description="Catch up on any long thread in seconds." /&gt;
/// </code>
/// The built-in demo runs only under full motion and while visible, can be paused, and is replaced by
/// <see cref="Demo"/> when you give your own content. With no data set the card shows its invented sample.
/// </summary>
[TemplatePart(TryPart, typeof(Button))]
[TemplatePart(LaterPart, typeof(Button))]
[TemplatePart(ClosePart, typeof(Button))]
[TemplatePart(PlayPart, typeof(Button))]
[TemplatePart(DemoPart, typeof(AiFeatureLaunchDemo))]
[TemplatePart(HighlightsPart, typeof(Panel))]
[PseudoClasses(CustomDemoPseudoClass, PausedPseudoClass, CompactPseudoClass)]
public class AiFeatureLaunch : TemplatedControl
{
    public const string TryPart = "PART_Try";
    public const string LaterPart = "PART_Later";
    public const string ClosePart = "PART_Close";
    public const string PlayPart = "PART_Play";
    public const string DemoPart = "PART_Demo";
    public const string HighlightsPart = "PART_Highlights";

    public const string CustomDemoPseudoClass = ":custom-demo";
    public const string PausedPseudoClass = ":paused";
    public const string CompactPseudoClass = ":compact";

    #region Properties

    /// <summary>Whether the announcement shows (two-way). Setting it to false shrinks it away without raising <see cref="Dismissed"/>.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<AiFeatureLaunch, bool>(nameof(IsOpen), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The feature's name ("Thread summaries").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AiFeatureLaunch, string?>(nameof(Title), AiFeatureLaunchSamples.Title);

    /// <summary>One or two sentences on what the feature does.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AiFeatureLaunch, string?>(nameof(Description), AiFeatureLaunchSamples.Description);

    /// <summary>Two to four short highlights, each shown with a check; null or empty hides the list.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> HighlightsProperty =
        AvaloniaProperty.Register<AiFeatureLaunch, IReadOnlyList<string>?>(nameof(Highlights), AiFeatureLaunchSamples.Highlights());

    /// <summary>Your own demo (an image, a video, a control) in place of the built-in animation; null uses the built-in one.</summary>
    public static readonly StyledProperty<object?> DemoProperty =
        AvaloniaProperty.Register<AiFeatureLaunch, object?>(nameof(Demo));

    /// <summary>What the demo shows, read by screen readers in its place.</summary>
    public static readonly StyledProperty<string?> DemoDescriptionProperty =
        AvaloniaProperty.Register<AiFeatureLaunch, string?>(nameof(DemoDescription), AiFeatureLaunchSamples.DemoDescription);

    /// <summary>Whether the built-in demo plays (two-way); the pause button flips it.</summary>
    public static readonly StyledProperty<bool> IsDemoPlayingProperty =
        AvaloniaProperty.Register<AiFeatureLaunch, bool>(nameof(IsDemoPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the demo area shows at all.</summary>
    public static readonly StyledProperty<bool> IsDemoVisibleProperty =
        AvaloniaProperty.Register<AiFeatureLaunch, bool>(nameof(IsDemoVisible), true);

    /// <summary>Run by the try button, with <see cref="Title"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> TryCommandProperty =
        AvaloniaProperty.Register<AiFeatureLaunch, ICommand?>(nameof(TryCommand));

    /// <summary>Whether trying the feature also closes the announcement (with the reason Tried).</summary>
    public static readonly StyledProperty<bool> DismissOnTryProperty =
        AvaloniaProperty.Register<AiFeatureLaunch, bool>(nameof(DismissOnTry), true);

    /// <summary>Whether the "Maybe later" button shows.</summary>
    public static readonly StyledProperty<bool> IsLaterVisibleProperty =
        AvaloniaProperty.Register<AiFeatureLaunch, bool>(nameof(IsLaterVisible), true);

    /// <summary>Every word the announcement shows or reads out, with English defaults.</summary>
    public static readonly StyledProperty<AiFeatureLaunchLabels> LabelsProperty =
        AvaloniaProperty.Register<AiFeatureLaunch, AiFeatureLaunchLabels>(nameof(Labels), new AiFeatureLaunchLabels());

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<AiFeatureLaunch, double>(nameof(CompactWidth), 360);

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="HighlightsProperty"/>
    public IReadOnlyList<string>? Highlights { get => GetValue(HighlightsProperty); set => SetValue(HighlightsProperty, value); }

    /// <inheritdoc cref="DemoProperty"/>
    public object? Demo { get => GetValue(DemoProperty); set => SetValue(DemoProperty, value); }

    /// <inheritdoc cref="DemoDescriptionProperty"/>
    public string? DemoDescription { get => GetValue(DemoDescriptionProperty); set => SetValue(DemoDescriptionProperty, value); }

    /// <inheritdoc cref="IsDemoPlayingProperty"/>
    public bool IsDemoPlaying { get => GetValue(IsDemoPlayingProperty); set => SetValue(IsDemoPlayingProperty, value); }

    /// <inheritdoc cref="IsDemoVisibleProperty"/>
    public bool IsDemoVisible { get => GetValue(IsDemoVisibleProperty); set => SetValue(IsDemoVisibleProperty, value); }

    /// <inheritdoc cref="TryCommandProperty"/>
    public ICommand? TryCommand { get => GetValue(TryCommandProperty); set => SetValue(TryCommandProperty, value); }

    /// <inheritdoc cref="DismissOnTryProperty"/>
    public bool DismissOnTry { get => GetValue(DismissOnTryProperty); set => SetValue(DismissOnTryProperty, value); }

    /// <inheritdoc cref="IsLaterVisibleProperty"/>
    public bool IsLaterVisible { get => GetValue(IsLaterVisibleProperty); set => SetValue(IsLaterVisibleProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AiFeatureLaunchLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    #endregion

    #region Events

    /// <summary>Raised when the try button is pressed (before <see cref="TryCommand"/> runs).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> TryRequestedEvent =
        RoutedEvent.Register<AiFeatureLaunch, RoutedEventArgs>(nameof(TryRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the person closes the announcement, with how they closed it.</summary>
    public static readonly RoutedEvent<AiFeatureLaunchDismissedEventArgs> DismissedEvent =
        RoutedEvent.Register<AiFeatureLaunch, AiFeatureLaunchDismissedEventArgs>(nameof(Dismissed), RoutingStrategies.Bubble);

    /// <summary>Raised when the try button is pressed (before <see cref="TryCommand"/> runs).</summary>
    public event EventHandler<RoutedEventArgs>? TryRequested
    {
        add => AddHandler(TryRequestedEvent, value);
        remove => RemoveHandler(TryRequestedEvent, value);
    }

    /// <summary>Raised when the person closes the announcement, with how they closed it.</summary>
    public event EventHandler<AiFeatureLaunchDismissedEventArgs>? Dismissed
    {
        add => AddHandler(DismissedEvent, value);
        remove => RemoveHandler(DismissedEvent, value);
    }

    #endregion

    private Button? _try;
    private Button? _later;
    private Button? _close;
    private Button? _play;
    private ArcGlyph? _playGlyph;
    private Panel? _highlights;
    private Control? _demo;
    private TextBlock? _badgeText;

    static AiFeatureLaunch()
    {
        FocusableProperty.OverrideDefaultValue<AiFeatureLaunch>(false);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<AiFeatureLaunch>(AutomationLandmarkType.Region);
    }

    internal AiFeatureLaunchDemo? BuiltInDemo => _demo as AiFeatureLaunchDemo;

    internal IReadOnlyList<Control> HighlightRows => _highlights?.Children ?? (IReadOnlyList<Control>)[];

    #region Public actions

    /// <summary>Shows the announcement again.</summary>
    public void Show() => SetCurrentValue(IsOpenProperty, true);

    /// <summary>Closes the announcement and raises <see cref="Dismissed"/> with <paramref name="reason"/>.</summary>
    /// <param name="reason">How it was closed.</param>
    public void Dismiss(AiFeatureLaunchDismissReason reason = AiFeatureLaunchDismissReason.Closed)
    {
        if (!IsOpen)
        {
            return;
        }
        SetCurrentValue(IsOpenProperty, false);
        RaiseEvent(new AiFeatureLaunchDismissedEventArgs(DismissedEvent, reason));
    }

    /// <summary>Does what the try button does: raises <see cref="TryRequested"/>, runs <see cref="TryCommand"/> and, with <see cref="DismissOnTry"/>, closes.</summary>
    public void TryFeature()
    {
        RaiseEvent(new RoutedEventArgs(TryRequestedEvent));
        if (TryCommand is { } command && command.CanExecute(Title))
        {
            command.Execute(Title);
        }
        if (DismissOnTry)
        {
            Dismiss(AiFeatureLaunchDismissReason.Tried);
        }
    }

    #endregion

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook(_try, OnTryClick);
        Unhook(_later, OnLaterClick);
        Unhook(_close, OnCloseClick);
        Unhook(_play, OnPlayClick);

        _try = e.NameScope.Find<Button>(TryPart);
        _later = e.NameScope.Find<Button>(LaterPart);
        _close = e.NameScope.Find<Button>(ClosePart);
        _play = e.NameScope.Find<Button>(PlayPart);
        _playGlyph = e.NameScope.Find<ArcGlyph>("PART_PlayGlyph");
        _highlights = e.NameScope.Find<Panel>(HighlightsPart);
        _demo = e.NameScope.Find<Control>(DemoPart);
        _badgeText = e.NameScope.Find<TextBlock>("PART_BadgeText");

        Hook(_try, OnTryClick);
        Hook(_later, OnLaterClick);
        Hook(_close, OnCloseClick);
        Hook(_play, OnPlayClick);

        PseudoClasses.Set(CustomDemoPseudoClass, Demo is not null);
        UpdateLabels();
        UpdatePlayState();
        BuildHighlights();
    }

    private static void Hook(Button? button, EventHandler<RoutedEventArgs> handler)
    {
        if (button is not null)
        {
            button.Click += handler;
        }
    }

    private static void Unhook(Button? button, EventHandler<RoutedEventArgs> handler)
    {
        if (button is not null)
        {
            button.Click -= handler;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DemoProperty)
        {
            PseudoClasses.Set(CustomDemoPseudoClass, Demo is not null);
        }
        else if (change.Property == IsDemoPlayingProperty)
        {
            UpdatePlayState();
        }
        else if (change.Property == HighlightsProperty)
        {
            BuildHighlights();
        }
        else if (change.Property == LabelsProperty)
        {
            UpdateLabels();
            UpdatePlayState();
            BuildHighlights();
        }
        else if (change.Property == BoundsProperty || change.Property == CompactWidthProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, Bounds.Width > 0 && Bounds.Width <= CompactWidth);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && IsOpen)
        {
            Dismiss();
            e.Handled = true;
        }
    }

    private void OnTryClick(object? sender, RoutedEventArgs e) => TryFeature();

    private void OnLaterClick(object? sender, RoutedEventArgs e) => Dismiss(AiFeatureLaunchDismissReason.Later);

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Dismiss();

    private void OnPlayClick(object? sender, RoutedEventArgs e) => SetCurrentValue(IsDemoPlayingProperty, !IsDemoPlaying);

    private void UpdatePlayState()
    {
        PseudoClasses.Set(PausedPseudoClass, !IsDemoPlaying);
        if (_playGlyph is not null)
        {
            _playGlyph.Data = IsDemoPlaying ? ArcIcons.Pause : ArcIcons.Play;
        }
        if (_play is not null)
        {
            AutomationProperties.SetName(_play, IsDemoPlaying ? Labels.Pause : Labels.Play);
        }
    }

    private void UpdateLabels()
    {
        var labels = Labels;
        if (_badgeText is not null)
        {
            _badgeText.Text = labels.Badge;
        }
        if (_try is not null)
        {
            _try.Content = labels.Try;
        }
        if (_later is not null)
        {
            _later.Content = labels.Later;
        }
        if (_close is not null)
        {
            AutomationProperties.SetName(_close, labels.Close);
        }
        if (_highlights is not null)
        {
            AutomationProperties.SetName(_highlights, labels.Highlights);
        }
    }

    private void BuildHighlights()
    {
        if (_highlights is null)
        {
            return;
        }
        _highlights.Children.Clear();
        if (Highlights is not { } highlights)
        {
            return;
        }
        foreach (var text in highlights)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }
            _highlights.Children.Add(new AiFeatureLaunchHighlight { Text = text });
        }
        _highlights.IsVisible = _highlights.Children.Count > 0;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AiFeatureLaunchPeer(this);

    private sealed class AiFeatureLaunchPeer(AiFeatureLaunch owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore()
            => base.GetNameCore() is { Length: > 0 } name ? name : ((AiFeatureLaunch)Owner).Title;
    }
}

/// <summary>One highlight of an <see cref="AiFeatureLaunch"/>: a check in the accent color and a line of text.</summary>
internal sealed class AiFeatureLaunchHighlight : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiFeatureLaunchHighlight, string?>(nameof(Text));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, Text);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AiFeatureLaunchHighlightPeer(this);

    private sealed class AiFeatureLaunchHighlightPeer(AiFeatureLaunchHighlight owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override bool IsContentElementCore() => true;
    }
}
