using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The base of <see cref="ShareSheet"/>'s buttons: a keyboard focus ring built on first use, and a <c>:hover</c> class
/// that a touch pointer never sets (hover styles are for mice and pens).
/// </summary>
[TemplatePart(RootPart, typeof(Panel))]
[PseudoClasses(HoverPseudoClass)]
internal abstract class ShareSheetPressable : AvaloniaButton
{
    internal const string RootPart = "PART_Root";
    internal const string HoverPseudoClass = ":hover";

    private LazyFocusRing? _focusRing;

    static ShareSheetPressable()
    {
        ClipToBoundsProperty.OverrideDefaultValue<ShareSheetPressable>(false);
        SpecialFocusModality.Watch();
    }

    /// <summary>Resource key of the ring's margin.</summary>
    protected virtual object FocusRingMarginKey => ShareSheetTokens.Key(ShareSheetTokenKind.FocusRingMargin);

    /// <summary>Resource key of the ring's corner radius.</summary>
    protected virtual object FocusRingRadiusKey => ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPill);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, FocusRingMarginKey, FocusRingRadiusKey);
        _focusRing.Attach(e.NameScope.Find<Panel>(RootPart), below: false);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }
}

/// <summary>
/// The Share button: a pill with the share glyph and the label. The same control is the button face inside the floating
/// shape (<c>:face</c>, transparent: the shape paints the fill).
/// </summary>
[PseudoClasses(FacePseudoClass)]
internal sealed class ShareSheetTrigger : ShareSheetPressable
{
    internal const string FacePseudoClass = ":face";

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ShareSheetTrigger, string?>(nameof(Label));

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<ShareSheetTrigger, Geometry?>(nameof(Glyph));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <summary>Marks the copy that lives inside the floating shape.</summary>
    public bool IsFace
    {
        get => PseudoClasses.Contains(FacePseudoClass);
        init => PseudoClasses.Set(FacePseudoClass, value);
    }

    protected override Type StyleKeyOverride => typeof(ShareSheetTrigger);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label ?? string.Empty);
        }
    }
}

/// <summary>A round icon button: the panel's close button and a chip's Remove button.</summary>
internal sealed class ShareSheetIconButton : ShareSheetPressable
{
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<ShareSheetIconButton, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<ShareSheetIconButton, double>(nameof(GlyphSize), 16);

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

    protected override Type StyleKeyOverride => typeof(ShareSheetIconButton);
}

/// <summary>The state of <see cref="ShareSheetCopyButton"/>.</summary>
internal enum ShareSheetCopyState
{
    Idle,
    Copied,
    Failed
}

/// <summary>
/// The copy button at the end of the link row: a raised pill whose glyph and label cross over to a green "Copied" (or
/// "Retry" when the clipboard refused), while its width springs to the new label.
/// </summary>
[TemplatePart(LabelPart, typeof(ArcContentSwap))]
[PseudoClasses(CopiedPseudoClass, FailedPseudoClass)]
internal sealed class ShareSheetCopyButton : ShareSheetPressable
{
    internal const string LabelPart = "PART_Label";
    internal const string CopiedPseudoClass = ":copied";
    internal const string FailedPseudoClass = ":failed";

    private ArcContentSwap? _label;
    private ShareSheetCopyState _state;
    private string _copy = "Copy";
    private string _copied = "Copied";
    private string _retry = "Retry";

    public ShareSheetCopyState State => _state;

    protected override Type StyleKeyOverride => typeof(ShareSheetCopyButton);

    /// <summary>Sets the three labels (the current one updates in place).</summary>
    public void SetWords(string copy, string copied, string retry)
    {
        _copy = copy;
        _copied = copied;
        _retry = retry;
        Refresh(animate: false);
    }

