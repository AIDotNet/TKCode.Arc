using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>Binds code-built parts of <see cref="ReferralCard"/> to its control tokens and to ArcDesign tokens.</summary>
internal static class ReferralCardTokenBinding
{
    /// <summary>Binds <paramref name="property"/> to a <see cref="ReferralCardToken"/> value and returns the target.</summary>
    public static T Tok<T>(this T target, AvaloniaProperty property, ReferralCardTokenKind kind) where T : StyledElement
    {
        target.Bind(property, target.GetResourceObservable(ReferralCardTokens.Key(kind)));
        return target;
    }

    /// <summary>Binds <paramref name="property"/> to an ArcDesign token and returns the target.</summary>
    public static T ReferralArc<T>(this T target, AvaloniaProperty property, ArcDesignTokenKind kind) where T : StyledElement
    {
        ArcTokens.Bind(target, property, kind);
        return target;
    }

    /// <summary>
    /// Shows <paramref name="content"/> in <paramref name="swap"/>: the Arc label swap (old out 100ms, new in 160ms rising 2px)
    /// when <paramref name="animate"/> and motion is full, a short crossfade with reduced motion, otherwise at once.
    /// </summary>
    public static void Swap(this ArcContentSwap swap, object content, bool animate)
    {
        var level = ArcMotion.GetEffectiveLevel(swap);
        swap.Mode = animate && swap.IsLoaded && swap.Content is not null && level != ArcMotionLevel.Off
            ? ContentSwapMode.Label
            : ContentSwapMode.None;
        swap.Content = content;
    }
}

/// <summary>The glyphs of <see cref="ReferralCard"/> that Core does not ship (Lucide paths, names in the comments).</summary>
internal static class ReferralCardIcons
{
    /// <summary>Lucide "gift".</summary>
    public static Geometry Gift => ArcIcons.Parse(
        "M4 8h16a1 1 0 0 1 1 1v2a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V9a1 1 0 0 1 1-1z M12 8v13 M19 12v7a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2v-7 M7.5 8a2.5 2.5 0 0 1 0-5A4.8 8 0 0 1 12 8a4.8 8 0 0 1 4.5-5 2.5 2.5 0 0 1 0 5");

    /// <summary>Lucide "user-check".</summary>
    public static Geometry UserCheck => ArcIcons.Parse(
        "m16 11 2 2 4-4 M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2 M5 7a4 4 0 1 0 8 0a4 4 0 1 0-8 0");

    /// <summary>The glyph of a stage: a clock while invited, a person with a check once signed up, a check once paid.</summary>
    public static Geometry Stage(ReferralStage stage) => stage switch
    {
        ReferralStage.SignedUp => UserCheck,
        ReferralStage.Earned => ArcIcons.CircleCheck,
        _ => ArcIcons.Clock
    };

    /// <summary>The glyph of a share channel (generic marks, no network logos).</summary>
    public static Geometry Share(ReferralShareChannel channel) => channel switch
    {
        ReferralShareChannel.Network => ArcIcons.Users,
        ReferralShareChannel.Email => ArcIcons.Mail,
        _ => ArcIcons.Send
    };
}

/// <summary>
/// The email editor: a bare text box inside the chip field (the field draws the frame). Enter, comma, space, Tab, Backspace
/// and pastes go to the card first, so typed addresses become chips.
/// </summary>
internal sealed class ReferralCardEmailBox : AvaloniaTextBox
{
    private bool _bypassPaste;

    public ReferralCardEmailBox()
    {
        AddHandler(PastingFromClipboardEvent, OnPastingFromClipboard);
    }

    internal ReferralCard? Owner { get; set; }

