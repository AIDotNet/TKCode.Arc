using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The reward card a won slice lifts into: the prize, a detail line, the code in a dashed box with a Copy button, and
/// "Spin again". The wheel keeps one live card and one invisible sizer card that reserves the tallest card's height.
/// </summary>
[TemplatePart(CopyPart, typeof(CopyButton))]
[TemplatePart(SpinAgainPart, typeof(AvaloniaButton))]
[PseudoClasses(CodePseudoClass)]
internal sealed class PrizeWheelCard : TemplatedControl
{
    public const string CopyPart = "PART_Copy";
    public const string SpinAgainPart = "PART_SpinAgain";
    public const string CodePseudoClass = ":code";

    public static readonly StyledProperty<string?> PrizeProperty =
        AvaloniaProperty.Register<PrizeWheelCard, string?>(nameof(Prize));

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<PrizeWheelCard, string?>(nameof(Detail));

    public static readonly StyledProperty<string?> CodeProperty =
        AvaloniaProperty.Register<PrizeWheelCard, string?>(nameof(Code));

    public static readonly StyledProperty<string> CopyTextProperty =
        AvaloniaProperty.Register<PrizeWheelCard, string>(nameof(CopyText), "Copy code");

    public static readonly StyledProperty<string> CopiedTextProperty =
        AvaloniaProperty.Register<PrizeWheelCard, string>(nameof(CopiedText), "Copied");

    public static readonly StyledProperty<string> CopyFailedTextProperty =
        AvaloniaProperty.Register<PrizeWheelCard, string>(nameof(CopyFailedText), "Copy failed");

    public static readonly StyledProperty<string> SpinAgainTextProperty =
        AvaloniaProperty.Register<PrizeWheelCard, string>(nameof(SpinAgainText), "Spin again");

    public static readonly StyledProperty<bool> IsSpinAgainVisibleProperty =
        AvaloniaProperty.Register<PrizeWheelCard, bool>(nameof(IsSpinAgainVisible), true);

    private CopyButton? _copy;
    private AvaloniaButton? _spinAgain;

    public string? Prize { get => GetValue(PrizeProperty); set => SetValue(PrizeProperty, value); }

    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    public string? Code { get => GetValue(CodeProperty); set => SetValue(CodeProperty, value); }

    public string CopyText { get => GetValue(CopyTextProperty); set => SetValue(CopyTextProperty, value); }

    public string CopiedText { get => GetValue(CopiedTextProperty); set => SetValue(CopiedTextProperty, value); }

    public string CopyFailedText { get => GetValue(CopyFailedTextProperty); set => SetValue(CopyFailedTextProperty, value); }

    public string SpinAgainText { get => GetValue(SpinAgainTextProperty); set => SetValue(SpinAgainTextProperty, value); }

    public bool IsSpinAgainVisible { get => GetValue(IsSpinAgainVisibleProperty); set => SetValue(IsSpinAgainVisibleProperty, value); }

    /// <summary>The Copy button, once the template is applied.</summary>
    internal CopyButton? CopyButton => _copy;

    /// <summary>"Spin again" was clicked.</summary>
    internal event EventHandler? SpinAgainRequested;

    /// <summary>The Copy button changed state (copied, failed or back to idle).</summary>
    internal event EventHandler<CopyState>? CopyStateChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_copy is not null)
        {
            _copy.PropertyChanged -= OnCopyChanged;
        }
        if (_spinAgain is not null)
        {
            _spinAgain.Click -= OnSpinAgain;
        }
        _copy = e.NameScope.Find<CopyButton>(CopyPart);
        _spinAgain = e.NameScope.Find<AvaloniaButton>(SpinAgainPart);
        if (_copy is not null)
        {
            _copy.PropertyChanged += OnCopyChanged;
        }
        if (_spinAgain is not null)
        {
            _spinAgain.Click += OnSpinAgain;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CodeProperty)
        {
            PseudoClasses.Set(CodePseudoClass, !string.IsNullOrEmpty(Code));
        }
    }

    private void OnSpinAgain(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SpinAgainRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCopyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == CopyButton.StateProperty && sender is CopyButton copy)
        {
            CopyStateChanged?.Invoke(this, copy.State);
        }
    }
}

/// <summary>One row of the odds list: the prize, a dot after the featured one, and its chance; the won row lights up.</summary>
[PseudoClasses(WonPseudoClass, FeaturedPseudoClass)]
internal sealed class PrizeWheelOddsRow : TemplatedControl
{
    public const string WonPseudoClass = ":won";
    public const string FeaturedPseudoClass = ":featured";

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<PrizeWheelOddsRow, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ChanceProperty =
        AvaloniaProperty.Register<PrizeWheelOddsRow, string?>(nameof(Chance));

    public static readonly StyledProperty<bool> IsFeaturedProperty =
        AvaloniaProperty.Register<PrizeWheelOddsRow, bool>(nameof(IsFeatured));

    public static readonly StyledProperty<bool> IsWonProperty =
        AvaloniaProperty.Register<PrizeWheelOddsRow, bool>(nameof(IsWon));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? Chance { get => GetValue(ChanceProperty); set => SetValue(ChanceProperty, value); }

    public bool IsFeatured { get => GetValue(IsFeaturedProperty); set => SetValue(IsFeaturedProperty, value); }

    public bool IsWon { get => GetValue(IsWonProperty); set => SetValue(IsWonProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsWonProperty)
        {
            PseudoClasses.Set(WonPseudoClass, IsWon);
        }
        else if (change.Property == IsFeaturedProperty)
        {
            PseudoClasses.Set(FeaturedPseudoClass, IsFeatured);
        }
    }
}