    public void SetState(ShareSheetCopyState state)
    {
        if (_state == state)
        {
            return;
        }
        _state = state;
        PseudoClasses.Set(CopiedPseudoClass, state == ShareSheetCopyState.Copied);
        PseudoClasses.Set(FailedPseudoClass, state == ShareSheetCopyState.Failed);
        Refresh(animate: true);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _label = e.NameScope.Find<ArcContentSwap>(LabelPart);
        Refresh(animate: false);
    }

    private void Refresh(bool animate)
    {
        if (_label is null)
        {
            return;
        }
        var (glyph, text) = _state switch
        {
            ShareSheetCopyState.Copied => (ArcIcons.Check, _copied),
            ShareSheetCopyState.Failed => (ArcIcons.RotateCw, _retry),
            _ => (ArcIcons.Copy, _copy)
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Bind(StackPanel.SpacingProperty, row.GetResourceObservable(ShareSheetTokens.Key(ShareSheetTokenKind.CopyGap)));
        var icon = new ArcGlyph { Data = glyph, VerticalAlignment = VerticalAlignment.Center };
        icon.Bind(ArcGlyph.SizeProperty, icon.GetResourceObservable(ShareSheetTokens.Key(ShareSheetTokenKind.CopyGlyphSize)));
        row.Children.Add(icon);
        row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        if (_state == ShareSheetCopyState.Copied)
        {
            ArcTokens.Bind(row, TextElement.ForegroundProperty, ArcDesignTokenKind.Success);
        }
        var mode = _label.Mode;
        if (!animate)
        {
            _label.Mode = ContentSwapMode.None;
        }
        _label.Content = row;
        _label.Mode = mode;
    }
}

/// <summary>
/// The row under the link that shows the chosen access level and opens the list of levels; the chevron turns over while
/// the list is open.
/// </summary>
[PseudoClasses(ExpandedPseudoClass)]
internal sealed class ShareSheetAccessRow : ShareSheetPressable
{
    internal const string ExpandedPseudoClass = ":expanded";

    public static readonly StyledProperty<ShareSheetAccess?> AccessProperty =
        AvaloniaProperty.Register<ShareSheetAccessRow, ShareSheetAccess?>(nameof(Access));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ShareSheetAccessRow, bool>(nameof(IsExpanded));

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<ShareSheetAccessRow, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ShareSheetAccessRow, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ShareSheetAccessRow, string?>(nameof(Description));

    public ShareSheetAccess? Access { get => GetValue(AccessProperty); set => SetValue(AccessProperty, value); }

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    protected override object FocusRingMarginKey => ShareSheetTokens.Key(ShareSheetTokenKind.InsetFocusRingMargin);

    protected override object FocusRingRadiusKey => ShareSheetTokens.Key(ShareSheetTokenKind.InsetFocusRadius);

    protected override Type StyleKeyOverride => typeof(ShareSheetAccessRow);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            PseudoClasses.Set(ExpandedPseudoClass, IsExpanded);
            AutomationProperties.SetItemStatus(this, IsExpanded ? "Expanded" : "Collapsed");
        }
        else if (change.Property == AccessProperty)
        {
            Glyph = Access?.Glyph;
            Label = Access?.Label ?? Access?.Value;
            Description = Access?.Description;
            AutomationProperties.SetName(this, Access?.Label ?? string.Empty);
            AutomationProperties.SetHelpText(this, Access?.Description ?? string.Empty);
        }
    }
}

/// <summary>One option of the access list (a listbox option): glyph, name, description and a check when chosen.</summary>
[PseudoClasses(SelectedPseudoClass)]
internal sealed class ShareSheetAccessOption : ShareSheetPressable
{
    internal const string SelectedPseudoClass = ":selected";

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<ShareSheetAccessOption, bool>(nameof(IsSelected));

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<ShareSheetAccessOption, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ShareSheetAccessOption, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ShareSheetAccessOption, string?>(nameof(Description));

