using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Where one field of a <see cref="SmartFormFill"/> stands.</summary>
internal enum SmartFormFillFieldState
{
    /// <summary>Typed or empty: no badge.</summary>
    Manual,

    /// <summary>Filled with a confident value: a success dot and the percentage.</summary>
    Filled,

    /// <summary>Filled with a guess: a warning dot, "Review", a warning ring, the reason under the input and a check button.</summary>
    Review,

    /// <summary>A guess the person checked: a success dot and "Checked".</summary>
    Checked,

    /// <summary>A filled value the person changed: a muted dot and "Edited".</summary>
    Edited
}

/// <summary>
/// One field of a <see cref="SmartFormFill"/>: the label with a confidence badge at the far end (a dot plus a percentage or
/// a word), an Arc <see cref="Input"/> (the company's logo at its start, a check button at its end while it needs review),
/// and the reason under it while it needs review. A value that arrives fades in over 200ms while the input breathes an
/// accent tint for 900ms; nothing moves or changes size.
/// </summary>
[TemplatePart(InputPart, typeof(Input))]
[TemplatePart(BadgePart, typeof(ArcContentSwap))]
[TemplatePart(TintPart, typeof(Border))]
[TemplatePart(NotePart, typeof(TextBlock))]
[PseudoClasses(ReviewPseudoClass)]
internal sealed class SmartFormFillFieldView : TemplatedControl
{
    internal const string InputPart = "PART_Input";
    internal const string BadgePart = "PART_Badge";
    internal const string TintPart = "PART_Tint";
    internal const string NotePart = "PART_Note";
    internal const string ReviewPseudoClass = ":review";

    /// <summary>The field's label.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<SmartFormFillFieldView, string?>(nameof(Label));

    /// <summary>The accent tint's opacity at its peak (from the token).</summary>
    public static readonly StyledProperty<double> TintPeakOpacityProperty =
        AvaloniaProperty.Register<SmartFormFillFieldView, double>(nameof(TintPeakOpacity), 0.14);

    /// <summary>The logo slot's size (from the token).</summary>
    public static readonly StyledProperty<double> LogoSizeProperty =
        AvaloniaProperty.Register<SmartFormFillFieldView, double>(nameof(LogoSize), 20);

    private readonly SmartFormFill _owner;
    private Input? _input;
    private ArcContentSwap? _badge;
    private Border? _tint;
    private TextBlock? _note;
    private ArcContentSwap? _logo;
    private FieldIconButton? _check;
    private string? _logoKey;
    private (SmartFormFillFieldState State, string Text)? _shownBadge;
    private MotionValue<double>? _tintValue;
    private MotionValue<double>? _textValue;
    private TextPresenter? _presenter;
    private bool _settingText;
    private string _text = string.Empty;

    public SmartFormFillFieldView(SmartFormFill owner, SmartFormFillField field)
    {
        _owner = owner;
        Field = field;
        Label = field.Label;
    }

    public SmartFormFillField Field { get; }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public double TintPeakOpacity { get => GetValue(TintPeakOpacityProperty); set => SetValue(TintPeakOpacityProperty, value); }

    public double LogoSize { get => GetValue(LogoSizeProperty); set => SetValue(LogoSizeProperty, value); }

    public SmartFormFillFieldState State { get; private set; }

    /// <summary>The value read for the field by the last fill (null when the field was not filled).</summary>
    public SmartFormFillValue? Read { get; private set; }

    public string Text => _text;

    internal Input? InputPartControl => _input;

    internal FieldIconButton? CheckButton => _check;

    internal TextBlock? NoteText => _note;

    /// <summary>The badge's text as shown ("94%", "Review"), or empty.</summary>
    internal string BadgeText => _shownBadge?.Text ?? string.Empty;

