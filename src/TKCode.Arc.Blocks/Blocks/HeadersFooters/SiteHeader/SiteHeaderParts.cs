// Ported from Arc's free site-header block (MIT, Copyright (c) 2026 Elia Kuratli): the interactive parts of site-header.tsx.
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Base of the header's buttons (internal): the hover state only for a mouse or pen (Arc's
/// <c>(hover: hover) and (pointer: fine)</c>) as <c>:fine-hover</c>, an expanded state (<c>:expanded</c>), a current
/// state (<c>:current</c>) and an automation peer that reports both.
/// </summary>
internal abstract class SiteHeaderButtonBase : AvaloniaButton
{
    public static readonly StyledProperty<bool> IsExpandedProperty = AvaloniaProperty.Register<SiteHeaderButtonBase, bool>(nameof(IsExpanded));

    public static readonly StyledProperty<bool> IsExpandableProperty = AvaloniaProperty.Register<SiteHeaderButtonBase, bool>(nameof(IsExpandable));

    public static readonly StyledProperty<bool> IsCurrentProperty = AvaloniaProperty.Register<SiteHeaderButtonBase, bool>(nameof(IsCurrent));

    static SiteHeaderButtonBase()
    {
        FocusableProperty.OverrideDefaultValue<SiteHeaderButtonBase>(true);
    }

    /// <summary>True while the thing the button opens is open.</summary>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <summary>True when the button opens something (a trigger).</summary>
    public bool IsExpandable { get => GetValue(IsExpandableProperty); set => SetValue(IsExpandableProperty, value); }

    /// <summary>True for the button of the current page.</summary>
    public bool IsCurrent { get => GetValue(IsCurrentProperty); set => SetValue(IsCurrentProperty, value); }

    /// <summary>True while a mouse or pen is over the button.</summary>
    public bool IsFineHover { get; private set; }

    /// <summary>Raised on every pointer enter of a mouse or pen (the header's hover intent).</summary>
    public event EventHandler<PointerEventArgs>? FinePointerEntered;

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            IsFineHover = true;
            PseudoClasses.Set(":fine-hover", true);
            FinePointerEntered?.Invoke(this, e);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        IsFineHover = false;
        PseudoClasses.Set(":fine-hover", false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            PseudoClasses.Set(":expanded", IsExpanded);
        }
        else if (change.Property == IsCurrentProperty)
        {
            PseudoClasses.Set(":current", IsCurrent);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SiteHeaderButtonPeer(this);

    /// <summary>A button that reports expanded / collapsed for triggers and selected for the current page.</summary>
    private sealed class SiteHeaderButtonPeer : ButtonAutomationPeer, IExpandCollapseProvider, ISelectionItemProvider
    {
        public SiteHeaderButtonPeer(SiteHeaderButtonBase owner) : base(owner)
        {
        }

        private SiteHeaderButtonBase Button => (SiteHeaderButtonBase)Owner;

        public ExpandCollapseState ExpandCollapseState => !Button.IsExpandable
            ? ExpandCollapseState.LeafNode
            : Button.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (Button.IsExpandable && !Button.IsExpanded)
            {
                Invoke();
            }
        }

        public void Collapse()
        {
            if (Button.IsExpandable && Button.IsExpanded)
            {
                Invoke();
            }
        }

        public bool IsSelected => Button.IsCurrent;

        public ISelectionProvider? SelectionContainer => null;

        public void Select() => Invoke();

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }
    }
}

/// <summary>A navigation item of the bar: a label, with a chevron when it opens a panel.</summary>
internal sealed class SiteNavButton : SiteHeaderButtonBase
{
    public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<SiteNavButton, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<bool> HasChevronProperty = AvaloniaProperty.Register<SiteNavButton, bool>(nameof(HasChevron));

    public static readonly StyledProperty<bool> IsCapsuleProperty = AvaloniaProperty.Register<SiteNavButton, bool>(nameof(IsCapsule));

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public bool HasChevron { get => GetValue(HasChevronProperty); set => SetValue(HasChevronProperty, value); }

    /// <summary>The centered layout's item (32 high, pill).</summary>
    public bool IsCapsule { get => GetValue(IsCapsuleProperty); set => SetValue(IsCapsuleProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HasChevronProperty)
        {
            PseudoClasses.Set(":chevron", HasChevron);
        }
        else if (change.Property == IsCapsuleProperty)
        {
            PseudoClasses.Set(":capsule", IsCapsule);
        }
    }
}

/// <summary>The brand: mark and name (the header puts them in <see cref="ContentControl.Content"/>).</summary>
internal sealed class SiteBrandButton : SiteHeaderButtonBase
{
}

/// <summary>An action of the bar or the sheet: primary or secondary.</summary>
internal sealed class SiteActionButton : SiteHeaderButtonBase
{
    public static readonly StyledProperty<bool> IsPrimaryProperty = AvaloniaProperty.Register<SiteActionButton, bool>(nameof(IsPrimary));

    public static readonly StyledProperty<bool> IsInSheetProperty = AvaloniaProperty.Register<SiteActionButton, bool>(nameof(IsInSheet));