    protected override Type StyleKeyOverride => typeof(ReferralCardEmailBox);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Owner is { } owner && owner.HandleEmailKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (Owner is { } owner && !string.IsNullOrEmpty(e.Text) && owner.HandleEmailText(e.Text))
        {
            e.Handled = true;
            return;
        }
        base.OnTextInput(e);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Owner?.OnEmailLostFocus();
    }

    /// <summary>
    /// A single-line text box keeps only the first line of a paste, so the clipboard is read here first: text with
    /// separators becomes chips; anything else goes on as a native paste.
    /// </summary>
    private async void OnPastingFromClipboard(object? sender, RoutedEventArgs e)
    {
        if (_bypassPaste)
        {
            _bypassPaste = false;
            return;
        }
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (Owner is null || clipboard is null)
        {
            return;
        }
        e.Handled = true;
        string? text;
        try
        {
            text = await Avalonia.Input.Platform.ClipboardExtensions.TryGetTextAsync(clipboard);
        }
        catch (Exception)
        {
            text = null;
        }
        if (string.IsNullOrEmpty(text) || Owner is null)
        {
            return;
        }
        if (Owner.HandleEmailPaste(text))
        {
            return;
        }
        _bypassPaste = true;
        Paste();
    }
}

/// <summary>
/// The frame of the chip field: a bordered surface that shows the accent edge while the editor has focus and the danger
/// edge while an address is not valid (state from <see cref="IsInvalid"/>; the theme draws it).
/// </summary>
[PseudoClasses(InvalidPseudoClass)]
internal sealed class ReferralCardFrame : ContentControl
{
    public const string InvalidPseudoClass = ":invalid";

    public static readonly StyledProperty<bool> IsInvalidProperty =
        AvaloniaProperty.Register<ReferralCardFrame, bool>(nameof(IsInvalid));

    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    protected override Type StyleKeyOverride => typeof(ReferralCardFrame);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsInvalidProperty)
        {
            PseudoClasses.Set(InvalidPseudoClass, IsInvalid);
        }
    }
}

/// <summary>A chip's remove button: a small round X that never takes focus (the field keeps it; Backspace removes too).</summary>
internal sealed class ReferralCardChipRemove : AvaloniaButton
{
    static ReferralCardChipRemove()
    {
        FocusableProperty.OverrideDefaultValue<ReferralCardChipRemove>(false);
    }

    protected override Type StyleKeyOverride => typeof(ReferralCardChipRemove);
}

/// <summary>
/// One address in the chip field: a pill with the address and a remove button. An address that is not valid gets a danger
/// edge, a tint and an alert glyph, and says so in its name, so the state never relies on color alone.
/// </summary>
internal sealed class ReferralCardChip : Border
{
    private readonly ArcGlyph _alert;
    private readonly TextBlock _text;
    private readonly List<IDisposable> _colors = [];

    public ReferralCardChip(string email, bool valid, ReferralCardLabels labels)
    {
        Email = email;
        IsValid = valid;
        VerticalAlignment = VerticalAlignment.Center;
        BorderThickness = new Thickness(1);
        this.Tok(HeightProperty, ReferralCardTokenKind.ChipHeight)
            .Tok(PaddingProperty, ReferralCardTokenKind.ChipPadding)
            .BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        _alert = new ArcGlyph { Data = ArcIcons.CircleAlert, Size = 13, VerticalAlignment = VerticalAlignment.Center, IsVisible = !valid }
            .ReferralArc(ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Danger);
        _text = new TextBlock { Text = email, MaxWidth = 220, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }
            .Tok(TextBlock.FontSizeProperty, ReferralCardTokenKind.ChipFontSize);
        Remove = new ReferralCardChipRemove { VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(Remove, labels.RemoveChip(email));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(_alert);
        row.Children.Add(_text);
        row.Children.Add(Remove);
        Child = row;
        AutomationProperties.SetName(this, valid ? email : email + ", " + labels.NotValid);
        _colors.Add(ArcTokens.Bind(this, BackgroundProperty, valid ? ArcDesignTokenKind.SurfaceMuted : ArcDesignTokenKind.DangerSubtle));
        _colors.Add(ArcTokens.Bind(this, BorderBrushProperty, valid ? ArcDesignTokenKind.BorderSubtle : ArcDesignTokenKind.Danger));
        _colors.Add(ArcTokens.Bind(_text, TextBlock.ForegroundProperty, valid ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.Danger));
    }

    /// <summary>The address.</summary>
    public string Email { get; }

    /// <summary>Whether the address reads as an email address.</summary>
    public bool IsValid { get; }

    /// <summary>The remove button.</summary>
    public ReferralCardChipRemove Remove { get; }
}

/// <summary>
/// One person in the list: the avatar with initials (or the photo), the name over the address (or the address alone),
/// the date the invite went out, and the stage as a glyph plus a word that crossfades in place when it changes. Long
/// addresses truncate while the date and the stage keep their room.
/// </summary>
internal sealed class ReferralCardRow : Grid
{
    private readonly Avatar _avatar;
    private readonly TextBlock _name;
    private readonly TextBlock _email;
    private readonly TextBlock _date;
    private readonly ArcContentSwap _stage;
    private ReferralStage? _shownStage;
    private string? _shownStageText;

