using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The body of the arena's standings popover (model arena spec 3.8), without the popup: a header with the title and the
/// vote count on the right, the ranked rows (<see cref="RankedBarList"/>) or the empty text, and a footnote with the tie
/// rule and Reset. 318 wide inside a 1px popover border (320 in all) with 14 16 8 inside; host it in a
/// <see cref="PopoverSurface"/> whose own padding is 0.
/// The body's height follows the rows or the empty text on the Smooth spring (5 rows 180 → empty 39) while the two
/// crossfade (out 100ms, in 160ms; Reduced 120ms; Off at once). Setting <see cref="Items"/> shows the rows at once;
/// <see cref="Replay"/> plays the change since an earlier list. Esc and closing belong to the popup's owner. Focus never
/// falls out: <see cref="FocusDefault"/> focuses the container (Arc's dialog element), and Reset hands focus to it when it
/// is disabled while focused (Arc drops focus to the page there; ai.md section 8).
/// Every word comes from the owner.
/// </summary>
[TemplatePart(BodySizePart, typeof(SpringSizeDecorator))]
[TemplatePart(BodyPart, typeof(ArenaStandingsBody))]
[TemplatePart(ListPart, typeof(RankedBarList))]
[TemplatePart(EmptyPart, typeof(TextBlock))]
[TemplatePart(ResetPart, typeof(Button))]
[PseudoClasses(EmptyPseudoClass)]
internal class ArenaStandings : TemplatedControl
{
    public const string BodySizePart = "PART_BodySize";
    public const string BodyPart = "PART_Body";
    public const string ListPart = "PART_List";
    public const string EmptyPart = "PART_Empty";
    public const string ResetPart = "PART_Reset";

    /// <summary>No rows: the empty text shows.</summary>
    public const string EmptyPseudoClass = ":empty";

    private static readonly TimeSpan s_exit = TimeSpan.FromMilliseconds(100);
    private static readonly Easing s_linear = new LinearEasing();

    #region Properties

    /// <summary>The rows, best first; empty shows <see cref="EmptyText"/>.</summary>
    public static readonly StyledProperty<IReadOnlyList<RankedBarItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArenaStandings, IReadOnlyList<RankedBarItem>?>(nameof(Items));

    /// <summary>The title ("Standings"); also the container's accessible name.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ArenaStandings, string?>(nameof(Title));

    /// <summary>The vote count on the right of the title ("6 votes", "No votes yet").</summary>
    public static readonly StyledProperty<string?> VotesTextProperty =
        AvaloniaProperty.Register<ArenaStandings, string?>(nameof(VotesText));

    /// <summary>What the body says without rows.</summary>
    public static readonly StyledProperty<string?> EmptyTextProperty =
        AvaloniaProperty.Register<ArenaStandings, string?>(nameof(EmptyText));

    /// <summary>The footnote ("Ties count as half a win").</summary>
    public static readonly StyledProperty<string?> FootnoteTextProperty =
        AvaloniaProperty.Register<ArenaStandings, string?>(nameof(FootnoteText));

    /// <summary>The Reset button's text.</summary>
    public static readonly StyledProperty<string?> ResetTextProperty =
        AvaloniaProperty.Register<ArenaStandings, string?>(nameof(ResetText));

    /// <summary>Whether Reset can be pressed (false without votes).</summary>
    public static readonly StyledProperty<bool> CanResetProperty =
        AvaloniaProperty.Register<ArenaStandings, bool>(nameof(CanReset));

    /// <summary>The most rows shown (5).</summary>
    public static readonly StyledProperty<int> LimitProperty =
        RankedBarList.LimitProperty.AddOwner<ArenaStandings>();

    /// <summary>The rank part of a row's accessible name when the item has none (see <see cref="RankedBarList.RankFormat"/>).</summary>
    public static readonly StyledProperty<string?> RankFormatProperty =
        RankedBarList.RankFormatProperty.AddOwner<ArenaStandings>();