    public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<SiteActionButton, string>(nameof(Label), string.Empty);

    static SiteActionButton()
    {
        IsPrimaryProperty.Changed.AddClassHandler<SiteActionButton>((b, _) => b.UpdateKind());
        IsInSheetProperty.Changed.AddClassHandler<SiteActionButton>((b, _) => b.PseudoClasses.Set(":sheet", b.IsInSheet));
    }

    public SiteActionButton()
    {
        UpdateKind();
    }

    public bool IsPrimary { get => GetValue(IsPrimaryProperty); set => SetValue(IsPrimaryProperty, value); }

    /// <summary>True in the menu sheet (taller, a border on the secondary action).</summary>
    public bool IsInSheet { get => GetValue(IsInSheetProperty); set => SetValue(IsInSheetProperty, value); }

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    private void UpdateKind()
    {
        PseudoClasses.Set(":primary", IsPrimary);
        PseudoClasses.Set(":secondary", !IsPrimary);
    }
}

/// <summary>The menu button of narrow layouts: a 40px square with the swapping menu / close icon.</summary>
internal sealed class SiteMenuButton : SiteHeaderButtonBase
{
}

/// <summary>A link of a mega panel: icon, title and description.</summary>
internal sealed class SitePanelLink : SiteHeaderButtonBase
{
    public static readonly StyledProperty<Geometry?> IconProperty = AvaloniaProperty.Register<SitePanelLink, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<SitePanelLink, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<SitePanelLink, string?>(nameof(Description));

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty)
        {
            PseudoClasses.Set(":has-icon", Icon is not null);
        }
        else if (change.Property == DescriptionProperty)
        {
            PseudoClasses.Set(":has-description", !string.IsNullOrEmpty(Description));
        }
    }
}

/// <summary>The feature card of a mega panel.</summary>
internal sealed class SiteFeatureCard : SiteHeaderButtonBase
{
    public static readonly StyledProperty<IImage?> ImageProperty = AvaloniaProperty.Register<SiteFeatureCard, IImage?>(nameof(Image));

    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<SiteFeatureCard, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<SiteFeatureCard, string?>(nameof(Description));

    public IImage? Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ImageProperty)
        {
            PseudoClasses.Set(":has-image", Image is not null);
        }
        else if (change.Property == DescriptionProperty)
        {
            PseudoClasses.Set(":has-description", !string.IsNullOrEmpty(Description));
        }
    }
}

/// <summary>A row of the menu sheet: a label, with a chevron for a group or a dot for the current page.</summary>
internal sealed class SiteSheetRow : SiteHeaderButtonBase
{
    public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<SiteSheetRow, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<bool> IsGroupProperty = AvaloniaProperty.Register<SiteSheetRow, bool>(nameof(IsGroup));

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public bool IsGroup { get => GetValue(IsGroupProperty); set => SetValue(IsGroupProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsGroupProperty)
        {
            PseudoClasses.Set(":group", IsGroup);
        }
    }
}

/// <summary>A link inside a group of the menu sheet.</summary>
internal sealed class SiteSheetLink : SiteHeaderButtonBase
{
    public static readonly StyledProperty<Geometry?> IconProperty = AvaloniaProperty.Register<SiteSheetLink, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<SiteSheetLink, string>(nameof(Label), string.Empty);

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty)
        {
            PseudoClasses.Set(":has-icon", Icon is not null);
        }
    }
}

/// <summary>
/// Fades and lowers its content in once it is attached (Arc: <c>initial {opacity 0, y -6}</c>, 240 ms on the enter
/// curve, delayed 40 ms + 35 ms per row): the rows of the menu sheet. Reduced motion and Off show it at once.
/// </summary>
/// <remarks>Code-driven because the delay depends on the row's position and the effect must not replay on re-layout.</remarks>
internal sealed class SiteRise : Decorator
{
    public static readonly StyledProperty<TimeSpan> DelayProperty = AvaloniaProperty.Register<SiteRise, TimeSpan>(nameof(Delay));

    private readonly TranslateTransform _translate = new();
    private MotionValue<double>? _progress;

    public SiteRise()
    {
        RenderTransform = _translate;
    }

    public TimeSpan Delay { get => GetValue(DelayProperty); set => SetValue(DelayProperty, value); }

    /// <summary>The entrance progress (0 hidden, 1 shown); tests read it.</summary>
    public double Progress => _progress?.Value ?? 1;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_progress is not null || !ArcMotion.IsFull(this))
        {
            return;
        }
        _progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        _progress.Changed += v =>
        {
            Opacity = v;
            _translate.Y = -6 * (1 - v);
        };
        _progress.Jump(0);
        _ = _progress.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, Delay));
    }
}

/// <summary>
/// The menu button's icon: Menu and X swap with Arc's popLayout transition. The new icon turns in from ±45 degrees and
/// from scale .8 on the Snappy spring {.26, 0} while fading in over 120 ms; the old one turns away the other way and fades.
/// Reduced motion only fades (120 ms), Off swaps at once.
/// </summary>
/// <remarks>Code-driven: the outgoing icon must stay on screen after the state changed, and both run at the same time.</remarks>
internal sealed class SiteMenuIcon : Panel
{
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<SiteMenuIcon, bool>(nameof(IsOpen));

