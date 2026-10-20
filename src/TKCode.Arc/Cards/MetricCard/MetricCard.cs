using System.Globalization;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Direction of a <see cref="MetricCard"/> change, read from the sign of <see cref="MetricCard.Change"/>.</summary>
public enum MetricChangeDirection
{
    /// <summary>No sign: a neutral outlined chip.</summary>
    None,

    /// <summary>Starts with "+": success tint.</summary>
    Up,

    /// <summary>Starts with "-" or "−": danger tint.</summary>
    Down
}

/// <summary>
/// Arc metric card: a compact summary for a number that needs context (uptime, orders). The value rolls like an
/// odometer (<see cref="AnimatedCounter"/>, from zero on first view); the label, context and change chip roll in
/// from the side the number moved toward (a larger number rises from below, a smaller one drops from above), and
/// the chip's width springs to fit. A signed change reads its direction in color as well as sign; set
/// <see cref="IsChangeGlyphVisible"/> to add a direction glyph and <see cref="Sparkline"/> for a small trend line.
/// </summary>
[TemplatePart(LabelPart, typeof(MessagesTextSwap))]
[TemplatePart(ContextPart, typeof(MessagesTextSwap))]
[TemplatePart(ChangePart, typeof(MessagesTextSwap))]
[PseudoClasses(NarrowPseudoClass, UpPseudoClass, DownPseudoClass)]
public class MetricCard : TemplatedControl
{
    public const string LabelPart = "PART_Label";
    public const string ContextPart = "PART_Context";
    public const string ChangePart = "PART_Change";
    public const string NarrowPseudoClass = ":narrow";
    public const string UpPseudoClass = ":up";
    public const string DownPseudoClass = ":down";

    /// <summary>In windows this narrow (Arc's max-width: 380px) cards tighten their padding and move the chip under the label.</summary>
    private const double NarrowWidth = 380;

    #region 公共属性定义

    /// <summary>What is measured.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MetricCard, string?>(nameof(Label));

    /// <summary>The number; it rolls from zero on first view, then between values.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<MetricCard, double>(nameof(Value));

    public static readonly StyledProperty<string?> PrefixProperty =
        AvaloniaProperty.Register<MetricCard, string?>(nameof(Prefix));

    /// <summary>Unit after the number, such as "%" or " ms".</summary>
    public static readonly StyledProperty<string?> SuffixProperty =
        AvaloniaProperty.Register<MetricCard, string?>(nameof(Suffix));

    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<MetricCard, int>(nameof(Decimals));

    /// <summary>The comparison line, such as "vs last week".</summary>
    public static readonly StyledProperty<string?> ContextProperty =
        AvaloniaProperty.Register<MetricCard, string?>(nameof(Context));

    /// <summary>The change chip, such as "+12%"; empty hides the chip. Write the sign: color is not the only cue.</summary>
    public static readonly StyledProperty<string?> ChangeProperty =
        AvaloniaProperty.Register<MetricCard, string?>(nameof(Change));

    /// <summary>Adds a trending up / down glyph to a signed change chip (default false, like Arc).</summary>
    public static readonly StyledProperty<bool> IsChangeGlyphVisibleProperty =
        AvaloniaProperty.Register<MetricCard, bool>(nameof(IsChangeGlyphVisible));

    /// <summary>Optional values for a small trend line beside the number.</summary>
    public static readonly StyledProperty<IList<double>?> SparklineProperty =
        AvaloniaProperty.Register<MetricCard, IList<double>?>(nameof(Sparkline));

    public static readonly DirectProperty<MetricCard, MetricChangeDirection> ChangeDirectionProperty =
        AvaloniaProperty.RegisterDirect<MetricCard, MetricChangeDirection>(nameof(ChangeDirection), o => o.ChangeDirection);

    internal static readonly DirectProperty<MetricCard, bool> HasChangeProperty =
        AvaloniaProperty.RegisterDirect<MetricCard, bool>(nameof(HasChange), o => o.HasChange);

    internal static readonly DirectProperty<MetricCard, Geometry?> ChangeGlyphProperty =
        AvaloniaProperty.RegisterDirect<MetricCard, Geometry?>(nameof(ChangeGlyph), o => o.ChangeGlyph);

    internal static readonly DirectProperty<MetricCard, bool> HasSparklineProperty =
        AvaloniaProperty.RegisterDirect<MetricCard, bool>(nameof(HasSparkline), o => o.HasSparkline);

