using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>One card of <see cref="RadioCards"/>: indicator, label with icon, description and meta.</summary>
[PseudoClasses(ListPseudoClass)]
internal sealed class RadioCard : TogglesItem
{
    public const string ListPseudoClass = ":list";

    // The ring is built by TogglesItem the first time the card is keyboard focused (the template used to keep a hidden Border).
    protected override (object Margin, object Radius)? FocusRingKeys =>
        (RadioCardsTokens.Key(RadioCardsTokenKind.FocusRingMargin), RadioCardsTokens.Key(RadioCardsTokenKind.FocusRingRadius));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<RadioCard, string?>(nameof(Description));

    public static readonly StyledProperty<string?> DisabledReasonProperty =
        AvaloniaProperty.Register<RadioCard, string?>(nameof(DisabledReason));

    public static readonly StyledProperty<object?> MetaProperty =
        AvaloniaProperty.Register<RadioCard, object?>(nameof(Meta));

    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<RadioCard, object?>(nameof(Icon));

    public static readonly DirectProperty<RadioCard, string?> ShownDescriptionProperty =
        AvaloniaProperty.RegisterDirect<RadioCard, string?>(nameof(ShownDescription), o => o.ShownDescription);

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string? DisabledReason { get => GetValue(DisabledReasonProperty); set => SetValue(DisabledReasonProperty, value); }
    public object? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    private string? _shownDescription;

    /// <summary>The disabled reason while the card is disabled (when given), otherwise the description.</summary>
    public string? ShownDescription
    {
        get => _shownDescription;
        private set => SetAndRaise(ShownDescriptionProperty, ref _shownDescription, value);
    }

    public bool IsListLayout
    {
        set => PseudoClasses.Set(ListPseudoClass, value);
    }

    private StackPanel? _body;
    private TextBlock? _description;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _body = e.NameScope.Find<StackPanel>("PART_Body");
        _description = null;
        UpdateDescription();
    }

    /// <summary>
    /// The description line (PART_Description) is built the first time there is one, under the label row in PART_Body,
    /// with the token bindings the template used to declare; cards without a description carry no TextBlock.
    /// </summary>
    private void UpdateDescription()
    {
        if (string.IsNullOrEmpty(ShownDescription))
        {
            if (_description is not null)
            {
                _description.IsVisible = false;
            }
            return;
        }
        if (_description is null)
        {
            if (_body is null)
            {
                return;
            }
            var block = new TextBlock { Name = "PART_Description", TextWrapping = TextWrapping.Wrap };
            block.Bind(TextBlock.TextProperty, this.GetObservable(ShownDescriptionProperty));
            ArcTokens.Bind(block, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS, BindingPriority.LocalValue);
            block.Bind(TextBlock.LineHeightProperty, block.GetResourceObservable(RadioCardsTokens.Key(RadioCardsTokenKind.DescriptionLineHeight)));
            ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary, BindingPriority.LocalValue);
            _body.Children.Add(block);
            _description = block;
        }
        _description.IsVisible = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DescriptionProperty || change.Property == DisabledReasonProperty || change.Property == IsEffectivelyEnabledProperty)
        {
            ShownDescription = !IsEffectivelyEnabled && !string.IsNullOrEmpty(DisabledReason) ? DisabledReason : Description;
            AutomationProperties.SetHelpText(this, ShownDescription);
            UpdateDescription();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TogglesRadioAutomationPeer(this);
}
