// Ported from Arc Rating (MIT, Copyright (c) 2026 Elia Kuratli): the value model, pointer scrubbing and keyboard rules.
using System.Diagnostics;
using System.Globalization;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="Rating.ValueChanged"/>.</summary>
public sealed class RatingValueChangedEventArgs : RoutedEventArgs
{
    public RatingValueChangedEventArgs(RoutedEvent routedEvent, double value) : base(routedEvent)
    {
        Value = value;
    }

    /// <summary>The committed value; 0 means the rating was cleared.</summary>
    public double Value { get; }
}

/// <summary>
/// Arc rating: a row of stars for a score from 1 to N. Pointing previews a value, a press or drag commits it, and a
/// rolling word beside the stars says what the value means. Half stars, clearing and a read-only display are options.
/// The arrow keys move by one step, Home and End jump, the number keys choose a star, and Backspace or Delete clear.
/// </summary>
[TemplatePart(StarsPart, typeof(RatingStars))]
[TemplatePart(ValuePart, typeof(RatingRollingText))]
[PseudoClasses(SmallPseudoClass, LargePseudoClass, UnratedPseudoClass)]
public class Rating : TemplatedControl
{
    public const string StarsPart = "PART_Stars";
    public const string ValuePart = "PART_Value";
    public const string SmallPseudoClass = ":small";
    public const string LargePseudoClass = ":large";
    public const string UnratedPseudoClass = ":unrated";

    /// <summary>Commits closer together than this pop only the chosen star, so held arrow keys stay calm.</summary>
    private static readonly TimeSpan s_waveGap = TimeSpan.FromMilliseconds(260);

    private static Cursor? s_hand;

    private static readonly string[] s_defaultWords = ["Poor", "Fair", "Good", "Great", "Excellent"];

    /// <summary>The visible label and the accessible name of the group.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<Rating, string?>(nameof(Label));

    /// <summary>A hint under the stars.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<Rating, string?>(nameof(Description));

    /// <summary>The committed value; 0 means not rated. It is shown rounded to the nearest step.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<Rating, double>(nameof(Value), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>The number of stars.</summary>
    public static readonly StyledProperty<int> MaximumProperty =
        AvaloniaProperty.Register<Rating, int>(nameof(Maximum), 5);

    /// <summary>Shows the value without taking input.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<Rating, bool>(nameof(IsReadOnly));

    /// <summary>The size of the stars.</summary>
    public static readonly StyledProperty<ControlSize> SizeProperty =
        AvaloniaProperty.Register<Rating, ControlSize>(nameof(Size), ControlSize.Medium);

    /// <summary>Lets the pointer, touch and arrow keys move in half stars.</summary>
    public static readonly StyledProperty<bool> AllowHalfProperty =
        AvaloniaProperty.Register<Rating, bool>(nameof(AllowHalf));

    /// <summary>Lets choosing the current value again clear it; Backspace and Delete clear from the keyboard.</summary>
    public static readonly StyledProperty<bool> AllowClearProperty =
        AvaloniaProperty.Register<Rating, bool>(nameof(AllowClear), true);

    /// <summary>Shows the word or number for the previewed value beside the stars.</summary>
    public static readonly StyledProperty<bool> ShowValueProperty =
        AvaloniaProperty.Register<Rating, bool>(nameof(ShowValue), true);

    /// <summary>One word per whole star. Five whole stars default to Poor through Excellent; anything else reads "4 out of 5".</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> ValueLabelsProperty =
        AvaloniaProperty.Register<Rating, IReadOnlyList<string>?>(nameof(ValueLabels));

    /// <summary>The text shown beside empty stars.</summary>
    public static readonly StyledProperty<string> EmptyLabelProperty =
        AvaloniaProperty.Register<Rating, string>(nameof(EmptyLabel), "Not rated");

    /// <summary>Raised when a person commits a new value; 0 means the rating was cleared.</summary>
    public static readonly RoutedEvent<RatingValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<Rating, RatingValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ValueChangedEvent"/>
    public event EventHandler<RatingValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="ValueProperty"/>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="MaximumProperty"/>
    public int Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <inheritdoc cref="IsReadOnlyProperty"/>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public ControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="AllowHalfProperty"/>
    public bool AllowHalf { get => GetValue(AllowHalfProperty); set => SetValue(AllowHalfProperty, value); }

    /// <inheritdoc cref="AllowClearProperty"/>
    public bool AllowClear { get => GetValue(AllowClearProperty); set => SetValue(AllowClearProperty, value); }

    /// <inheritdoc cref="ShowValueProperty"/>
    public bool ShowValue { get => GetValue(ShowValueProperty); set => SetValue(ShowValueProperty, value); }

    /// <inheritdoc cref="ValueLabelsProperty"/>
    public IReadOnlyList<string>? ValueLabels { get => GetValue(ValueLabelsProperty); set => SetValue(ValueLabelsProperty, value); }

    /// <inheritdoc cref="EmptyLabelProperty"/>
    public string EmptyLabel { get => GetValue(EmptyLabelProperty); set => SetValue(EmptyLabelProperty, value); }

