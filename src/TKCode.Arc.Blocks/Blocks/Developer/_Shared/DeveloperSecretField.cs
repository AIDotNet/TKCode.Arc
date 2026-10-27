using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The two faces of a <see cref="DeveloperSecretField"/>.</summary>
internal enum DeveloperSecretMode
{
    /// <summary>
    /// A secret shown once (a new API key, a webhook signing secret): label, a monospace box with the full value and a
    /// Copy button, and a warning line. The value develops out of an 8px blur.
    /// </summary>
    OneTime,

    /// <summary>
    /// A stored value (an environment variable): a fixed 36px slot that crossfades between a mask of bullets and the value,
    /// with a reveal toggle and a copy button that pops in while revealed; a sensitive value shows a lock instead.
    /// </summary>
    Masked
}

/// <summary>
/// The 36px icon toggle of the Developer blocks (Env variables' reveal eye): radius 10, TextSecondary glyph, Foreground 5%
/// fill on hover and 8% while on (fills mixed in code), a 2px ring outside on keyboard focus.
/// </summary>
internal sealed class DeveloperIconToggle : ToggleButton
{
    /// <summary>Fill on hover.</summary>
    public static readonly StyledProperty<IBrush?> HoverBrushProperty =
        AvaloniaProperty.Register<DeveloperIconToggle, IBrush?>(nameof(HoverBrush));

    /// <summary>Fill while checked.</summary>
    public static readonly StyledProperty<IBrush?> OnBrushProperty =
        AvaloniaProperty.Register<DeveloperIconToggle, IBrush?>(nameof(OnBrush));

    /// <inheritdoc cref="HoverBrushProperty"/>
    public IBrush? HoverBrush { get => GetValue(HoverBrushProperty); set => SetValue(HoverBrushProperty, value); }

    /// <inheritdoc cref="OnBrushProperty"/>
    public IBrush? OnBrush { get => GetValue(OnBrushProperty); set => SetValue(OnBrushProperty, value); }

    protected override Type StyleKeyOverride => typeof(DeveloperIconToggle);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPointerOverProperty || change.Property == IsCheckedProperty
            || change.Property == HoverBrushProperty || change.Property == OnBrushProperty)
        {
            Background = IsChecked == true ? OnBrush ?? Brushes.Transparent
                : IsPointerOver ? HoverBrush ?? Brushes.Transparent
                : Brushes.Transparent;
        }
    }
}

/// <summary>
/// A secret value for the Developer blocks, in one of two <see cref="Mode"/>s (Arc API keys / Webhooks one-time reveal and
/// Env variables' masked value; design reference). The secret is write-only: it is handed over with
/// <see cref="SetSecret"/> or fetched from <see cref="SecretProvider"/> at the moment it is shown, kept in a private
/// field, and only put into the visual tree (text and copy button) while it must be visible: a hidden masked value has no
/// text anywhere in the tree, a sensitive one never does, and <see cref="ClearSecret"/> drops every reference (call it when
/// a one-time reveal closes). There is no property to read it back.
/// <para>
/// OneTime: label (14/19.6 500), box (padding 6 6 6 14, radius 12, 1px Accent 45% into Border, Surface, at least 50 high)
/// with the value in mono 14/21 wrapping anywhere and a Copy button (resets after 1.9s), then a TriangleAlert warning line
/// (14/19.6 TextSecondary). A new secret develops: opacity 0 → 1 and blur 8 → 0 over 480ms after 140ms (enter curve).
/// </para>
/// <para>
/// Masked: a 36px slot. Revealing fades the 16-bullet mask out (160ms, rising 4px) and the value in (240ms, from 4px
/// below), and pops the copy slot in (opacity and scale .8 → 1 on Quick); hiding reverses (the copy slot leaves in 180ms
/// EaseExit) and clears the text. Reduced motion: 120ms crossfades without travel or scale; Off: jumps.
/// </para>
/// </summary>
[TemplatePart(SecretPart, typeof(TextBlock))]
[TemplatePart(CopyPart, typeof(CopyButton))]
[TemplatePart(MaskPart, typeof(TextBlock))]
[TemplatePart(CopySlotPart, typeof(Control))]
[TemplatePart(RevealPart, typeof(DeveloperIconToggle))]
[TemplatePart(RevealIconPart, typeof(ArcGlyph))]
[TemplatePart(LabelPart, typeof(TextBlock))]
[TemplatePart(BoxPart, typeof(Border))]
[PseudoClasses(RevealedPseudoClass, SensitivePseudoClass, EmptyPseudoClass)]
internal sealed class DeveloperSecretField : TemplatedControl
{
    internal const string SecretPart = "PART_Secret";
    internal const string CopyPart = "PART_Copy";
    internal const string MaskPart = "PART_Mask";
    internal const string CopySlotPart = "PART_CopySlot";
    internal const string RevealPart = "PART_Reveal";
    internal const string RevealIconPart = "PART_RevealIcon";
    internal const string LabelPart = "PART_Label";
    internal const string BoxPart = "PART_Box";
    private const string RevealedPseudoClass = ":revealed";
    private const string SensitivePseudoClass = ":sensitive";
    private const string EmptyPseudoClass = ":empty";

