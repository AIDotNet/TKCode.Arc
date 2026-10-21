using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Mixins;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using AvaloniaSelectingItemsControl = Avalonia.Controls.Primitives.SelectingItemsControl;

namespace TKCode.Arc;

/// <summary>
/// One tab of a <see cref="PageHeader"/>: <see cref="HeaderedContentControl.Header"/> is the label in the tab row,
/// <see cref="Count"/> an optional number beside it that rolls digit by digit when it changes, and
/// <see cref="ContentControl.Content"/> the page body shown in the header's scroller while the tab is selected.
/// </summary>
[TemplatePart(CountPart, typeof(RollingDigits))]
[PseudoClasses(SelectedPseudoClass, PressedPseudoClass, HasCountPseudoClass)]
public class PageHeaderTab : HeaderedContentControl, ISelectable
{
    public const string CountPart = "PART_Count";
    public const string SelectedPseudoClass = ":selected";
    public const string PressedPseudoClass = ":pressed";
    public const string HasCountPseudoClass = ":has-count";

    /// <summary>Whether this tab is the selected one.</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaSelectingItemsControl.IsSelectedProperty.AddOwner<PageHeaderTab>();

    /// <summary>Number shown after the label (open issues, files); null shows none. Changes roll the digits.</summary>
    public static readonly StyledProperty<int?> CountProperty =
        AvaloniaProperty.Register<PageHeaderTab, int?>(nameof(Count));

    /// <summary>Whether this tab is the selected one.</summary>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>Number shown after the label (open issues, files); null shows none. Changes roll the digits.</summary>
    public int? Count
    {
        get => GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    private RollingDigits? _digits;

    static PageHeaderTab()
    {
        SelectableMixin.Attach<PageHeaderTab>(IsSelectedProperty);
        PressedMixin.Attach<PageHeaderTab>();
        FocusableProperty.OverrideDefaultValue<PageHeaderTab>(true);
    }

    private PageHeader? Owner => ItemsControl.ItemsControlFromItemContainer(this) as PageHeader;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _digits = e.NameScope.Find<RollingDigits>(CountPart);
        UpdateCount();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty || change.Property == ContentTemplateProperty)
        {
            Owner?.OnTabContentChanged(this);
        }
        else if (change.Property == HeaderProperty || change.Property == CountProperty)
        {
            if (change.Property == CountProperty)
            {
                UpdateCount();
            }
            UpdateName();
        }
    }

    private void UpdateCount()
    {
        PseudoClasses.Set(HasCountPseudoClass, Count.HasValue);
        if (_digits is not null && Count is { } count)
        {
            _digits.Value = count;
        }
    }

    private void UpdateName()
    {
        if (Header is string header)
        {
            AutomationProperties.SetName(this, Count is { } count ? $"{header}, {count}" : header);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Radix activates a trigger on mouse down with the primary button; touch activates on release (below).
        if (e.Handled || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            Owner?.OnTabPressed(this);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Handled || e.Pointer.Type != PointerType.Touch)
        {
            return;
        }
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            Owner?.OnTabPressed(this);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Owner?.OnTabFocused(this, e.NavigationMethod);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PageHeaderTabAutomationPeer(this);

    private sealed class PageHeaderTabAutomationPeer : ListItemAutomationPeer
    {
        public PageHeaderTabAutomationPeer(PageHeaderTab owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;
    }
}
