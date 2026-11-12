using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Templates of the review bar's buttons, built in code (a ContentPresenter with a lazy-free 2px focus ring).</summary>
internal static class ImportWizardReviewTemplates
{
    public const string RingPart = "PART_FocusRing";

    /// <summary>
    /// The content on the button's background, border, radius and padding, and a 2px FocusRing 2 outside its edge (a
    /// capsule) shown while it has keyboard focus.
    /// </summary>
    public static FuncControlTemplate<AvaloniaButton> Capsule { get; } = new((button, scope) =>
    {
        var presenter = new ContentPresenter { Name = "PART_ContentPresenter" };
        presenter.Bind(ContentPresenter.ContentProperty, new TemplateBinding(ContentControl.ContentProperty));
        presenter.Bind(ContentPresenter.BackgroundProperty, new TemplateBinding(TemplatedControl.BackgroundProperty));
        presenter.Bind(ContentPresenter.BorderBrushProperty, new TemplateBinding(TemplatedControl.BorderBrushProperty));
        presenter.Bind(ContentPresenter.BorderThicknessProperty, new TemplateBinding(TemplatedControl.BorderThicknessProperty));
        presenter.Bind(ContentPresenter.CornerRadiusProperty, new TemplateBinding(TemplatedControl.CornerRadiusProperty));
        presenter.Bind(ContentPresenter.PaddingProperty, new TemplateBinding(TemplatedControl.PaddingProperty));
        presenter.Bind(ContentPresenter.HorizontalContentAlignmentProperty, new TemplateBinding(ContentControl.HorizontalContentAlignmentProperty));
        presenter.Bind(ContentPresenter.VerticalContentAlignmentProperty, new TemplateBinding(ContentControl.VerticalContentAlignmentProperty));
        presenter.RegisterInNameScope(scope);
        var ring = new Border { Name = RingPart, Margin = new Thickness(-4), IsVisible = false, IsHitTestVisible = false }
            .BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(ring, Border.BorderBrushProperty, ArcDesignTokenKind.FocusRing);
        ArcTokens.Bind(ring, Border.BorderThicknessProperty, ArcDesignTokenKind.FocusEdgeThickness);
        ring.RegisterInNameScope(scope);
        return new Panel { Children = { presenter, ring } };
    });
}

/// <summary>
/// One option of the review filter: a 28px capsule (padding 0 12) holding the label (12 / 16.8) and its count rolling in
/// the muted color, 5 apart. The selected one's label is <see cref="ImportWizardToken.StrongText"/>, the other's
/// TextSecondary (an instant change; no hover). A radio for screen readers ("To fix 9", selected or not); every option is a
/// tab stop, like Arc's.
/// </summary>
internal sealed class ImportWizardReviewFilterOption : AvaloniaButton
{
    private readonly TextBlock _label;
    private readonly RollingDigits _count;
    private IDisposable? _labelColor;
    private Border? _ring;
    private bool _selected;

    static ImportWizardReviewFilterOption()
    {
        ClipToBoundsProperty.OverrideDefaultValue<ImportWizardReviewFilterOption>(false);
        FocusableProperty.OverrideDefaultValue<ImportWizardReviewFilterOption>(true);
    }