    /// <summary>OneTime (default) or Masked; each has its own template.</summary>
    public static readonly StyledProperty<DeveloperSecretMode> ModeProperty =
        AvaloniaProperty.Register<DeveloperSecretField, DeveloperSecretMode>(nameof(Mode));

    /// <summary>OneTime: the label above the box ("Secret key"); it also names the box for screen readers.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<DeveloperSecretField, string?>(nameof(Label));

    /// <summary>OneTime: space between the label and the box (API keys 8, Webhooks 16).</summary>
    public static readonly StyledProperty<double> LabelSpacingProperty =
        AvaloniaProperty.Register<DeveloperSecretField, double>(nameof(LabelSpacing), 8);

    /// <summary>OneTime: the warning line below the box; null or empty hides it.</summary>
    public static readonly StyledProperty<string?> NoticeProperty =
        AvaloniaProperty.Register<DeveloperSecretField, string?>(nameof(Notice));

    /// <summary>Masked: the value is shown (two-way). Always false while <see cref="IsSensitive"/>.</summary>
    public static readonly StyledProperty<bool> IsRevealedProperty =
        AvaloniaProperty.Register<DeveloperSecretField, bool>(nameof(IsRevealed), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay,
            coerce: (o, v) => v && !((DeveloperSecretField)o).IsSensitive);

    /// <summary>Masked: the value is write-only; a lock and <see cref="SensitiveText"/> replace the reveal and copy buttons.</summary>
    public static readonly StyledProperty<bool> IsSensitiveProperty =
        AvaloniaProperty.Register<DeveloperSecretField, bool>(nameof(IsSensitive));

    /// <summary>What the value belongs to (a key name) for the accessible names ("Reveal DATABASE_URL").</summary>
    public static readonly StyledProperty<string?> SubjectProperty =
        AvaloniaProperty.Register<DeveloperSecretField, string?>(nameof(Subject));

    /// <summary>Masked: number of bullets in the mask (16; it never reflects the value's length).</summary>
    public static readonly StyledProperty<int> MaskLengthProperty =
        AvaloniaProperty.Register<DeveloperSecretField, int>(nameof(MaskLength), 16);

    /// <summary>Accessible name of the reveal toggle; {0} is <see cref="Subject"/> (default "Reveal {0}").</summary>
    public static readonly StyledProperty<string> RevealNameFormatProperty =
        AvaloniaProperty.Register<DeveloperSecretField, string>(nameof(RevealNameFormat), "Reveal {0}");

    /// <summary>Accessible name of the copy button; {0} is <see cref="Subject"/> (default "Copy {0}"; OneTime uses the label).</summary>
    public static readonly StyledProperty<string> CopyNameFormatProperty =
        AvaloniaProperty.Register<DeveloperSecretField, string>(nameof(CopyNameFormat), "Copy {0}");

    /// <summary>Screen reader text of the mask (default "Hidden value").</summary>
    public static readonly StyledProperty<string> HiddenTextProperty =
        AvaloniaProperty.Register<DeveloperSecretField, string>(nameof(HiddenText), "Hidden value");

    /// <summary>Visible label of a sensitive value (default "Sensitive").</summary>
    public static readonly StyledProperty<string> SensitiveTextProperty =
        AvaloniaProperty.Register<DeveloperSecretField, string>(nameof(SensitiveText), "Sensitive");

    /// <summary>Screen reader text of a sensitive mask (default "Sensitive value, write-only").</summary>
    public static readonly StyledProperty<string> SensitiveNameProperty =
        AvaloniaProperty.Register<DeveloperSecretField, string>(nameof(SensitiveName), "Sensitive value, write-only");

