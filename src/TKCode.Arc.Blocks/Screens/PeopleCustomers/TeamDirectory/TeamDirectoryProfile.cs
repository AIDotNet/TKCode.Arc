using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Primitives;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The profile of one person: large avatar, name and role, the bio, and five fields (status, team, current work,
/// location with time zone, email with a copy button). It owns its copy state: Copied or Retry for
/// <see cref="CopyResetDelay"/> after a copy, then back to rest; a click while not at rest copies again without
/// extending the time unless the outcome changed (Arc). A new profile is made for every person shown, so the state never
/// carries over.
/// </summary>
[TemplatePart(AvatarPart, typeof(Avatar))]
[TemplatePart(CopyPart, typeof(TeamDirectoryCopyButton))]
[TemplatePart(EmailPart, typeof(TeamDirectoryLinkButton))]
[TemplatePart(AnnouncerPart, typeof(ProjectsAnnouncer))]
[PseudoClasses(AwayPseudoClass)]
internal sealed class TeamDirectoryProfile : TemplatedControl
{
    public const string AvatarPart = "PART_Avatar";
    public const string CopyPart = "PART_Copy";
    public const string EmailPart = "PART_Email";
    public const string AnnouncerPart = "PART_Announcer";
    public const string AwayPseudoClass = ":away";

    public static readonly StyledProperty<double> LabelWidthProperty =
        AvaloniaProperty.Register<TeamDirectoryProfile, double>(nameof(LabelWidth), 96);

    public static readonly StyledProperty<bool> IsCopyIconOnlyProperty =
        AvaloniaProperty.Register<TeamDirectoryProfile, bool>(nameof(IsCopyIconOnly));

    public static readonly StyledProperty<TimeSpan> CopyResetDelayProperty =
        AvaloniaProperty.Register<TeamDirectoryProfile, TimeSpan>(nameof(CopyResetDelay), TimeSpan.FromMilliseconds(1800));

    // Texts shown by the template.
    public static readonly StyledProperty<string?> NameTextProperty =
        AvaloniaProperty.Register<TeamDirectoryProfile, string?>(nameof(NameText));
    public static readonly StyledProperty<string?> RoleTextProperty =
        AvaloniaProperty.Register<TeamDirectoryProfile, string?>(nameof(RoleText));
    public static readonly StyledProperty<string?> BioTextProperty =
        AvaloniaProperty.Register<TeamDirectoryProfile, string?>(nameof(BioText));
    public static readonly StyledProperty<string?> StatusLabelProperty =
        AvaloniaProperty.Register<TeamDirectoryProfile, string?>(nameof(StatusLabel));
    public static readonly StyledProperty<string?> StatusTextProperty =
        AvaloniaProperty.Register<TeamDirectoryProfile, string?>(nameof(StatusText));
    public static readonly StyledProperty<string?> TeamLabelProperty =
        AvaloniaProperty.Register<TeamDirectoryProfile, string?>(nameof(TeamLabel));
    public static readonly StyledProperty<string?> TeamTextProperty =
        AvaloniaProperty.Register<TeamDirectoryProfile, string?>(nameof(TeamText));
    public static readonly StyledProperty<string?> FocusLabelProperty =
        AvaloniaProperty.Register<TeamDirectoryProfile, string?>(nameof(FocusLabel));
    public static readonly StyledProperty<string?> FocusTextProperty =
        AvaloniaProperty.Register<TeamDirectoryProfile, string?>(nameof(FocusText));
    public static readonly StyledProperty<string?> LocationLabelProperty =
        AvaloniaProperty.Register<TeamDirectoryProfile, string?>(nameof(LocationLabel));
    public static readonly StyledProperty<string?> CityTextProperty =
        AvaloniaProperty.Register<TeamDirectoryProfile, string?>(nameof(CityText));
    public static readonly StyledProperty<string?> TimeZoneTextProperty =
        AvaloniaProperty.Register<TeamDirectoryProfile, string?>(nameof(TimeZoneText));
    public static readonly StyledProperty<string?> EmailLabelProperty =
        AvaloniaProperty.Register<TeamDirectoryProfile, string?>(nameof(EmailLabel));
    public static readonly StyledProperty<string?> EmailTextProperty =
        AvaloniaProperty.Register<TeamDirectoryProfile, string?>(nameof(EmailText));

    private Avatar? _avatar;
    private TeamDirectoryCopyButton? _copy;
    private TeamDirectoryLinkButton? _email;
    private ProjectsAnnouncer? _announcer;
    private TeamDirectoryLabels _labels;
    private PausableTimer? _resetTimer;
    private CopyState _copyState;

    public TeamDirectoryProfile(TeamDirectoryPerson person, TeamDirectoryLabels labels)
    {
        Person = person;
        _labels = labels;
        Refresh(labels);
    }

    public TeamDirectoryPerson Person { get; }

    /// <summary>True while the profile fades out after another took its place.</summary>
    public bool IsLeaving { get; set; }

    public double LabelWidth { get => GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }

    public bool IsCopyIconOnly { get => GetValue(IsCopyIconOnlyProperty); set => SetValue(IsCopyIconOnlyProperty, value); }

    public TimeSpan CopyResetDelay { get => GetValue(CopyResetDelayProperty); set => SetValue(CopyResetDelayProperty, value); }

    public string? NameText { get => GetValue(NameTextProperty); set => SetValue(NameTextProperty, value); }
    public string? RoleText { get => GetValue(RoleTextProperty); set => SetValue(RoleTextProperty, value); }
    public string? BioText { get => GetValue(BioTextProperty); set => SetValue(BioTextProperty, value); }
    public string? StatusLabel { get => GetValue(StatusLabelProperty); set => SetValue(StatusLabelProperty, value); }
    public string? StatusText { get => GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }
    public string? TeamLabel { get => GetValue(TeamLabelProperty); set => SetValue(TeamLabelProperty, value); }
    public string? TeamText { get => GetValue(TeamTextProperty); set => SetValue(TeamTextProperty, value); }
    public string? FocusLabel { get => GetValue(FocusLabelProperty); set => SetValue(FocusLabelProperty, value); }
    public string? FocusText { get => GetValue(FocusTextProperty); set => SetValue(FocusTextProperty, value); }
    public string? LocationLabel { get => GetValue(LocationLabelProperty); set => SetValue(LocationLabelProperty, value); }
    public string? CityText { get => GetValue(CityTextProperty); set => SetValue(CityTextProperty, value); }
    public string? TimeZoneText { get => GetValue(TimeZoneTextProperty); set => SetValue(TimeZoneTextProperty, value); }
    public string? EmailLabel { get => GetValue(EmailLabelProperty); set => SetValue(EmailLabelProperty, value); }
    public string? EmailText { get => GetValue(EmailTextProperty); set => SetValue(EmailTextProperty, value); }

    /// <summary>True while Copied or Retry waits for its time to end.</summary>
    internal bool IsCopyResetPending => _resetTimer?.IsRunning == true;

    /// <summary>Where the copy button stands.</summary>
    public CopyState CopyState => _copyState;

    public TeamDirectoryCopyButton? CopyButton => _copy;

    public TeamDirectoryLinkButton? EmailLink => _email;

    public ProjectsAnnouncer? Announcer => _announcer;

    /// <summary>The copy button was pressed.</summary>
    public event EventHandler? CopyClicked;

    /// <summary>The email link was pressed.</summary>
    public event EventHandler? EmailClicked;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_copy is not null)
        {
            _copy.Click -= OnCopyClick;
        }
        if (_email is not null)
        {
            _email.Click -= OnEmailClick;
        }
        base.OnApplyTemplate(e);
        _avatar = e.NameScope.Find<Avatar>(AvatarPart);
        _copy = e.NameScope.Find<TeamDirectoryCopyButton>(CopyPart);
        _email = e.NameScope.Find<TeamDirectoryLinkButton>(EmailPart);
        _announcer = e.NameScope.Find<ProjectsAnnouncer>(AnnouncerPart);
        if (_copy is not null)
        {
            _copy.Click += OnCopyClick;
        }
        if (_email is not null)
        {
            _email.Click += OnEmailClick;
        }
        Refresh(_labels);
        ApplyCopyState();
    }

    /// <summary>Shows the person's current fields with <paramref name="labels"/>.</summary>
    public void Refresh(TeamDirectoryLabels labels)
    {
        _labels = labels;
        var p = Person;
        var available = p.Availability == TeamDirectoryAvailability.Available;
        PseudoClasses.Set(AwayPseudoClass, !available);
        NameText = p.Name;
        RoleText = p.Role;
        BioText = p.Bio;
        StatusLabel = labels.StatusLabel;
        StatusText = available ? labels.Available : labels.Away;
        TeamLabel = labels.TeamLabel;
        TeamText = p.Team;
        FocusLabel = labels.FocusLabel;
        FocusText = p.Focus;
        LocationLabel = labels.LocationLabel;
        CityText = p.City;
        TimeZoneText = p.TimeZone;
        EmailLabel = labels.EmailLabel;
        EmailText = p.Email;
        AutomationProperties.SetName(this, labels.ProfileName(p.Name, p.Role));
        if (_avatar is not null)
        {
            _avatar.DisplayName = p.Name;
            _avatar.Source = p.Avatar;
            AutomationProperties.SetName(_avatar, p.Name);
        }
        if (_email is not null)
        {
            AutomationProperties.SetName(_email, p.Email);
        }
        ApplyCopyState();
    }

    /// <summary>Shows the outcome of a copy; the time to rest restarts only when the state changes.</summary>
    public void ShowCopyResult(bool succeeded)
    {
        var state = succeeded ? CopyState.Copied : CopyState.Error;
        _announcer?.Announce(succeeded ? _labels.CopiedAnnouncement(Person.Name) : _labels.CopyFailedAnnouncement);
        if (state == _copyState)
        {
            return;
        }
        _copyState = state;
        ApplyCopyState();
        _resetTimer ??= new PausableTimer(CopyResetDelay, ResetCopy);
        _resetTimer.Restart(CopyResetDelay);
    }

    /// <summary>Back to rest (the reset timer's end; tests call it directly).</summary>
    internal void ResetCopy()
    {
        _resetTimer?.Stop();
        if (_copyState == CopyState.Idle)
        {
            return;
        }
        _copyState = CopyState.Idle;
        _announcer?.Clear();
        ApplyCopyState();
    }

    private void ApplyCopyState()
    {
        if (_copy is null)
        {
            return;
        }
        _copy.State = _copyState;
        _copy.Label = _copyState switch
        {
            CopyState.Copied => _labels.Copied,
            CopyState.Error => _labels.Retry,
            _ => _labels.Copy
        };
        _copy.IdleLabel = _labels.Copy;
        _copy.CopiedLabel = _labels.Copied;
        _copy.RetryLabel = _labels.Retry;
        AutomationProperties.SetName(_copy, _copyState == CopyState.Copied ? _labels.CopiedName : _labels.CopyName);
    }

    private void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        CopyClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnEmailClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        EmailClicked?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _resetTimer?.Stop();
    }
}

