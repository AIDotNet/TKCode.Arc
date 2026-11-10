using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A compact card that asks people to vote on a short list of proposed features. Picking an option adds the viewer's
/// vote to its count at once (the digits roll and the share bars glide), the Vote button sends the choice, and the card
/// turns into a thank-you with the results and a way to change the vote.
/// <code>
/// &lt;arc:FeatureVoting Title="What should we build next?" MaxVotes="1"&gt;
///     &lt;arc:FeatureVotingOption Id="offline" Title="Offline mode" Votes="412" /&gt;
/// &lt;/arc:FeatureVoting&gt;
/// </code>
/// Without options the card shows a sample list. <see cref="Voted"/> reports the choice; update
/// <see cref="FeatureVotingOption.Votes"/> in place to show live counts.
/// </summary>
[TemplatePart(HostPart, typeof(Decorator))]
[PseudoClasses(SubmittedPseudoClass)]
public class FeatureVoting : TemplatedControl
{
    public const string HostPart = "PART_Host";

    /// <summary>Set while the thank-you shows.</summary>
    public const string SubmittedPseudoClass = ":submitted";

    #region Properties

    /// <summary>The question at the top ("What should we build next?").</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<FeatureVoting, string>(nameof(Title), "What should we build next?");

    /// <summary>A line under the title; null or empty shows none.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<FeatureVoting, string?>(nameof(Description), "Your vote decides what we start on this quarter.");

    /// <summary>How many options one person may pick (1: picking another moves the vote). At least 1.</summary>
    public static readonly StyledProperty<int> MaxVotesProperty =
        AvaloniaProperty.Register<FeatureVoting, int>(nameof(MaxVotes), 1, coerce: static (_, value) => Math.Max(1, value));

