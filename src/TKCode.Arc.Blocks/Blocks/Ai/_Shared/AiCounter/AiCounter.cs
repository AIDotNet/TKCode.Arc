using System.Globalization;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The character counter of an AI composer's toolbar: a small ring of the share used and "N left". It stays folded away
/// (zero width) until <see cref="Length"/> reaches <see cref="CounterThreshold"/> of <see cref="Max"/> (80%), then opens
/// from zero width on a spring while it fades in; from <see cref="WarningThreshold"/> (95%) the ring and the text turn to
/// the warning color, and past <see cref="Max"/> a red alert mark replaces the ring and the text reads "N over".
/// <code>
/// &lt;arc:AiCounter Length="{Binding Text.Length}" Max="4000" LeftText="{Binding LeftFormat}" OverText="{Binding OverFormat}" /&gt;
/// </code>
/// Numbers use tabular figures. <see cref="IsTiny"/> (a very narrow composer) keeps only the ring or the mark; the
/// tooltip and the accessible name (<see cref="Tip"/>) still give the full count. The texts are only formatted while the
/// counter shows, so typing far below the threshold costs nothing; a counter folding away keeps its last text.
/// </summary>
[TemplatePart(RingPart, typeof(ArcRing))]
[PseudoClasses(ShownPseudoClass, NearPseudoClass, OverPseudoClass, TinyPseudoClass)]
internal class AiCounter : TemplatedControl
{
    public const string RingPart = "PART_Ring";

    /// <summary>At or past <see cref="CounterThreshold"/>: the counter is open.</summary>
    public const string ShownPseudoClass = ":shown";

    /// <summary>At or past <see cref="WarningThreshold"/> and not over: warning color (kept while folding away).</summary>
    public const string NearPseudoClass = ":near";

    /// <summary>Past <see cref="Max"/>: danger color and the alert mark (kept while folding away).</summary>
    public const string OverPseudoClass = ":over";

    /// <summary>Only the ring or the mark (set through <see cref="IsTiny"/>).</summary>
    public const string TinyPseudoClass = ":tiny";

    #region Properties

    /// <summary>The number of characters typed.</summary>
    public static readonly StyledProperty<int> LengthProperty =
        AvaloniaProperty.Register<AiCounter, int>(nameof(Length));

    /// <summary>The limit (default 4000); 0 or less means no limit, and the counter never shows.</summary>
    public static readonly StyledProperty<int> MaxProperty =
        AvaloniaProperty.Register<AiCounter, int>(nameof(Max), 4000);

    /// <summary>The share of <see cref="Max"/> from which the counter shows (default 0.8).</summary>
    public static readonly StyledProperty<double> CounterThresholdProperty =
        AvaloniaProperty.Register<AiCounter, double>(nameof(CounterThreshold), 0.8);

    /// <summary>The share of <see cref="Max"/> from which it turns to the warning color (default 0.95).</summary>
    public static readonly StyledProperty<double> WarningThresholdProperty =
        AvaloniaProperty.Register<AiCounter, double>(nameof(WarningThreshold), 0.95);

    /// <summary>The visible text for the characters left ("{0} left"); null shows the number alone.</summary>
    public static readonly StyledProperty<Func<int, string>?> LeftTextProperty =
        AvaloniaProperty.Register<AiCounter, Func<int, string>?>(nameof(LeftText));

    /// <summary>The visible text for the characters over the limit ("{0} over"); null shows the number alone.</summary>
    public static readonly StyledProperty<Func<int, string>?> OverTextProperty =
        AvaloniaProperty.Register<AiCounter, Func<int, string>?>(nameof(OverText));

    /// <summary>
    /// The tooltip and accessible name from (<see cref="Length"/>, <see cref="Max"/>), such as "3,300 of 4,000 characters
    /// left" or "40 characters over the limit"; null uses the visible text.
    /// </summary>
    public static readonly StyledProperty<Func<int, int, string>?> TipProperty =
        AvaloniaProperty.Register<AiCounter, Func<int, int, string>?>(nameof(Tip));

    /// <summary>Only the ring or the mark, without the text (the owner sets it in a very narrow composer).</summary>
    public static readonly StyledProperty<bool> IsTinyProperty =
        AvaloniaProperty.Register<AiCounter, bool>(nameof(IsTiny));

    /// <summary>True from <see cref="CounterThreshold"/> on (with a limit).</summary>
    public static readonly DirectProperty<AiCounter, bool> IsShownProperty =
        AvaloniaProperty.RegisterDirect<AiCounter, bool>(nameof(IsShown), o => o.IsShown);

    /// <summary>True from <see cref="WarningThreshold"/> up to the limit itself.</summary>
    public static readonly DirectProperty<AiCounter, bool> IsNearProperty =
        AvaloniaProperty.RegisterDirect<AiCounter, bool>(nameof(IsNear), o => o.IsNear);

    /// <summary>True when <see cref="Length"/> is past <see cref="Max"/>: the owner blocks sending.</summary>
    public static readonly DirectProperty<AiCounter, bool> IsOverProperty =
        AvaloniaProperty.RegisterDirect<AiCounter, bool>(nameof(IsOver), o => o.IsOver);

