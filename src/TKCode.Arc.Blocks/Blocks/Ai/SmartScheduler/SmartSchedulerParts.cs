using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>A proposed slot in the list under the week: its rank, the time and why it was picked; selected, it is tinted.</summary>
internal sealed class SmartSchedulerOption : AvaloniaButton
{
    public static readonly StyledProperty<string?> RankProperty =
        AvaloniaProperty.Register<SmartSchedulerOption, string?>(nameof(Rank));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SmartSchedulerOption, string?>(nameof(Title));

    public static readonly StyledProperty<string?> ReasonsProperty =
        AvaloniaProperty.Register<SmartSchedulerOption, string?>(nameof(Reasons));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<SmartSchedulerOption, bool>(nameof(IsSelected));

    private LazyFocusRing? _focusRing;

    public string? Rank { get => GetValue(RankProperty); set => SetValue(RankProperty, value); }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Reasons { get => GetValue(ReasonsProperty); set => SetValue(ReasonsProperty, value); }

    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    public int Index { get; init; }

    protected override Type StyleKeyOverride => typeof(SmartSchedulerOption);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(":selected", IsSelected);
            AutomationProperties.SetItemStatus(this, IsSelected ? "selected" : null);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, SmartSchedulerTokens.Key(SmartSchedulerTokenKind.FocusMargin), ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep14));
        _focusRing.Attach(e.NameScope.Find<Panel>("PART_Root"));
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
}

/// <summary>One understood limit, shown back as a small pill ("Not Friday").</summary>
internal sealed class SmartSchedulerChip : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SmartSchedulerChip, string?>(nameof(Text));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
}