    private sealed class Gesture
    {
        public double Last;
        public bool Scrub;
        public bool Touch;
    }

    private RatingStars? _stars;
    private RatingRollingText? _valueText;
    private double? _preview;
    private double? _heldAt;
    private Gesture? _gesture;
    private int _pressed = -1;
    private double _shown;
    private long _lastCommit;
    private string? _spoken;

    static Rating()
    {
        FocusableProperty.OverrideDefaultValue<Rating>(true);
    }

    private bool Interactive => !IsReadOnly && IsEffectivelyEnabled;

    private double Step => AllowHalf ? 0.5 : 1;

    private int StarCount => Math.Max(1, Maximum);

    private double Normalize(double next) => Math.Clamp(Math.Round(next / Step, MidpointRounding.AwayFromZero) * Step, 0, StarCount);

    private double Current => Normalize(Value);

    private double Display => _preview ?? Current;

    private IReadOnlyList<string>? Words => ValueLabels ?? (StarCount == 5 && !AllowHalf ? s_defaultWords : null);

    private string? WordFor(double next)
        => next == Math.Floor(next) && Words is { } words && next >= 1 && next <= words.Count ? words[(int)next - 1] : null;

    private string TextFor(double next)
        => next <= 0 ? EmptyLabel : WordFor(next) ?? string.Create(CultureInfo.InvariantCulture, $"{next} out of {StarCount}");

