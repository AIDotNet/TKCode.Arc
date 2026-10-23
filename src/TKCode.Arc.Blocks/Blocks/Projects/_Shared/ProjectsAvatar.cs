using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// Draws <see cref="ProjectPerson"/> avatars for the Projects blocks: the picture clipped to a circle, or the initials on a
/// soft color from the person's hue (light: a pale tint with dark letters; dark: a deep tint with light letters). A
/// surface-colored ring can separate overlapping avatars (Arc's 2px stack ring). Brushes and text are cached per painter;
/// call <see cref="Invalidate"/> when the theme changes.
/// </summary>
internal sealed class ProjectsAvatarPainter
{
    private readonly Dictionary<int, (IBrush Fill, IBrush Ink)> _tints = new();
    private readonly Dictionary<string, FormattedText> _texts = new(StringComparer.Ordinal);
    private IBrush? _ring;
    private IPen? _hairline;
    private Typeface _typeface;
    private bool _dark;
    private bool _ready;

    /// <summary>Forgets cached brushes and text (theme or font change).</summary>
    public void Invalidate()
    {
        _ready = false;
        _tints.Clear();
        _texts.Clear();
    }

    private void EnsureColors(Control owner)
    {
        if (_ready)
        {
            return;
        }
        _ready = true;
        var surface = ArcTokens.GetColor(owner, ArcDesignTokenKind.Surface);
        _dark = Oklch.FromColor(surface).L < 0.5;
        _ring = new ImmutableSolidColorBrush(surface);
        _hairline = new ImmutablePen(new ImmutableSolidColorBrush(
            Oklch.WithAlpha(ArcTokens.GetColor(owner, ArcDesignTokenKind.Foreground), _dark ? 0.10 : 0.06)), 1);
        FontFamily family;
        try
        {
            family = ArcTokens.Get<FontFamily>(owner, ArcDesignTokenKind.BodyFontFamily) ?? FontFamily.Default;
        }
        catch (Exception)
        {
            // ArcTokens.Get<T> throws without a theme context (a detached owner); the default font is fine for one frame.
            family = FontFamily.Default;
        }
        _typeface = new Typeface(family, FontStyle.Normal, FontWeight.SemiBold);
    }

    private (IBrush Fill, IBrush Ink) Tint(double hue)
    {
        var key = (int)Math.Round(hue);
        if (!_tints.TryGetValue(key, out var tint))
        {
            var fill = _dark ? Oklch.ToColor(0.36, 0.055, hue) : Oklch.ToColor(0.925, 0.045, hue);
            var ink = _dark ? Oklch.ToColor(0.90, 0.06, hue) : Oklch.ToColor(0.40, 0.09, hue);
            tint = (new ImmutableSolidColorBrush(fill), new ImmutableSolidColorBrush(ink));
            _tints[key] = tint;
        }
        return tint;
    }

    /// <summary>Draws one avatar centred on <paramref name="center"/>.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="owner">The control that reads the theme tokens.</param>
    /// <param name="person">The person (null draws an empty tinted circle).</param>
    /// <param name="center">The circle's centre.</param>
    /// <param name="radius">The circle's radius (without the ring).</param>
    /// <param name="ring">Width of the surface-colored ring drawn outside the circle (0 for none).</param>
    /// <param name="textShift">Horizontal shift of the initials (a stacked avatar centres them in the part left uncovered).</param>
    public void Draw(DrawingContext context, Control owner, ProjectPerson? person, Point center, double radius, double ring, double textShift = 0)
    {
        if (radius <= 0)
        {
            return;
        }
        EnsureColors(owner);
        if (ring > 0)
        {
            context.DrawEllipse(_ring, null, center, radius + ring, radius + ring);
        }
        var box = new Rect(center.X - radius, center.Y - radius, 2 * radius, 2 * radius);
        if (person?.Avatar is { } image)
        {
            using (context.PushGeometryClip(new EllipseGeometry(box)))
            {
                context.DrawImage(image, box);
            }
            context.DrawEllipse(null, _hairline, center, radius - 0.5, radius - 0.5);
            return;
        }
        var (fill, ink) = Tint(person?.DisplayHue ?? 0);
        context.DrawEllipse(fill, null, center, radius, radius);
        var text = person?.DisplayInitials;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        // Initials take about 39% of the diameter (11px on a 28px avatar, 8.5px on 22px), quantized to half pixels.
        var size = Math.Max(7, Math.Round(radius * 2 * 0.39 * 2) / 2);
        var key = string.Create(CultureInfo.InvariantCulture, $"{text}|{size}|{(int)Math.Round(person!.DisplayHue)}");
        if (!_texts.TryGetValue(key, out var formatted))
        {
            formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _typeface, size, ink);
            if (_texts.Count > 256)
            {
                _texts.Clear();
            }
            _texts[key] = formatted;
        }
        context.DrawText(formatted, new Point(center.X + textShift - formatted.WidthIncludingTrailingWhitespace / 2, center.Y - formatted.Height / 2));
    }
}