    public ShareSheetAccessOption(ShareSheetAccess access)
    {
        Access = access;
        Glyph = access.Glyph;
        Label = access.Label ?? access.Value;
        Description = access.Description;
        AutomationProperties.SetName(this, access.Label ?? access.Value ?? string.Empty);
        AutomationProperties.SetHelpText(this, access.Description ?? string.Empty);
    }

    public ShareSheetAccess Access { get; }

    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    protected override object FocusRingRadiusKey => ShareSheetTokens.Key(ShareSheetTokenKind.OptionFocusRadius);

    protected override Type StyleKeyOverride => typeof(ShareSheetAccessOption);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(SelectedPseudoClass, IsSelected);
            AutomationProperties.SetItemStatus(this, IsSelected ? "Selected" : string.Empty);
        }
    }
}

/// <summary>The state of a channel button.</summary>
internal enum ShareSheetChannelState
{
    Idle,
    Pending,
    Done,
    Failed
}

/// <summary>
/// A channel's pill: its glyph and label. While the channel runs the glyph is a spinner; when it finishes the label
/// blurs over to the channel's done word (or "Failed") for two seconds.
/// </summary>
[TemplatePart(GlyphPart, typeof(ArcContentSwap))]
[TemplatePart(LabelPart, typeof(ArcContentSwap))]
[PseudoClasses(PendingPseudoClass, DonePseudoClass, FailedPseudoClass)]
internal sealed class ShareSheetChannelButton : ShareSheetPressable
{
    internal const string GlyphPart = "PART_Glyph";
    internal const string LabelPart = "PART_Label";
    internal const string PendingPseudoClass = ":pending";
    internal const string DonePseudoClass = ":done";
    internal const string FailedPseudoClass = ":failed";

    private ArcContentSwap? _glyph;
    private ArcContentSwap? _label;
    private ShareSheetChannelState _state;
    private string _failedWord = "Failed";

    public ShareSheetChannelButton(ShareSheetChannel channel)
    {
        Channel = channel;
        AutomationProperties.SetName(this, channel.Label ?? channel.Id ?? string.Empty);
    }

    public ShareSheetChannel Channel { get; }

    public ShareSheetChannelState State => _state;

    /// <summary>Cancels the reset back to idle when the state changes again.</summary>
    internal int Generation { get; set; }

    protected override Type StyleKeyOverride => typeof(ShareSheetChannelButton);

    public void SetFailedWord(string word) => _failedWord = word;

    public void SetState(ShareSheetChannelState state)
    {
        if (_state == state)
        {
            return;
        }
        var wasPending = _state == ShareSheetChannelState.Pending;
        _state = state;
        PseudoClasses.Set(PendingPseudoClass, state == ShareSheetChannelState.Pending);
        PseudoClasses.Set(DonePseudoClass, state == ShareSheetChannelState.Done);
        PseudoClasses.Set(FailedPseudoClass, state == ShareSheetChannelState.Failed);
        if (state == ShareSheetChannelState.Pending || wasPending)
        {
            RefreshGlyph();
        }
        RefreshLabel();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _glyph = e.NameScope.Find<ArcContentSwap>(GlyphPart);
        _label = e.NameScope.Find<ArcContentSwap>(LabelPart);
        RefreshGlyph();
        RefreshLabel();
    }

    private void RefreshGlyph()
    {
        if (_glyph is null)
        {
            return;
        }
        if (_state == ShareSheetChannelState.Pending)
        {
            var spinner = new ArcSpinner();
            spinner.Bind(WidthProperty, spinner.GetResourceObservable(ShareSheetTokens.Key(ShareSheetTokenKind.GlyphSize)));
            spinner.Bind(HeightProperty, spinner.GetResourceObservable(ShareSheetTokens.Key(ShareSheetTokenKind.GlyphSize)));
            _glyph.Content = spinner;
            return;
        }
        var glyph = new ArcGlyph { Data = Channel.Glyph };
        glyph.Bind(ArcGlyph.SizeProperty, glyph.GetResourceObservable(ShareSheetTokens.Key(ShareSheetTokenKind.GlyphSize)));
        _glyph.Content = glyph;
    }