/// <summary>The profile area when nobody matches: two centred lines.</summary>
internal sealed class TeamDirectoryNoProfile : TemplatedControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<TeamDirectoryNoProfile, string?>(nameof(Title));

    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<TeamDirectoryNoProfile, string?>(nameof(Hint));

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Hint { get => GetValue(HintProperty); set => SetValue(HintProperty, value); }

    /// <summary>True while it fades out after a profile took its place.</summary>
    public bool IsLeaving { get; set; }
}

/// <summary>
/// The profile pane: one profile (or the no-match message) at a time. A change cross-fades: the old view stays where it
/// was, out of the layout, and fades out (180ms) while the new one fades in (220ms) and, for a profile, rises 4px
/// (230ms); an interrupted entrance fades out from where it is, so up to three views can overlap. The pane is as high as
/// the current view, or as high as it is given. Reduced and Off motion swap at once. Code-driven because several views
/// overlap on their own clocks and leave the layout while they fade.
/// </summary>
internal sealed class TeamDirectoryDetailHost : Panel
{
    public static readonly StyledProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.Register<TeamDirectoryDetailHost, Thickness>(nameof(ContentPadding), new Thickness(24));

    public static readonly StyledProperty<double> LabelWidthProperty =
        AvaloniaProperty.Register<TeamDirectoryDetailHost, double>(nameof(LabelWidth), 96);

