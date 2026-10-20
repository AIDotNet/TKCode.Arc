using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Tone of an <see cref="Alert"/>: picks the icon, its color and how urgently it is announced.</summary>
public enum AlertTone
{
    /// <summary>Neutral information (accent Info icon).</summary>
    Info,

    /// <summary>Something worked (Check icon).</summary>
    Success,

    /// <summary>Attention needed soon (TriangleAlert icon).</summary>
    Warning,

    /// <summary>Something failed; announced assertively (CircleX icon).</summary>
    Danger
}

/// <summary>
/// Arc alert: a persistent message that helps people recover or continue. Keep one instance and change
/// <see cref="Tone"/>, <see cref="Title"/> and the content to morph it between states: the icon pops into the new
/// tone, new text rises out of a soft blur and the height springs to fit. Hiding it (<see cref="IsOpen"/>) collapses
/// the height while it fades, so the content below closes the gap. A string <c>Content</c> is the description line;
/// any other content is shown as is.
/// </summary>
[TemplatePart(PresencePart, typeof(MessagesPresenceFrame))]
[TemplatePart(IconPart, typeof(MessagesGlyphSwap))]
[TemplatePart(DismissPart, typeof(AvaloniaButton))]
[PseudoClasses(DismissiblePseudoClass, RichContentPseudoClass)]
public class Alert : ContentControl
{
    public const string PresencePart = "PART_Presence";
    public const string IconPart = "PART_Icon";
    public const string DismissPart = "PART_Dismiss";
    public const string DismissiblePseudoClass = ":dismissible";
    public const string RichContentPseudoClass = ":rich-content";

    #region 公共属性定义

    /// <summary>The headline. A new title rises in from below while the old one lifts away.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Alert, string?>(nameof(Title));

    /// <summary>Tone (default <see cref="AlertTone.Info"/>).</summary>
    public static readonly StyledProperty<AlertTone> ToneProperty =
        AvaloniaProperty.Register<Alert, AlertTone>(nameof(Tone));

    /// <summary>Whether the alert is shown. Hiding collapses its height and fades it out. Two-way.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<Alert, bool>(nameof(IsOpen), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Shows the dismiss button. Dismissing collapses the alert, then raises <see cref="Dismissed"/>.</summary>
    public static readonly StyledProperty<bool> IsDismissibleProperty =
        AvaloniaProperty.Register<Alert, bool>(nameof(IsDismissible));

    /// <summary>Raised after a dismissed alert finished collapsing.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> DismissedEvent =
        RoutedEvent.Register<Alert, RoutedEventArgs>(nameof(Dismissed), RoutingStrategies.Bubble);

    internal static readonly DirectProperty<Alert, Geometry?> IconDataProperty =
        AvaloniaProperty.RegisterDirect<Alert, Geometry?>(nameof(IconData), o => o.IconData);

    internal static readonly DirectProperty<Alert, string?> DescriptionTextProperty =
        AvaloniaProperty.RegisterDirect<Alert, string?>(nameof(DescriptionText), o => o.DescriptionText);

    internal static readonly DirectProperty<Alert, Thickness> DescriptionMarginProperty =
        AvaloniaProperty.RegisterDirect<Alert, Thickness>(nameof(DescriptionMargin), o => o.DescriptionMargin);

    internal static readonly StyledProperty<double> DescriptionSpacingProperty =
        AvaloniaProperty.Register<Alert, double>(nameof(DescriptionSpacing), 4);

    /// <summary>The headline. A new title rises in from below while the old one lifts away.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Tone (default <see cref="AlertTone.Info"/>).</summary>
    public AlertTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    /// <summary>Whether the alert is shown. Hiding collapses its height and fades it out. Two-way.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Shows the dismiss button. Dismissing collapses the alert, then raises <see cref="Dismissed"/>.</summary>
    public bool IsDismissible
    {
        get => GetValue(IsDismissibleProperty);
        set => SetValue(IsDismissibleProperty, value);
    }

    /// <summary>Raised after a dismissed alert finished collapsing.</summary>
    public event EventHandler<RoutedEventArgs>? Dismissed
    {
        add => AddHandler(DismissedEvent, value);
        remove => RemoveHandler(DismissedEvent, value);
    }

    #endregion

    #region 内部属性定义

    internal Geometry? IconData
    {
        get => _iconData;
        private set => SetAndRaise(IconDataProperty, ref _iconData, value);
    }

