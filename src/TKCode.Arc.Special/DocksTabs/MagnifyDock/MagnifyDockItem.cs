using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One icon of a <see cref="MagnifyDock"/>: a button whose content is the icon, with a <see cref="Label"/> that shows
/// above it under the pointer or keyboard focus, and a dot beneath it while <see cref="IsRunning"/>.
/// </summary>
[TemplatePart(RootPart, typeof(Panel))]
[PseudoClasses(RunningPseudoClass)]
public class MagnifyDockItem : AvaloniaButton
{
    public const string RootPart = "PART_Root";
    public const string RunningPseudoClass = ":running";

    /// <summary>The name shown above the icon and read by assistive technology.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MagnifyDockItem, string?>(nameof(Label));

    /// <summary>Whether the app is open: a dot shows under the icon. A launch sets it when the dock marks launches.</summary>
    public static readonly StyledProperty<bool> IsRunningProperty =
        AvaloniaProperty.Register<MagnifyDockItem, bool>(nameof(IsRunning), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private readonly MatrixTransform _pose = new();
    private LazyFocusRing? _focusRing;
    private bool _ownsName;

    static MagnifyDockItem()
    {
        ClipToBoundsProperty.OverrideDefaultValue<MagnifyDockItem>(false);
    }

    public MagnifyDockItem()
    {
        RenderTransformOrigin = new RelativePoint(0.5, 1, RelativeUnit.Relative);
        RenderTransform = _pose;
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="IsRunningProperty"/>
    public bool IsRunning
    {
        get => GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(MagnifyDockItem);

    internal MagnifyDock? Dock { get; set; }

    /// <summary>Writes the icon's magnification and lift; the transform is reused, so a frame allocates nothing.</summary>
    internal void Pose(double offset, double scale, double lift)
    {
        var matrix = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset, -lift);
        if (_pose.Matrix != matrix)
        {
            _pose.Matrix = matrix;
        }
    }

    internal Matrix CurrentPose => _pose.Matrix;

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, MagnifyDockTokens.Key(MagnifyDockTokenKind.ItemFocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep16));
        _focusRing.Attach(e.NameScope.Find<Panel>(RootPart));
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        UpdateName();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            UpdateName();
            Dock?.OnItemLabelChanged();
        }
        else if (change.Property == IsRunningProperty)
        {
            PseudoClasses.Set(RunningPseudoClass, IsRunning);
            Dock?.OnItemRunningChanged();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
        Dock?.OnItemFocusChanged();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
        Dock?.OnItemFocusChanged();
    }

    private void UpdateName()
    {
        if (AutomationProperties.GetName(this) is not { Length: > 0 } || _ownsName)
        {
            _ownsName = true;
            AutomationProperties.SetName(this, Label ?? string.Empty);
        }
    }
}