    /// <summary>The words between name and percentage in a built row name (see <see cref="RankedBarList.WinRateSuffix"/>).</summary>
    public static readonly StyledProperty<string?> WinRateSuffixProperty =
        RankedBarList.WinRateSuffixProperty.AddOwner<ArenaStandings>();

    /// <inheritdoc cref="ItemsProperty"/>
    public IReadOnlyList<RankedBarItem>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="VotesTextProperty"/>
    public string? VotesText { get => GetValue(VotesTextProperty); set => SetValue(VotesTextProperty, value); }

    /// <inheritdoc cref="EmptyTextProperty"/>
    public string? EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    /// <inheritdoc cref="FootnoteTextProperty"/>
    public string? FootnoteText { get => GetValue(FootnoteTextProperty); set => SetValue(FootnoteTextProperty, value); }

    /// <inheritdoc cref="ResetTextProperty"/>
    public string? ResetText { get => GetValue(ResetTextProperty); set => SetValue(ResetTextProperty, value); }

    /// <inheritdoc cref="CanResetProperty"/>
    public bool CanReset { get => GetValue(CanResetProperty); set => SetValue(CanResetProperty, value); }

    /// <inheritdoc cref="LimitProperty"/>
    public int Limit { get => GetValue(LimitProperty); set => SetValue(LimitProperty, value); }

    /// <inheritdoc cref="RankFormatProperty"/>
    public string? RankFormat { get => GetValue(RankFormatProperty); set => SetValue(RankFormatProperty, value); }

    /// <inheritdoc cref="WinRateSuffixProperty"/>
    public string? WinRateSuffix { get => GetValue(WinRateSuffixProperty); set => SetValue(WinRateSuffixProperty, value); }

    #endregion

    /// <summary>Raised when Reset is pressed; the owner clears the standings (and turns <see cref="CanReset"/> off).</summary>
    public event EventHandler? ResetRequested;

    private readonly MotionValue<double> _listFade;
    private readonly MotionValue<double> _emptyFade;
    private SpringSizeDecorator? _bodySize;
    private ArenaStandingsBody? _body;
    private RankedBarList? _list;
    private TextBlock? _empty;
    private Button? _reset;
    private IReadOnlyList<RankedBarItem>? _pendingReplay;
    private bool _showsEmpty;
    private bool _placed;
    private int _swap;

    static ArenaStandings()
    {
        // The container takes focus like Arc's dialog element (tabindex -1): programmatically, never by Tab.
        FocusableProperty.OverrideDefaultValue<ArenaStandings>(true);
        IsTabStopProperty.OverrideDefaultValue<ArenaStandings>(false);
    }

    public ArenaStandings()
    {
        // Code-driven: the outgoing layer must leave the body's measure at the swap and stay drawn until it has faded,
        // and the rows it shows are released only then; a ControlTheme opacity transition cannot order these steps.
        _listFade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _listFade.Changed += opacity =>
        {
            if (_list is not null)
            {
                _list.Opacity = opacity;
            }
        };
        _emptyFade = new MotionValue<double>(this, 0, MotionVectors.Double);
        _emptyFade.Changed += opacity =>
        {
            if (_empty is not null)
            {
                _empty.Opacity = opacity;
            }
        };
    }

    /// <summary>The rows (template part).</summary>
    internal RankedBarList? List => _list;

    /// <summary>The empty text (template part).</summary>
    internal TextBlock? EmptyBlock => _empty;

    /// <summary>The Reset button (template part).</summary>
    internal Button? ResetButton => _reset;

    /// <summary>The body's height spring (template part).</summary>
    internal SpringSizeDecorator? BodySize => _bodySize;

    /// <summary>True while the empty text is the body's content.</summary>
    internal bool ShowsEmpty => _showsEmpty;

    /// <summary>Plays the change from <paramref name="from"/> to <see cref="Items"/> on the rows (see <see cref="RankedBarList.Replay"/>).</summary>
    public void Replay(IReadOnlyList<RankedBarItem> from)
    {
        ArgumentNullException.ThrowIfNull(from);
        if (Items is not { Count: > 0 })
        {
            return;
        }
        if (_list is null)
        {
            _pendingReplay = from;
            return;
        }
        _list.Replay(from);
    }

