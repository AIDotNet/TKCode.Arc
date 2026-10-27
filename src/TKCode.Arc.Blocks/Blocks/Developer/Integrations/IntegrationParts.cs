using System.Globalization;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The presence of a connected row's settings button (Arc design reference): it arrives 140ms after the connect, fading in
/// over 240ms (enter curve) out of a 4px blur while sliding 8px from the right on a spring (k 351, c 36); it leaves by
/// fading into a 2px blur in 160ms without moving, then collapses. Code-driven because enter and exit differ in blur,
/// travel and delay and the slide is a spring, which one ControlTheme transition per property cannot express. Reduced
/// motion: 120ms fades only; Off: it jumps. <see cref="IsSuppressed"/> hides it whatever its state (narrow rows).
/// </summary>
internal sealed class IntegrationReveal : Decorator
{
    private static readonly SpringSpec SlideSpring = SpringSpec.FromPhysical(351, 36);
    private static readonly TimeSpan EnterDelay = TimeSpan.FromMilliseconds(140);

    private readonly TranslateTransform _translate = new();
    private MotionValue<double>? _fade;
    private MotionValue<double>? _slide;
    private bool _isShown;
    private bool _isSuppressed;
    private bool _entering;
    private int _generation;

    public IntegrationReveal()
    {
        RenderTransform = _translate;
        IsVisible = false;
    }

    /// <summary>Whether it is (or is becoming) shown.</summary>
    public bool IsShown => _isShown;

    /// <summary>Hides it regardless of <see cref="IsShown"/> (the compact row has no settings button).</summary>
    public bool IsSuppressed
    {
        get => _isSuppressed;
        set
        {
            _isSuppressed = value;
            UpdateVisibility();
        }
    }

    /// <summary>Shows or hides the child; <paramref name="animate"/> plays the measured motion (per the motion level).</summary>
    public void Show(bool shown, bool animate)
    {
        if (shown == _isShown && (_fade is null || !_fade.IsAnimating))
        {
            if (!animate)
            {
                Snap(shown);
            }
            return;
        }
        _isShown = shown;
        var generation = ++_generation;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || level == ArcMotionLevel.Off || VisualRoot is null)
        {
            Snap(shown);
            return;
        }
        EnsureValues();
        IsHitTestVisible = shown;
        _entering = shown;
        if (shown)
        {
            UpdateVisibility();
            var full = level == ArcMotionLevel.Full;
            _fade!.Jump(0);
            _slide!.Jump(full ? 8 : 0);
            _ = _fade.AnimateTo(1, full ? DeveloperSprings.FadeIn(EnterDelay) : DeveloperSprings.ReducedFade());
            if (full)
            {
                _ = _slide.AnimateTo(0, MotionSpec.Spring(SlideSpring, EnterDelay));
            }
            return;
        }
        _slide!.Jump(0);
        _ = HideAsync(generation, level == ArcMotionLevel.Full);
    }

    private async Task HideAsync(int generation, bool full)
    {
        await _fade!.AnimateTo(0, full ? DeveloperSprings.FadeOut() : DeveloperSprings.ReducedFade());
        if (generation == _generation && !_isShown)
        {
            UpdateVisibility();
        }
    }

    private void Snap(bool shown)
    {
        _isShown = shown;
        _generation++;
        _fade?.Jump(shown ? 1 : 0);
        _slide?.Jump(0);
        _entering = shown;
        IsHitTestVisible = shown;
        Apply();
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        var fading = _fade is { IsAnimating: true };
        IsVisible = !_isSuppressed && (_isShown || fading);
    }

    private void EnsureValues()
    {
        if (_fade is not null)
        {
            return;
        }
        _fade = new MotionValue<double>(this, _isShown ? 1 : 0, MotionVectors.Double);
        _slide = new MotionValue<double>(this, 0, MotionVectors.Double);
        _fade.Changed += _ => Apply();
        _slide.Changed += _ => Apply();
    }

    private void Apply()
    {
        var fade = _fade?.Value ?? (_isShown ? 1 : 0);
        Opacity = fade;
        _translate.X = _slide?.Value ?? 0;
        // Enter: blur 4 → 0; exit: 0 → 2 (Full only).
        var blur = ArcMotion.IsFull(this) && fade < 1 ? (1 - fade) * (_entering ? 4 : 2) : 0;
        Effect = ArcBlur.Get(blur);
    }
}

/// <summary>
/// An app's 20×20 mark, drawn without a plate (Arc design reference): <see cref="IntegrationItem.Logo"/> when it is an
/// image or a control, a geometry filled with the mark brush, or (no logo) a generated mark: the monogram on a rounded tile
/// in a chart series color picked from the key. Never a third-party logo of ours.
/// </summary>
internal sealed class IntegrationMark : Decorator
{
    /// <summary>The app.</summary>
    public static readonly StyledProperty<IntegrationItem?> ItemProperty =
        AvaloniaProperty.Register<IntegrationMark, IntegrationItem?>(nameof(Item));

