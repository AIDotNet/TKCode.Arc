// Ported from Arc's free hero-section block (MIT, Copyright (c) 2026 Elia Kuratli): hero-section.tsx and hero-section.module.css. The
// products, copy and sample data are this library's own.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A landing page hero in three designs, each one full screen tall. Centered sets a headline over a drifting mesh with a live
/// dashboard rising from the bottom edge, Split puts the copy beside a live workflow graph that routes sample events node by
/// node, and Minimal is large type over a mesh gradient with customer marks along the bottom edge. Set <see cref="Title"/> to
/// show your own content in the chosen layout, at its natural height.
/// </summary>
/// <remarks>Ported from Arc's free hero-section block (MIT, Copyright (c) 2026 Elia Kuratli).</remarks>
[TemplatePart(ScenePart, typeof(Panel))]
[PseudoClasses(CenteredPseudoClass, SplitPseudoClass, MinimalPseudoClass, CustomPseudoClass)]
public class HeroSection : TemplatedControl
{
    public const string ScenePart = "PART_Scene";

    /// <summary>Set for the Centered design.</summary>
    public const string CenteredPseudoClass = ":centered";

    /// <summary>Set for the Split design.</summary>
    public const string SplitPseudoClass = ":split";

    /// <summary>Set for the Minimal design.</summary>
    public const string MinimalPseudoClass = ":minimal";

    /// <summary>Set while the hero shows your own content (<see cref="Title"/> is set).</summary>
    public const string CustomPseudoClass = ":custom";

    /// <summary>Which design to show: Centered (default), Split or Minimal.</summary>
    public static readonly StyledProperty<HeroSectionVariant> VariantProperty =
        AvaloniaProperty.Register<HeroSection, HeroSectionVariant>(nameof(Variant));

    /// <summary>Plays the entrance once when the hero appears.</summary>
    public static readonly StyledProperty<bool> IsEntranceAnimatedProperty =
        AvaloniaProperty.Register<HeroSection, bool>(nameof(IsEntranceAnimated), true);

    /// <summary>The main call to action; null shows the design's own.</summary>
    public static readonly StyledProperty<HeroAction?> PrimaryActionProperty =
        AvaloniaProperty.Register<HeroSection, HeroAction?>(nameof(PrimaryAction));

    /// <summary>The second call to action; null shows the design's own.</summary>
    public static readonly StyledProperty<HeroAction?> SecondaryActionProperty =
        AvaloniaProperty.Register<HeroSection, HeroAction?>(nameof(SecondaryAction));

    /// <summary>Your own headline. When set, the hero shows your content in the chosen layout instead of the designed sample.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<HeroSection, string?>(nameof(Title));

    /// <summary>Your own content: one or two sentences under the title.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<HeroSection, string?>(nameof(Description));

    /// <summary>Your own content: a short link above the title, for real news.</summary>
    public static readonly StyledProperty<HeroAction?> AnnouncementProperty =
        AvaloniaProperty.Register<HeroSection, HeroAction?>(nameof(Announcement));

    /// <summary>Your own content: a command shown with a copy button.</summary>
    public static readonly StyledProperty<string?> InstallCommandProperty =
        AvaloniaProperty.Register<HeroSection, string?>(nameof(InstallCommand));

    /// <summary>Your own content: a visual beside the copy in the Split layout.</summary>
    public static readonly StyledProperty<object?> MediaProperty =
        AvaloniaProperty.Register<HeroSection, object?>(nameof(Media));

    /// <summary>Your own content: small facts under the actions.</summary>
    public static readonly StyledProperty<AvaloniaList<string>?> MetaProperty =
        AvaloniaProperty.Register<HeroSection, AvaloniaList<string>?>(nameof(Meta));

    /// <summary>An action was pressed (before it opens its address or confirms in place).</summary>
    public static readonly RoutedEvent<CtaActionEventArgs> ActionInvokedEvent =
        RoutedEvent.Register<HeroSection, CtaActionEventArgs>(nameof(ActionInvoked), RoutingStrategies.Bubble);