/// <summary>
/// One generated avatar (<see cref="ProjectsAvatarPainter"/>) sized by the control: the circle fills the smaller side.
/// Not interactive; screen readers read the person's name unless <see cref="IsDecorative"/>.
/// </summary>
internal sealed class ProjectsAvatar : Control
{
    /// <summary>The person shown.</summary>
    public static readonly StyledProperty<ProjectPerson?> PersonProperty =
        AvaloniaProperty.Register<ProjectsAvatar, ProjectPerson?>(nameof(Person));

    /// <summary>Width of the surface-colored ring drawn outside the circle (0 for none).</summary>
    public static readonly StyledProperty<double> RingProperty =
        AvaloniaProperty.Register<ProjectsAvatar, double>(nameof(Ring));

    /// <summary>True hides the avatar from screen readers (the name is already said next to it).</summary>
    public static readonly StyledProperty<bool> IsDecorativeProperty =
        AvaloniaProperty.Register<ProjectsAvatar, bool>(nameof(IsDecorative));

    private readonly ProjectsAvatarPainter _painter = new();
    private ProjectPerson? _subscribed;

    static ProjectsAvatar()
    {
        AffectsRender<ProjectsAvatar>(PersonProperty, RingProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ProjectsAvatar>(false);
        FocusableProperty.OverrideDefaultValue<ProjectsAvatar>(false);
    }

    public ProjectsAvatar()
    {
        ResourcesChanged += (_, _) => Refresh();
        ActualThemeVariantChanged += (_, _) => Refresh();
    }

    /// <inheritdoc cref="PersonProperty"/>
    public ProjectPerson? Person { get => GetValue(PersonProperty); set => SetValue(PersonProperty, value); }

    /// <inheritdoc cref="RingProperty"/>
    public double Ring { get => GetValue(RingProperty); set => SetValue(RingProperty, value); }

    /// <inheritdoc cref="IsDecorativeProperty"/>
    public bool IsDecorative { get => GetValue(IsDecorativeProperty); set => SetValue(IsDecorativeProperty, value); }

    private void Refresh()
    {
        _painter.Invalidate();
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PersonProperty)
        {
            if (_subscribed is not null)
            {
                _subscribed.PropertyChanged -= OnPersonChanged;
            }
            _subscribed = Person;
            if (_subscribed is not null)
            {
                _subscribed.PropertyChanged += OnPersonChanged;
            }
            UpdateName();
        }
        else if (change.Property == IsDecorativeProperty)
        {
            UpdateName();
        }
    }

    private void OnPersonChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        UpdateName();
        InvalidateVisual();
    }

    private void UpdateName()
    {
        AutomationProperties.SetName(this, IsDecorative ? string.Empty : Person?.Name ?? string.Empty);
        AutomationProperties.SetAccessibilityView(this, IsDecorative ? AccessibilityView.Raw : AccessibilityView.Content);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ProjectsImagePeer(this);

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        _painter.Draw(context, this, Person, new Point(Bounds.Width / 2, Bounds.Height / 2), size / 2, Ring);
    }
}

/// <summary>
/// A row of overlapping generated avatars (Arc's static owner / team / attendee stacks): each avatar is
/// <see cref="Diameter"/> wide, overlaps the previous one by <see cref="Overlap"/>, and a surface ring
/// (<see cref="Ring"/>) cuts it from its neighbour; later avatars draw over earlier ones. No hover, no tooltip, not
/// focusable. Screen readers read <see cref="Label"/> (or "A, B and C").
/// </summary>
internal sealed class ProjectsAvatarStack : Control
{
    /// <summary>The people, in drawing order.</summary>
    public static readonly StyledProperty<IEnumerable?> PeopleProperty =
        AvaloniaProperty.Register<ProjectsAvatarStack, IEnumerable?>(nameof(People));

    /// <summary>Avatar diameter (28).</summary>
    public static readonly StyledProperty<double> DiameterProperty =
        AvaloniaProperty.Register<ProjectsAvatarStack, double>(nameof(Diameter), 28);

    /// <summary>How far each avatar overlaps the previous one (6).</summary>
    public static readonly StyledProperty<double> OverlapProperty =
        AvaloniaProperty.Register<ProjectsAvatarStack, double>(nameof(Overlap), 6);

    /// <summary>Width of the surface ring around each avatar (2).</summary>
    public static readonly StyledProperty<double> RingProperty =
        AvaloniaProperty.Register<ProjectsAvatarStack, double>(nameof(Ring), 2);

    /// <summary>At most this many avatars are drawn (0 = all).</summary>
    public static readonly StyledProperty<int> MaxVisibleProperty =
        AvaloniaProperty.Register<ProjectsAvatarStack, int>(nameof(MaxVisible));