    /// <summary>Whether the thank-you shows (two-way): true after <see cref="Submit"/>, false after "Change my vote".</summary>
    public static readonly StyledProperty<bool> IsSubmittedProperty =
        AvaloniaProperty.Register<FeatureVoting, bool>(nameof(IsSubmitted), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether counts and bars show before the viewer has voted (default true); false keeps them hidden until then.</summary>
    public static readonly StyledProperty<bool> ShowsResultsBeforeVotingProperty =
        AvaloniaProperty.Register<FeatureVoting, bool>(nameof(ShowsResultsBeforeVoting), true);

    /// <summary>The label of the button that sends the vote ("Vote").</summary>
    public static readonly StyledProperty<string> SubmitLabelProperty =
        AvaloniaProperty.Register<FeatureVoting, string>(nameof(SubmitLabel), "Vote");

    /// <summary>The heading of the thank-you ("Thanks for voting").</summary>
    public static readonly StyledProperty<string> ThankYouTitleProperty =
        AvaloniaProperty.Register<FeatureVoting, string>(nameof(ThankYouTitle), "Thanks for voting");

    /// <summary>The line under the thank-you heading.</summary>
    public static readonly StyledProperty<string> ThankYouMessageProperty =
        AvaloniaProperty.Register<FeatureVoting, string>(nameof(ThankYouMessage), "We will post what we pick in the changelog.");

    /// <summary>The label of the button that reopens the choice ("Change my vote").</summary>
    public static readonly StyledProperty<string> ChangeVoteLabelProperty =
        AvaloniaProperty.Register<FeatureVoting, string>(nameof(ChangeVoteLabel), "Change my vote");

    /// <summary>The options chosen now, in list order.</summary>
    public static readonly DirectProperty<FeatureVoting, IReadOnlyList<FeatureVotingOption>> SelectedOptionsProperty =
        AvaloniaProperty.RegisterDirect<FeatureVoting, IReadOnlyList<FeatureVotingOption>>(nameof(SelectedOptions), o => o.SelectedOptions);

    /// <summary>Every vote counted now, the viewer's included.</summary>
    public static readonly DirectProperty<FeatureVoting, int> TotalVotesProperty =
        AvaloniaProperty.RegisterDirect<FeatureVoting, int>(nameof(TotalVotes), o => o.TotalVotes);

    /// <summary>Raised when the viewer sends a vote.</summary>
    public static readonly RoutedEvent<FeatureVotingVotedEventArgs> VotedEvent =
        RoutedEvent.Register<FeatureVoting, FeatureVotingVotedEventArgs>(nameof(Voted), RoutingStrategies.Bubble);

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="MaxVotesProperty"/>
    public int MaxVotes { get => GetValue(MaxVotesProperty); set => SetValue(MaxVotesProperty, value); }

    /// <inheritdoc cref="IsSubmittedProperty"/>
    public bool IsSubmitted { get => GetValue(IsSubmittedProperty); set => SetValue(IsSubmittedProperty, value); }

    /// <inheritdoc cref="ShowsResultsBeforeVotingProperty"/>
    public bool ShowsResultsBeforeVoting { get => GetValue(ShowsResultsBeforeVotingProperty); set => SetValue(ShowsResultsBeforeVotingProperty, value); }

    /// <inheritdoc cref="SubmitLabelProperty"/>
    public string SubmitLabel { get => GetValue(SubmitLabelProperty); set => SetValue(SubmitLabelProperty, value); }

    /// <inheritdoc cref="ThankYouTitleProperty"/>
    public string ThankYouTitle { get => GetValue(ThankYouTitleProperty); set => SetValue(ThankYouTitleProperty, value); }

    /// <inheritdoc cref="ThankYouMessageProperty"/>
    public string ThankYouMessage { get => GetValue(ThankYouMessageProperty); set => SetValue(ThankYouMessageProperty, value); }

    /// <inheritdoc cref="ChangeVoteLabelProperty"/>
    public string ChangeVoteLabel { get => GetValue(ChangeVoteLabelProperty); set => SetValue(ChangeVoteLabelProperty, value); }

    /// <inheritdoc cref="SelectedOptionsProperty"/>
    public IReadOnlyList<FeatureVotingOption> SelectedOptions { get => _selected; private set => SetAndRaise(SelectedOptionsProperty, ref _selected, value); }

    /// <inheritdoc cref="TotalVotesProperty"/>
    public int TotalVotes { get => _total; private set => SetAndRaise(TotalVotesProperty, ref _total, value); }

    /// <summary>The proposed features, in the order they show. Empty shows a sample list.</summary>
    [Content]
    public AvaloniaList<FeatureVotingOption> Options { get; } = new();

    /// <inheritdoc cref="VotedEvent"/>
    public event EventHandler<FeatureVotingVotedEventArgs>? Voted
    {
        add => AddHandler(VotedEvent, value);
        remove => RemoveHandler(VotedEvent, value);
    }

    #endregion

    internal static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<FeatureVoting, double>(nameof(BarHeight), 6);

    internal static readonly StyledProperty<double> IndicatorSizeProperty =
        AvaloniaProperty.Register<FeatureVoting, double>(nameof(IndicatorSize), 20);

    internal double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }

    internal double IndicatorSize { get => GetValue(IndicatorSizeProperty); set => SetValue(IndicatorSizeProperty, value); }

    private static readonly CultureInfo s_culture = CultureInfo.GetCultureInfo("en-US");

    private IReadOnlyList<FeatureVotingOption> _selected = Array.Empty<FeatureVotingOption>();
    private int _total;
    private readonly HashSet<FeatureVotingOption> _chosen = new();
    private readonly List<FeatureVotingRow> _rows = new();
    private readonly List<FeatureVotingOption> _watched = new();
    private readonly StackPanel _root;
    private readonly ArcContentSwap _header;
    private readonly StackPanel _formHeader;
    private readonly TextBlock _titleText;
    private readonly TextBlock _descriptionText;
    private readonly TextBlock _hintText;
    private readonly StackPanel _thanksHeader;
    private readonly TextBlock _thanksTitle;
    private readonly TextBlock _thanksMessage;
    private readonly StrokeDrawGlyph _thanksCheck;
    private readonly StackPanel _list;
    private readonly RollingDigits _totalDigits;
    private readonly TextBlock _totalWord;
    private readonly ArcContentSwap _action;
    private readonly Button _submit;
    private readonly Button _change;
    private readonly SupportKitAnnouncer _announcer = new();
    private Decorator? _host;
    private bool _sample;