    private static readonly ArcDesignTokenKind[] SeriesKinds =
    [
        ArcDesignTokenKind.Series1, ArcDesignTokenKind.Series2, ArcDesignTokenKind.Series3, ArcDesignTokenKind.Series4,
    ];

    private IntegrationItem? _observed;
    private FormattedText? _letter;
    private string? _letterText;
    private IBrush? _tileBrush;
    private Color? _letterColor;
    private Geometry? _geometry;

    public IntegrationMark()
    {
        ResourcesChanged += (_, _) => Invalidate();
        ActualThemeVariantChanged += (_, _) => Invalidate();
        Width = 20;
        Height = 20;
        ClipToBounds = false;
    }

    /// <inheritdoc cref="ItemProperty"/>
    public IntegrationItem? Item { get => GetValue(ItemProperty); set => SetValue(ItemProperty, value); }

    /// <summary>The series index a key maps to (stable across runs: an ordinal hash).</summary>
    internal static int SeriesIndex(string key)
    {
        unchecked
        {
            var hash = 17;
            foreach (var c in key)
            {
                hash = hash * 31 + c;
            }
            return (hash & 0x7fffffff) % SeriesKinds.Length;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemProperty)
        {
            if (_observed is not null)
            {
                _observed.PropertyChanged -= OnItemChanged;
            }
            _observed = Item;
            if (_observed is not null)
            {
                _observed.PropertyChanged += OnItemChanged;
            }
            Rebuild();
        }
    }


