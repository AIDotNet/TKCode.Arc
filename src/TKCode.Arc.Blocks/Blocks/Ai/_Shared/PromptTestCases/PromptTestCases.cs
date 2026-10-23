using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The test cases of Prompt studio as one segmented control: a 36px pill frame (1px Border, 2px padding) holding a 30px
/// option per case, 2px apart; one SurfaceMuted pill glides under the selected option on the quick, critically damped
/// spring (Reduced and Off jump). Only the selected option is a tab stop; Left / Up and Right / Down move and select at
/// once (wrapping), Home and End go to the ends. Stretched (<c>HorizontalAlignment="Stretch"</c>, Arc's narrow card) the
/// options share the width equally.
/// </summary>
/// <remarks>
/// The options follow <see cref="Cases"/> (a list that reports its changes is followed as it changes; option controls are
/// reused by position) and each case's <see cref="PromptTestCase.Label"/>. A <see cref="SelectedCase"/> that is not in
/// the list selects nothing: no background, the first option is the tab stop (the owner picks the first case).
/// <see cref="SelectionChanged"/> is raised only for the person's choices (pointer or keys), never when code sets
/// <see cref="SelectedCase"/>. Screen readers get a radio group named <see cref="AccessibleName"/> with one radio button
/// per case.
/// </remarks>
[TemplatePart(TrackPart, typeof(PromptTestCaseTrack))]
[TemplatePart(SelectionPart, typeof(GlidingIndicator))]
[PseudoClasses(StretchedPseudoClass)]
internal class PromptTestCases : TemplatedControl
{
    public const string TrackPart = "PART_Track";
    public const string SelectionPart = "PART_Selection";
    public const string StretchedPseudoClass = ":stretched";

    /// <summary>The test cases, in order.</summary>
    public static readonly StyledProperty<IList<PromptTestCase>?> CasesProperty =
        AvaloniaProperty.Register<PromptTestCases, IList<PromptTestCase>?>(nameof(Cases));

    /// <summary>The selected case (two-way); one that is not in <see cref="Cases"/> selects nothing.</summary>
    public static readonly StyledProperty<PromptTestCase?> SelectedCaseProperty =
        AvaloniaProperty.Register<PromptTestCases, PromptTestCase?>(nameof(SelectedCase), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The accessible name of the group ("Test case").</summary>
    public static readonly StyledProperty<string?> AccessibleNameProperty =
        AvaloniaProperty.Register<PromptTestCases, string?>(nameof(AccessibleName));

    /// <summary>Raised when the person (pointer or keys) selects a different case, after <see cref="SelectedCase"/> changed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<PromptTestCases, RoutedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    private readonly List<PromptTestCaseOption> _options = new();
    private INotifyCollectionChanged? _observed;
    private PromptTestCaseTrack? _track;
    private GlidingIndicator? _indicator;
    private PromptTestCaseOption? _selected;
    private Rect? _lastTarget;
    private bool _attached;
    private bool _layoutHooked;
    private bool _placePending;
    private bool _glidePending;

    static PromptTestCases()
    {
        FocusableProperty.OverrideDefaultValue<PromptTestCases>(false);
    }

    /// <inheritdoc cref="CasesProperty"/>
    public IList<PromptTestCase>? Cases { get => GetValue(CasesProperty); set => SetValue(CasesProperty, value); }

    /// <inheritdoc cref="SelectedCaseProperty"/>
    public PromptTestCase? SelectedCase { get => GetValue(SelectedCaseProperty); set => SetValue(SelectedCaseProperty, value); }

    /// <inheritdoc cref="AccessibleNameProperty"/>
    public string? AccessibleName { get => GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }

    /// <summary>Raised when the person selects a different case.</summary>
    public event EventHandler<RoutedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <summary>The option controls, in case order.</summary>
    internal IReadOnlyList<PromptTestCaseOption> Options => _options;

    /// <summary>The selected option, or null.</summary>
    internal PromptTestCaseOption? SelectedOption => _selected;

    /// <summary>The gliding background (template part).</summary>
    internal GlidingIndicator? Indicator => _indicator;

    /// <summary>The row of options (template part).</summary>
    internal PromptTestCaseTrack? Track => _track;

    /// <summary>Focuses the selected option (or the first) as the keyboard does; false when there is none or it cannot take focus.</summary>
    public bool FocusSelected()
    {
        var option = _selected ?? (_options.Count > 0 ? _options[0] : null);
        return option is not null && option.IsEffectivelyVisible && option.IsEffectivelyEnabled && option.Focus(NavigationMethod.Tab);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_track is not null)
        {
            _track.Arranged -= OnTrackArranged;
            foreach (var option in _options)
            {
                _track.Children.Remove(option);
            }
        }
        _lastTarget = null;
        _track = e.NameScope.Find<PromptTestCaseTrack>(TrackPart);
        _indicator = e.NameScope.Find<GlidingIndicator>(SelectionPart);
        // A local Stretch equal to the default raises no change, so the template reads it.
        var stretched = HorizontalAlignment == HorizontalAlignment.Stretch;
        PseudoClasses.Set(StretchedPseudoClass, stretched);
        if (_track is not null)
        {
            _track.Arranged += OnTrackArranged;
            _track.IsUniform = stretched;
            foreach (var option in _options)
            {
                _track.Children.Add(option);
            }
        }
        RebuildOptions();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Observe(Cases);
        RebuildOptions();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Observe(null);
        UnhookLayout();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CasesProperty)
        {
            if (_attached)
            {
                Observe(change.GetNewValue<IList<PromptTestCase>?>());
            }
            RebuildOptions();
        }
        else if (change.Property == SelectedCaseProperty)
        {
            ApplySelection(glide: true);
            if (ControlAutomationPeer.FromElement(this) is GroupPeer peer)
            {
                peer.SelectionChanged();
            }
        }
        else if (change.Property == AccessibleNameProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
        }
        else if (change.Property == HorizontalAlignmentProperty)
        {
            var stretched = change.GetNewValue<HorizontalAlignment>() == HorizontalAlignment.Stretch;
            PseudoClasses.Set(StretchedPseudoClass, stretched);
            if (_track is not null)
            {
                _track.IsUniform = stretched;
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    #region Options

    private void Observe(IList<PromptTestCase>? cases)
    {
        var observed = cases as INotifyCollectionChanged;
        if (ReferenceEquals(observed, _observed))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnCasesChanged;
        }
        _observed = observed;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnCasesChanged;
        }
    }

