// Ported from Arc's free logo-marquee block (MIT, Copyright (c) 2026 Elia Kuratli): logo-marquee.tsx.
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A quiet, continuously moving row of marks under a heading and a line of text, with a control to pause it. The row holds
/// still while a mouse rests on it. Below full motion it stops, shows each mark once and hides the control. The default
/// marks are invented; pass <see cref="Brands"/> with marks you have permission to show.
/// </summary>
/// <remarks>Ported from Arc's free logo-marquee block (MIT, Copyright (c) 2026 Elia Kuratli).</remarks>
[TemplatePart(TrackPart, typeof(LogoMarqueeTrack))]
[TemplatePart(MarqueePart, typeof(Border))]
[TemplatePart(MotionButtonPart, typeof(Button))]
[TemplatePart(AnnouncerPart, typeof(SiteAnnouncer))]
[PseudoClasses(NarrowPseudoClass, PausedPseudoClass, MonoPseudoClass, StaticPseudoClass)]
public class LogoMarquee : TemplatedControl
{
    public const string TrackPart = "PART_Track";
    public const string MarqueePart = "PART_Marquee";
    public const string MotionButtonPart = "PART_MotionButton";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the block is less than 640 wide: one column, smaller marks.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while <see cref="IsPaused"/> is true.</summary>
    public const string PausedPseudoClass = ":paused";

    /// <summary>Set for <see cref="LogoMarqueeTone.Mono"/>.</summary>
    public const string MonoPseudoClass = ":mono";

    /// <summary>Set below full motion: the row stands still and the pause control is hidden.</summary>
    public const string StaticPseudoClass = ":static";

    /// <summary>The heading ("Good work moves between tools").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<LogoMarquee, string?>(nameof(Title), "Good work moves between tools");

