using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public enum CopyButtonVariant
{
    Outline,
    Plain
}

public enum CopyState
{
    Idle,
    Copied,
    Error
}

/// <summary>
/// Copies a value with immediate, in-place confirmation: the icon swaps to a hand-drawn check, the label morphs
/// to "Copied", and everything returns to idle after 1.9s. The label cell reserves its widest state so the
/// button never changes width.
/// </summary>
[PseudoClasses(CopiedPseudoClass, ErrorPseudoClass, IconOnlyPseudoClass, PlainPseudoClass)]
public class CopyButton : AvaloniaButton
{
    public const string CopiedPseudoClass = ":copied";
    public const string ErrorPseudoClass = ":error";
    public const string IconOnlyPseudoClass = ":icon-only";

    /// <summary>Set while <see cref="Variant"/> is Plain (the theme selects on it, not on the property).</summary>
    public const string PlainPseudoClass = ":plain";

    /// <summary>Text copied to the clipboard when the button is clicked.</summary>
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<CopyButton, string?>(nameof(Value));

    /// <summary>Label of the button in its idle state (default "Copy").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<CopyButton, string>(nameof(Label), "Copy");

    /// <summary>Label shown after a successful copy (default "Copied").</summary>
    public static readonly StyledProperty<string> CopiedLabelProperty =
        AvaloniaProperty.Register<CopyButton, string>(nameof(CopiedLabel), "Copied");

    /// <summary>Label shown when the copy fails (default "Failed").</summary>
    public static readonly StyledProperty<string> FailedLabelProperty =
        AvaloniaProperty.Register<CopyButton, string>(nameof(FailedLabel), "Failed");

    /// <summary>Shows only the icon, without the label.</summary>
    public static readonly StyledProperty<bool> IsIconOnlyProperty =
        AvaloniaProperty.Register<CopyButton, bool>(nameof(IsIconOnly));

    /// <summary>Visual style: Outline (default) or Plain.</summary>
    public static readonly StyledProperty<CopyButtonVariant> VariantProperty =
        AvaloniaProperty.Register<CopyButton, CopyButtonVariant>(nameof(Variant));

    /// <summary>Time the button keeps the copied or error state before it returns to idle (default 1.9 seconds).</summary>
    public static readonly StyledProperty<TimeSpan> ResetAfterProperty =
        AvaloniaProperty.Register<CopyButton, TimeSpan>(nameof(ResetAfter), TimeSpan.FromMilliseconds(1900));

    /// <summary>Current copy state: Idle, Copied or Error (read-only).</summary>
    public static readonly DirectProperty<CopyButton, CopyState> StateProperty =
        AvaloniaProperty.RegisterDirect<CopyButton, CopyState>(nameof(State), o => o.State);

    internal static readonly DirectProperty<CopyButton, string> DisplayLabelProperty =
        AvaloniaProperty.RegisterDirect<CopyButton, string>(nameof(DisplayLabel), o => o.DisplayLabel);

    internal static readonly DirectProperty<CopyButton, Control?> IconProperty =
        AvaloniaProperty.RegisterDirect<CopyButton, Control?>(nameof(Icon), o => o.Icon);

    /// <summary>The three labels, so the label cell reserves the widest (one measuring sizer instead of three hidden TextBlocks).</summary>
    internal static readonly DirectProperty<CopyButton, IReadOnlyList<string>> SizerLabelsProperty =
        AvaloniaProperty.RegisterDirect<CopyButton, IReadOnlyList<string>>(nameof(SizerLabels), o => o.SizerLabels);

    /// <inheritdoc cref="ValueProperty"/>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <inheritdoc cref="CopiedLabelProperty"/>
    public string CopiedLabel { get => GetValue(CopiedLabelProperty); set => SetValue(CopiedLabelProperty, value); }
    /// <inheritdoc cref="FailedLabelProperty"/>
    public string FailedLabel { get => GetValue(FailedLabelProperty); set => SetValue(FailedLabelProperty, value); }
    /// <inheritdoc cref="IsIconOnlyProperty"/>
    public bool IsIconOnly { get => GetValue(IsIconOnlyProperty); set => SetValue(IsIconOnlyProperty, value); }
    /// <inheritdoc cref="VariantProperty"/>
    public CopyButtonVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <inheritdoc cref="ResetAfterProperty"/>
    public TimeSpan ResetAfter { get => GetValue(ResetAfterProperty); set => SetValue(ResetAfterProperty, value); }