    private void RefreshLabel()
    {
        if (_label is null)
        {
            return;
        }
        _label.Content = _state switch
        {
            ShareSheetChannelState.Done => Channel.DoneLabel,
            ShareSheetChannelState.Failed => _failedWord,
            _ => Channel.Label ?? Channel.Id ?? string.Empty
        };
    }
}

/// <summary>
/// A recent person: their avatar over their first name, a toggle (pressed while picked). A picked avatar gets an accent
/// ring with a gap and a check badge at its corner.
/// </summary>
[TemplatePart(RootPart, typeof(Panel))]
[TemplatePart(AvatarPart, typeof(ShareSheetAvatar))]
[TemplatePart(NamePart, typeof(TextBlock))]
[PseudoClasses(HoverPseudoClass)]
internal sealed class ShareSheetPersonButton : ToggleButton
{
    internal const string RootPart = "PART_Root";
    internal const string AvatarPart = "PART_Avatar";
    internal const string NamePart = "PART_Name";
    internal const string HoverPseudoClass = ":hover";

    private LazyFocusRing? _focusRing;

    static ShareSheetPersonButton()
    {
        ClipToBoundsProperty.OverrideDefaultValue<ShareSheetPersonButton>(false);
    }

    public ShareSheetPersonButton(ShareSheetPerson person)
    {
        Person = person;
        DataContext = person;
        AutomationProperties.SetName(this, person.DisplayName);
    }

    public ShareSheetPerson Person { get; }

    /// <summary>The avatar, where a pick's flight starts.</summary>
    public ShareSheetAvatar? Avatar { get; private set; }

    protected override Type StyleKeyOverride => typeof(ShareSheetPersonButton);

    /// <summary>The panel decides what a press does (it may also come from the chip's Remove).</summary>
    protected override void Toggle()
    {
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Avatar = e.NameScope.Find<ShareSheetAvatar>(AvatarPart);
        if (Avatar is not null)
        {
            Avatar.Person = Person;
        }
        if (e.NameScope.Find<TextBlock>(NamePart) is { } name)
        {
            name.Text = Person.ShortName;
        }
        _focusRing ??= new LazyFocusRing(this, ShareSheetTokens.Key(ShareSheetTokenKind.FocusRingMargin),
            ShareSheetTokens.Key(ShareSheetTokenKind.PersonFocusRadius));
        _focusRing.Attach(e.NameScope.Find<Panel>(RootPart), below: false);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }
}

/// <summary>
/// The Send button. With nobody picked it looks quiet but still answers (the recipients row shakes); with people picked
/// it reads "Send to 3 people" and only the count rolls when it changes; while sending it shows a spinner.
/// </summary>
[TemplatePart(LabelPart, typeof(ArcContentSwap))]
[PseudoClasses(EmptyPseudoClass, SendingPseudoClass)]
internal sealed class ShareSheetSendButton : ShareSheetPressable
{
    internal const string LabelPart = "PART_Label";
    internal const string EmptyPseudoClass = ":empty";
    internal const string SendingPseudoClass = ":sending";

    private ArcContentSwap? _label;
    private ShareSheetCount? _count;
    private TextBlock? _before;
    private TextBlock? _after;
    private int _shownCount = -1;
    private bool _shownSending;
    private string _send = "Send";
    private string _sending = "Sending";
    private Func<int, string> _sendTo = n => "Send to {0}";

    public int Count { get; private set; }

    public bool IsSending { get; private set; }

    protected override Type StyleKeyOverride => typeof(ShareSheetSendButton);

    public void SetWords(string send, string sending, Func<int, string> sendTo)
    {
        _send = send;
        _sending = sending;
        _sendTo = sendTo;
        _shownCount = -1;
        Refresh(animate: false);
    }