    public static readonly StyledProperty<bool> IsCopyIconOnlyProperty =
        AvaloniaProperty.Register<TeamDirectoryDetailHost, bool>(nameof(IsCopyIconOnly));

    public static readonly StyledProperty<double> RiseProperty =
        AvaloniaProperty.Register<TeamDirectoryDetailHost, double>(nameof(Rise), 4);

    private readonly Dictionary<Control, PaneMotion> _motions = new();
    private Control? _current;

    public TeamDirectoryDetailHost()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    public Thickness ContentPadding { get => GetValue(ContentPaddingProperty); set => SetValue(ContentPaddingProperty, value); }

    public double LabelWidth { get => GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }

    public bool IsCopyIconOnly { get => GetValue(IsCopyIconOnlyProperty); set => SetValue(IsCopyIconOnlyProperty, value); }

    public double Rise { get => GetValue(RiseProperty); set => SetValue(RiseProperty, value); }

    /// <summary>The view shown now (a profile or the no-match message), or null before the first <see cref="Show"/>.</summary>
    public Control? Current => _current;

    /// <summary>The profile shown now, or null.</summary>
    public TeamDirectoryProfile? CurrentProfile => _current as TeamDirectoryProfile;

    /// <summary>Views still fading out.</summary>
    public IEnumerable<Control> Leaving => Children.Where(c => !ReferenceEquals(c, _current));