    /// <summary>Copy button label (default "Copy").</summary>
    public static readonly StyledProperty<string> CopyTextProperty =
        AvaloniaProperty.Register<DeveloperSecretField, string>(nameof(CopyText), "Copy");

    /// <summary>Copy button label after a copy (default "Copied").</summary>
    public static readonly StyledProperty<string> CopiedTextProperty =
        AvaloniaProperty.Register<DeveloperSecretField, string>(nameof(CopiedText), "Copied");

    private string? _secret;
    private TextBlock? _secretText;
    private CopyButton? _copy;
    private TextBlock? _mask;
    private Control? _copySlot;
    private DeveloperIconToggle? _reveal;
    private ArcGlyph? _revealIcon;
    private TextBlock? _label;
    private Border? _box;
    private MotionValue<double>? _develop;
    private MotionValue<double>? _maskFade;
    private MotionValue<double>? _valueFade;
    private MotionValue<double>? _copyPop;
    private readonly TranslateTransform _maskShift = new();
    private readonly TranslateTransform _valueShift = new();
    private readonly ScaleTransform _copyScale = new();
    private bool _travel;
    private bool _syncingToggle;

    /// <inheritdoc cref="ModeProperty"/>
    public DeveloperSecretMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="LabelSpacingProperty"/>
    public double LabelSpacing { get => GetValue(LabelSpacingProperty); set => SetValue(LabelSpacingProperty, value); }

    /// <inheritdoc cref="NoticeProperty"/>
    public string? Notice { get => GetValue(NoticeProperty); set => SetValue(NoticeProperty, value); }

    /// <inheritdoc cref="IsRevealedProperty"/>
    public bool IsRevealed { get => GetValue(IsRevealedProperty); set => SetValue(IsRevealedProperty, value); }

    /// <inheritdoc cref="IsSensitiveProperty"/>
    public bool IsSensitive { get => GetValue(IsSensitiveProperty); set => SetValue(IsSensitiveProperty, value); }

    /// <inheritdoc cref="SubjectProperty"/>
    public string? Subject { get => GetValue(SubjectProperty); set => SetValue(SubjectProperty, value); }

    /// <inheritdoc cref="MaskLengthProperty"/>
    public int MaskLength { get => GetValue(MaskLengthProperty); set => SetValue(MaskLengthProperty, value); }

    /// <inheritdoc cref="RevealNameFormatProperty"/>
    public string RevealNameFormat { get => GetValue(RevealNameFormatProperty); set => SetValue(RevealNameFormatProperty, value); }

    /// <inheritdoc cref="CopyNameFormatProperty"/>
    public string CopyNameFormat { get => GetValue(CopyNameFormatProperty); set => SetValue(CopyNameFormatProperty, value); }

    /// <inheritdoc cref="HiddenTextProperty"/>
    public string HiddenText { get => GetValue(HiddenTextProperty); set => SetValue(HiddenTextProperty, value); }

    /// <inheritdoc cref="SensitiveTextProperty"/>
    public string SensitiveText { get => GetValue(SensitiveTextProperty); set => SetValue(SensitiveTextProperty, value); }

    /// <inheritdoc cref="SensitiveNameProperty"/>
    public string SensitiveName { get => GetValue(SensitiveNameProperty); set => SetValue(SensitiveNameProperty, value); }

    /// <inheritdoc cref="CopyTextProperty"/>
    public string CopyText { get => GetValue(CopyTextProperty); set => SetValue(CopyTextProperty, value); }

    /// <inheritdoc cref="CopiedTextProperty"/>
    public string CopiedText { get => GetValue(CopiedTextProperty); set => SetValue(CopiedTextProperty, value); }

    /// <summary>
    /// Masked: supplies the value at the moment it is revealed (or copied) when none was set with
    /// <see cref="SetSecret"/>; the result is not kept after the value hides again.
    /// </summary>
    public Func<string?>? SecretProvider { get; set; }

    /// <summary>True while a secret is held (set, or fetched for display).</summary>
    public bool HasSecret => _secret is not null;

    /// <summary>Raised after the value was copied to the clipboard.</summary>
    public event EventHandler? Copied;

    /// <summary>Raised when the user reveals or hides the value (Masked), with the new state.</summary>
    public event EventHandler<bool>? RevealToggled;