    public ImportWizardReviewFilterOption()
    {
        Template = ImportWizardReviewTemplates.Capsule;
        Height = 28;
        Padding = new Thickness(12, 0);
        Background = Brushes.Transparent;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        FocusAdorner = null;
        Cursor = new Cursor(StandardCursorType.Hand);
        this.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        _label = ImportWizardUi.Text(string.Empty, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary, lineHeight: 16.8);
        _label.LetterSpacing = -0.12;
        _count = new RollingDigits
        {
            Falloff = RollingDigitsFalloff.None,
            WheelMotion = MotionSpec.Spring(ImportWizardMotion.Quick),
            GlyphPitch = 1.2,
            IsArmed = false,
            UseGrouping = true,
            // Arc's count strip is 14.4 tall at the top of the 16.8 line, so the digits sit a little higher than the label.
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true,
        };
        ArcTokens.Bind(_count, RollingDigits.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(_count, RollingDigits.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_count, RollingDigits.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        _label.VerticalAlignment = VerticalAlignment.Top;
        row.Children.Add(_label);
        row.Children.Add(_count);
        Content = row;
        Click += (_, _) => Chosen?.Invoke(this);
        ApplySelected();
    }

    /// <summary>The label.</summary>
    public string Label
    {
        get => _label.Text ?? string.Empty;
        set
        {
            _label.Text = value;
            UpdateName();
        }
    }

    /// <summary>The count shown.</summary>
    public int Count { get; private set; }

    /// <summary>True for the chosen option.</summary>
    public bool IsSelected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }
            _selected = value;
            ApplySelected();
        }
    }

    /// <summary>Raised when the option is chosen (click, Enter, Space or a screen reader's select).</summary>
    public event Action<ImportWizardReviewFilterOption>? Chosen;

    /// <summary>Sets the count; it rolls unless <paramref name="instant"/>.</summary>
    public void SetCount(int count, bool instant)
    {
        Count = count;
        _count.IsInstant = instant;
        _count.Value = count;
        UpdateName();
    }

    private void UpdateName() => AutomationProperties.SetName(this, $"{_label.Text} {Count.ToString("N0", CultureInfo.CurrentCulture)}");

    private void ApplySelected()
    {
        _labelColor?.Dispose();
        _labelColor = _selected
            ? _label.Bind(TextBlock.ForegroundProperty, _label.GetResourceObservable(ImportWizardTokens.Key(ImportWizardTokenKind.StrongText)))
            : ArcTokens.Bind(_label, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
    }

    protected override Type StyleKeyOverride => typeof(ImportWizardReviewFilterOption);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _ring = e.NameScope.Find<Border>(ImportWizardReviewTemplates.RingPart);
    }


    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        UpdateRing();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        UpdateRing();
    }

    private void UpdateRing()
    {
        if (_ring is not null)
        {
            _ring.IsVisible = IsFocused && Classes.Contains(":focus-visible");
        }
    }

    internal void Choose() => Chosen?.Invoke(this);

    protected override AutomationPeer OnCreateAutomationPeer() => new OptionPeer(this);

    private sealed class OptionPeer(ImportWizardReviewFilterOption owner) : ButtonAutomationPeer(owner), ISelectionItemProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.RadioButton;

        public bool IsSelected => owner.IsSelected;

        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }

        public void Select() => owner.Choose();
    }
}

/// <summary>
/// The review filter (Arc's segmented control in this block): a SurfaceMuted track with a 1px Border edge, padding 3 and
/// a capsule radius, 36 tall, holding "To fix" and "All" side by side. The selected option sits on a raised pill
/// (SurfaceRaised, a 1px Border ring and ShadowResting) that slides and stretches to the chosen option on the smooth spring
/// (it snaps when an option only changes width, as Arc's pill lives inside its option). A group named "Rows".
/// </summary>
internal sealed class ImportWizardReviewFilter : Border
{
    private readonly ImportWizardReviewFilterOption _issues = new();
    private readonly ImportWizardReviewFilterOption _all = new();
    private readonly FilterPanel _panel;
    private bool _showAll;