    public ReferralCardRow()
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto");
        this.Tok(HeightProperty, ReferralCardTokenKind.RowHeight);
        ColumnSpacing = 12;
        _avatar = new Avatar { Size = AvatarSize.Small, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetAccessibilityView(_avatar, AccessibilityView.Raw);
        _name = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis }
            .Tok(TextBlock.FontSizeProperty, ReferralCardTokenKind.RowFontSize)
            .ReferralArc(TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium)
            .ReferralArc(TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _email = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis }
            .Tok(TextBlock.FontSizeProperty, ReferralCardTokenKind.RowDetailFontSize)
            .ReferralArc(TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
        names.Children.Add(_name);
        names.Children.Add(_email);
        _date = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontFeatures = ReferralCard.TabularNumbers }
            .Tok(TextBlock.FontSizeProperty, ReferralCardTokenKind.RowDetailFontSize)
            .ReferralArc(TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        _stage = new ArcContentSwap
        {
            Mode = ContentSwapMode.None,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        }.Tok(MinWidthProperty, ReferralCardTokenKind.StageWidth);
        Children.Add(_avatar);
        Children.Add(names);
        Children.Add(_date);
        Children.Add(_stage);
        SetColumn(names, 1);
        SetColumn(_date, 2);
        SetColumn(_stage, 3);
    }

    /// <summary>The referral on screen.</summary>
    public Referral? Referral { get; private set; }

    /// <summary>The stage word on screen.</summary>
    public string StageText => _shownStageText ?? string.Empty;

    /// <summary>The stage on screen.</summary>
    public ReferralStage? Stage => _shownStage;

    /// <summary>The name line on screen.</summary>
    public string NameText => _name.Text ?? string.Empty;

    /// <summary>Shows <paramref name="referral"/>; a stage change crossfades when <paramref name="animate"/>.</summary>
    public void Show(Referral referral, string stageText, string date, bool animate)
    {
        Referral = referral;
        var hasName = !string.IsNullOrWhiteSpace(referral.Name);
        _name.Text = hasName ? referral.Name!.Trim() : referral.Email;
        _email.Text = hasName ? referral.Email : string.Empty;
        _email.IsVisible = hasName;
        _date.Text = date;
        _avatar.DisplayName = ReferralCardFormat.AvatarName(referral);
        _avatar.Source = referral.Avatar;
        if (_shownStage != referral.Stage || _shownStageText != stageText)
        {
            var changed = _shownStage is not null && _shownStage != referral.Stage;
            _shownStage = referral.Stage;
            _shownStageText = stageText;
            _stage.Swap(StageView(referral.Stage, stageText), animate && changed);
        }
        AutomationProperties.SetName(this, ReferralCardFormat.Who(referral) + ", " + stageText + ", " + date);
    }

