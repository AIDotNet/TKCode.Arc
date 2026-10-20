using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Arc empty state: a useful next step when there is nothing to show yet (an empty list, zero search results,
/// first use). A centered icon block, a title, one or two sentences and an optional action. Keep it mounted and
/// change its properties to morph between states: the icon pops into the new one, the copy rises in while the old
/// copy leaves, and the text block's height springs to fit. The icon block settles in once when it first appears.
/// </summary>
[TemplatePart(IconBlockPart, typeof(Control))]
[TemplatePart(TitlePart, typeof(MessagesTextSwap))]
[TemplatePart(DescriptionPart, typeof(MessagesTextSwap))]
[PseudoClasses(HasActionPseudoClass)]
public class EmptyState : TemplatedControl
{
    public const string IconBlockPart = "PART_IconBlock";
    public const string TitlePart = "PART_Title";
    public const string DescriptionPart = "PART_Description";
    public const string HasActionPseudoClass = ":has-action";

    #region 公共属性定义

    /// <summary>The headline (a heading, level 3).</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Title));

    /// <summary>One or two sentences on why it is empty and what to do next.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Description));

    /// <summary>The call to action, usually a button (or a panel of two).</summary>
    public static readonly StyledProperty<object?> ActionProperty =
        AvaloniaProperty.Register<EmptyState, object?>(nameof(Action));

    /// <summary>The icon: a control, or a 24×24 Lucide <see cref="Geometry"/>. Default: Lucide Folder at stroke 1.5.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<EmptyState, object?>(nameof(Icon));

    /// <summary>Accessible name of the region.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Label));

    /// <summary>Horizontal padding (space-5); the vertical padding follows the window (clamp(space-8, 8vw, space-12)).</summary>
    internal static readonly StyledProperty<double> InlinePaddingProperty =
        AvaloniaProperty.Register<EmptyState, double>(nameof(InlinePadding), 20);

    internal static readonly DirectProperty<EmptyState, Thickness> ContentPaddingProperty =
        AvaloniaProperty.RegisterDirect<EmptyState, Thickness>(nameof(ContentPadding), o => o.ContentPadding);

    internal static readonly DirectProperty<EmptyState, object?> IconContentProperty =
        AvaloniaProperty.RegisterDirect<EmptyState, object?>(nameof(IconContent), o => o.IconContent);

    /// <summary>The headline (a heading, level 3).</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    /// <summary>One or two sentences on why it is empty and what to do next.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>The call to action, usually a button (or a panel of two).</summary>
    public object? Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }
    /// <summary>The icon: a control, or a 24×24 Lucide <see cref="Geometry"/>. Default: Lucide Folder at stroke 1.5.</summary>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    /// <summary>Accessible name of the region.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    internal double InlinePadding
    {
        get => GetValue(InlinePaddingProperty);
        set => SetValue(InlinePaddingProperty, value);
    }

    internal Thickness ContentPadding
    {
        get => _contentPadding;
        private set => SetAndRaise(ContentPaddingProperty, ref _contentPadding, value);
    }

    internal object? IconContent
    {
        get => _iconContent;
        private set => SetAndRaise(IconContentProperty, ref _iconContent, value);
    }

    #endregion

    private object? _iconContent;
    private Thickness _contentPadding = new(20, 48);
    private double _blockPadding = 48;
    private Control? _iconBlock;
    private MessagesTextSwap? _title;
    private MotionValue<double>? _settle;
    private ScaleTransform? _iconScale;
    private bool _settled;
    private IDisposable? _clientSizeSubscription;

    static EmptyState()
    {
        FocusableProperty.OverrideDefaultValue<EmptyState>(false);
    }

    public EmptyState()
    {
        IconContent = CreateGlyph(ArcIcons.Folder);
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Region);
    }

    private static ArcGlyph CreateGlyph(Geometry data) => new() { Data = data, Size = 24, StrokeThickness = 1.5 };

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _iconBlock = e.NameScope.Find<Control>(IconBlockPart);
        _title = e.NameScope.Find<MessagesTextSwap>(TitlePart);
        if (_title is not null)
        {
            AutomationProperties.SetHeadingLevel(_title, 3);
        }
        if (_iconBlock is not null)
        {
            _iconScale = new ScaleTransform();
            _iconBlock.RenderTransform = _iconScale;
            _iconBlock.RenderTransformOrigin = RelativePoint.Center;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty)
        {
            // A new icon pops in; the same icon stays still.
            IconContent = Icon switch
            {
                null => CreateGlyph(ArcIcons.Folder),
                Geometry geometry => CreateGlyph(geometry),
                _ => Icon
            };
        }
        else if (change.Property == ActionProperty)
        {
            PseudoClasses.Set(HasActionPseudoClass, Action is not null);
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label ?? string.Empty);
        }
        else if (change.Property == InlinePaddingProperty)
        {
            ContentPadding = new Thickness(InlinePadding, _blockPadding);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            _clientSizeSubscription = topLevel.GetObservable(TopLevel.ClientSizeProperty).Subscribe(new ClientSizeObserver(this));
        }
        if (!_settled && _iconBlock is not null && ArcMotion.IsFull(this))
        {
            // The icon block settles in once (0.92 → 1, considered 480ms on the enter curve); hide it before the first frame.
            _iconBlock.Opacity = 0;
            if (_iconScale is not null)
            {
                _iconScale.ScaleX = 0.92;
                _iconScale.ScaleY = 0.92;
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _clientSizeSubscription?.Dispose();
        _clientSizeSubscription = null;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_settled || _iconBlock is null)
        {
            return;
        }
        _settled = true;
        if (!ArcMotion.IsFull(this))
        {
            ApplySettle(1);
            return;
        }
        _settle = new MotionValue<double>(this, 0, MotionVectors.Double);
        _settle.Changed += ApplySettle;
        _ = _settle.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.Enter));
    }

    private void ApplySettle(double p)
    {
        if (_iconBlock is null)
        {
            return;
        }
        _iconBlock.Opacity = Math.Clamp(p, 0, 1);
        if (_iconScale is not null)
        {
            var s = 0.92 + 0.08 * p;
            _iconScale.ScaleX = s;
            _iconScale.ScaleY = s;
        }
    }

    private sealed class ClientSizeObserver : IObserver<Size>
    {
        private readonly EmptyState _owner;

        public ClientSizeObserver(EmptyState owner) => _owner = owner;

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        // Vertical padding follows the window: clamp(space-8, 8vw, space-12).
        public void OnNext(Size value)
        {
            _owner._blockPadding = Math.Clamp(value.Width * 0.08, 32, 48);
            _owner.ContentPadding = new Thickness(_owner.InlinePadding, _owner._blockPadding);
        }
    }
}