    private static readonly HeroMeshPoint[] s_dashboardMesh =
    [
        new(0.1, 0.92, 0.7), new(0.92, 0.86, 0.66), new(0.14, 0.08, 0.64), new(0.86, 0.04, 0.6), new(0.5, 0.38, 0.46)
    ];

    private static readonly HeroMeshPoint[] s_editorialMesh =
    [
        new(0.1, 0.88, 0.72), new(0.86, 0.12, 0.66), new(0.16, 0.14, 0.56), new(0.62, 0.62, 0.5), new(0.94, 0.92, 0.48), new(0.44, 0.3, 0.34)
    ];

    private Panel? _sceneHost;
    private HeroMeshView? _dashboardMesh;
    private HeroMeshView? _editorialMesh;
    private HeroDotField? _dots;
    private HeroScene? _scene;
    private IDisposable? _viewportWatch;
    private bool _hasPlayed;

    /// <inheritdoc cref="VariantProperty"/>
    public HeroSectionVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <inheritdoc cref="IsEntranceAnimatedProperty"/>
    public bool IsEntranceAnimated { get => GetValue(IsEntranceAnimatedProperty); set => SetValue(IsEntranceAnimatedProperty, value); }

    /// <inheritdoc cref="PrimaryActionProperty"/>
    public HeroAction? PrimaryAction { get => GetValue(PrimaryActionProperty); set => SetValue(PrimaryActionProperty, value); }