    public void SetState(int count, bool sending, bool animate = true)
    {
        if (count == Count && sending == IsSending)
        {
            return;
        }
        Count = count;
        IsSending = sending;
        PseudoClasses.Set(EmptyPseudoClass, count == 0 && !sending);
        PseudoClasses.Set(SendingPseudoClass, sending);
        Refresh(animate);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _label = e.NameScope.Find<ArcContentSwap>(LabelPart);
        PseudoClasses.Set(EmptyPseudoClass, Count == 0 && !IsSending);
        _shownCount = -1;
        Refresh(animate: false);
    }

    private void Refresh(bool animate)
    {
        if (_label is null)
        {
            return;
        }
        var wasCounting = _shownCount > 0 && !_shownSending;
        var counting = Count > 0 && !IsSending;
        AutomationProperties.SetName(this, IsSending ? _sending : Count == 0 ? _send : string.Format(_sendTo(Count), Count));
        if (counting && wasCounting && _count is not null && _before is not null && _after is not null)
        {
            // Same label, another number: the words change in place and only the digits roll.
            SplitFormat(_sendTo(Count), out var before, out var after);
            _before.Text = before;
            _after.Text = after;
            _count.RollTo(Count, animate);
            _shownCount = Count;
            return;
        }
        Control content;
        if (IsSending)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Bind(StackPanel.SpacingProperty, row.GetResourceObservable(ShareSheetTokens.Key(ShareSheetTokenKind.GlyphGap)));
            var spinner = new ArcSpinner { VerticalAlignment = VerticalAlignment.Center };
            spinner.Bind(WidthProperty, spinner.GetResourceObservable(ShareSheetTokens.Key(ShareSheetTokenKind.GlyphSize)));
            spinner.Bind(HeightProperty, spinner.GetResourceObservable(ShareSheetTokens.Key(ShareSheetTokenKind.GlyphSize)));
            row.Children.Add(spinner);
            row.Children.Add(new TextBlock { Text = _sending, VerticalAlignment = VerticalAlignment.Center });
            content = row;
            _count = null;
        }
        else if (counting)
        {
            SplitFormat(_sendTo(Count), out var before, out var after);
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            _before = new TextBlock { Text = before, VerticalAlignment = VerticalAlignment.Center };
            _after = new TextBlock { Text = after, VerticalAlignment = VerticalAlignment.Center };
            _count = new ShareSheetCount { VerticalAlignment = VerticalAlignment.Center };
            _count.RollTo(Count, animate: false);
            row.Children.Add(_before);
            row.Children.Add(_count);
            row.Children.Add(_after);
            content = row;
        }
        else
        {
            content = new TextBlock { Text = _send, VerticalAlignment = VerticalAlignment.Center };
            _count = null;
        }
        var mode = _label.Mode;
        if (!animate)
        {
            _label.Mode = ContentSwapMode.None;
        }
        _label.Content = content;
        _label.Mode = mode;
        _shownCount = Count;
        _shownSending = IsSending;
    }

    internal static void SplitFormat(string format, out string before, out string after)
    {
        var at = format.IndexOf("{0}", StringComparison.Ordinal);
        if (at < 0)
        {
            before = format + " ";
            after = string.Empty;
            return;
        }
        before = format[..at];
        after = format[(at + 3)..];
    }
}

/// <summary>
/// A number whose digits roll one step when it changes: the old value leaves upward (count up) or downward (count down)
/// and fades, the new one comes in from the other side on a quick spring.
/// </summary>
internal sealed class ShareSheetCount : Control
{
    private readonly TextBlock _current = new();
    private readonly TextBlock _leaving = new() { Opacity = 0 };
    private readonly TranslateTransform _currentShift = new();
    private readonly TranslateTransform _leavingShift = new();
    private MotionValue<double>? _progress;
    private int _value;
    private int _direction = 1;
    private double _distance = 14;