    private static Control StageView(ReferralStage stage, string text)
    {
        var ink = stage switch
        {
            ReferralStage.Earned => ArcDesignTokenKind.Success,
            ReferralStage.SignedUp => ArcDesignTokenKind.Foreground,
            _ => ArcDesignTokenKind.TextMuted
        };
        var glyph = new ArcGlyph { Data = ReferralCardIcons.Stage(stage), VerticalAlignment = VerticalAlignment.Center }
            .Tok(ArcGlyph.SizeProperty, ReferralCardTokenKind.StageGlyphSize)
            .ReferralArc(ArcGlyph.ForegroundProperty, ink);
        var word = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }
            .Tok(TextBlock.FontSizeProperty, ReferralCardTokenKind.RowDetailFontSize)
            .ReferralArc(TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium)
            .ReferralArc(TextBlock.ForegroundProperty, stage == ReferralStage.Invited ? ArcDesignTokenKind.TextSecondary : ink);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(glyph);
        row.Children.Add(word);
        return row;
    }
}

/// <summary>
/// The milestone bar: one segment per paid referral needed (up to 12), filled from the left in 240ms as referrals pay, the
/// accent turning to the success color when the goal is reached (also 240ms). Drawn in <see cref="Render"/>, so a fill never
/// lays anything out. Reads as a progress bar with its value as help text ("4 of 5 paid referrals").
/// <para>
/// Code-driven (<see cref="MotionValue{T}"/>) because the fill moves across segment boundaries and the unlock blends two
/// token colors in the drawing; neither is a property a ControlTheme transition can target.
/// </para>
/// </summary>
internal sealed class ReferralCardMilestoneBar : Control
{
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<ReferralCardMilestoneBar, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<ReferralCardMilestoneBar, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<IBrush?> DoneBrushProperty =
        AvaloniaProperty.Register<ReferralCardMilestoneBar, IBrush?>(nameof(DoneBrush));

    public static readonly StyledProperty<double> SegmentHeightProperty =
        AvaloniaProperty.Register<ReferralCardMilestoneBar, double>(nameof(SegmentHeight), 8);

    public static readonly StyledProperty<double> SegmentGapProperty =
        AvaloniaProperty.Register<ReferralCardMilestoneBar, double>(nameof(SegmentGap), 4);

    public static readonly StyledProperty<CornerRadius> SegmentCornerRadiusProperty =
        AvaloniaProperty.Register<ReferralCardMilestoneBar, CornerRadius>(nameof(SegmentCornerRadius), new CornerRadius(4));

    /// <summary>The fill and the unlock blend: 240ms on the standard curve.</summary>
    internal static readonly TimeSpan FillDuration = TimeSpan.FromMilliseconds(240);

    private MotionValue<double>? _fill;
    private MotionValue<double>? _unlock;
    private double _fillShown;
    private double _unlockShown;
    private int _segments = 5;
    private double _fillTarget;
    private bool _unlocked;
    private bool _drawn;

    static ReferralCardMilestoneBar()
    {
        AffectsRender<ReferralCardMilestoneBar>(TrackBrushProperty, FillBrushProperty, DoneBrushProperty, SegmentGapProperty, SegmentCornerRadiusProperty);
        AffectsMeasure<ReferralCardMilestoneBar>(SegmentHeightProperty);
    }

    public ReferralCardMilestoneBar()
    {
        IsHitTestVisible = false;
    }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    public IBrush? DoneBrush { get => GetValue(DoneBrushProperty); set => SetValue(DoneBrushProperty, value); }

    public double SegmentHeight { get => GetValue(SegmentHeightProperty); set => SetValue(SegmentHeightProperty, value); }

    public double SegmentGap { get => GetValue(SegmentGapProperty); set => SetValue(SegmentGapProperty, value); }

    public CornerRadius SegmentCornerRadius { get => GetValue(SegmentCornerRadiusProperty); set => SetValue(SegmentCornerRadiusProperty, value); }

