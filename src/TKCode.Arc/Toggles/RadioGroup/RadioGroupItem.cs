using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>One row of a <see cref="RadioGroup"/>: a ring with a springing dot, the label and an optional description.</summary>
internal sealed class RadioGroupItem : TogglesItem
{
    protected override (object Margin, object Radius)? FocusRingKeys => (RadioGroupTokens.Key(RadioGroupTokenKind.FocusRingMargin), RadioGroupTokens.Key(RadioGroupTokenKind.FocusRingRadius));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<RadioGroupItem, string?>(nameof(Description));

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    private Panel? _copy;
    private TextBlock? _description;

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
            if (_description is not null)
            {
                _description.IsVisible = false;
            }
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