    private void OnCasesChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildOptions();

    /// <summary>Brings the options in line with <see cref="Cases"/>, reusing the option controls by position.</summary>
    private void RebuildOptions()
    {
        if (_track is null)
        {
            return;
        }
        var cases = Cases;
        var count = cases?.Count ?? 0;
        for (var i = 0; i < count; i++)
        {
            PromptTestCaseOption option;
            if (i < _options.Count)
            {
                option = _options[i];
            }
            else
            {
                option = new PromptTestCaseOption();
                option.Click += OnOptionClick;
                _options.Add(option);
                _track.Children.Add(option);
            }
            option.Case = cases![i];
            AutomationProperties.SetPositionInSet(option, i + 1);
            AutomationProperties.SetSizeOfSet(option, count);
        }
        for (var i = _options.Count - 1; i >= count; i--)
        {
            var option = _options[i];
            _options.RemoveAt(i);
            _track.Children.Remove(option);
            option.Click -= OnOptionClick;
            option.Case = null;
        }
        ApplySelection(glide: false);
    }

    private PromptTestCaseOption? FindOption(PromptTestCase? testCase)
    {
        if (testCase is null)
        {
            return null;
        }
        foreach (var option in _options)
        {
            if (ReferenceEquals(option.Case, testCase))
            {
                return option;
            }
        }
        return null;
    }

    #endregion

    #region Selection and keys

    /// <summary>Marks the option of <see cref="SelectedCase"/>, moves the tab stop and the background to it.</summary>
    private void ApplySelection(bool glide)
    {
        var selected = FindOption(SelectedCase);
        var changed = !ReferenceEquals(selected, _selected);
        _selected = selected;
        foreach (var option in _options)
        {
            option.IsSelected = ReferenceEquals(option, selected);
            // Roving tab stop: only the selected option (the first while none is selected) is reached with Tab.
            KeyboardNavigation.SetIsTabStop(option, selected is null ? ReferenceEquals(option, _options[0]) : ReferenceEquals(option, selected));
        }
        if (selected is null)
        {
            _indicator?.Hide(animate: false);
            _lastTarget = null;
            return;
        }
        RequestPlacement(glide && changed);
    }

