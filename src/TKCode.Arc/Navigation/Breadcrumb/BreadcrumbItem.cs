using System.Windows.Input;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One crumb of a <see cref="Breadcrumb"/>. <see cref="ContentControl.Content"/> is the label; a crumb with a
/// <see cref="NavigateUri"/> behaves as a link, one with a <see cref="Command"/> (or any crumb inside a breadcrumb
/// that handles <see cref="Breadcrumb.ItemClick"/>) as a button. The last crumb is the current page: never a link.
/// </summary>
[PseudoClasses(FirstPseudoClass, CurrentPseudoClass, InteractivePseudoClass, OverflowPseudoClass)]
[TemplatePart(ReservePart, typeof(ContentPresenter))]
public class BreadcrumbItem : ContentControl
{
    public const string FirstPseudoClass = ":first";
    public const string CurrentPseudoClass = ":current";
    public const string InteractivePseudoClass = ":interactive";
    public const string OverflowPseudoClass = ":overflow";
    public const string ReservePart = "PART_Reserve";

    /// <summary>Address opened with the platform launcher when the crumb is chosen.</summary>
    public static readonly StyledProperty<Uri?> NavigateUriProperty =
        AvaloniaProperty.Register<BreadcrumbItem, Uri?>(nameof(NavigateUri));

    /// <summary>Command run when the crumb is chosen (client-side paths such as a file browser).</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<BreadcrumbItem, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<BreadcrumbItem, object?>(nameof(CommandParameter));

    /// <summary>False renders the crumb as plain text (Arc: an item without href or onClick). Defaults to true.</summary>
    public static readonly StyledProperty<bool> IsClickableProperty =
        AvaloniaProperty.Register<BreadcrumbItem, bool>(nameof(IsClickable), true);

    /// <summary>True for the last crumb, the current page.</summary>
    public static readonly DirectProperty<BreadcrumbItem, bool> IsCurrentProperty =
        AvaloniaProperty.RegisterDirect<BreadcrumbItem, bool>(nameof(IsCurrent), o => o.IsCurrent);

    /// <inheritdoc cref="NavigateUriProperty"/>
    public Uri? NavigateUri { get => GetValue(NavigateUriProperty); set => SetValue(NavigateUriProperty, value); }

    /// <inheritdoc cref="CommandProperty"/>
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    /// <summary>Parameter passed to <see cref="Command"/>.</summary>
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    /// <inheritdoc cref="IsClickableProperty"/>
    public bool IsClickable { get => GetValue(IsClickableProperty); set => SetValue(IsClickableProperty, value); }

    private bool _isCurrent;

    /// <inheritdoc cref="IsCurrentProperty"/>
    public bool IsCurrent
    {
        get => _isCurrent;
        internal set
        {
            if (SetAndRaise(IsCurrentProperty, ref _isCurrent, value))
            {
                UpdateState();
            }
        }
    }

    private bool _isFirst;

    internal bool IsFirst
    {
        get => _isFirst;
        set
        {
            if (_isFirst != value)
            {
                _isFirst = value;
                PseudoClasses.Set(FirstPseudoClass, value);
            }
        }
    }

    private bool _isOverflowAnchor;

    /// <summary>The first collapsed crumb stands in for the whole collapsed run as an ellipsis button.</summary>
    internal bool IsOverflowAnchor
    {
        get => _isOverflowAnchor;
        set
        {
            if (_isOverflowAnchor != value)
            {
                _isOverflowAnchor = value;
                UpdateState();
            }
        }
    }

    internal int PathIndex { get; set; } = -1;

    internal string? PathKey { get; set; }

    /// <summary>Text for the overflow menu and for ghosts.</summary>
    internal object? MenuLabel => Content is Control ? AutomationProperties.GetName(this) : Content;

    internal bool IsInteractive => IsOverflowAnchor || (!IsCurrent && IsClickable);

    /// <summary>A leaving copy: looks like the crumb it replaces but never takes focus or input.</summary>
    internal bool IsGhost { get; private init; }

    private ContentPresenter? _reserve;

    /// <summary>The crumb's label frame (without the separator): the overflow menu anchors here.</summary>
    internal Control? Frame { get; private set; }
    private TranslateTransform? _translate;
    private MotionValue<double>? _enter;
    private MotionValue<Vector>? _shift;
    private BlurEffect? _blur;
    private bool _pressed;

    static BreadcrumbItem()
    {
        FocusableProperty.OverrideDefaultValue<BreadcrumbItem>(true);
    }

    public BreadcrumbItem()
    {
        UpdateState();
    }

    private Breadcrumb? Owner => ItemsControl.ItemsControlFromItemContainer(this) as Breadcrumb;

    private void UpdateState()
    {
        var interactive = IsInteractive;
        PseudoClasses.Set(CurrentPseudoClass, IsCurrent && !IsOverflowAnchor);
        PseudoClasses.Set(InteractivePseudoClass, interactive);
        PseudoClasses.Set(OverflowPseudoClass, IsOverflowAnchor);
        Focusable = interactive && !IsGhost;
        IsTabStop = interactive && !IsGhost;
        AutomationProperties.SetItemStatus(this, IsCurrent && !IsOverflowAnchor ? "current page" : string.Empty);
        if (IsOverflowAnchor)
        {
            AutomationProperties.SetName(this, Owner?.OverflowLabel ?? "Show hidden path");
        }
        else if (Content is string text)
        {
            AutomationProperties.SetName(this, text);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _reserve = e.NameScope.Find<ContentPresenter>(ReservePart);
        Frame = e.NameScope.Find<Control>("PART_Frame");
        UpdateReserve();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty || change.Property == ContentTemplateProperty)
        {
            UpdateReserve();
            UpdateState();
        }
        else if (change.Property == IsClickableProperty)
        {
            UpdateState();
        }
    }

