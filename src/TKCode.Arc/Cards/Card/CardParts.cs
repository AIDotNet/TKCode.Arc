using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The card's heading. With <see cref="IsInteractive"/> (the card has details) it is the quick look trigger: a button in
/// the tab order that opens on Enter / Space; otherwise a plain heading that takes no focus.
/// </summary>
internal sealed class CardTitle : AvaloniaButton
{
    public static readonly StyledProperty<bool> IsInteractiveProperty =
        AvaloniaProperty.Register<CardTitle, bool>(nameof(IsInteractive));

    public bool IsInteractive { get => GetValue(IsInteractiveProperty); set => SetValue(IsInteractiveProperty, value); }

    protected override Type StyleKeyOverride => typeof(CardTitle);

    static CardTitle()
    {
        FocusableProperty.OverrideDefaultValue<CardTitle>(false);
        IsTabStopProperty.OverrideDefaultValue<CardTitle>(false);
        AutomationProperties.HeadingLevelProperty.OverrideDefaultValue<CardTitle>(3);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<CardTitle>(AutomationControlType.Text);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsInteractiveProperty)
        {
            var interactive = change.GetNewValue<bool>();
            SetCurrentValue(FocusableProperty, interactive);
            SetCurrentValue(IsTabStopProperty, interactive);
            SetCurrentValue(AutomationProperties.ControlTypeOverrideProperty, interactive ? AutomationControlType.Button : AutomationControlType.Text);
        }
        else if (change.Property == ContentProperty)
        {
            AutomationProperties.SetName(this, change.NewValue as string ?? string.Empty);
        }
    }

    protected override void OnClick()
    {
        if (IsInteractive)
        {
            base.OnClick();
        }
    }
}

/// <summary>
/// The quick look panel of <see cref="Card"/>: a <see cref="DialogSurface"/> (title, description, close button, dialog
/// semantics) laid out like Arc's quick look: media on top, a floating close button, the card's copy and footer, then
/// the details. Shown by a <see cref="Primitives.ModalPresence"/>; its keyed pieces fly with the card's.
/// </summary>
internal sealed class CardQuickLook : DialogSurface
{
    public const string ReturningPseudoClass = ":returning";

    public static readonly StyledProperty<string?> StatusProperty =
        AvaloniaProperty.Register<CardQuickLook, string?>(nameof(Status));

    /// <summary>The card's description width, so the copy never rewraps while it travels (Arc's --card-measure).</summary>
    public static readonly StyledProperty<double> DescriptionMeasureProperty =
        AvaloniaProperty.Register<CardQuickLook, double>(nameof(DescriptionMeasure), double.PositiveInfinity);

    public string? Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    public double DescriptionMeasure { get => GetValue(DescriptionMeasureProperty); set => SetValue(DescriptionMeasureProperty, value); }

    protected override Type StyleKeyOverride => typeof(CardQuickLook);

    /// <summary>Parts of the applied template.</summary>
    public CardParts? Parts { get; private set; }

    public event EventHandler? PartsChanged;

    static CardQuickLook()
    {
        CloseButtonNameProperty.OverrideDefaultValue<CardQuickLook>("Close quick look");
    }

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Parts = CardParts.Find(e.NameScope);
        PartsChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void SetFlag(string pseudoClass, bool value) => PseudoClasses.Set(pseudoClass, value);
}

/// <summary>Named parts shared by the card template and the quick look template.</summary>
internal sealed class CardParts
{
    public Border? Surface { get; private init; }
    public Border? Media { get; private init; }
    public Decorator? Zoom { get; private init; }
    public ContentPresenter? MediaPresenter { get; private init; }
    public Control? Title { get; private init; }
    public TextBlock? Description { get; private init; }
    public ContentPresenter? ContentHost { get; private init; }
    public ContentPresenter? AvatarPresenter { get; private init; }
    public ContentPresenter? MetaPresenter { get; private init; }
    public CardStatusLine? Status { get; private init; }
    public ContentPresenter? ActionPresenter { get; private init; }
    public ContentPresenter? DetailsPresenter { get; private init; }
    public Control? Details { get; private init; }
    public Control? CloseSlot { get; private init; }

    /// <summary>The media zoom (hover drift in the card, settle in the quick look).</summary>
    public ScaleTransform ZoomTransform { get; } = new();

    public static CardParts Find(INameScope scope)
    {
        var parts = new CardParts
        {
            Surface = scope.Find<Border>("PART_Surface"),
            Media = scope.Find<Border>("PART_Media"),
            Zoom = scope.Find<Decorator>("PART_Zoom"),
            MediaPresenter = scope.Find<ContentPresenter>("PART_MediaPresenter"),
            Title = scope.Find<Control>("PART_Title"),
            Description = scope.Find<TextBlock>("PART_Description"),
            ContentHost = scope.Find<ContentPresenter>("PART_ContentHost"),
            AvatarPresenter = scope.Find<ContentPresenter>("PART_AvatarPresenter"),
            MetaPresenter = scope.Find<ContentPresenter>("PART_MetaPresenter"),
            Status = scope.Find<CardStatusLine>("PART_Status"),
            ActionPresenter = scope.Find<ContentPresenter>("PART_ActionPresenter"),
            DetailsPresenter = scope.Find<ContentPresenter>("PART_DetailsPresenter"),
            Details = scope.Find<Control>("PART_Details"),
            CloseSlot = scope.Find<Control>("PART_CloseSlot")
        };
        if (parts.Zoom is { } zoom)
        {
            zoom.RenderTransformOrigin = RelativePoint.Center;
            zoom.RenderTransform = parts.ZoomTransform;
        }
        return parts;
    }

    public void SetZoom(double scale)
    {
        ZoomTransform.ScaleX = scale;
        ZoomTransform.ScaleY = scale;
    }
}