    public ShareSheetCount()
    {
        _current.RenderTransform = _currentShift;
        _leaving.RenderTransform = _leavingShift;
        LogicalChildren.Add(_leaving);
        LogicalChildren.Add(_current);
        VisualChildren.Add(_leaving);
        VisualChildren.Add(_current);
        ClipToBounds = false;
    }

    public int Value => _value;

    /// <summary>The leaving copy, for tests.</summary>
    internal TextBlock Leaving => _leaving;

    public void RollTo(int value, bool animate)
    {
        if (value == _value && _current.Text is not null)
        {
            return;
        }
        _direction = value >= _value ? 1 : -1;
        _leaving.Text = _current.Text;
        _value = value;
        _current.Text = value.ToString(System.Globalization.CultureInfo.CurrentCulture);
        if (!animate || _leaving.Text is null || !ArcMotion.IsFull(this))
        {
            _progress?.Jump(1);
            Apply(1);
            return;
        }
        // Code-driven: the digit's travel is a spring that must restart from the current value when the count changes
        // again mid-roll, and the two copies live in this control rather than in a template.
        _distance = this.TryFindResource(ShareSheetTokens.Key(ShareSheetTokenKind.DigitRoll), out var d) && d is double roll ? roll : 14;
        _progress ??= CreateProgress();
        _progress.Jump(0);
        _ = _progress.AnimateTo(1, MotionSpec.Spring(ShareSheetMotion.Roll));
    }

    private MotionValue<double> CreateProgress()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += Apply;
        return value;
    }

    private void Apply(double p)
    {
        var distance = _distance;
        _currentShift.Y = (1 - p) * distance * _direction;
        _current.Opacity = Math.Clamp(p * 2.5, 0, 1);
        _leavingShift.Y = -p * distance * _direction;
        _leaving.Opacity = Math.Clamp(1 - p * 2.5, 0, 1);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _leaving.Measure(availableSize);
        _current.Measure(availableSize);
        return _current.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _current.Arrange(new Rect(finalSize));
        _leaving.Arrange(new Rect(new Point((finalSize.Width - _leaving.DesiredSize.Width) / 2, 0), _leaving.DesiredSize));
        return finalSize;
    }
}

/// <summary>
/// A round avatar: the person's picture when they have one, otherwise a soft three-color gradient seeded by their id, so
/// the same person always looks the same.
/// </summary>
internal sealed class ShareSheetAvatar : Control
{
    public static readonly StyledProperty<ShareSheetPerson?> PersonProperty =
        AvaloniaProperty.Register<ShareSheetAvatar, ShareSheetPerson?>(nameof(Person));

    private IBrush? _base;
    private IBrush? _glow;
    private ArcImage? _image;
    private Size _clipSize;

    static ShareSheetAvatar()
    {
        AffectsRender<ShareSheetAvatar>(PersonProperty);
    }

    public ShareSheetPerson? Person { get => GetValue(PersonProperty); set => SetValue(PersonProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PersonProperty)
        {
            BuildBrushes();
            UpdateImage();
        }
    }