    public ImportWizardReviewFilter()
    {
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(this, BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        this.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        Padding = new Thickness(3);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;
        var pill = new GlidingIndicator { StrokeThickness = 1 }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(pill, GlidingIndicator.FillProperty, ArcDesignTokenKind.SurfaceRaised);
        ArcTokens.Bind(pill, GlidingIndicator.StrokeProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(pill, GlidingIndicator.BoxShadowProperty, ArcDesignTokenKind.ShadowResting);
        _panel = new FilterPanel(pill, _issues, _all);
        Child = _panel;
        _issues.IsSelected = true;
        _issues.Chosen += _ => Select(false, animate: true);
        _all.Chosen += _ => Select(true, animate: true);
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Group);
    }

    /// <summary>The "To fix" option.</summary>
    public ImportWizardReviewFilterOption Issues => _issues;

    /// <summary>The "All" option.</summary>
    public ImportWizardReviewFilterOption All => _all;

    /// <summary>True when "All" is chosen.</summary>
    public bool ShowAll => _showAll;

    /// <summary>Raised when the choice changed: true for "All".</summary>
    public event Action<bool>? Changed;

    /// <summary>Sets the words: the two options and the group's name.</summary>
    public void SetLabels(string issues, string all, string name)
    {
        _issues.Label = issues;
        _all.Label = all;
        AutomationProperties.SetName(this, name);
    }

    /// <summary>Sets the counts; they roll unless <paramref name="instant"/>.</summary>
    public void SetCounts(int issues, int all, bool instant)
    {
        _issues.SetCount(issues, instant);
        _all.SetCount(all, instant);
    }

    /// <summary>Chooses "All" (true) or "To fix" (false).</summary>
    public void Select(bool all, bool animate)
    {
        if (_showAll == all)
        {
            return;
        }
        _showAll = all;
        _issues.IsSelected = !all;
        _all.IsSelected = all;
        _panel.Glide(animate);
        Changed?.Invoke(all);
    }

    /// <summary>The options side by side and the pill under the selected one.</summary>
    private sealed class FilterPanel : Panel
    {
        private readonly ImportWizardReviewFilterOption _issues;
        private readonly ImportWizardReviewFilterOption _all;
        private Rect _target;
        private bool _glide;
        private bool _placed;

        public FilterPanel(GlidingIndicator pill, ImportWizardReviewFilterOption issues, ImportWizardReviewFilterOption all)
        {
            Pill = pill;
            _issues = issues;
            _all = all;
            Children.Add(pill);
            Children.Add(issues);
            Children.Add(all);
        }

        public GlidingIndicator Pill { get; }

        public void Glide(bool animate)
        {
            _glide = animate;
            InvalidateArrange();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var unbounded = new Size(double.PositiveInfinity, availableSize.Height);
            _issues.Measure(unbounded);
            _all.Measure(unbounded);
            Pill.Measure(availableSize);
            return new Size(_issues.DesiredSize.Width + _all.DesiredSize.Width, Math.Max(_issues.DesiredSize.Height, _all.DesiredSize.Height));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var issues = new Rect(0, 0, _issues.DesiredSize.Width, finalSize.Height);
            var all = new Rect(issues.Right, 0, _all.DesiredSize.Width, finalSize.Height);
            _issues.Arrange(issues);
            _all.Arrange(all);
            Pill.Arrange(new Rect(finalSize));
            // The 1px ring sits outside the pill (Arc's 0 0 0 1px shadow): the stroke is centred half a pixel out.
            var target = (_all.IsSelected ? all : issues).Inflate(0.5);
            if (!_placed)
            {
                Pill.MoveTo(target, ImportWizardMotion.Smooth, animate: false);
                _placed = true;
            }
            else if (target != _target)
            {
                // A new choice glides; an option that only changed width (a count gained a digit) snaps.
                Pill.MoveTo(target, ImportWizardMotion.Smooth, animate: _glide);
            }
            _target = target;
            _glide = false;
            return finalSize;
        }
    }
}

/// <summary>
/// "Clean up n values": 32 tall, padding 0 12, a capsule with the 14px wand-sparkles (Lucide) and the words 6 apart in
/// <see cref="ImportWizardToken.StrongText"/>; the accent at 13% (10% light) behind with a 1px edge of the accent at 30%,
/// 18% on hover (160ms ease-out). Shown only while values can be cleaned up: it leaves with its last words, fading to 0,
/// blurring to 3px and shrinking to 0.96 on the quick spring, then takes no room; it comes back the same way reversed.
/// Reduced motion keeps only the fade (240ms); Off swaps at once. The tints and the presence are code-driven because the
/// tints are derived from the accent here and the exit must finish before the button gives up its room.
/// </summary>
internal sealed class ImportWizardReviewCleanUp : AvaloniaButton
{
    /// <summary>The accent the tints are made from.</summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<ImportWizardReviewCleanUp, IBrush?>(nameof(AccentBrush));

    /// <summary>The text color.</summary>
    public static readonly StyledProperty<IBrush?> StrongBrushProperty =
        AvaloniaProperty.Register<ImportWizardReviewCleanUp, IBrush?>(nameof(StrongBrush));

    /// <summary>Lucide wand-sparkles (ISC), from the repository's Lucide data.</summary>
    private static readonly Geometry s_wand = ArcIcons.Parse(
        "M21.64 3.64l-1.28-1.28a1.21 1.21 0 0 0-1.72 0L2.36 18.64a1.21 1.21 0 0 0 0 1.72l1.28 1.28a1.2 1.2 0 0 0 1.72 0L21.64 5.36a1.2 1.2 0 0 0 0-1.72 M14 7l3 3 M5 6v4 M19 14v4 M10 2v2 M7 8H3 M21 16h-4 M11 3H9");