    /// <inheritdoc cref="SecondaryActionProperty"/>
    public HeroAction? SecondaryAction { get => GetValue(SecondaryActionProperty); set => SetValue(SecondaryActionProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="AnnouncementProperty"/>
    public HeroAction? Announcement { get => GetValue(AnnouncementProperty); set => SetValue(AnnouncementProperty, value); }

    /// <inheritdoc cref="InstallCommandProperty"/>
    public string? InstallCommand { get => GetValue(InstallCommandProperty); set => SetValue(InstallCommandProperty, value); }

    /// <inheritdoc cref="MediaProperty"/>
    public object? Media { get => GetValue(MediaProperty); set => SetValue(MediaProperty, value); }

    /// <inheritdoc cref="MetaProperty"/>
    public AvaloniaList<string>? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    /// <inheritdoc cref="ActionInvokedEvent"/>
    public event EventHandler<CtaActionEventArgs>? ActionInvoked
    {
        add => AddHandler(ActionInvokedEvent, value);
        remove => RemoveHandler(ActionInvokedEvent, value);
    }

    private bool IsCustom => !string.IsNullOrEmpty(Title);

    internal HeroScene? Scene => _scene;

    internal HeroMeshView? DashboardMesh => _dashboardMesh;

    internal HeroMeshView? EditorialMesh => _editorialMesh;

    internal HeroDotField? Dots => _dots;

    /// <summary>Plays the entrance again, the way switching designs does in Arc's preview.</summary>
    public void ReplayEntrance()
    {
        _hasPlayed = false;
        Rebuild();
        PlayEntrance();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _sceneHost = e.NameScope.Find<Panel>(ScenePart);
        _dashboardMesh = e.NameScope.Find<HeroMeshView>("PART_DashboardMesh");
        _editorialMesh = e.NameScope.Find<HeroMeshView>("PART_EditorialMesh");
        _dots = e.NameScope.Find<HeroDotField>("PART_Dots");
        _dashboardMesh?.SetPoints(s_dashboardMesh);
        _editorialMesh?.SetPoints(s_editorialMesh);
        Rebuild();
        PlayEntrance();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _viewportWatch = ArcViewport.Observe(this, _ =>
        {
            InvalidateMeasure();
            UpdateSceneSize();
        });
        PlayEntrance();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _viewportWatch?.Dispose();
        _viewportWatch = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            UpdateSceneSize();
        }
        else if (change.Property == VariantProperty || change.Property == TitleProperty || change.Property == DescriptionProperty
                 || change.Property == PrimaryActionProperty || change.Property == SecondaryActionProperty
                 || change.Property == AnnouncementProperty || change.Property == InstallCommandProperty
                 || change.Property == MediaProperty || change.Property == MetaProperty)
        {
            var wasPlaying = change.Property == VariantProperty;
            if (wasPlaying)
            {
                _hasPlayed = false;
            }
            Rebuild();
            if (wasPlaying)
            {
                PlayEntrance();
            }
            else
            {
                _scene?.PlayEntrance(false);
            }
        }
    }

    private double ViewportHeight => ArcViewport.GetSize(this)?.Height is > 0 and var height ? height : 800;

    private void Rebuild()
    {
        if (_sceneHost is null)
        {
            return;
        }
        var custom = IsCustom;
        var variant = Variant;
        PseudoClasses.Set(CenteredPseudoClass, !custom && variant == HeroSectionVariant.Centered);
        PseudoClasses.Set(SplitPseudoClass, !custom && variant == HeroSectionVariant.Split);
        PseudoClasses.Set(MinimalPseudoClass, !custom && variant == HeroSectionVariant.Minimal);
        PseudoClasses.Set(CustomPseudoClass, custom);

        _sceneHost.Children.Clear();
        _scene = custom
            ? new HeroContentScene(this, variant)
            : variant switch
            {
                HeroSectionVariant.Split => new HeroRelayScene(this,
                    PrimaryAction ?? new HeroAction { Label = "Start building", ConfirmedLabel = "Workspace created" },
                    SecondaryAction ?? new HeroAction { Label = "Read the docs", ConfirmedLabel = "Opening docs" }),
                HeroSectionVariant.Minimal => new HeroEditorialScene(this,
                    PrimaryAction ?? new HeroAction { Label = "Get the app", ConfirmedLabel = "Download started" },
                    SecondaryAction ?? new HeroAction { Label = "Try it on the web", ConfirmedLabel = "Opening Weekly" }),
                _ => new HeroDashboardScene(this,
                    PrimaryAction ?? new HeroAction { Label = "Start free trial", ConfirmedLabel = "Trial started" },
                    SecondaryAction ?? new HeroAction { Label = "Book a demo", ConfirmedLabel = "Demo requested" })
            };
        _sceneHost.Children.Add(_scene);
        AutomationProperties.SetName(this, custom ? Title : "Hero");
        InvalidateMeasure();
        UpdateSceneSize();
    }

    private void PlayEntrance()
    {
        if (_scene is null || VisualRoot is null || _hasPlayed)
        {
            return;
        }
        _hasPlayed = true;
        _scene.PlayEntrance(IsEntranceAnimated);
    }

    private void UpdateSceneSize()
    {
        var width = Bounds.Width > 0 ? Bounds.Width : ArcViewport.GetSize(this)?.Width ?? 1280;
        _scene?.UpdateSize(width, ViewportHeight);
        if (_dots is not null)
        {
            _dots.IsStacked = width <= 900;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (IsCustom)
        {
            return base.MeasureOverride(availableSize);
        }
        // The designs are one full screen (min-height: 100svh): the height they are given, or the viewport's inside a scroller.
        // The dashboard design is exactly one screen and never shorter than 600.
        var height = double.IsInfinity(availableSize.Height) ? ViewportHeight : availableSize.Height;
        if (Variant == HeroSectionVariant.Centered)
        {
            height = Math.Max(600, height);
            return new Size(base.MeasureOverride(new Size(availableSize.Width, height)).Width, height);
        }
        // The other two grow past one screen when their content needs it, as min-height does.
        var size = base.MeasureOverride(new Size(availableSize.Width, double.PositiveInfinity));
        return new Size(size.Width, Math.Max(height, size.Height));
    }
}