    internal double TintOpacity => _tint?.Opacity ?? 0;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_input is not null)
        {
            _input.TextChanged -= OnTextChanged;
            _input.GotFocus -= OnFocus;
            _input.LostFocus -= OnBlur;
        }
        if (_check is not null)
        {
            _check.Click -= OnCheck;
        }
        _input = e.NameScope.Find<Input>(InputPart);
        _badge = e.NameScope.Find<ArcContentSwap>(BadgePart);
        _tint = e.NameScope.Find<Border>(TintPart);
        _note = e.NameScope.Find<TextBlock>(NotePart);
        _presenter = null;
        _shownBadge = null;
        _logoKey = null;
        if (_input is not null)
        {
            _input.Label = Field.Label;
            _input.Placeholder = Field.Placeholder;
            _settingText = true;
            _input.Text = _text;
            _settingText = false;
            _input.TextChanged += OnTextChanged;
            _input.GotFocus += OnFocus;
            _input.LostFocus += OnBlur;
            _check = new FieldIconButton
            {
                Cursor = new Cursor(StandardCursorType.Hand),
                Content = new ArcGlyph { Data = ArcIcons.Check, Size = 15 },
                IsVisible = false,
            };
            _check.Classes.Add("smart-form-fill-check");
            _check.Click += OnCheck;
            _input.Suffix = _check;
            if (Field.Kind == SmartFormFillFieldKind.Company)
            {
                _logo = new ArcContentSwap
                {
                    Mode = ContentSwapMode.Label,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                };
                _input.Prefix = _logo;
            }
        }
        if (_tint is not null)
        {
            _tint.Opacity = 0;
        }
        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty && _input is not null)
        {
            _input.Label = Label;
            Refresh();
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _owner.OnFieldHover(this, true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _owner.OnFieldHover(this, false);
    }

    private void OnFocus(object? sender, FocusChangedEventArgs e) => _owner.OnFieldFocus(this, true);

    private void OnBlur(object? sender, RoutedEventArgs e) => _owner.OnFieldFocus(this, false);

    private void OnCheck(object? sender, RoutedEventArgs e) => _owner.Check(Field.Id);

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = _input?.Text ?? string.Empty;
        if (_settingText || text == _text)
        {
            return;
        }
        _text = text;
        if (State is SmartFormFillFieldState.Filled or SmartFormFillFieldState.Review or SmartFormFillFieldState.Checked)
        {
            State = SmartFormFillFieldState.Edited;
        }
        Refresh();
        _owner.OnFieldTyped(this);
    }

    /// <summary>Sets the value from code (a fill, Undo, defaults): no Edited state, no typed notification.</summary>
    public void SetText(string? text)
    {
        _text = text ?? string.Empty;
        if (_input is not null && _input.Text != _text)
        {
            _settingText = true;
            _input.Text = _text;
            _settingText = false;
        }
        RefreshLogo();
    }

    /// <summary>Takes a value read from the text: sets it, picks Filled or Review and plays the arrival unless <paramref name="instant"/>.</summary>
    public void Fill(SmartFormFillValue value, bool review, bool instant)
    {
        Read = value;
        State = review ? SmartFormFillFieldState.Review : SmartFormFillFieldState.Filled;
        SetText(value.Value);
        Refresh();
        if (!instant)
        {
            PlayArrival();
        }
    }

    /// <summary>Puts the field back to a plain typed state (Undo, a new read that left it alone keeps it).</summary>
    public void Reset(string? text)
    {
        Read = null;
        State = SmartFormFillFieldState.Manual;
        StopArrival();
        SetText(text);
        Refresh();
    }

    /// <summary>A new read is about to fill this field: its old read no longer counts (its badge stays until the value lands).</summary>
    public void AwaitFill() => Read = null;

    public void MarkChecked()
    {
        if (State != SmartFormFillFieldState.Review)
        {
            return;
        }
        State = SmartFormFillFieldState.Checked;
        Refresh();
    }

    public void FocusInput()
    {
        _input?.Focus(NavigationMethod.Tab);
    }

    /// <summary>Brings the badge, the note, the check button, the ring and the description in line with the state.</summary>
    public void Refresh()
    {
        if (_input is null)
        {
            return;
        }
        var labels = _owner.Labels;
        var review = State == SmartFormFillFieldState.Review;
        PseudoClasses.Set(ReviewPseudoClass, review);
        var badge = State switch
        {
            SmartFormFillFieldState.Filled => labels.Confidence(Read?.Confidence ?? 1),
            SmartFormFillFieldState.Review => labels.Review,
            SmartFormFillFieldState.Checked => labels.Checked,
            SmartFormFillFieldState.Edited => labels.Edited,
            _ => string.Empty,
        };
        if (_badge is not null && _shownBadge != (State, badge))
        {
            _shownBadge = (State, badge);
            _badge.Content = badge.Length == 0 ? null : CreateBadge(State, badge);
        }
        var note = review ? (string.IsNullOrWhiteSpace(Read?.Note) ? labels.ReviewNote : Read!.Note!) : null;
        if (_note is not null)
        {
            _note.Text = note;
            _note.IsVisible = note is not null;
        }
        if (_check is not null)
        {
            _check.IsVisible = review;
            AutomationProperties.SetName(_check, labels.CheckField(Field.Label ?? Field.Id));
        }
        // aria-description: the confidence and, for a guess, the reason; aria-invalid while it needs review.
        var description = State switch
        {
            SmartFormFillFieldState.Filled => labels.ConfidenceDescription(Read?.Confidence ?? 1),
            SmartFormFillFieldState.Review => labels.ReviewDescription(Read?.Confidence ?? 0, Read?.Note ?? string.Empty),
            SmartFormFillFieldState.Checked => labels.Checked,
            SmartFormFillFieldState.Edited => labels.Edited,
            _ => null,
        };
        AutomationProperties.SetHelpText(_input, description);
        AutomationProperties.SetItemStatus(_input, review ? labels.Review : null);
        RefreshLogo();
    }

    private void RefreshLogo()
    {
        if (_logo is null)
        {
            return;
        }
        var key = SmartFormFillLogo.KeyOf(_text) + "|" + (_owner.ResolveLogo is null ? string.Empty : _text.Trim());
        if (_logoKey == key)
        {
            return;
        }
        _logoKey = key;
        _logo.Content = SmartFormFillLogo.Create(_text, _owner.ResolveLogo, LogoSize);
    }

    internal void RebuildLogo()
    {
        _logoKey = null;
        RefreshLogo();
    }

    private static Control CreateBadge(SmartFormFillFieldState state, string text)
    {
        var tone = state switch
        {
            SmartFormFillFieldState.Review => ArcDesignTokenKind.Warning,
            SmartFormFillFieldState.Edited => ArcDesignTokenKind.TextMuted,
            _ => ArcDesignTokenKind.Success,
        };
        var dot = new Ellipse { Width = 6, Height = 6, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(dot, Shape.FillProperty, tone);
        var label = new TextBlock { Text = text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(label, TextBlock.ForegroundProperty,
            state == SmartFormFillFieldState.Review ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(label, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            Children = { dot, label },
        };
    }

    /// <summary>
    /// The arrival: the value fades in over 200ms and the input breathes an accent tint for 900ms (in over 180ms, out over
    /// 720ms). Code-driven because the text presenter lives inside the Input's template, which this theme cannot style, and
    /// the tint is a one-shot pulse, not a state. Reduced motion and Off: the value is just there, no tint.
    /// </summary>
    private void PlayArrival()
    {
        if (!ArcMotion.IsFull(this) || _tint is null)
        {
            return;
        }
        _presenter ??= _input?.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        if (_presenter is { } presenter)
        {
            if (_textValue is null)
            {
                _textValue = new MotionValue<double>(this, 1, MotionVectors.Double);
                _textValue.Changed += v =>
                {
                    if (_presenter is not null)
                    {
                        _presenter.Opacity = v;
                    }
                };
            }
            _textValue.Jump(0);
            presenter.Opacity = 0;
            _ = _textValue.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(200), ArcEasings.Standard));
        }
        if (_tintValue is null)
        {
            _tintValue = new MotionValue<double>(this, 0, MotionVectors.Double);
            _tintValue.Changed += v =>
            {
                if (_tint is not null)
                {
                    _tint.Opacity = v;
                }
            };
        }
        _ = Breathe(_tintValue);
    }

    private async Task Breathe(MotionValue<double> value)
    {
        if (await value.AnimateTo(TintPeakOpacity, MotionSpec.Tween(TimeSpan.FromMilliseconds(180), ArcEasings.Standard)))
        {
            await value.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(720), ArcEasings.InOut));
        }
    }

    private void StopArrival()
    {
        _tintValue?.Jump(0);
        if (_tint is not null)
        {
            _tint.Opacity = 0;
        }
        _textValue?.Jump(1);
        if (_presenter is not null)
        {
            _presenter.Opacity = 1;
        }
    }
}