    /// <summary>The line beside the heading.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<LogoMarquee, string?>(nameof(Description),
            "From the first idea to the final handoff, a familiar set of tools stays close to the work.");

    /// <summary>The marks to show. Null shows seven invented sample marks.</summary>
    public static readonly StyledProperty<IReadOnlyList<LogoMarqueeBrand>?> BrandsProperty =
        AvaloniaProperty.Register<LogoMarquee, IReadOnlyList<LogoMarqueeBrand>?>(nameof(Brands));

    /// <summary><see cref="LogoMarqueeTone.Color"/> (the default) shows each mark in its colors; <see cref="LogoMarqueeTone.Mono"/> draws every mark in the text color.</summary>
    public static readonly StyledProperty<LogoMarqueeTone> ToneProperty =
        AvaloniaProperty.Register<LogoMarquee, LogoMarqueeTone>(nameof(Tone));

    /// <summary>Whether the row is paused (two way). The pause control sets it.</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<LogoMarquee, bool>(nameof(IsPaused), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The screen reader name of the row ("Illustrative tool logos").</summary>
    public static readonly StyledProperty<string> RegionLabelProperty =
        AvaloniaProperty.Register<LogoMarquee, string>(nameof(RegionLabel), "Illustrative tool logos");

    /// <summary>The control's text while the row moves ("Pause").</summary>
    public static readonly StyledProperty<string> PauseLabelProperty =
        AvaloniaProperty.Register<LogoMarquee, string>(nameof(PauseLabel), "Pause");

    /// <summary>The control's text while the row is paused ("Play").</summary>
    public static readonly StyledProperty<string> PlayLabelProperty =
        AvaloniaProperty.Register<LogoMarquee, string>(nameof(PlayLabel), "Play");

    /// <summary>The control's screen reader name while the row moves ("Pause logo motion").</summary>
    public static readonly StyledProperty<string> PauseActionNameProperty =
        AvaloniaProperty.Register<LogoMarquee, string>(nameof(PauseActionName), "Pause logo motion");

    /// <summary>The control's screen reader name while the row is paused ("Play logo motion").</summary>
    public static readonly StyledProperty<string> PlayActionNameProperty =
        AvaloniaProperty.Register<LogoMarquee, string>(nameof(PlayActionName), "Play logo motion");

    /// <summary>What a screen reader hears when the row pauses ("Motion paused").</summary>
    public static readonly StyledProperty<string> PausedAnnouncementProperty =
        AvaloniaProperty.Register<LogoMarquee, string>(nameof(PausedAnnouncement), "Motion paused");

    internal static readonly DirectProperty<LogoMarquee, IReadOnlyList<LogoMarqueeBrand>?> EffectiveBrandsProperty =
        AvaloniaProperty.RegisterDirect<LogoMarquee, IReadOnlyList<LogoMarqueeBrand>?>(nameof(EffectiveBrands), o => o.EffectiveBrands);

    private IReadOnlyList<LogoMarqueeBrand>? _effectiveBrands;
    private LogoMarqueeTrack? _track;
    private Border? _marquee;
    private Button? _button;
    private ArcGlyph? _glyph;
    private TextBlock? _label;
    private SiteAnnouncer? _announcer;
    private bool _hovering;

    static LogoMarquee()
    {
        FocusableProperty.OverrideDefaultValue<LogoMarquee>(false);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<LogoMarquee>(AutomationLandmarkType.Region);
    }

    /// <summary>Creates the block.</summary>
    public LogoMarquee()
    {
        _effectiveBrands = LogoMarqueeSamples.Create();
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="BrandsProperty"/>
    public IReadOnlyList<LogoMarqueeBrand>? Brands { get => GetValue(BrandsProperty); set => SetValue(BrandsProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public LogoMarqueeTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc cref="IsPausedProperty"/>
    public bool IsPaused { get => GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }

    /// <inheritdoc cref="RegionLabelProperty"/>
    public string RegionLabel { get => GetValue(RegionLabelProperty); set => SetValue(RegionLabelProperty, value); }

    /// <inheritdoc cref="PauseLabelProperty"/>
    public string PauseLabel { get => GetValue(PauseLabelProperty); set => SetValue(PauseLabelProperty, value); }

    /// <inheritdoc cref="PlayLabelProperty"/>
    public string PlayLabel { get => GetValue(PlayLabelProperty); set => SetValue(PlayLabelProperty, value); }

    /// <inheritdoc cref="PauseActionNameProperty"/>
    public string PauseActionName { get => GetValue(PauseActionNameProperty); set => SetValue(PauseActionNameProperty, value); }

    /// <inheritdoc cref="PlayActionNameProperty"/>
    public string PlayActionName { get => GetValue(PlayActionNameProperty); set => SetValue(PlayActionNameProperty, value); }

    /// <inheritdoc cref="PausedAnnouncementProperty"/>
    public string PausedAnnouncement { get => GetValue(PausedAnnouncementProperty); set => SetValue(PausedAnnouncementProperty, value); }

    internal IReadOnlyList<LogoMarqueeBrand>? EffectiveBrands
    {
        get => _effectiveBrands;
        private set => SetAndRaise(EffectiveBrandsProperty, ref _effectiveBrands, value);
    }

    internal LogoMarqueeTrack? Track => _track;

    internal Button? MotionButton => _button;

    internal SiteAnnouncer? Announcer => _announcer;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_button is not null)
        {
            _button.Click -= OnMotionClick;
        }
        if (_marquee is not null)
        {
            _marquee.PointerEntered -= OnMarqueeEntered;
            _marquee.PointerExited -= OnMarqueeExited;
        }
        _track = e.NameScope.Find<LogoMarqueeTrack>(TrackPart);
        _marquee = e.NameScope.Find<Border>(MarqueePart);
        _button = e.NameScope.Find<Button>(MotionButtonPart);
        _glyph = e.NameScope.Find<ArcGlyph>("PART_MotionGlyph");
        _label = e.NameScope.Find<TextBlock>("PART_MotionLabel");
        _announcer = e.NameScope.Find<SiteAnnouncer>(AnnouncerPart);
        if (_button is not null)
        {
            _button.Click += OnMotionClick;
        }
        if (_marquee is not null)
        {
            _marquee.PointerEntered += OnMarqueeEntered;
            _marquee.PointerExited += OnMarqueeExited;
            AutomationProperties.SetName(_marquee, RegionLabel);
        }
        ApplyMotionControl();
        ApplyHeld();
        RefreshLevel();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == BrandsProperty)
        {
            EffectiveBrands = Brands ?? LogoMarqueeSamples.Create();
        }
        else if (property == ToneProperty)
        {
            PseudoClasses.Set(MonoPseudoClass, Tone == LogoMarqueeTone.Mono);
        }
        else if (property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title);
        }
        else if (property == IsPausedProperty)
        {
            PseudoClasses.Set(PausedPseudoClass, IsPaused);
            ApplyMotionControl();
            ApplyHeld();
            if (IsPaused)
            {
                _announcer?.Announce(PausedAnnouncement);
            }
        }
        else if (property == PauseLabelProperty || property == PlayLabelProperty || property == PauseActionNameProperty || property == PlayActionNameProperty)
        {
            ApplyMotionControl();
        }
        else if (property == RegionLabelProperty && _marquee is not null)
        {
            AutomationProperties.SetName(_marquee, RegionLabel);
        }
        else if (property == ArcMotion.LevelProperty)
        {
            RefreshLevel();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AutomationProperties.SetName(this, Title);
        RefreshLevel();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        PseudoClasses.Set(NarrowPseudoClass, e.NewSize.Width < 640);
    }

    private void RefreshLevel()
    {
        PseudoClasses.Set(StaticPseudoClass, !ArcMotion.IsFull(this));
        _track?.RefreshLevel();
    }

    private void OnMotionClick(object? sender, RoutedEventArgs e) => SetCurrentValue(IsPausedProperty, !IsPaused);

    private void OnMarqueeEntered(object? sender, PointerEventArgs e)
    {
        // Hovering the row holds it still so a logo can be read; touch has no hover.
        if (e.Pointer.Type != PointerType.Touch)
        {
            _hovering = true;
            ApplyHeld();
        }
    }

    private void OnMarqueeExited(object? sender, PointerEventArgs e)
    {
        _hovering = false;
        ApplyHeld();
    }

    private void ApplyHeld()
    {
        if (_track is not null)
        {
            _track.IsHeld = IsPaused || _hovering;
        }
    }

    private void ApplyMotionControl()
    {
        if (_button is null)
        {
            return;
        }
        AutomationProperties.SetName(_button, IsPaused ? PlayActionName : PauseActionName);
        if (_glyph is not null)
        {
            _glyph.Data = IsPaused ? ArcIcons.Play : ArcIcons.Pause;
        }
        if (_label is not null)
        {
            _label.Text = IsPaused ? PlayLabel : PauseLabel;
        }
    }
}