    /// <summary>Creates the widget.</summary>
    public FeatureVoting()
    {
        BlockFocusModality.Watch();
        _titleText = SupportKit.Heading(Title, ArcDesignTokenKind.TypeBase);
        _descriptionText = SupportKit.Text(Description, ArcDesignTokenKind.TextSecondary, wrap: true);
        _hintText = SupportKit.Text(null, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS);
        _formHeader = new StackPanel { Spacing = 4, Children = { _titleText, _descriptionText, _hintText } };
        _hintText.Margin = new Thickness(0, 4, 0, 0);

        _thanksCheck = new StrokeDrawGlyph { Data = ArcIcons.Check, Size = 16, StrokeThickness = 2.5, Progress = 1 };
        ArcTokens.Bind(_thanksCheck, StrokeDrawGlyph.ForegroundProperty, ArcDesignTokenKind.AccentForeground);
        var badge = new Border
        {
            Width = 32,
            Height = 32,
            Child = _thanksCheck,
            HorizontalAlignment = HorizontalAlignment.Left,
        }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(badge, Border.BackgroundProperty, ArcDesignTokenKind.Accent);
        _thanksTitle = SupportKit.Heading(ThankYouTitle, ArcDesignTokenKind.TypeBase);
        _thanksMessage = SupportKit.Text(ThankYouMessage, ArcDesignTokenKind.TextSecondary, wrap: true);
        _thanksHeader = new StackPanel { Spacing = 4, Children = { badge, _thanksTitle, _thanksMessage } };
        _thanksTitle.Margin = new Thickness(0, 8, 0, 0);

        _header = new ArcContentSwap { Mode = ContentSwapMode.Lift, HorizontalContentAlignment = HorizontalAlignment.Stretch, Content = _formHeader };

        _list = new StackPanel { Spacing = 2 };
        AutomationProperties.SetName(_list, Title);

        _totalDigits = new RollingDigits { UseGrouping = true, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.Medium };
        ArcTokens.Bind(_totalDigits, RollingDigits.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(_totalDigits, RollingDigits.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        _totalWord = SupportKit.Text("votes", ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS);
        var total = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { _totalDigits, _totalWord } };

        _submit = SupportKit.Button(SubmitLabel, ButtonVariant.Primary, size: ControlSize.Medium);
        _submit.Click += (_, _) => Submit();
        _change = SupportKit.Button(ChangeVoteLabel, ButtonVariant.Ghost, size: ControlSize.Medium);
        _change.Click += (_, _) => ChangeVote();
        _action = new ArcContentSwap { Mode = ContentSwapMode.Label, Content = _submit, HorizontalContentAlignment = HorizontalAlignment.Right };

        var footer = SupportKit.Grid("*,Auto", columnSpacing: 12);
        footer.Children.Add(total.At(0));
        footer.Children.Add(_action.At(1));

        _root = new StackPanel { Spacing = 16, Children = { _header, _list, footer, _announcer } };

        Options.CollectionChanged += OnOptionsChanged;
        AddHandler(KeyDownEvent, OnBlockKeyDown, RoutingStrategies.Tunnel);
        RebuildRows();
    }

    #region Internals for tests

    internal IReadOnlyList<FeatureVotingRow> Rows => _rows;

    internal Button SubmitButton => _submit;

    internal Button ChangeButton => _change;

    internal string? LastAnnouncement => _announcer.Announcement;

    /// <summary>The options shown: <see cref="Options"/>, or the sample when it is empty.</summary>
    internal IReadOnlyList<FeatureVotingOption> Effective => _effective;

    #endregion

    private IReadOnlyList<FeatureVotingOption> _effective = Array.Empty<FeatureVotingOption>();

    /// <summary>Chooses or unchooses <paramref name="option"/> (ignored after the vote is sent).</summary>
    /// <returns>Whether the choice changed.</returns>
    public bool Toggle(FeatureVotingOption option)
    {
        if (IsSubmitted || !_effective.Contains(option))
        {
            return false;
        }
        if (_chosen.Remove(option))
        {
            Refresh();
            _announcer.Announce($"Removed your vote for {option.Title}.");
            return true;
        }
        if (MaxVotes == 1)
        {
            _chosen.Clear();
        }
        else if (_chosen.Count >= MaxVotes)
        {
            _announcer.Announce($"You can pick up to {MaxVotes}. Remove one first.");
            return false;
        }
        _chosen.Add(option);
        Refresh();
        _announcer.Announce($"Picked {option.Title}, {SupportKit.Count(Displayed(option), "vote", "votes")}.");
        return true;
    }

    /// <summary>Sends the vote: raises <see cref="Voted"/> and shows the thank-you. Does nothing without a choice.</summary>
    public void Submit()
    {
        if (IsSubmitted || _chosen.Count == 0)
        {
            return;
        }
        SetCurrentValue(IsSubmittedProperty, true);
    }

    /// <summary>Leaves the thank-you so the viewer can change the vote (the choice stays).</summary>
    public void ChangeVote() => SetCurrentValue(IsSubmittedProperty, false);

    #region Template and properties

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Decorator>(HostPart);
        SupportKit.Mount(_host, _root);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == TitleProperty)
        {
            _titleText.Text = Title;
            AutomationProperties.SetName(_list, Title);
        }
        else if (property == DescriptionProperty)
        {
            _descriptionText.Text = Description;
            _descriptionText.IsVisible = !string.IsNullOrEmpty(Description);
        }
        else if (property == MaxVotesProperty)
        {
            while (_chosen.Count > MaxVotes)
            {
                _chosen.Remove(_effective.Last(_chosen.Contains));
            }
            Refresh();
        }
        else if (property == IsSubmittedProperty)
        {
            OnSubmittedChanged(change.GetNewValue<bool>());
        }
        else if (property == ShowsResultsBeforeVotingProperty || property == BarHeightProperty || property == IndicatorSizeProperty)
        {
            Refresh();
        }
        else if (property == SubmitLabelProperty)
        {
            SetButtonLabel(_submit, SubmitLabel);
        }
        else if (property == ChangeVoteLabelProperty)
        {
            SetButtonLabel(_change, ChangeVoteLabel);
        }
        else if (property == ThankYouTitleProperty)
        {
            _thanksTitle.Text = ThankYouTitle;
        }
        else if (property == ThankYouMessageProperty)
        {
            _thanksMessage.Text = ThankYouMessage;
        }
    }

    private static void SetButtonLabel(Button button, string label)
    {
        button.Content = label;
        AutomationProperties.SetName(button, label);
    }

    private void OnSubmittedChanged(bool submitted)
    {
        PseudoClasses.Set(SubmittedPseudoClass, submitted);
        _header.Content = submitted ? _thanksHeader : _formHeader;
        _action.Content = submitted ? _change : _submit;
        if (submitted)
        {
            if (ArcMotion.IsFull(this))
            {
                _thanksCheck.Progress = 0;
                _ = _thanksCheck.Play(TimeSpan.FromMilliseconds(340), ArcEasings.Enter, TimeSpan.FromMilliseconds(120));
            }
            else
            {
                _thanksCheck.Progress = 1;
            }
            var chosen = SelectedOptions;
            RaiseEvent(new FeatureVotingVotedEventArgs(VotedEvent, chosen));
            _announcer.Announce($"{ThankYouTitle}. {ThankYouMessage}");
        }
        Refresh();
        // Keep focus on the block's action after the swap.
        if (_action.Content is Button next && (_submit.IsFocused || _change.IsFocused || IsKeyboardFocusWithin))
        {
            next.Focus(BlockFocusModality.For(this));
        }
    }

    #endregion

    #region Options

    private void OnOptionsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildRows();

    private void RebuildRows()
    {
        foreach (var option in _watched)
        {
            option.PropertyChanged -= OnOptionChanged;
        }
        _watched.Clear();
        _sample = Options.Count == 0;
        _effective = _sample ? FeatureVotingSamples.Create() : Options.ToList();
        _chosen.RemoveWhere(o => !_effective.Contains(o));
        _list.Children.Clear();
        _rows.Clear();
        foreach (var option in _effective)
        {
            option.PropertyChanged += OnOptionChanged;
            _watched.Add(option);
            var row = new FeatureVotingRow(this, option);
            row.Row.Click += (_, _) => Toggle(option);
            _rows.Add(row);
            _list.Children.Add(row.Row);
        }
        Refresh();
    }

    private void OnOptionChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private int Displayed(FeatureVotingOption option) => option.Votes + (_chosen.Contains(option) ? 1 : 0);

    /// <summary>Brings every row, the totals, the hint and the button in line with the choice and the counts.</summary>
    private void Refresh()
    {
        var total = 0;
        foreach (var option in _effective)
        {
            total += Displayed(option);
        }
        TotalVotes = total;
        SelectedOptions = _effective.Where(_chosen.Contains).ToList();
        var results = IsSubmitted || ShowsResultsBeforeVoting;
        foreach (var row in _rows)
        {
            var votes = Displayed(row.Option);
            row.Update(_chosen.Contains(row.Option), votes, total == 0 ? 0 : (double)votes / total, results, IsSubmitted);
        }
        _totalDigits.Value = total;
        _totalWord.Text = total == 1 ? "vote" : "votes";
        _totalDigits.IsVisible = results;
        _totalWord.IsVisible = results;
        _submit.IsEnabled = _chosen.Count > 0;
        _hintText.Text = MaxVotes == 1
            ? "Pick the one you want most."
            : $"Pick up to {MaxVotes}. {_chosen.Count} of {MaxVotes} picked.";
    }

    #endregion

    #region Keyboard

    private void OnBlockKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }
        var focused = _rows.FindIndex(r => r.Row.IsFocused);
        switch (e.Key)
        {
            case Key.Down or Key.Up when focused >= 0:
                var step = e.Key == Key.Down ? 1 : -1;
                var next = Math.Clamp(focused + step, 0, _rows.Count - 1);
                _rows[next].Row.Focus(NavigationMethod.Directional);
                e.Handled = true;
                break;
            case Key.Home when focused >= 0:
                _rows[0].Row.Focus(NavigationMethod.Directional);
                e.Handled = true;
                break;
            case Key.End when focused >= 0:
                _rows[^1].Row.Focus(NavigationMethod.Directional);
                e.Handled = true;
                break;
            case Key.Enter when (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0:
                Submit();
                e.Handled = true;
                break;
        }
    }

    #endregion
}