    private void OnOptionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is PromptTestCaseOption option)
        {
            Choose(option);
        }
    }

    /// <summary>The person chose <paramref name="option"/>: select its case and say so when that changed the selection.</summary>
    internal void Choose(PromptTestCaseOption option)
    {
        if (option.Case is not { } testCase || ReferenceEquals(testCase, SelectedCase))
        {
            return;
        }
        SetCurrentValue(SelectedCaseProperty, testCase);
        RaiseEvent(new RoutedEventArgs(SelectionChangedEvent, this));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _options.Count == 0 || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var from = e.Source is Visual source ? IndexOf(source) : -1;
        if (from < 0)
        {
            return;
        }
        var count = _options.Count;
        var to = e.Key switch
        {
            Key.Left or Key.Up => (from - 1 + count) % count,
            Key.Right or Key.Down => (from + 1) % count,
            Key.Home => 0,
            Key.End => count - 1,
            _ => -1
        };
        if (to < 0)
        {
            return;
        }
        e.Handled = true;
        var option = _options[to];
        // Auto-activation: the option that takes focus is selected at once.
        Choose(option);
        option.Focus(NavigationMethod.Directional);
    }

    private int IndexOf(Visual source)
    {
        for (var i = 0; i < _options.Count; i++)
        {
            if (ReferenceEquals(_options[i], source))
            {
                return i;
            }
        }
        return -1;
    }

    #endregion

    #region Background placement

    /// <summary>Moves the background to the selected option now when the row is laid out, or after the next layout pass.</summary>
    private void RequestPlacement(bool glide)
    {
        _placePending = true;
        _glidePending |= glide;
        if (_track is { IsArrangeValid: true } && _selected is { IsArrangeValid: true })
        {
            Place();
        }
        else
        {
            HookLayout();
        }
    }

    private void Place()
    {
        _placePending = false;
        var glide = _glidePending;
        _glidePending = false;
        if (_selected is not { } option || _indicator is null || option.Bounds.Width <= 0)
        {
            return;
        }
        // The background is arranged over the whole track, so an option's bounds are its rectangle there too.
        var rect = option.Bounds;
        if (glide || _lastTarget != rect || !_indicator.HasTarget)
        {
            // A layout change during a glide retargets it; otherwise the background follows the option at once.
            var animate = glide || (_lastTarget is { } last && _indicator.CurrentRect != last);
            _indicator.MoveTo(rect, animate, ArcSpring.Quick);
            _lastTarget = rect;
        }
    }

    private void OnTrackArranged()
    {
        if (_selected is { } option && (_lastTarget != option.Bounds || _placePending))
        {
            _placePending = true;
            HookLayout();
        }
    }

    private void HookLayout()
    {
        if (_layoutHooked)
        {
            return;
        }
        _layoutHooked = true;
        LayoutUpdated += OnLayoutUpdated;
    }

    private void UnhookLayout()
    {
        if (!_layoutHooked)
        {
            return;
        }
        _layoutHooked = false;
        LayoutUpdated -= OnLayoutUpdated;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_track is not { IsArrangeValid: true } || _selected is { IsArrangeValid: false })
        {
            return;
        }
        UnhookLayout();
        if (_placePending)
        {
            Place();
        }
    }

    #endregion

    /// <summary>A radio group (Arc's <c>role="radiogroup"</c>) named by <see cref="AccessibleName"/>; the selection is the selected case.</summary>
    private sealed class GroupPeer(PromptTestCases owner) : ControlAutomationPeer(owner), ISelectionProvider
    {
        private PromptTestCases Cases => (PromptTestCases)Owner;

        public bool CanSelectMultiple => false;

        public bool IsSelectionRequired => true;

        public IReadOnlyList<AutomationPeer> GetSelection() =>
            Cases._selected is { } option && CreatePeerForElement(option) is { } peer ? [peer] : [];

        public void SelectionChanged() => RaisePropertyChangedEvent(SelectionPatternIdentifiers.SelectionProperty, null, null);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(ISelectionProvider) ? this : base.GetProviderCore(providerType);
    }
}