    private readonly ArcGlyph _glyph;
    private readonly ImportWizardCountText _words;
    private readonly ScaleTransform _scale = new(1, 1);
    private Border? _ring;
    private MotionValue<double>? _hover;
    private double _hoverValue;
    private MotionValue<double>? _presence;
    private double _presenceValue;
    private bool _shown = true;

    static ImportWizardReviewCleanUp()
    {
        ClipToBoundsProperty.OverrideDefaultValue<ImportWizardReviewCleanUp>(false);
        FocusableProperty.OverrideDefaultValue<ImportWizardReviewCleanUp>(true);
    }

    public ImportWizardReviewCleanUp()
    {
        Template = ImportWizardReviewTemplates.Capsule;
        Height = 32;
        Padding = new Thickness(12, 0);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        FocusAdorner = null;
        Cursor = new Cursor(StandardCursorType.Hand);
        RenderTransformOrigin = RelativePoint.Center;
        RenderTransform = _scale;
        this.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(this, BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        ArcTokens.Bind(this, AccentBrushProperty, ArcDesignTokenKind.Accent);
        ImportWizardUi.Bind(this, StrongBrushProperty, ImportWizardTokenKind.StrongText);
        _glyph = new ArcGlyph { Data = s_wand, Size = 14, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center };
        _words = new ImportWizardCountText(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(_glyph);
        row.Children.Add(_words);
        Content = row;
        ActualThemeVariantChanged += (_, _) => UpdateTints();
    }

    /// <inheritdoc cref="AccentBrushProperty"/>
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    /// <inheritdoc cref="StrongBrushProperty"/>
    public IBrush? StrongBrush { get => GetValue(StrongBrushProperty); set => SetValue(StrongBrushProperty, value); }

    /// <summary>The words (tests: their plain text).</summary>
    public ImportWizardCountText Words => _words;

    /// <summary>True while it is shown or coming in (false while it leaves and once it left).</summary>
    public bool IsShown => _shown;

    /// <summary>Shows "Clean up n values"; the number rolls unless <paramref name="instant"/>.</summary>
    public void SetCount(string format, int count, bool instant)
    {
        _words.IsInstant = instant;
        _words.Set(format, count);
        AutomationProperties.SetName(this, _words.PlainText);
    }

    /// <summary>Brings the button in or sends it away.</summary>
    public void SetShown(bool shown, bool animate)
    {
        if (_shown == shown && (_presence is null || !_presence.IsAnimating))
        {
            if (shown)
            {
                IsVisible = true;
            }
            return;
        }
        _shown = shown;
        IsHitTestVisible = shown;
        var level = ArcMotion.GetEffectiveLevel(this);
        var target = shown ? 0.0 : 1.0;
        if (shown)
        {
            IsVisible = true;
        }
        if (!animate || !IsLoaded || level == ArcMotionLevel.Off)
        {
            _presence?.Jump(target);
            ApplyPresence(target);
            IsVisible = shown;
            return;
        }
        _presence ??= CreatePresence();
        var spec = level == ArcMotionLevel.Full
            ? MotionSpec.Spring(ImportWizardMotion.Quick)
            : MotionSpec.Tween(ImportWizardMotion.Standard, ImportWizardMotion.EaseOut);
        _ = Run(_presence.AnimateTo(target, spec), shown);
    }

    private async Task Run(Task<bool> motion, bool shown)
    {
        var done = await motion;
        if (done && !shown && !_shown)
        {
            IsVisible = false;
        }
    }

    private MotionValue<double> CreatePresence()
    {
        var motion = new MotionValue<double>(this, _presenceValue, MotionVectors.Double);
        motion.Changed += ApplyPresence;
        return motion;
    }

    private void ApplyPresence(double gone)
    {
        _presenceValue = gone;
        Opacity = 1 - gone;
        var full = ArcMotion.IsFull(this);
        var scale = full ? 1 - 0.04 * gone : 1;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        Effect = full ? ArcBlur.Get(3 * gone) : null;
    }

    protected override Type StyleKeyOverride => typeof(ImportWizardReviewCleanUp);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _ring = e.NameScope.Find<Border>(ImportWizardReviewTemplates.RingPart);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AccentBrushProperty)
        {
            UpdateTints();
        }
        else if (change.Property == StrongBrushProperty)
        {
            _glyph.Foreground = StrongBrush;
            _words.ForegroundOverride = StrongBrush;
        }
        else if (change.Property == IsPointerOverProperty)
        {
            var target = IsPointerOver ? 1.0 : 0.0;
            if (IsLoaded && ArcMotion.IsFull(this))
            {
                _hover ??= CreateHover();
                _ = _hover.AnimateTo(target, MotionSpec.Tween(ImportWizardMotion.Fast, ImportWizardMotion.EaseOut));
            }
            else
            {
                _hover?.Jump(target);
                _hoverValue = target;
                UpdateTints();
            }
        }
    }