    internal string? DescriptionText
    {
        get => _descriptionText;
        private set => SetAndRaise(DescriptionTextProperty, ref _descriptionText, value);
    }

    internal Thickness DescriptionMargin
    {
        get => _descriptionMargin;
        private set => SetAndRaise(DescriptionMarginProperty, ref _descriptionMargin, value);
    }

    internal double DescriptionSpacing
    {
        get => GetValue(DescriptionSpacingProperty);
        set => SetValue(DescriptionSpacingProperty, value);
    }

    #endregion

    private Geometry? _iconData = ArcIcons.Info;
    private string? _descriptionText;
    private Thickness _descriptionMargin;
    private MessagesPresenceFrame? _presence;
    private MessagesGlyphSwap? _icon;
    private AvaloniaButton? _dismiss;
    private bool _dismissPending;

    /// <summary>Collapses the alert (like the dismiss button) and raises <see cref="Dismissed"/> afterwards.</summary>
    public void Dismiss()
    {
        if (!IsOpen)
        {
            return;
        }
        _dismissPending = true;
        SetCurrentValue(IsOpenProperty, false);
        if (_presence is null)
        {
            RaiseDismissed();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_presence is not null)
        {
            _presence.Settled -= OnPresenceSettled;
        }
        if (_dismiss is not null)
        {
            _dismiss.Click -= OnDismissClick;
        }
        _presence = e.NameScope.Find<MessagesPresenceFrame>(PresencePart);
        _icon = e.NameScope.Find<MessagesGlyphSwap>(IconPart);
        _dismiss = e.NameScope.Find<AvaloniaButton>(DismissPart);
        if (_presence is not null)
        {
            _presence.Settled += OnPresenceSettled;
        }
        if (_dismiss is not null)
        {
            _dismiss.Click += OnDismissClick;
        }
        UpdateAutomation();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToneProperty)
        {
            IconData = Tone switch
            {
                AlertTone.Success => ArcIcons.Check,
                AlertTone.Warning => ArcIcons.TriangleAlert,
                AlertTone.Danger => MessagesIcons.CircleX,
                _ => ArcIcons.Info
            };
            UpdateAutomation();
        }
        else if (change.Property == ContentProperty)
        {
            var content = change.NewValue;
            var text = content switch
            {
                string s => s,
                null => null,
                IFormattable f when content is not Control => f.ToString(null, null),
                _ => null
            };
            var rich = content is not null && text is null;
            PseudoClasses.Set(RichContentPseudoClass, rich);
            DescriptionText = string.IsNullOrEmpty(text) ? null : text;
            UpdateDescriptionMargin();
            UpdateAutomation();
        }
        else if (change.Property == DescriptionSpacingProperty)
        {
            UpdateDescriptionMargin();
        }
        else if (change.Property == TitleProperty)
        {
            UpdateAutomation();
        }
        else if (change.Property == IsDismissibleProperty)
        {
            PseudoClasses.Set(DismissiblePseudoClass, IsDismissible);
        }
        else if (change.Property == IsOpenProperty)
        {
            if (change.GetNewValue<bool>())
            {
                _dismissPending = false;
                if (IsLoaded)
                {
                    _icon?.PlayEnter();
                }
            }
        }
    }

    private void UpdateDescriptionMargin()
    {
        var hasDetails = DescriptionText is not null || PseudoClasses.Contains(RichContentPseudoClass);
        DescriptionMargin = hasDetails ? new Thickness(0, DescriptionSpacing, 0, 0) : default;
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, Title ?? string.Empty);
        AutomationProperties.SetHelpText(this, DescriptionText ?? string.Empty);
        // role="alert" for danger, role="status" otherwise.
        AutomationProperties.SetLiveSetting(this, Tone == AlertTone.Danger ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
        if (_dismiss is not null)
        {
            AutomationProperties.SetName(_dismiss, $"Dismiss: {Title}");
        }
    }

    private void OnDismissClick(object? sender, RoutedEventArgs e)
    {
        Dismiss();
    }

    private void OnPresenceSettled(object? sender, bool open)
    {
        if (!open && _dismissPending)
        {
            RaiseDismissed();
        }
    }

    private void RaiseDismissed()
    {
        _dismissPending = false;
        RaiseEvent(new RoutedEventArgs(DismissedEvent, this));
    }
}