    private void OnItemChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IntegrationItem.LogoProperty || e.Property == IntegrationItem.MonogramProperty ||
            e.Property == IntegrationItem.NameProperty || e.Property == IntegrationItem.MarkBrushProperty ||
            e.Property == IntegrationItem.KeyProperty)
        {
            Rebuild();
        }
    }

    private void Invalidate()
    {
        _tileBrush = null;
        _letterColor = null;
        _letter = null;
        InvalidateVisual();
    }

    private void Rebuild()
    {
        var logo = Item?.Logo;
        _geometry = logo as Geometry;
        Child = logo switch
        {
            IImage image => new Image { Source = image, Width = 20, Height = 20, Stretch = Stretch.Uniform },
            Control control when control.Parent is null => control,
            null or Geometry => null,
            _ => new ContentPresenter { Content = logo, Width = 20, Height = 20 },
        };
        Invalidate();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Item is not { } item || Child is not null)
        {
            return;
        }
        var bounds = new Rect(0, 0, 20, 20);
        var brush = item.MarkBrush ?? (_tileBrush ??= ResolveSeries(item));
        if (brush is null)
        {
            return;
        }
        if (_geometry is { } geometry)
        {
            var g = geometry.Bounds;
            if (g.Width > 0 && g.Height > 0)
            {
                var scale = Math.Min(20 / g.Width, 20 / g.Height);
                var offset = new Point((20 - g.Width * scale) / 2 - g.X * scale, (20 - g.Height * scale) / 2 - g.Y * scale);
                using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset.X, offset.Y)))
                {
                    context.DrawGeometry(brush, null, geometry);
                }
            }
            return;
        }
        context.DrawRectangle(brush, null, new RoundedRect(bounds, 6));
        var text = MonogramOf(item);
        if (text.Length == 0)
        {
            return;
        }
        if (_letter is null || _letterText != text)
        {
            _letterText = text;
            _letterColor ??= ResolveLetterColor();
            _letter = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(TextElement.GetFontFamily(this), FontStyle.Normal, FontWeight.SemiBold), text.Length > 1 ? 9 : 11,
                new ImmutableSolidColorBrush(_letterColor ?? Colors.White));
        }
        context.DrawText(_letter, new Point((20 - _letter.Width) / 2, (20 - _letter.Height) / 2));
    }

    /// <summary>The generated mark's letters: the monogram (at most two characters) or the name's first letter.</summary>
    internal static string MonogramOf(IntegrationItem item)
    {
        var source = !string.IsNullOrWhiteSpace(item.Monogram) ? item.Monogram!.Trim() : item.Name.Trim();
        if (source.Length == 0)
        {
            return string.Empty;
        }
        var max = string.IsNullOrWhiteSpace(item.Monogram) ? 1 : 2;
        return source[..Math.Min(max, source.Length)].ToUpper(CultureInfo.CurrentCulture);
    }

    private IBrush? ResolveSeries(IntegrationItem item)
    {
        try
        {
            return new ImmutableSolidColorBrush(ArcTokens.GetColor(this, SeriesKinds[SeriesIndex(item.EffectiveKey)]));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private Color? ResolveLetterColor()
    {
        try
        {
            // The surface color reads on the series tiles in both themes (white on light, near black on dark).
            return ArcTokens.GetColor(this, ArcDesignTokenKind.Surface);
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>
/// The app's name in a row: a text button (Arc: no hover change, a square focus ring hugging the text) that opens the
/// drawer. Its accessible help text is the description.
/// </summary>
internal sealed class IntegrationNameButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(IntegrationNameButton);
}

/// <summary>One permission in the drawer: an icon picked by its kind and the phrase.</summary>
[TemplatePart(IconPart, typeof(ArcGlyph))]
internal sealed class IntegrationScopeRow : TemplatedControl
{
    internal const string IconPart = "PART_Icon";

    /// <summary>The permission.</summary>
    public static readonly StyledProperty<IntegrationScope?> ScopeProperty =
        AvaloniaProperty.Register<IntegrationScopeRow, IntegrationScope?>(nameof(Scope));

    /// <summary>The phrase (mirrors the scope).</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<IntegrationScopeRow, string?>(nameof(Text));

    /// <summary>The icon (mirrors the scope).</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<IntegrationScopeRow, Geometry?>(nameof(Icon));

    /// <summary>Whether a line closes the list under this row (the last one).</summary>
    public static readonly StyledProperty<bool> IsLastProperty =
        AvaloniaProperty.Register<IntegrationScopeRow, bool>(nameof(IsLast));

    /// <inheritdoc cref="ScopeProperty"/>
    public IntegrationScope? Scope { get => GetValue(ScopeProperty); set => SetValue(ScopeProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc cref="IsLastProperty"/>
    public bool IsLast { get => GetValue(IsLastProperty); set => SetValue(IsLastProperty, value); }

    /// <summary>The icon of a permission kind.</summary>
    internal static Geometry IconFor(IntegrationScopeKind kind) => kind switch
    {
        IntegrationScopeKind.Write => ArcIcons.PenLine,
        IntegrationScopeKind.Sync => ArcIcons.RefreshCw,
        IntegrationScopeKind.Admin => ArcIcons.ShieldCheck,
        _ => ArcIcons.Eye,
    };

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ScopeProperty)
        {
            if (change.OldValue is IntegrationScope old)
            {
                old.PropertyChanged -= OnScopeChanged;
            }
            if (change.NewValue is IntegrationScope scope)
            {
                scope.PropertyChanged += OnScopeChanged;
            }
            Sync();
        }
    }

    private void OnScopeChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => Sync();

    private void Sync()
    {
        Text = Scope?.Text;
        Icon = Scope is { } s ? s.Icon ?? IconFor(s.Kind) : null;
    }
}

/// <summary>A small text-only empty state of the list (Arc design reference): a title, a hint and one secondary button.</summary>
[TemplatePart(ActionPart, typeof(Button))]
internal sealed class IntegrationEmpty : TemplatedControl
{
    internal const string ActionPart = "PART_Action";

    /// <summary>Which empty state it is.</summary>
    public IntegrationEmptyKind Kind { get; set; }

    /// <summary>The title.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<IntegrationEmpty, string?>(nameof(Title));

    /// <summary>The hint under it.</summary>
    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<IntegrationEmpty, string?>(nameof(Hint));

    /// <summary>The button's text; null hides it.</summary>
    public static readonly StyledProperty<string?> ActionTextProperty =
        AvaloniaProperty.Register<IntegrationEmpty, string?>(nameof(ActionText));

    private Button? _action;

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="HintProperty"/>
    public string? Hint { get => GetValue(HintProperty); set => SetValue(HintProperty, value); }

    /// <inheritdoc cref="ActionTextProperty"/>
    public string? ActionText { get => GetValue(ActionTextProperty); set => SetValue(ActionTextProperty, value); }

    /// <summary>The button (tests and focus).</summary>
    public Button? Action => _action;

    /// <summary>Raised when the button is pressed.</summary>
    public event EventHandler? ActionInvoked;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_action is not null)
        {
            _action.Click -= OnAction;
        }
        _action = e.NameScope.Find<Button>(ActionPart);
        if (_action is not null)
        {
            _action.Click += OnAction;
        }
    }

    private void OnAction(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ActionInvoked?.Invoke(this, EventArgs.Empty);
}

/// <summary>Which empty state the list shows.</summary>
internal enum IntegrationEmptyKind
{
    /// <summary>The search matches nothing.</summary>
    Search,

    /// <summary>The Connected filter has nothing (and no search).</summary>
    Connected,

    /// <summary>A category without apps (and no search).</summary>
    Category
}

/// <summary>Accessible name helpers of the Integrations parts.</summary>
internal static class IntegrationAutomation
{
    /// <summary>Sets an element's accessible name and help text.</summary>
    public static void Describe(Control control, string? name, string? help)
    {
        AutomationProperties.SetName(control, name ?? string.Empty);
        AutomationProperties.SetHelpText(control, help ?? string.Empty);
    }
}