    private MotionValue<double> CreateHover()
    {
        var motion = new MotionValue<double>(this, _hoverValue, MotionVectors.Double);
        motion.Changed += v =>
        {
            _hoverValue = v;
            UpdateTints();
        };
        return motion;
    }

    /// <summary>Accent at 13% (10% light) behind, 18% on hover; a 1px edge of the accent at 30%.</summary>
    private void UpdateTints()
    {
        if (AccentBrush is not ISolidColorBrush accent)
        {
            return;
        }
        var color = accent.Color;
        var rest = ActualThemeVariant == ThemeVariant.Dark ? 0.13 : 0.10;
        var alpha = rest + (0.18 - rest) * _hoverValue;
        Background = new ImmutableSolidColorBrush(Oklch.WithAlpha(color, alpha * color.A / 255.0));
        BorderBrush = new ImmutableSolidColorBrush(Oklch.WithAlpha(color, 0.30 * color.A / 255.0));
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        UpdateRing();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        UpdateRing();
    }

    private void UpdateRing()
    {
        if (_ring is not null)
        {
            _ring.IsVisible = IsFocused && Classes.Contains(":focus-visible");
        }
    }
}

/// <summary>
/// The review bar: the filter on the left and the clean-up button on the right, centred on one 36px line, 12 apart at
/// least. When both do not fit (phones) the button wraps under the filter, 12 below it (Arc's flex-wrap).
/// </summary>
internal sealed class ImportWizardReviewBar : Panel
{
    private const double Gap = 12;

    private readonly ImportWizardReviewFilter _filter;
    private readonly ImportWizardReviewCleanUp _cleanUp;

    public ImportWizardReviewBar(ImportWizardReviewFilter filter, ImportWizardReviewCleanUp cleanUp)
    {
        _filter = filter;
        _cleanUp = cleanUp;
        Children.Add(filter);
        Children.Add(cleanUp);
    }

    /// <summary>True when the button sits under the filter.</summary>
    public bool IsWrapped { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var unbounded = new Size(double.PositiveInfinity, double.PositiveInfinity);
        _filter.Measure(unbounded);
        _cleanUp.Measure(unbounded);
        var filter = _filter.DesiredSize;
        var button = _cleanUp.IsVisible ? _cleanUp.DesiredSize : default;
        var line = filter.Width + (button.Width > 0 ? Gap + button.Width : 0);
        IsWrapped = button.Width > 0 && line > availableSize.Width + 0.5;
        var width = double.IsInfinity(availableSize.Width) ? line : availableSize.Width;
        var height = IsWrapped ? filter.Height + Gap + button.Height : Math.Max(filter.Height, button.Height);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var filter = _filter.DesiredSize;
        var button = _cleanUp.DesiredSize;
        if (IsWrapped)
        {
            _filter.Arrange(new Rect(0, 0, filter.Width, filter.Height));
            _cleanUp.Arrange(new Rect(0, filter.Height + Gap, button.Width, button.Height));
            return finalSize;
        }
        _filter.Arrange(new Rect(0, (finalSize.Height - filter.Height) / 2, filter.Width, filter.Height));
        _cleanUp.Arrange(new Rect(finalSize.Width - button.Width, (finalSize.Height - button.Height) / 2, button.Width, button.Height));
        return finalSize;
    }
}