    /// <summary>The screen reader name; null reads "<see cref="LabelPrefix"/>A, B and C".</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ProjectsAvatarStack, string?>(nameof(Label));

    /// <summary>Text put before the generated name list ("Attendees: ").</summary>
    public static readonly StyledProperty<string?> LabelPrefixProperty =
        AvaloniaProperty.Register<ProjectsAvatarStack, string?>(nameof(LabelPrefix));

    /// <summary>The word joining the last two names in the generated label ("and").</summary>
    public static readonly StyledProperty<string> AndWordProperty =
        AvaloniaProperty.Register<ProjectsAvatarStack, string>(nameof(AndWord), "and");

    private readonly ProjectsAvatarPainter _painter = new();
    private readonly List<ProjectPerson> _people = new();
    private INotifyCollectionChanged? _observed;

    static ProjectsAvatarStack()
    {
        AffectsMeasure<ProjectsAvatarStack>(PeopleProperty, DiameterProperty, OverlapProperty, MaxVisibleProperty);
        AffectsRender<ProjectsAvatarStack>(RingProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ProjectsAvatarStack>(false);
        FocusableProperty.OverrideDefaultValue<ProjectsAvatarStack>(false);
    }

    public ProjectsAvatarStack()
    {
        ResourcesChanged += (_, _) => Refresh();
        ActualThemeVariantChanged += (_, _) => Refresh();
    }

    /// <inheritdoc cref="PeopleProperty"/>
    public IEnumerable? People { get => GetValue(PeopleProperty); set => SetValue(PeopleProperty, value); }

    /// <inheritdoc cref="DiameterProperty"/>
    public double Diameter { get => GetValue(DiameterProperty); set => SetValue(DiameterProperty, value); }

    /// <inheritdoc cref="OverlapProperty"/>
    public double Overlap { get => GetValue(OverlapProperty); set => SetValue(OverlapProperty, value); }

    /// <inheritdoc cref="RingProperty"/>
    public double Ring { get => GetValue(RingProperty); set => SetValue(RingProperty, value); }

    /// <inheritdoc cref="MaxVisibleProperty"/>
    public int MaxVisible { get => GetValue(MaxVisibleProperty); set => SetValue(MaxVisibleProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="LabelPrefixProperty"/>
    public string? LabelPrefix { get => GetValue(LabelPrefixProperty); set => SetValue(LabelPrefixProperty, value); }

    /// <inheritdoc cref="AndWordProperty"/>
    public string AndWord { get => GetValue(AndWordProperty); set => SetValue(AndWordProperty, value); }

    /// <summary>The people drawn right now (tests).</summary>
    public IReadOnlyList<ProjectPerson> VisiblePeople => _people;

    private void Refresh()
    {
        _painter.Invalidate();
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PeopleProperty)
        {
            if (_observed is not null)
            {
                _observed.CollectionChanged -= OnPeopleChanged;
            }
            _observed = People as INotifyCollectionChanged;
            if (_observed is not null)
            {
                _observed.CollectionChanged += OnPeopleChanged;
            }
            Rebuild();
        }
        else if (change.Property == MaxVisibleProperty || change.Property == LabelProperty
                 || change.Property == LabelPrefixProperty || change.Property == AndWordProperty)
        {
            Rebuild();
        }
    }

    private void OnPeopleChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        _people.Clear();
        if (People is not null)
        {
            foreach (var item in People)
            {
                if (item is ProjectPerson person)
                {
                    if (MaxVisible > 0 && _people.Count >= MaxVisible)
                    {
                        break;
                    }
                    _people.Add(person);
                }
            }
        }
        var label = Label ?? (LabelPrefix ?? string.Empty) + ProjectsText.JoinNames(_people.Select(p => p.Name), AndWord);
        AutomationProperties.SetName(this, label);
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ProjectsImagePeer(this);

    protected override Size MeasureOverride(Size availableSize)
    {
        var n = _people.Count;
        if (n == 0)
        {
            return default;
        }
        return new Size(n * Diameter - (n - 1) * Overlap, Diameter);
    }

    public override void Render(DrawingContext context)
    {
        var r = Diameter / 2;
        var cy = Bounds.Height / 2;
        for (var i = 0; i < _people.Count; i++)
        {
            var cx = r + i * (Diameter - Overlap);
            // The next avatar covers Overlap + Ring of this one: centre the initials in what stays visible.
            var shift = i < _people.Count - 1 ? -(Overlap + Ring) / 2 : 0;
            _painter.Draw(context, this, _people[i], new Point(cx, cy), r, Ring, shift);
        }
    }
}

/// <summary>An automation peer that reports an image with the control's name.</summary>
internal sealed class ProjectsImagePeer(Control owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

    protected override bool IsContentElementCore() => AutomationProperties.GetAccessibilityView(Owner) != AccessibilityView.Raw;

    protected override bool IsControlElementCore() => AutomationProperties.GetAccessibilityView(Owner) != AccessibilityView.Raw;
}