    /// <summary>Each crumb reserves the width of its medium-weight label so becoming current never shifts the path.</summary>
    private void UpdateReserve()
    {
        if (_reserve is null)
        {
            return;
        }
        // The template goes on after the content, and only with content: a presenter builds the template it has, and one attached
        // to empty content is built with null.
        var content = Content is Control ? null : Content;
        _reserve.ContentTemplate = null;
        _reserve.Content = content;
        _reserve.ContentTemplate = content is null ? null : ContentTemplate;
    }

    #region Input

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && IsInteractive && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressed)
        {
            _pressed = false;
            if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
            {
                e.Handled = true;
                Owner?.Invoke(this);
            }
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && IsInteractive && (e.Key == Key.Enter || e.Key == Key.Space))
        {
            e.Handled = true;
            Owner?.Invoke(this);
            return;
        }
        base.OnKeyDown(e);
    }

    /// <summary>Runs the command and opens the address of this crumb.</summary>
    internal void Execute()
    {
        if (Command is { } command && command.CanExecute(CommandParameter))
        {
            command.Execute(CommandParameter);
        }
        if (NavigateUri is { } uri && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = launcher.LaunchUriAsync(uri);
        }
    }

    #endregion

    #region Motion

    /// <summary>P19 enter: 8px in from the path before it out of a subtle blur (standard + enter).</summary>
    internal void PlayEnter()
    {
        EnsureTransform();
        _blur ??= new BlurEffect();
        _enter ??= CreateEnter();
        _enter.Jump(0);
        _ = _enter.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
    }

    /// <summary>Siblings make room: drawn at their old position, they spring to the new one (smooth).</summary>
    internal void PlayShift(Vector from)
    {
        EnsureTransform();
        if (_shift is null)
        {
            _shift = new MotionValue<Vector>(this, default, MotionVectors.Vector);
            _shift.Changed += _ => ApplyTransform();
        }
        _shift.Jump(_shift.Value + from);
        _ = _shift.AnimateTo(default, MotionSpec.Spring(ArcSpring.Smooth));
    }

    /// <summary>Exit of a ghost copy: 4px back and fading into a subtle blur in 120ms.</summary>
    internal async void PlayExit(Action done)
    {
        EnsureTransform();
        _blur ??= new BlurEffect();
        Effect = _blur;
        var exit = new MotionValue<double>(this, 0, MotionVectors.Double);
        exit.Changed += p =>
        {
            Opacity = 1 - p;
            _translate!.X = -4 * p;
            _blur.Radius = 2 * p;
        };
        await exit.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        done();
    }

    /// <summary>A non-interactive copy used to animate a removed crumb out.</summary>
    internal BreadcrumbItem CreateGhost()
    {
        var ghost = new BreadcrumbItem
        {
            IsGhost = true,
            Content = MenuLabel,
            ContentTemplate = Content is Control ? null : ContentTemplate,
            IsClickable = IsClickable,
            IsHitTestVisible = false
        };
        ghost.IsFirst = IsFirst;
        ghost.IsCurrent = IsCurrent;
        AutomationProperties.SetAccessibilityView(ghost, AccessibilityView.Raw);
        return ghost;
    }

    private void EnsureTransform()
    {
        if (_translate is null)
        {
            _translate = new TranslateTransform();
            RenderTransform = _translate;
        }
    }

    private MotionValue<double> CreateEnter()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += p =>
        {
            Opacity = Math.Clamp(p, 0, 1);
            var radius = 2 * (1 - p);
            if (radius < 0.05)
            {
                Effect = null;
            }
            else
            {
                _blur!.Radius = radius;
                Effect = _blur;
            }
            ApplyTransform();
            if (p >= 1)
            {
                ClearValue(OpacityProperty);
            }
        };
        return value;
    }

    private void ApplyTransform()
    {
        if (_translate is null)
        {
            return;
        }
        var enter = _enter?.Value ?? 1;
        var shift = _shift?.Value ?? default;
        _translate.X = shift.X - 8 * (1 - enter);
        _translate.Y = shift.Y;
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new BreadcrumbItemAutomationPeer(this);

    private sealed class BreadcrumbItemAutomationPeer : ContentControlAutomationPeer, IInvokeProvider
    {
        public BreadcrumbItemAutomationPeer(BreadcrumbItem owner) : base(owner)
        {
        }

        private BreadcrumbItem Crumb => (BreadcrumbItem)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore()
            => !Crumb.IsInteractive ? AutomationControlType.Text
                : Crumb.NavigateUri is not null ? AutomationControlType.Hyperlink
                : AutomationControlType.Button;

        public void Invoke()
        {
            if (Crumb.IsInteractive)
            {
                (ItemsControl.ItemsControlFromItemContainer(Crumb) as Breadcrumb)?.Invoke(Crumb);
            }
        }
    }
}