    private string SpokenFor(double next)
    {
        var stars = string.Create(CultureInfo.InvariantCulture, $"{next} of {StarCount} stars");
        return next <= 0 ? EmptyLabel : WordFor(next) is { } word ? $"{word}, {stars}" : stars;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_stars is not null)
        {
            _stars.Owner = null;
        }
        _stars = e.NameScope.Find<RatingStars>(StarsPart);
        _valueText = e.NameScope.Find<RatingRollingText>(ValuePart);
        if (_stars is not null)
        {
            _stars.Owner = this;
        }
        _shown = Display;
        _stars?.Reset(StarCount, Display, Current);
        _valueText?.Show(TextFor(Display), 1);
        UpdateValueWidth();
        UpdateState();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            Refresh();
        }
        else if (change.Property == MaximumProperty || change.Property == AllowHalfProperty)
        {
            _preview = null;
            _shown = Display;
            _stars?.Reset(StarCount, Display, Current);
            _valueText?.Show(TextFor(Display), 1);
            UpdateValueWidth();
            UpdateState();
        }
        else if (change.Property == ValueLabelsProperty || change.Property == EmptyLabelProperty)
        {
            _valueText?.Show(TextFor(Display), 1);
            UpdateValueWidth();
            UpdateState();
        }
        else if (change.Property == SizeProperty)
        {
            PseudoClasses.Set(SmallPseudoClass, Size == ControlSize.Small);
            PseudoClasses.Set(LargePseudoClass, Size == ControlSize.Large);
        }
        else if (change.Property == IsReadOnlyProperty || change.Property == IsEffectivelyEnabledProperty)
        {
            if (!Interactive)
            {
                CancelGesture();
                _preview = null;
                Refresh();
            }
            UpdateState();
        }
        else if (change.Property == LabelProperty || change.Property == DescriptionProperty)
        {
            UpdateState();
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty)
        {
            UpdateValueWidth();
        }
    }

    /// <summary>The value column keeps the width of its longest text, so nothing beside the rating moves.</summary>
    private void UpdateValueWidth()
    {
        if (_valueText is null)
        {
            return;
        }
        var probe = new TextBlock { FontFamily = FontFamily, FontSize = FontSize, FontWeight = FontWeight };
        var width = 0.0;
        for (var next = 0.0; next <= StarCount; next += Step)
        {
            probe.Text = TextFor(next);
            probe.Measure(Avalonia.Size.Infinity);
            width = Math.Max(width, probe.DesiredSize.Width);
        }
        _valueText.MinWidth = Math.Ceiling(width);
    }

    /// <summary>Moves the stars and the words to what should show now.</summary>
    private void Refresh()
    {
        var display = Display;
        var from = _shown;
        _shown = display;
        _stars?.Show(display, Current, from, _pressed, IsLoaded);
        _valueText?.Show(TextFor(display), display >= from ? 1 : -1);
        UpdateState();
    }

    private void UpdateState()
    {
        PseudoClasses.Set(UnratedPseudoClass, Display <= 0);
        SetCurrentValue(FocusableProperty, !IsReadOnly);
        if (_stars is not null)
        {
            _stars.Cursor = Interactive ? s_hand ??= new Cursor(StandardCursorType.Hand) : null;
            _stars.IsHitTestVisible = !IsReadOnly;
            _stars.FocusedCell = Classes.Contains(":focus-visible") && Interactive ? Math.Max(0, (int)Math.Ceiling(Current) - 1) : -1;
        }
        _spoken = SpokenFor(Current);
        AutomationProperties.SetHelpText(this, string.IsNullOrEmpty(Description) ? _spoken : $"{_spoken}. {Description}");
    }

    private void Commit(double next, bool toggle)
    {
        if (!Interactive)
        {
            return;
        }
        var chosen = Normalize(next);
        var current = Current;
        if (toggle && AllowClear && chosen == current)
        {
            chosen = 0;
        }
        if (!AllowClear)
        {
            chosen = Math.Max(Step, chosen);
        }
        if (_preview is { } preview)
        {
            _heldAt = preview;
        }
        _preview = null;
        var now = Stopwatch.GetTimestamp();
        var wave = Stopwatch.GetElapsedTime(_lastCommit, now) > s_waveGap;
        _lastCommit = now;
        if (chosen == current)
        {
            Refresh();
        }
        else
        {
            SetCurrentValue(ValueProperty, chosen);
            RaiseEvent(new RatingValueChangedEventArgs(ValueChangedEvent, chosen));
        }
        if (chosen > 0 && Current == chosen)
        {
            _stars?.Pop(chosen, wave);
        }
    }

    /// <summary>Maps a position on the row to a value; dragging past the first star clears when that is allowed.</summary>
    private double ValueAt(double x)
    {
        if (_stars is not { } stars)
        {
            return Current;
        }
        for (var index = 0; index < StarCount; index++)
        {
            var (left, right) = stars.CellRange(index);
            var width = right - left;
            if (index == 0 && x < left - width / 2)
            {
                return AllowClear ? 0 : Step;
            }
            if (x <= right + 1 || index == StarCount - 1)
            {
                return AllowHalf && x < left + width / 2 ? index + 0.5 : index + 1;
            }
        }
        return Current;
    }

    private static int StarOf(double next) => next > 0 ? (int)Math.Ceiling(next) - 1 : -1;

    internal void OnStarsPressed(double x, bool touch)
    {
        if (!Interactive)
        {
            return;
        }
        var at = ValueAt(x);
        Focus(NavigationMethod.Pointer);
        _gesture = new Gesture { Last = at, Touch = touch };
        _pressed = StarOf(at);
        if (touch)
        {
            _preview = at;
        }
        Refresh();
    }

    internal void OnStarsMoved(double x, bool mouse)
    {
        if (!Interactive)
        {
            return;
        }
        var at = ValueAt(x);
        if (_gesture is { } active)
        {
            if (at != active.Last)
            {
                active.Last = at;
                active.Scrub = true;
            }
            _pressed = StarOf(at);
            if (active.Touch || active.Scrub)
            {
                _preview = at;
            }
            Refresh();
            return;
        }
        if (!mouse || _heldAt == at)
        {
            return;
        }
        _heldAt = null;
        _preview = at;
        Refresh();
    }

    internal void OnStarsReleased(double x)
    {
        if (_gesture is not { } active)
        {
            return;
        }
        _gesture = null;
        _pressed = -1;
        var at = ValueAt(x);
        Commit(at, toggle: !active.Scrub);
        _heldAt = active.Touch ? null : at;
        if (active.Touch)
        {
            _preview = null;
        }
        Refresh();
    }

    internal void OnStarsCancelled() => CancelGesture();

    internal void OnStarsLeft()
    {
        if (_gesture is not null)
        {
            return;
        }
        _heldAt = null;
        if (_preview is not null)
        {
            _preview = null;
            Refresh();
        }
    }

    private void CancelGesture()
    {
        if (_gesture is null)
        {
            return;
        }
        var touch = _gesture.Touch;
        _gesture = null;
        _pressed = -1;
        if (touch)
        {
            _preview = null;
        }
        Refresh();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !Interactive)
        {
            return;
        }
        var current = Current;
        double? next = e.Key switch
        {
            Key.Right or Key.Up => current + Step,
            Key.Left or Key.Down => current - Step,
            Key.Home => Step,
            Key.End => StarCount,
            Key.Space or Key.Enter => current > 0 ? current : Step,
            Key.Back or Key.Delete when AllowClear => 0,
            >= Key.D1 and <= Key.D9 when e.Key - Key.D0 <= StarCount => e.Key - Key.D0,
            >= Key.NumPad1 and <= Key.NumPad9 when e.Key - Key.NumPad0 <= StarCount => e.Key - Key.NumPad0,
            _ => null
        };
        if (next is not { } value)
        {
            return;
        }
        e.Handled = true;
        Commit(value <= 0 ? 0 : Math.Clamp(value, Step, StarCount), toggle: false);
        UpdateState();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        UpdateState();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        UpdateState();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RatingAutomationPeer(this);

    private sealed class RatingAutomationPeer : ControlAutomationPeer, IRangeValueProvider
    {
        public RatingAutomationPeer(Rating owner) : base(owner)
        {
        }

        private Rating Rating => (Rating)Owner;

        public bool IsReadOnly => !Rating.Interactive;
        public double Minimum => 0;
        public double Maximum => Rating.StarCount;
        public double Value => Rating.Current;
        public double SmallChange => Rating.Step;
        public double LargeChange => 1;

        public void SetValue(double value) => Rating.Commit(value, toggle: false);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override string? GetNameCore() => base.GetNameCore() ?? Rating.Label;
    }
}