    private void BuildBrushes()
    {
        var seed = Seed(Person?.Id ?? Person?.Name);
        var h1 = seed % 360;
        var h2 = (h1 + 70 + (seed >> 9) % 80) % 360;
        var h3 = (h1 + 200 + (seed >> 17) % 60) % 360;
        var c1 = Oklch.ToColor(0.72, 0.16, h1);
        var c2 = Oklch.ToColor(0.66, 0.19, h2);
        var c3 = Oklch.ToColor(0.82, 0.14, h3);
        var angle = (seed >> 5) % 4;
        var (start, end) = angle switch
        {
            0 => (new RelativePoint(0, 0, RelativeUnit.Relative), new RelativePoint(1, 1, RelativeUnit.Relative)),
            1 => (new RelativePoint(1, 0, RelativeUnit.Relative), new RelativePoint(0, 1, RelativeUnit.Relative)),
            2 => (new RelativePoint(0.5, 0, RelativeUnit.Relative), new RelativePoint(0.5, 1, RelativeUnit.Relative)),
            _ => (new RelativePoint(0, 0.5, RelativeUnit.Relative), new RelativePoint(1, 0.5, RelativeUnit.Relative))
        };
        _base = new ImmutableLinearGradientBrush(
            [new ImmutableGradientStop(0, c1), new ImmutableGradientStop(1, c2)],
            1, null, null, GradientSpreadMethod.Pad, start, end);
        var cx = 0.25 + (seed >> 11) % 50 / 100.0;
        var cy = 0.25 + (seed >> 13) % 50 / 100.0;
        _glow = new ImmutableRadialGradientBrush(new RadialGradientBrush
        {
            Center = new RelativePoint(cx, cy, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(cx, cy, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.6, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.6, RelativeUnit.Relative),
            GradientStops = { new GradientStop(c3, 0), new GradientStop(Oklch.WithAlpha(c3, 0), 1) }
        });
    }

    /// <summary>A stable hash (string.GetHashCode changes between runs).</summary>
    internal static int Seed(string? text)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var c in text ?? string.Empty)
            {
                hash = (hash ^ c) * 16777619;
            }
            return hash & 0x7fffffff;
        }
    }

    private void UpdateImage()
    {
        if (Person?.Avatar is { } source)
        {
            if (_image is null)
            {
                _image = new ArcImage { Stretch = Stretch.UniformToFill, IsHitTestVisible = false };
                LogicalChildren.Add(_image);
                VisualChildren.Add(_image);
            }
            _image.Source = source;
        }
        else if (_image is not null)
        {
            _image.Source = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _image?.Measure(availableSize);
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_image is not null)
        {
            if (_clipSize != finalSize)
            {
                _clipSize = finalSize;
                _image.Clip = new EllipseGeometry(new Rect(finalSize));
            }
            _image.Arrange(new Rect(finalSize));
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (_base is null)
        {
            BuildBrushes();
        }
        var rect = new Rect(Bounds.Size);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }
        var center = rect.Center;
        context.DrawEllipse(_base, null, center, rect.Width / 2, rect.Height / 2);
        context.DrawEllipse(_glow, null, center, rect.Width / 2, rect.Height / 2);
    }
}

/// <summary>
/// A chip in the recipients row: the person's small avatar, their first name and a round Remove button. Its fill fades
/// in after the avatar has flown up; its avatar stays hidden until the flight lands.
/// </summary>
[TemplatePart(FillPart, typeof(Border))]
[TemplatePart(AvatarPart, typeof(ShareSheetAvatar))]
[TemplatePart(NamePart, typeof(TextBlock))]
[TemplatePart(RemovePart, typeof(ShareSheetIconButton))]
internal sealed class ShareSheetChip : TemplatedControl
{
    internal const string FillPart = "PART_Fill";
    internal const string AvatarPart = "PART_Avatar";
    internal const string NamePart = "PART_Name";
    internal const string RemovePart = "PART_Remove";

    public ShareSheetChip(ShareSheetPerson person)
    {
        Person = person;
        DataContext = person;
        Focusable = false;
    }

    public ShareSheetPerson Person { get; }

    public Border? Fill { get; private set; }

    public ShareSheetAvatar? Avatar { get; private set; }

    public ShareSheetIconButton? Remove { get; private set; }

    /// <summary>Raised when the template is applied (the panel wires the Remove button then).</summary>
    public event EventHandler? Ready;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Fill = e.NameScope.Find<Border>(FillPart);
        Avatar = e.NameScope.Find<ShareSheetAvatar>(AvatarPart);
        Remove = e.NameScope.Find<ShareSheetIconButton>(RemovePart);
        if (Avatar is not null)
        {
            Avatar.Person = Person;
        }
        if (e.NameScope.Find<TextBlock>(NamePart) is { } name)
        {
            name.Text = Person.ShortName;
        }
        Ready?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Spring and tween constants measured from Arc's Share sheet (design reference).</summary>
internal static class ShareSheetMotion
{
    /// <summary>The shape growing into the panel (and the panel's own size changes): critically damped, ω ≈ 11.</summary>
    public static readonly SpringSpec Grow = SpringSpec.FromVisual(0.48, 0);