    /// <summary>Focuses the container (the popover's dialog), for opening and for a Reset that turned itself off.</summary>
    public bool FocusDefault() => Focus();

    #region State

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty)
        {
            _pendingReplay = null;
            Sync(animate: true);
        }
        else if (change.Property == CanResetProperty)
        {
            SyncReset();
        }
        else if (change.Property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title);
        }
    }

    /// <summary>Shows the rows or the empty text; a switch crossfades while the body's height springs.</summary>
    private void Sync(bool animate)
    {
        var items = Items;
        var empty = items is not { Count: > 0 };
        PseudoClasses.Set(EmptyPseudoClass, empty);
        if (_list is null || _empty is null || _body is null)
        {
            return;
        }
        if (!empty)
        {
            _list.Items = items;
        }
        if (_placed && empty == _showsEmpty)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        animate = animate && _placed && level != ArcMotionLevel.Off && TopLevel.GetTopLevel(this) is not null;
        _placed = true;
        _showsEmpty = empty;
        var swap = ++_swap;
        Control incoming = empty ? _empty : _list;
        Control outgoing = empty ? _list : _empty;
        var fadeIn = empty ? _emptyFade : _listFade;
        var fadeOut = empty ? _listFade : _emptyFade;
        if (empty)
        {
            // The rows rest where they are while they fade (a replay that was running ends here).
            _list.FinishReplay();
        }
        incoming.IsVisible = true;
        _body.Current = incoming;
        if (!animate)
        {
            fadeIn.Jump(1);
            fadeOut.Jump(0);
            Hide(outgoing);
            return;
        }
        var full = level == ArcMotionLevel.Full;
        _ = fadeIn.AnimateTo(1, full ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard) : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
        _ = HideWhenFadedAsync(outgoing, fadeOut.AnimateTo(0, full ? MotionSpec.Tween(s_exit, ArcEasings.Standard) : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear)), swap);
    }

    private async Task HideWhenFadedAsync(Control outgoing, Task<bool> fade, int swap)
    {
        await fade;
        if (swap == _swap)
        {
            Hide(outgoing);
        }
    }

    /// <summary>Takes a faded layer out of the tree's reading order; the rows are released once they are gone.</summary>
    private void Hide(Control layer)
    {
        layer.IsVisible = false;
        if (ReferenceEquals(layer, _list) && _showsEmpty)
        {
            _list.Items = null;
        }
    }

    private void SyncReset()
    {
        if (_reset is null)
        {
            return;
        }
        if (!CanReset && _reset.IsFocused)
        {
            // A disabled button loses focus; hand it to the container first so the keyboard stays in the popover.
            FocusDefault();
        }
        _reset.IsEnabled = CanReset;
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ResetRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_reset is not null)
        {
            _reset.Click -= OnResetClick;
        }
        _bodySize = e.NameScope.Find<SpringSizeDecorator>(BodySizePart);
        _body = e.NameScope.Find<ArenaStandingsBody>(BodyPart);
        _list = e.NameScope.Find<RankedBarList>(ListPart);
        _empty = e.NameScope.Find<TextBlock>(EmptyPart);
        _reset = e.NameScope.Find<Button>(ResetPart);
        if (_reset is not null)
        {
            _reset.Click += OnResetClick;
        }
        if (_list is not null)
        {
            _list.Opacity = _listFade.Value;
        }
        if (_empty is not null)
        {
            _empty.Opacity = _emptyFade.Value;
        }
        _placed = false;
        Sync(animate: false);
        SyncReset();
        if (_pendingReplay is { } from && _list is not null)
        {
            _pendingReplay = null;
            _list.Replay(from);
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new StandingsPeer(this);

    /// <summary>The popover's dialog: a group named by the title.</summary>
    private sealed class StandingsPeer(ArenaStandings owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}
