using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// One row of a <see cref="RadioGroup"/>: a ring with a springing dot, the label (the row's content) and an optional
/// description. The group creates a row for each option. For a <see cref="RadioOption"/> the row takes its label,
/// description and disabled state from the option; for any other item the label is the group's <c>ItemTemplate</c>, and a
/// style on <c>arc|RadioGroupItem</c> (or the group's <c>ItemContainerTheme</c>) can bind <see cref="Description"/>,
/// <c>IsEnabled</c> and <c>AutomationProperties.Name</c> to the item.
/// </summary>
[PseudoClasses(BorderlessPseudoClass)]
public sealed class RadioGroupItem : TogglesItem
{
    /// <summary>Set while the owning <see cref="RadioGroup"/> uses <see cref="RadioGroupVariant.Borderless"/>.</summary>
    public const string BorderlessPseudoClass = ":borderless";

    private protected override (object Margin, object Radius)? FocusRingKeys => (RadioGroupTokens.Key(RadioGroupTokenKind.FocusRingMargin), RadioGroupTokens.Key(RadioGroupTokenKind.FocusRingRadius));

    /// <summary>A line under the label that explains the option; also the row's accessible help text.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<RadioGroupItem, string?>(nameof(Description));

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    private Panel? _copy;
    private TextBlock? _description;
    private bool _isBorderless;

    internal void SetBorderless(bool borderless)
    {
        if (_isBorderless == borderless)
        {
            return;
        }
        _isBorderless = borderless;
        PseudoClasses.Set(BorderlessPseudoClass, borderless);
        UpdateRingShape();
    }

    private protected override void OnFocusRingUpdated() => UpdateRingShape();

    private void UpdateRingShape()
    {
        if (FocusRing?.Ring is not { } ring)
        {
            return;
        }
        if (_isBorderless)
        {
            ring.Bind(MarginProperty, ring.GetResourceObservable(RadioGroupTokens.Key(RadioGroupTokenKind.BorderlessFocusRingMargin)), BindingPriority.LocalValue);
            ring.Bind(Border.CornerRadiusProperty, ring.GetResourceObservable(RadioGroupTokens.Key(RadioGroupTokenKind.BorderlessFocusRingRadius)), BindingPriority.LocalValue);
        }
        else
        {
            ring.ClearValue(MarginProperty);
            ring.ClearValue(Border.CornerRadiusProperty);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        // The copy column is the label's parent; the description goes under the label.
        _copy = e.NameScope.Find<ContentPresenter>("PART_Label")?.GetVisualParent() as Panel;
        _description = null;
        UpdateDescription();
    }

    /// <summary>
    /// The description line (PART_Description) is built the first time there is a description, under the label, with the
    /// token bindings the template used to declare; most rows have none.
    /// </summary>
    private void UpdateDescription()
    {
        if (string.IsNullOrEmpty(Description))
        {
            _description?.IsVisible = false;
            return;
        }
        if (_description is null)
        {
            if (_copy is null)
            {
                return;
            }
            var block = new TextBlock { Name = "PART_Description", TextWrapping = TextWrapping.Wrap };
            block.Bind(TextBlock.TextProperty, this.GetObservable(DescriptionProperty));
            block.Bind(MarginProperty, block.GetResourceObservable(RadioGroupTokens.Key(RadioGroupTokenKind.DescriptionMargin)));
            ArcTokens.Bind(block, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS, BindingPriority.LocalValue);
            block.Bind(TextBlock.LineHeightProperty, block.GetResourceObservable(RadioGroupTokens.Key(RadioGroupTokenKind.DescriptionLineHeight)));
            ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted, BindingPriority.LocalValue);
            _copy.Children.Add(block);
            _description = block;
        }
        _description.IsVisible = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DescriptionProperty)
        {
            AutomationProperties.SetHelpText(this, Description);
            UpdateDescription();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TogglesRadioAutomationPeer(this);
}