    /// <summary>The share of the limit used, 0 to 1 (what the ring shows).</summary>
    public static readonly DirectProperty<AiCounter, double> UsedProperty =
        AvaloniaProperty.RegisterDirect<AiCounter, double>(nameof(Used), o => o.Used);

    /// <summary>The visible text ("700 left", "40 over"); kept while the counter folds away.</summary>
    public static readonly DirectProperty<AiCounter, string?> TextProperty =
        AvaloniaProperty.RegisterDirect<AiCounter, string?>(nameof(Text), o => o.Text);

    /// <inheritdoc cref="LengthProperty"/>
    public int Length { get => GetValue(LengthProperty); set => SetValue(LengthProperty, value); }

    /// <inheritdoc cref="MaxProperty"/>
    public int Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <inheritdoc cref="CounterThresholdProperty"/>
    public double CounterThreshold { get => GetValue(CounterThresholdProperty); set => SetValue(CounterThresholdProperty, value); }

    /// <inheritdoc cref="WarningThresholdProperty"/>
    public double WarningThreshold { get => GetValue(WarningThresholdProperty); set => SetValue(WarningThresholdProperty, value); }

    /// <inheritdoc cref="LeftTextProperty"/>
    public Func<int, string>? LeftText { get => GetValue(LeftTextProperty); set => SetValue(LeftTextProperty, value); }

    /// <inheritdoc cref="OverTextProperty"/>
    public Func<int, string>? OverText { get => GetValue(OverTextProperty); set => SetValue(OverTextProperty, value); }

    /// <inheritdoc cref="TipProperty"/>
    public Func<int, int, string>? Tip { get => GetValue(TipProperty); set => SetValue(TipProperty, value); }

    /// <inheritdoc cref="IsTinyProperty"/>
    public bool IsTiny { get => GetValue(IsTinyProperty); set => SetValue(IsTinyProperty, value); }

    /// <inheritdoc cref="IsShownProperty"/>
    public bool IsShown { get => _isShown; private set => SetAndRaise(IsShownProperty, ref _isShown, value); }

    /// <inheritdoc cref="IsNearProperty"/>
    public bool IsNear { get => _isNear; private set => SetAndRaise(IsNearProperty, ref _isNear, value); }

    /// <inheritdoc cref="IsOverProperty"/>
    public bool IsOver { get => _isOver; private set => SetAndRaise(IsOverProperty, ref _isOver, value); }

    /// <inheritdoc cref="UsedProperty"/>
    public double Used { get => _used; private set => SetAndRaise(UsedProperty, ref _used, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => _text; private set => SetAndRaise(TextProperty, ref _text, value); }

    #endregion

    private bool _isShown;
    private bool _isNear;
    private bool _isOver;
    private double _used;
    private string? _text;
    private ArcRing? _ring;

    protected override Type StyleKeyOverride => typeof(AiCounter);

    /// <summary>The ring part, for tests.</summary>
    internal ArcRing? Ring => _ring;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LengthProperty || change.Property == MaxProperty || change.Property == CounterThresholdProperty
            || change.Property == WarningThresholdProperty || change.Property == LeftTextProperty || change.Property == OverTextProperty
            || change.Property == TipProperty)
        {
            Update();
        }
        else if (change.Property == IsTinyProperty)
        {
            PseudoClasses.Set(TinyPseudoClass, change.GetNewValue<bool>());
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _ring = e.NameScope.Find<ArcRing>(RingPart);
        Update();
    }

    private void Update()
    {
        var max = Max;
        var length = Math.Max(0, Length);
        var limited = max > 0;
        // A hair of tolerance: 0.95 × 100 is 94.99999999999999 in binary.
        var shown = limited && length >= max * CounterThreshold - 1e-9;
        var over = limited && length > max;
        IsOver = over;
        IsNear = shown && !over && length >= max * WarningThreshold - 1e-9;
        Used = limited ? Math.Min(1, length / (double)max) : 0;
        IsShown = shown;
        PseudoClasses.Set(ShownPseudoClass, shown);
        if (shown)
        {
            // The look follows the count only while shown: a counter folding away keeps its last text, color and ring.
            Text = over ? Format(OverText, length - max) : Format(LeftText, max - length);
            PseudoClasses.Set(NearPseudoClass, IsNear);
            PseudoClasses.Set(OverPseudoClass, over);
            if (_ring is not null)
            {
                _ring.Value = Used;
            }
        }
        UpdateName(shown, length, max);
    }

    private static string Format(Func<int, string>? format, int count)
        => format?.Invoke(count) ?? count.ToString("N0", CultureInfo.CurrentCulture);

    /// <summary>The full count as the tooltip and the accessible name while shown; a folded counter has neither.</summary>
    private void UpdateName(bool shown, int length, int max)
    {
        var name = shown ? Tip?.Invoke(length, max) ?? Text : null;
        if (string.IsNullOrEmpty(name))
        {
            ClearValue(AutomationProperties.NameProperty);
            Tooltip.SetTip(this, null);
            return;
        }
        AutomationProperties.SetName(this, name);
        Tooltip.SetTip(this, name);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AiCounterAutomationPeer(this);

    /// <summary>A text element named with the full count while shown; out of the tree of content while folded.</summary>
    private sealed class AiCounterAutomationPeer(AiCounter owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

        protected override bool IsContentElementCore() => owner.IsShown;

        protected override bool IsControlElementCore() => owner.IsShown;
    }
}