    /// <summary>
    /// Hands over the secret. OneTime shows it at once (developing out of a blur); Masked keeps it for the next reveal (or
    /// shows it if revealed). Ignored while <see cref="IsSensitive"/>; null clears.
    /// </summary>
    public void SetSecret(string? secret)
    {
        if (secret is null || (Mode == DeveloperSecretMode.Masked && IsSensitive))
        {
            ClearSecret();
            return;
        }
        _secret = secret;
        PseudoClasses.Set(EmptyPseudoClass, false);
        if (Mode == DeveloperSecretMode.OneTime)
        {
            ShowOneTime(animate: true);
        }
        else if (IsRevealed)
        {
            ShowValue(animate: false);
        }
    }

    /// <summary>Drops the secret: no text, no copy value, no reference (a one-time reveal closing, a value made sensitive).</summary>
    public void ClearSecret()
    {
        _secret = null;
        PseudoClasses.Set(EmptyPseudoClass, true);
        if (_secretText is not null)
        {
            _secretText.Text = null;
        }
        if (Mode == DeveloperSecretMode.OneTime && _copy is not null)
        {
            _copy.Value = null;
        }
        if (Mode == DeveloperSecretMode.Masked)
        {
            SetCurrentValue(IsRevealedProperty, false);
            HideValueNow();
        }
    }