    /// <summary>What is measured.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>The number; it rolls from zero on first view, then between values.</summary>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <summary>Text before the number, such as "$".</summary>
    public string? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }
    /// <summary>Unit after the number, such as "%" or " ms".</summary>
    public string? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }
    /// <summary>Fraction digits of the number (default 0).</summary>
    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }
    /// <summary>The comparison line, such as "vs last week".</summary>
    public string? Context { get => GetValue(ContextProperty); set => SetValue(ContextProperty, value); }
    /// <summary>The change chip, such as "+12%"; empty hides the chip. Write the sign: color is not the only cue.</summary>
    public string? Change { get => GetValue(ChangeProperty); set => SetValue(ChangeProperty, value); }
    /// <summary>Adds a trending up / down glyph to a signed change chip (default false, like Arc).</summary>
    public bool IsChangeGlyphVisible { get => GetValue(IsChangeGlyphVisibleProperty); set => SetValue(IsChangeGlyphVisibleProperty, value); }
    /// <summary>Optional values for a small trend line beside the number.</summary>
    public IList<double>? Sparkline { get => GetValue(SparklineProperty); set => SetValue(SparklineProperty, value); }

    /// <summary>Direction read from the sign of <see cref="Change"/>.</summary>
    public MetricChangeDirection ChangeDirection
    {
        get => _changeDirection;
        private set => SetAndRaise(ChangeDirectionProperty, ref _changeDirection, value);
    }

    internal bool HasChange
    {
        get => _hasChange;
        private set => SetAndRaise(HasChangeProperty, ref _hasChange, value);
    }

    internal Geometry? ChangeGlyph
    {
        get => _changeGlyph;
        private set => SetAndRaise(ChangeGlyphProperty, ref _changeGlyph, value);
    }

    internal bool HasSparkline
    {
        get => _hasSparkline;
        private set => SetAndRaise(HasSparklineProperty, ref _hasSparkline, value);
    }

    #endregion

    private MetricChangeDirection _changeDirection;
    private bool _hasChange;
    private Geometry? _changeGlyph;
    private bool _hasSparkline;
    private MessagesTextSwap? _label;
    private MessagesTextSwap? _context;
    private MessagesTextSwap? _change;
    private string? _lastChange;
    private IDisposable? _clientSizeSubscription;

    static MetricCard()
    {
        FocusableProperty.OverrideDefaultValue<MetricCard>(false);
    }

    public MetricCard()
    {
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Group);
    }

    internal MessagesTextSwap? LabelSwap => _label;

    internal MessagesTextSwap? ContextSwap => _context;

    internal MessagesTextSwap? ChangeSwap => _change;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _label = e.NameScope.Find<MessagesTextSwap>(LabelPart);
        _context = e.NameScope.Find<MessagesTextSwap>(ContextPart);
        _change = e.NameScope.Find<MessagesTextSwap>(ChangePart);
        Assign(_label, Label, null);
        Assign(_context, Context, null);
        Assign(_change, Change, null);
        _lastChange = Change;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            Assign(_label, Label, change.GetOldValue<string?>());
            UpdateAutomation();
        }
        else if (change.Property == ContextProperty)
        {
            Assign(_context, Context, change.GetOldValue<string?>());
            UpdateAutomation();
        }
        else if (change.Property == ChangeProperty)
        {
            var text = Change;
            HasChange = !string.IsNullOrEmpty(text);
            ChangeDirection = string.IsNullOrEmpty(text) ? MetricChangeDirection.None
                : text[0] == '+' ? MetricChangeDirection.Up
                : text[0] is '-' or '−' ? MetricChangeDirection.Down
                : MetricChangeDirection.None;
            PseudoClasses.Set(UpPseudoClass, ChangeDirection == MetricChangeDirection.Up);
            PseudoClasses.Set(DownPseudoClass, ChangeDirection == MetricChangeDirection.Down);
            UpdateGlyph();
            // An emptied chip keeps its last text while it fades out.
            if (HasChange)
            {
                Assign(_change, text, _lastChange);
                _lastChange = text;
            }
            UpdateAutomation();
        }
        else if (change.Property == IsChangeGlyphVisibleProperty)
        {
            UpdateGlyph();
        }
        else if (change.Property == SparklineProperty)
        {
            HasSparkline = Sparkline is { Count: > 1 };
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            _clientSizeSubscription = topLevel.GetObservable(TopLevel.ClientSizeProperty).Subscribe(new ClientSizeObserver(this));
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _clientSizeSubscription?.Dispose();
        _clientSizeSubscription = null;
    }

    private sealed class ClientSizeObserver : IObserver<Size>
    {
        private readonly MetricCard _owner;

        public ClientSizeObserver(MetricCard owner) => _owner = owner;

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(Size value) => _owner.PseudoClasses.Set(NarrowPseudoClass, value.Width > 0 && value.Width <= NarrowWidth);
    }

    private void UpdateGlyph()
    {
        ChangeGlyph = !IsChangeGlyphVisible ? null : ChangeDirection switch
        {
            MetricChangeDirection.Up => MessagesIcons.TrendingUp,
            MetricChangeDirection.Down => MessagesIcons.TrendingDown,
            _ => null
        };
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, Label ?? string.Empty);
        AutomationProperties.SetHelpText(this, string.IsNullOrEmpty(Change) ? Context ?? string.Empty : $"{Change} {Context}");
    }

    /// <summary>Copy that holds a number enters from the side it moved toward.</summary>
    private static void Assign(MessagesTextSwap? swap, string? text, string? previous)
    {
        if (swap is null)
        {
            return;
        }
        var next = AmountIn(text);
        var before = AmountIn(previous);
        swap.Direction = !double.IsNaN(next) && !double.IsNaN(before) && next < before ? -1 : 1;
        swap.Text = text;
    }

    /// <summary>The first number in a string ("1,204 orders" → 1204), or NaN.</summary>
    internal static double AmountIn(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return double.NaN;
        }
        Span<char> buffer = stackalloc char[Math.Min(text.Length, 64)];
        var count = 0;
        var started = false;
        var dot = false;
        for (var i = 0; i < text.Length && count < buffer.Length; i++)
        {
            var c = text[i];
            if (char.IsAsciiDigit(c))
            {
                if (!started && i > 0 && (text[i - 1] == '-' || text[i - 1] == '−'))
                {
                    buffer[count++] = '-';
                }
                started = true;
                buffer[count++] = c;
            }
            else if (started && c == ',')
            {
                continue;
            }
            else if (started && c == '.' && !dot && i + 1 < text.Length && char.IsAsciiDigit(text[i + 1]))
            {
                dot = true;
                buffer[count++] = c;
            }
            else if (started)
            {
                break;
            }
        }
        return count > 0 && double.TryParse(buffer[..count], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : double.NaN;
    }
}