    /// <summary>The opacity and rise of a view (tests read them).</summary>
    internal (double Opacity, double Y) MotionOf(Control view) =>
        _motions.TryGetValue(view, out var m) ? (m.Opacity.Value, m.Y.Value) : (view.Opacity, 0);

    /// <summary>Shows <paramref name="view"/> in place of the current one.</summary>
    public void Show(Control view, bool animate)
    {
        if (ReferenceEquals(view, _current))
        {
            return;
        }
        var full = animate && IsLoaded && ArcMotion.IsFull(this);
        var old = _current;
        _current = view;
        Apply(view);
        Children.Add(view);
        var motion = new PaneMotion(view);
        _motions[view] = motion;
        if (full)
        {
            motion.Opacity.Jump(0);
            _ = motion.Opacity.AnimateTo(1, TeamDirectoryMotion.PaneEnterSpec);
            if (view is TeamDirectoryProfile)
            {
                motion.Y.Jump(Rise);
                _ = motion.Y.AnimateTo(0, TeamDirectoryMotion.PaneRiseSpec);
            }
        }
        if (old is not null)
        {
            Leave(old, full);
        }
        InvalidateMeasure();
    }

    private void Leave(Control view, bool full)
    {
        SetLeaving(view, true);
        view.IsHitTestVisible = false;
        AutomationProperties.SetAccessibilityView(view, AccessibilityView.Raw);
        if (!full || !_motions.TryGetValue(view, out var motion))
        {
            Remove(view);
            return;
        }
        _ = FadeOut(view, motion);
    }

    private async Task FadeOut(Control view, PaneMotion motion)
    {
        await motion.Opacity.AnimateTo(0, TeamDirectoryMotion.PaneExitSpec);
        if (!ReferenceEquals(view, _current))
        {
            Remove(view);
        }
    }

    private void Remove(Control view)
    {
        if (_motions.Remove(view, out var motion))
        {
            motion.Dispose();
        }
        Children.Remove(view);
    }

    /// <summary>Removes every view still fading out.</summary>
    public void Settle()
    {
        foreach (var view in Leaving.ToList())
        {
            Remove(view);
        }
        if (_current is not null && _motions.TryGetValue(_current, out var motion))
        {
            motion.Opacity.Jump(1);
            motion.Y.Jump(0);
        }
    }

    private static void SetLeaving(Control view, bool leaving)
    {
        switch (view)
        {
            case TeamDirectoryProfile profile:
                profile.IsLeaving = leaving;
                break;
            case TeamDirectoryNoProfile none:
                none.IsLeaving = leaving;
                break;
        }
    }

    private void Apply(Control view)
    {
        if (view is TeamDirectoryProfile profile)
        {
            profile.Padding = ContentPadding;
            profile.LabelWidth = LabelWidth;
            profile.IsCopyIconOnly = IsCopyIconOnly;
        }
        else if (view is TeamDirectoryNoProfile none)
        {
            none.Padding = ContentPadding;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentPaddingProperty || change.Property == LabelWidthProperty || change.Property == IsCopyIconOnlyProperty)
        {
            foreach (var child in Children)
            {
                Apply(child);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = new Size(availableSize.Width, double.PositiveInfinity);
        foreach (var child in Children)
        {
            child.Measure(size);
        }
        var current = _current?.DesiredSize ?? default;
        var width = double.IsInfinity(availableSize.Width) ? current.Width : availableSize.Width;
        return new Size(width, current.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var height = ReferenceEquals(child, _current) ? finalSize.Height : Math.Max(finalSize.Height, child.DesiredSize.Height);
            child.Arrange(new Rect(0, 0, finalSize.Width, height));
        }
        return finalSize;
    }

    /// <summary>A view's opacity and vertical offset (its RenderTransform and Opacity belong to this object).</summary>
    private sealed class PaneMotion : IDisposable
    {
        private readonly Control _view;
        private readonly TranslateTransform _transform = new();

        public PaneMotion(Control view)
        {
            _view = view;
            Opacity = new MotionValue<double>(view, 1, MotionVectors.Double);
            Y = new MotionValue<double>(view, 0, MotionVectors.Double);
            Opacity.Changed += v => view.Opacity = Math.Clamp(v, 0, 1);
            Y.Changed += v => _transform.Y = v;
            view.Opacity = 1;
            view.RenderTransform = _transform;
        }

        public MotionValue<double> Opacity { get; }

        public MotionValue<double> Y { get; }

        public void Dispose()
        {
            Opacity.Dispose();
            Y.Dispose();
        }
    }
}