    /// <summary>OneTime: plays the develop-out-of-blur reveal again (a reveal panel that opens on an already set secret).</summary>
    public void PlayReveal() => ShowOneTime(animate: true);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_copy is not null)
        {
            _copy.Copied -= OnCopied;
        }
        if (_reveal is not null)
        {
            _reveal.IsCheckedChanged -= OnRevealToggled;
        }
        _secretText = e.NameScope.Find<TextBlock>(SecretPart);
        _copy = e.NameScope.Find<CopyButton>(CopyPart);
        _mask = e.NameScope.Find<TextBlock>(MaskPart);
        _copySlot = e.NameScope.Find<Control>(CopySlotPart);
        _reveal = e.NameScope.Find<DeveloperIconToggle>(RevealPart);
        _revealIcon = e.NameScope.Find<ArcGlyph>(RevealIconPart);
        _label = e.NameScope.Find<TextBlock>(LabelPart);
        _box = e.NameScope.Find<Border>(BoxPart);
        _develop = null;
        _maskFade = null;
        _valueFade = null;
        _copyPop = null;
        if (_copy is not null)
        {
            _copy.Copied += OnCopied;
            _copy.ResetAfter = DeveloperSprings.CopyReset;
        }
        if (_reveal is not null)
        {
            _reveal.IsCheckedChanged += OnRevealToggled;
        }
        if (_mask is not null)
        {
            _mask.RenderTransform = _maskShift;
        }
        if (_secretText is not null && Mode == DeveloperSecretMode.Masked)
        {
            _secretText.RenderTransform = _valueShift;
        }
        if (_copySlot is not null)
        {
            _copySlot.RenderTransformOrigin = RelativePoint.Center;
            _copySlot.RenderTransform = _copyScale;
        }
        PseudoClasses.Set(EmptyPseudoClass, _secret is null);
        SyncToggle();
        UpdateTexts();
        UpdateBrushes();
        if (Mode == DeveloperSecretMode.OneTime)
        {
            ShowOneTime(animate: false);
        }
        else if (IsRevealed && !IsSensitive)
        {
            ShowValue(animate: false);
        }
        else
        {
            HideValueNow();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
        ResourcesChanged += OnResourcesChanged;
        UpdateBrushes();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeChanged;
        ResourcesChanged -= OnResourcesChanged;
    }

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) => OnThemeChanged(sender, EventArgs.Empty);

    private void OnThemeChanged(object? sender, EventArgs e) => UpdateBrushes();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsRevealedProperty)
        {
            PseudoClasses.Set(RevealedPseudoClass, IsRevealed);
            SyncToggle();
            if (Mode == DeveloperSecretMode.Masked)
            {
                if (IsRevealed)
                {
                    ShowValue(animate: true);
                }
                else
                {
                    HideValue(animate: true);
                }
            }
            UpdateTexts();
        }
        else if (change.Property == IsSensitiveProperty)
        {
            PseudoClasses.Set(SensitivePseudoClass, IsSensitive);
            CoerceValue(IsRevealedProperty);
            if (IsSensitive && Mode == DeveloperSecretMode.Masked)
            {
                // A value made write-only is dropped at once (it may not stay in memory or in the tree).
                _secret = null;
                HideValueNow();
            }
            UpdateTexts();
        }
        else if (change.Property == SubjectProperty || change.Property == LabelProperty
                 || change.Property == RevealNameFormatProperty || change.Property == CopyNameFormatProperty
                 || change.Property == HiddenTextProperty || change.Property == SensitiveNameProperty
                 || change.Property == MaskLengthProperty || change.Property == CopyTextProperty
                 || change.Property == CopiedTextProperty || change.Property == LabelSpacingProperty)
        {
            UpdateTexts();
        }
    }

    private void UpdateTexts()
    {
        if (_mask is not null)
        {
            _mask.Text = DeveloperText.Bullets(MaskLength);
            AutomationProperties.SetName(_mask, IsSensitive ? SensitiveName : HiddenText);
        }
        if (_reveal is not null)
        {
            AutomationProperties.SetName(_reveal, DeveloperText.Format(RevealNameFormat, Subject ?? string.Empty).Trim());
        }
        if (_copy is not null)
        {
            _copy.Label = CopyText;
            _copy.CopiedLabel = CopiedText;
            var subject = Mode == DeveloperSecretMode.OneTime ? Subject ?? Label : Subject;
            if (!string.IsNullOrEmpty(subject))
            {
                AutomationProperties.SetName(_copy, DeveloperText.Format(CopyNameFormat, subject));
            }
        }
        if (_label is not null)
        {
            _label.Margin = new Thickness(0, 0, 0, LabelSpacing);
        }
        if (_box is not null)
        {
            AutomationProperties.SetName(_box, Label ?? string.Empty);
        }
    }

    private void UpdateBrushes()
    {
        if (VisualRoot is null)
        {
            return;
        }
        var palette = DeveloperPalette.For(this);
        if (_box is not null)
        {
            _box.BorderBrush = palette.SecretBorder;
        }
        if (_reveal is not null)
        {
            _reveal.HoverBrush = palette.IconHover;
            _reveal.OnBrush = palette.IconOn;
        }
    }

    private void SyncToggle()
    {
        if (_reveal is null || _syncingToggle)
        {
            return;
        }
        _syncingToggle = true;
        _reveal.IsChecked = IsRevealed;
        _syncingToggle = false;
        if (_revealIcon is not null)
        {
            _revealIcon.Data = IsRevealed ? ArcIcons.EyeOff : ArcIcons.Eye;
        }
    }

    private void OnRevealToggled(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_syncingToggle || _reveal is null)
        {
            return;
        }
        var wanted = _reveal.IsChecked == true;
        SetCurrentValue(IsRevealedProperty, wanted);
        if (IsRevealed != wanted)
        {
            SyncToggle();
            return;
        }
        RevealToggled?.Invoke(this, wanted);
    }

    private void OnCopied(object? sender, EventArgs e) => Copied?.Invoke(this, EventArgs.Empty);

    private ArcMotionLevel Level(bool animate) =>
        animate && IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;

    // ---- OneTime ----

    private void ShowOneTime(bool animate)
    {
        if (_secretText is null)
        {
            return;
        }
        _secretText.Text = _secret;
        if (_copy is not null)
        {
            _copy.Value = _secret;
        }
        _develop ??= CreateDevelop(_secretText);
        var level = Level(animate && _secret is not null);
        if (level == ArcMotionLevel.Off)
        {
            _develop.Jump(1);
            return;
        }
        _travel = level == ArcMotionLevel.Full;
        _develop.Jump(0);
        _ = _develop.AnimateTo(1, _travel
            ? MotionSpec.Tween(DeveloperSprings.SecretReveal, ArcEasings.Enter, DeveloperSprings.SecretRevealDelay)
            : DeveloperSprings.ReducedFade());
    }

    private MotionValue<double> CreateDevelop(TextBlock text)
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += p =>
        {
            text.Opacity = Math.Clamp(p, 0, 1);
            var radius = _travel ? 8 * (1 - Math.Clamp(p, 0, 1)) : 0;
            text.Effect = radius < 0.05 ? null : ArcBlur.Get(radius);
        };
        return value;
    }

    // ---- Masked ----

    private void EnsureMaskedValues()
    {
        if (_mask is { } mask && _maskFade is null)
        {
            _maskFade = new MotionValue<double>(this, 1, MotionVectors.Double);
            _maskFade.Changed += p =>
            {
                mask.Opacity = Math.Clamp(p, 0, 1);
                _maskShift.Y = _travel ? -4 * (1 - Math.Clamp(p, 0, 1)) : 0;
            };
        }
        if (_secretText is { } text && _valueFade is null)
        {
            _valueFade = new MotionValue<double>(this, 0, MotionVectors.Double);
            _valueFade.Changed += p =>
            {
                text.Opacity = Math.Clamp(p, 0, 1);
                _valueShift.Y = _travel && IsRevealed ? 4 * (1 - Math.Clamp(p, 0, 1)) : 0;
                // Once hidden, the value leaves the tree.
                if (!IsRevealed && p <= 0 && _valueFade?.Target <= 0)
                {
                    DropShownValue();
                }
            };
        }
        if (_copySlot is { } slot && _copyPop is null)
        {
            _copyPop = new MotionValue<double>(this, 0, MotionVectors.Double);
            _copyPop.Changed += p =>
            {
                slot.Opacity = Math.Clamp(p, 0, 1);
                var scale = _travel && IsRevealed ? 0.8 + 0.2 * p : 1;
                _copyScale.ScaleX = scale;
                _copyScale.ScaleY = scale;
                if (!IsRevealed && p <= 0 && _copyPop?.Target <= 0)
                {
                    slot.IsVisible = false;
                    if (_copy is not null)
                    {
                        _copy.Value = null;
                    }
                }
            };
        }
    }

    private void ShowValue(bool animate)
    {
        if (Mode != DeveloperSecretMode.Masked || IsSensitive)
        {
            return;
        }
        var value = _secret ?? SecretProvider?.Invoke();
        if (_secret is null && value is not null)
        {
            _fetched = true;
        }
        _secret = value;
        EnsureMaskedValues();
        if (_secretText is not null)
        {
            _secretText.Text = value;
        }
        if (_copySlot is not null)
        {
            _copySlot.IsVisible = value is not null;
        }
        if (_copy is not null)
        {
            _copy.Value = value;
        }
        var level = Level(animate);
        _travel = level == ArcMotionLevel.Full;
        if (level == ArcMotionLevel.Off)
        {
            _maskFade?.Jump(0);
            _valueFade?.Jump(1);
            _copyPop?.Jump(1);
            return;
        }
        _ = _maskFade?.AnimateTo(0, _travel ? DeveloperSprings.FadeOut() : DeveloperSprings.ReducedFade());
        _ = _valueFade?.AnimateTo(1, _travel ? DeveloperSprings.Tween(DeveloperSprings.Standard) : DeveloperSprings.ReducedFade());
        _ = _copyPop?.AnimateTo(1, _travel ? DeveloperSprings.Spring(DeveloperSprings.Quick) : DeveloperSprings.ReducedFade());
    }

    private bool _fetched;

    private void HideValue(bool animate)
    {
        if (Mode != DeveloperSecretMode.Masked)
        {
            return;
        }
        EnsureMaskedValues();
        var level = Level(animate);
        _travel = level == ArcMotionLevel.Full;
        if (level == ArcMotionLevel.Off)
        {
            HideValueNow();
            return;
        }
        _ = _maskFade?.AnimateTo(1, _travel ? DeveloperSprings.FadeOut() : DeveloperSprings.ReducedFade());
        _ = _valueFade?.AnimateTo(0, _travel ? DeveloperSprings.Tween(DeveloperSprings.Standard) : DeveloperSprings.ReducedFade());
        _ = _copyPop?.AnimateTo(0, _travel ? DeveloperSprings.ExitTween() : DeveloperSprings.ReducedFade());
    }

    private void HideValueNow()
    {
        if (Mode != DeveloperSecretMode.Masked)
        {
            return;
        }
        EnsureMaskedValues();
        _travel = false;
        _maskFade?.Jump(1);
        _valueFade?.Jump(0);
        _copyPop?.Jump(0);
        DropShownValue();
        if (_copySlot is not null)
        {
            _copySlot.IsVisible = false;
        }
        if (_copy is not null)
        {
            _copy.Value = null;
        }
    }

    private void DropShownValue()
    {
        if (_secretText is not null)
        {
            _secretText.Text = null;
        }
        // A value fetched for display is not kept once hidden.
        if (_fetched)
        {
            _fetched = false;
            _secret = null;
        }
    }
}