/// <summary>One option of a <see cref="FeatureVoting"/>: the choice mark, the title and line, the share bar and the rolling count.</summary>
internal sealed class FeatureVotingRow
{
    private static readonly Cursor s_hand = new(StandardCursorType.Hand);

    private readonly FeatureVoting _owner;
    private readonly SupportKitBindings _tints = new();
    private bool? _tinted;
    private readonly Border _mark;
    private readonly PresenceHost _check;
    private readonly TextBlock _title;
    private readonly TextBlock _description;
    private readonly FeatureVotingBar _bar;
    private readonly RollingDigits _count;

    public FeatureVotingRow(FeatureVoting owner, FeatureVotingOption option)
    {
        _owner = owner;
        Option = option;
        _check = new PresenceHost
        {
            Mode = PresenceMode.Pop,
            Child = SupportKit.Glyph(ArcIcons.Check, 13, ArcDesignTokenKind.AccentForeground),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _mark = new Border
        {
            Child = _check,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 0, 0),
        }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(_mark, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        _title = SupportKit.Text(option.Title, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, FontWeight.Medium, wrap: true);
        _description = SupportKit.Text(option.Description, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, wrap: true);
        _bar = new FeatureVotingBar { Margin = new Thickness(0, 8, 0, 0) };
        _count = new RollingDigits { UseGrouping = true, VerticalAlignment = VerticalAlignment.Top, FontWeight = FontWeight.Medium };
        ArcTokens.Bind(_count, RollingDigits.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_count, RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_count, RollingDigits.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);

        var text = new StackPanel { Spacing = 2, Children = { _title, _description, _bar } };
        var grid = SupportKit.Grid("Auto,*,Auto", columnSpacing: 12);
        grid.Children.Add(_mark.At(0));
        grid.Children.Add(text.At(1));
        grid.Children.Add(_count.At(2));
        Row = new SupportKitRow { Content = grid, Item = option, Padding = new Thickness(10, 10) };
    }

    public FeatureVotingOption Option { get; }

    public SupportKitRow Row { get; }

    public FeatureVotingBar Bar => _bar;

    public RollingDigits Count => _count;

    public bool IsChosen { get; private set; }

    public void Update(bool chosen, int votes, double share, bool results, bool submitted)
    {
        IsChosen = chosen;
        var size = _owner.IndicatorSize;
        _mark.Width = size;
        _mark.Height = size;
        _bar.Height = _owner.BarHeight;
        _check.IsPresent = chosen;
        if (_tinted != chosen)
        {
            _tinted = chosen;
            _tints.Rebind(_mark, Border.BackgroundProperty, chosen ? ArcDesignTokenKind.Accent : ArcDesignTokenKind.Surface);
            _tints.Rebind(_mark, Border.BorderBrushProperty, chosen ? ArcDesignTokenKind.Accent : ArcDesignTokenKind.BorderStrong);
            _tints.Rebind(_bar, FeatureVotingBar.FillBrushProperty, chosen ? ArcDesignTokenKind.Accent : ArcDesignTokenKind.BorderStrong);
        }
        _title.Text = Option.Title;
        _description.Text = Option.Description;
        _description.IsVisible = !string.IsNullOrEmpty(Option.Description);
        _bar.IsVisible = results;
        _count.IsVisible = results;
        _bar.Fraction = share;
        _count.Value = votes;
        Row.IsChecked = chosen;
        Row.Focusable = !submitted;
        Row.Cursor = submitted ? Cursor.Default : s_hand;
        var percent = Math.Round(share * 100);
        AutomationProperties.SetName(Row, results
            ? $"{Option.Title}, {SupportKit.Count(votes, "vote", "votes")}, {percent}%{(chosen ? ", your vote" : string.Empty)}"
            : $"{Option.Title}{(chosen ? ", your vote" : string.Empty)}");
    }
}

/// <summary>The sample list a <see cref="FeatureVoting"/> without options shows.</summary>
internal static class FeatureVotingSamples
{
    public static List<FeatureVotingOption> Create() =>
    [
        new("offline", "Offline mode", "Keep working on a plane; changes sync when you are back.", 412),
        new("calendar", "Calendar view for tasks", "See due dates laid out by week and drag to reschedule.", 296),
        new("templates", "Shared templates", "Save a project as a template the whole team can start from.", 241),
        new("export", "Export to spreadsheet", "Download any board as a sheet with every custom field.", 157),
    ];
}