    public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<SiteMenuIcon, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<double> SizeProperty = AvaloniaProperty.Register<SiteMenuIcon, double>(nameof(Size), 20);

    public static readonly StyledProperty<double> StrokeProperty = AvaloniaProperty.Register<SiteMenuIcon, double>(nameof(Stroke), 1.75);

    private static readonly SpringSpec s_snappy = SpringSpec.FromVisual(0.26, 0);

    private readonly Layer _menu;
    private readonly Layer _close;

    static SiteMenuIcon()
    {
        AffectsMeasure<SiteMenuIcon>(SizeProperty);
        IsOpenProperty.Changed.AddClassHandler<SiteMenuIcon>((icon, e) => icon.OnOpenChanged(e.GetNewValue<bool>()));
        IsHitTestVisibleProperty.OverrideDefaultValue<SiteMenuIcon>(false);
    }

    public SiteMenuIcon()
    {
        _menu = new Layer(this, ArcIcons.Menu, 1);
        _close = new Layer(this, ArcIcons.X, 0);
        Children.Add(_menu.Glyph);
        Children.Add(_close.Glyph);
        _close.Hide();
        Bind(_menu.Glyph, _close.Glyph);
    }

    private void Bind(params ArcGlyph[] glyphs)
    {
        foreach (var glyph in glyphs)
        {
            glyph.Bind(ArcGlyph.ForegroundProperty, this.GetObservable(ForegroundProperty));
            glyph.Bind(ArcGlyph.SizeProperty, this.GetObservable(SizeProperty));
            glyph.Bind(ArcGlyph.StrokeThicknessProperty, this.GetObservable(StrokeProperty));
        }
    }

    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public double Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    /// <summary>True while the swap is still moving (tests read it).</summary>
    public bool IsSwapping => _menu.Busy || _close.Busy;

    /// <summary>The icon shown at rest (Menu or X) and its opacity.</summary>
    public double MenuOpacity => _menu.Glyph.Opacity;

    public double CloseOpacity => _close.Glyph.Opacity;

    protected override Size MeasureOverride(Size availableSize)
    {
        _menu.Glyph.Measure(availableSize);
        _close.Glyph.Measure(availableSize);
        return new Size(Size, Size);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _menu.Glyph.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
        _close.Glyph.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
        return finalSize;
    }

    private void OnOpenChanged(bool open)
    {
        var entering = open ? _close : _menu;
        var leaving = open ? _menu : _close;
        // Arc: the new icon starts at rotate(menuOpen ? -45 : 45), the old one leaves to rotate(menuOpen ? 45 : -45).
        entering.Enter(open ? -1 : 1);
        leaving.Leave(open ? 1 : -1);
    }

    private sealed class Layer
    {
        private readonly RotateTransform _rotate = new();
        private readonly ScaleTransform _scale = new();
        private MotionValue<double>? _pose;
        private MotionValue<double>? _opacity;
        private readonly SiteMenuIcon _owner;

        public Layer(SiteMenuIcon owner, Geometry data, double opacity)
        {
            _owner = owner;
            Glyph = new ArcGlyph { Data = data, Opacity = opacity, IsHitTestVisible = false, RenderTransformOrigin = RelativePoint.Center };
            Glyph.RenderTransform = new TransformGroup { Children = { _scale, _rotate } };
        }

        public ArcGlyph Glyph { get; }

        public bool Busy => _pose?.IsAnimating == true || _opacity?.IsAnimating == true;

        private MotionValue<double> Pose()
        {
            if (_pose is null)
            {
                _pose = new MotionValue<double>(_owner, 0, MotionVectors.Double);
                _pose.Changed += Apply;
            }
            return _pose;
        }

        private MotionValue<double> Fade()
        {
            if (_opacity is null)
            {
                _opacity = new MotionValue<double>(_owner, Glyph.Opacity, MotionVectors.Double);
                _opacity.Changed += v => Glyph.Opacity = v;
            }
            return _opacity;
        }

        // The pose runs from -1..1 (in units of 45 degrees); the scale drops by .2 at either end.
        private void Apply(double pose)
        {
            _rotate.Angle = pose * 45;
            _scale.ScaleX = _scale.ScaleY = 1 - 0.2 * Math.Abs(pose);
        }

        public void Hide()
        {
            Glyph.Opacity = 0;
            Fade().Jump(0);
        }

        public void Enter(double from)
        {
            var level = ArcMotion.GetEffectiveLevel(_owner);
            if (level == ArcMotionLevel.Full)
            {
                Pose().Jump(from);
                _ = Pose().AnimateTo(0, MotionSpec.Spring(s_snappy));
            }
            else
            {
                Pose().Jump(0);
            }
            Fade().Jump(0);
            _ = Fade().AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }

        public void Leave(double to)
        {
            var level = ArcMotion.GetEffectiveLevel(_owner);
            if (level == ArcMotionLevel.Full)
            {
                _ = Pose().AnimateTo(to, MotionSpec.Spring(s_snappy));
            }
            _ = Fade().AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
    }
}