    private CopyState _state;
    /// <inheritdoc cref="StateProperty"/>
    public CopyState State
    {
        get => _state;
        private set => SetAndRaise(StateProperty, ref _state, value);
    }

    private string _displayLabel = "Copy";
    internal string DisplayLabel
    {
        get => _displayLabel;
        private set => SetAndRaise(DisplayLabelProperty, ref _displayLabel, value);
    }

    private Control? _icon;
    internal Control? Icon
    {
        get => _icon;
        private set => SetAndRaise(IconProperty, ref _icon, value);
    }

    private IReadOnlyList<string> _sizerLabels = [];

    /// <inheritdoc cref="SizerLabelsProperty"/>
    internal IReadOnlyList<string> SizerLabels
    {
        get => _sizerLabels;
        private set => SetAndRaise(SizerLabelsProperty, ref _sizerLabels, value);
    }

    private void UpdateSizerLabels() => SizerLabels = [Label, CopiedLabel, FailedLabel];

    /// <summary>Occurs after the value is copied to the clipboard.</summary>
    public event EventHandler? Copied;

    private PausableTimer? _resetTimer;
    private LazyFocusRing? _focusRing;

    public CopyButton()
    {
        Icon = CreateIcon(CopyState.Idle);
        PseudoClasses.Set(PlainPseudoClass, Variant == CopyButtonVariant.Plain);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        // The ring (4px outside, like Button's) is built the first time the button is keyboard focused, as the last
        // child of the template's root panel, where the template used to keep a hidden Border.
        _focusRing ??= new LazyFocusRing(this, ButtonTokens.Key(ButtonTokenKind.FocusRingMargin), CopyButtonTokens.Key(CopyButtonTokenKind.FocusRingRadius));
        _focusRing.Attach(VisualChildren.Count > 0 ? VisualChildren[0] as Panel : null);
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

    protected override async void OnClick()
    {
        base.OnClick();
        var ok = Value is not null && await ArcClipboard.CopyTextAsync(this, Value);
        SetState(ok ? CopyState.Copied : CopyState.Error);
        if (ok)
        {
            Copied?.Invoke(this, EventArgs.Empty);
        }
        _resetTimer ??= new PausableTimer(ResetAfter, () => SetState(CopyState.Idle));
        _resetTimer.Restart(ResetAfter);
    }

    private void SetState(CopyState state)
    {
        if (State == state)
        {
            return;
        }
        State = state;
        PseudoClasses.Set(CopiedPseudoClass, state == CopyState.Copied);
        PseudoClasses.Set(ErrorPseudoClass, state == CopyState.Error);
        DisplayLabel = state switch
        {
            CopyState.Copied => CopiedLabel,
            CopyState.Error => FailedLabel,
            _ => Label
        };
        Icon = CreateIcon(state);
        AutomationProperties.SetHelpText(this, state switch
        {
            CopyState.Copied => $"{Label}: {CopiedLabel}",
            CopyState.Error => $"{Label}: {FailedLabel}",
            _ => string.Empty
        });
    }

    private static Control CreateIcon(CopyState state) => state switch
    {
        CopyState.Copied => new StrokeDrawGlyph { Data = ArcIcons.Check, Size = 16, DrawOnAttach = true, Classes = { "copy-success" } },
        CopyState.Error => new ArcGlyph { Data = ArcIcons.CircleAlert, Size = 16, Classes = { "copy-error" } },
        _ => new ArcGlyph { Data = ArcIcons.Copy, Size = 16 }
    };

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty || change.Property == CopiedLabelProperty || change.Property == FailedLabelProperty)
        {
            UpdateSizerLabels();
            if (change.Property == LabelProperty && State == CopyState.Idle)
            {
                DisplayLabel = Label;
                AutomationProperties.SetName(this, Label);
            }
        }
        else if (change.Property == IsIconOnlyProperty)
        {
            PseudoClasses.Set(IconOnlyPseudoClass, IsIconOnly);
        }
        else if (change.Property == VariantProperty)
        {
            PseudoClasses.Set(PlainPseudoClass, Variant == CopyButtonVariant.Plain);
        }
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        DisplayLabel = Label;
        UpdateSizerLabels();
        AutomationProperties.SetName(this, Label);
    }
}