    /// <summary>The shape shrinking to the confirmation or the button: critically damped, ω ≈ 13.7.</summary>
    public static readonly SpringSpec Fold = SpringSpec.FromVisual(0.38, 0);

    /// <summary>A face settling from 0.94 to full size.</summary>
    public static readonly SpringSpec FaceScale = SpringSpec.FromVisual(0.48, 0);

    /// <summary>A face sliding 32 px in between the panel and the confirmation.</summary>
    public static readonly SpringSpec FaceSlide = SpringSpec.FromVisual(0.42, 0);

    /// <summary>The send label and the count rolling.</summary>
    public static readonly SpringSpec Roll = SpringSpec.FromVisual(0.28, 0);

    /// <summary>A picked avatar flying up to its chip.</summary>
    public static readonly SpringSpec Flight = SpringSpec.FromVisual(0.5, 0);

    /// <summary>Chips closing a gap after one is removed.</summary>
    public static readonly SpringSpec Reflow = SpringSpec.FromVisual(0.38, 0);

    /// <summary>The recipients row's nudge when Send is pressed with nobody picked (kicked with <see cref="ShakeVelocity"/>).</summary>
    public static readonly SpringSpec Shake = SpringSpec.FromPhysical(961, 62);

    /// <summary>Initial velocity of the nudge, px/s (negative: toward the start).</summary>
    public const double ShakeVelocity = -280;

    /// <summary>The bottom sheet springing back after a short drag.</summary>
    public static readonly SpringSpec SheetSettle = SpringSpec.FromPhysical(180, 36);

    /// <summary>The bottom sheet leaving after a long drag or a flick.</summary>
    public static readonly SpringSpec SheetLeave = SpringSpec.FromPhysical(300, 36);

    /// <summary>The outgoing face's fade and blur.</summary>
    public static readonly TimeSpan FaceExit = TimeSpan.FromMilliseconds(100);

    /// <summary>The incoming face's fade and blur.</summary>
    public static readonly TimeSpan FaceEnter = TimeSpan.FromMilliseconds(160);

    /// <summary>The incoming face waits this long before it fades in.</summary>
    public static readonly TimeSpan FaceEnterDelay = TimeSpan.FromMilliseconds(30);

    /// <summary>The outgoing face's shrink to 0.96.</summary>
    public static readonly TimeSpan FaceExitScale = TimeSpan.FromMilliseconds(130);

    /// <summary>Blur of a face at the far end of the crossfade.</summary>
    public const double FaceBlur = 4;

    /// <summary>Scale an incoming face starts from.</summary>
    public const double FaceEnterScale = 0.94;

    /// <summary>Scale an outgoing face shrinks to.</summary>
    public const double FaceExitScaleTo = 0.96;

    /// <summary>How far a face slides between the panel and the confirmation.</summary>
    public const double FaceSlideDistance = 32;

    /// <summary>The incoming face's fade curve (decelerating, no overshoot).</summary>
    public static readonly Avalonia.Animation.Easings.Easing FaceEnterEasing = new Avalonia.Animation.Easings.SplineEasing(0.2, 0, 0, 1);

    /// <summary>How long a channel shows its result before it reads its label again.</summary>
    public static readonly TimeSpan ChannelResult = TimeSpan.FromMilliseconds(2000);

    /// <summary>How long the copy button reads "Copied" or "Retry".</summary>
    public static readonly TimeSpan CopyResult = TimeSpan.FromMilliseconds(1800);
}