    /// <summary>How many segments are drawn.</summary>
    public int Segments => _segments;

    /// <summary>The filled share the bar is heading to, in segments.</summary>
    public double FillTarget => _fillTarget;

    /// <summary>Whether the bar shows the unlocked color (or is heading to it).</summary>
    public bool IsUnlocked => _unlocked;

    /// <summary>Sets the segments, the fill and the unlocked state; <paramref name="animate"/> fills and blends over 240ms.</summary>
    public void Set(int segments, double filled, bool unlocked, bool animate)
    {
        var segmentsChanged = segments != _segments;
        _segments = Math.Max(1, segments);
        _fillTarget = Math.Clamp(filled, 0, _segments);
        _unlocked = unlocked;
        var run = animate && _drawn && !segmentsChanged && IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        _drawn = true;
        var spec = MotionSpec.Tween(FillDuration, ArcEasings.Standard);
        var fill = _fill ??= Value(v => _fillShown = v, _fillShown);
        var unlock = _unlock ??= Value(v => _unlockShown = v, _unlockShown);
        if (run)
        {
            _ = fill.AnimateTo(_fillTarget, spec);
            _ = unlock.AnimateTo(unlocked ? 1 : 0, spec);
        }
        else
        {
            fill.Jump(_fillTarget);
            unlock.Jump(unlocked ? 1 : 0);
            _fillShown = _fillTarget;
            _unlockShown = unlocked ? 1 : 0;
        }
        InvalidateVisual();
    }

    private MotionValue<double> Value(Action<double> write, double start)
    {
        var value = new MotionValue<double>(this, start, MotionVectors.Double);
        value.Changed += v =>
        {
            write(v);
            InvalidateVisual();
        };
        return value;
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsFinite(availableSize.Width) ? availableSize.Width : 0, SegmentHeight);

    public override void Render(DrawingContext context)
    {
        var count = _segments;
        var gap = SegmentGap;
        var width = Bounds.Width;
        var height = Math.Min(SegmentHeight, Bounds.Height);
        if (width <= 0 || height <= 0)
        {
            return;
        }
        var segment = (width - gap * (count - 1)) / count;
        if (segment <= 0)
        {
            return;
        }
        var radius = Math.Min(SegmentCornerRadius.TopLeft, height / 2);
        var fill = Blend(FillBrush, DoneBrush, _unlockShown);
        for (var i = 0; i < count; i++)
        {
            var x = i * (segment + gap);
            var rect = new Rect(x, 0, segment, height);
            if (TrackBrush is { } track)
            {
                context.DrawRectangle(track, null, new RoundedRect(rect, radius));
            }
            var share = Math.Clamp(_fillShown - i, 0, 1);
            if (share > 0.001 && fill is not null)
            {
                using (context.PushClip(new RoundedRect(rect, radius)))
                {
                    context.DrawRectangle(fill, null, new Rect(x, 0, segment * share, height));
                }
            }
        }
    }

    private static IBrush? Blend(IBrush? from, IBrush? to, double amount)
    {
        if (amount <= 0.001 || to is null)
        {
            return from;
        }
        if (amount >= 0.999 || from is null)
        {
            return to;
        }
        if (from is ISolidColorBrush a && to is ISolidColorBrush b)
        {
            var t = amount;
            var color = Color.FromArgb(
                (byte)Math.Round(a.Color.A + (b.Color.A - a.Color.A) * t),
                (byte)Math.Round(a.Color.R + (b.Color.R - a.Color.R) * t),
                (byte)Math.Round(a.Color.G + (b.Color.G - a.Color.G) * t),
                (byte)Math.Round(a.Color.B + (b.Color.B - a.Color.B) * t));
            return new ImmutableSolidColorBrush(color);
        }
        return amount < 0.5 ? from : to;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new BarPeer(this);

    private sealed class BarPeer(ReferralCardMilestoneBar owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
