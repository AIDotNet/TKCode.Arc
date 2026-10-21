// Ported from Arc's free command-palette block (MIT, Copyright (c) 2026 Elia Kuratli): the parts of
// command-palette.tsx (search field, clear and close buttons, shared highlight, group heading, result row).

using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>The bare text box of a <see cref="CommandPalette"/> search row (the row draws the frame).</summary>
internal sealed class CommandPaletteInput : AvaloniaTextBox
{
    protected override Type StyleKeyOverride => typeof(CommandPaletteInput);
}

/// <summary>The 24px clear button that pops in while the query has text.</summary>
internal sealed class CommandPaletteClearButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(CommandPaletteClearButton);
}

/// <summary>The "Esc" key-cap button that asks the host to close the palette.</summary>
internal sealed class CommandPaletteCloseButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(CommandPaletteCloseButton);
}

/// <summary>How the shared highlight travels to a new row.</summary>
internal enum CommandPaletteHighlightMove
{
    /// <summary>Lands in place (first appearance, reduced motion).</summary>
    Jump,

    /// <summary>The pointer moved onto a row: snappy spring.</summary>
    Pointer,

    /// <summary>An arrow, Home or End key: 70ms on the enter curve.</summary>
    Keyboard,

    /// <summary>The highlighted row moved with the list (data changed): smooth spring, like the rows.</summary>
    Glide
}

/// <summary>
/// The one highlight behind the active result (Arc's <c>.highlight</c>): its top and height follow the row, its sides
/// span the list. Drawn in <see cref="Render"/>, so moving it never relayouts. Code-driven (<see cref="MotionValue{T}"/>)
/// because the transition differs per move (spring for the pointer, a 70ms tween for keys, a fade on first appearance),
/// which a ControlTheme transition cannot choose.
/// </summary>
internal sealed class CommandPaletteHighlight : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<CommandPaletteHighlight, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<CommandPaletteHighlight, CornerRadius>(nameof(CornerRadius));

    private static readonly TimeSpan s_keyTravel = TimeSpan.FromMilliseconds(70);
    private static readonly TimeSpan s_fadeIn = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan s_fadeOut = TimeSpan.FromMilliseconds(100);

    private MotionValue<Vector>? _box;
    private MotionValue<double>? _opacity;
    private bool _shown;

    static CommandPaletteHighlight()
    {
        AffectsRender<CommandPaletteHighlight>(FillProperty, CornerRadiusProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<CommandPaletteHighlight>(false);
    }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>True while it is shown (or fading in).</summary>
    public bool IsShown => _shown;

    /// <summary>Current (animated) top of the highlight.</summary>
    public double CurrentTop => _box?.Value.X ?? 0;

    /// <summary>Current (animated) height of the highlight.</summary>
    public double CurrentHeight => _box?.Value.Y ?? 0;

    /// <summary>The top it is heading to.</summary>
    public double TargetTop => _box?.Target.X ?? 0;

    /// <summary>Current opacity (0 hidden).</summary>
    public double CurrentOpacity => _opacity?.Value ?? 0;

    private ArcMotionLevel Level => IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;

    public void Show(double top, double height, CommandPaletteHighlightMove move)
    {
        _box ??= Track(new MotionValue<Vector>(this, new Vector(top, height), MotionVectors.Vector));
        _opacity ??= Track(new MotionValue<double>(this, 0, MotionVectors.Double));
        var level = Level;
        var target = new Vector(top, height);
        // A first appearance lands in place and fades in; reduced motion never travels.
        if (!_shown || level != ArcMotionLevel.Full)
        {
            move = CommandPaletteHighlightMove.Jump;
        }
        switch (move)
        {
            case CommandPaletteHighlightMove.Pointer:
                _ = _box.AnimateTo(target, MotionSpec.Spring(ArcSpring.Snappy));
                break;
            case CommandPaletteHighlightMove.Keyboard:
                _ = _box.AnimateTo(target, MotionSpec.Tween(s_keyTravel, ArcEasings.Enter));
                break;
            case CommandPaletteHighlightMove.Glide:
                _ = _box.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
                break;
            default:
                _box.Jump(target);
                break;
        }
        _shown = true;
        if (level == ArcMotionLevel.Off)
        {
            _opacity.Jump(1);
        }
        else if (_opacity.Target < 1)
        {
            _ = _opacity.AnimateTo(1, MotionSpec.Tween(s_fadeIn, ArcEasings.Standard));
        }
    }

    public void Hide()
    {
        _shown = false;
        if (_opacity is null)
        {
            return;
        }
        if (Level == ArcMotionLevel.Off)
        {
            _opacity.Jump(0);
        }
        else if (_opacity.Target > 0)
        {
            _ = _opacity.AnimateTo(0, MotionSpec.Tween(s_fadeOut, ArcEasings.Standard));
        }
    }

    private MotionValue<T> Track<T>(MotionValue<T> value) where T : struct
    {
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    /// <summary>
    /// Asks for the whole available width: the results frame (<see cref="Primitives.SpringSizeDecorator"/>, height axis)
    /// clips to its child's desired width while it springs, so the content must claim the full row width or the right
    /// edge (shortcut caps, highlight) would be cut off mid-animation.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
        => new(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, 0);

    public override void Render(DrawingContext context)
    {
        if (_box is null || _opacity is null || _opacity.Value <= 0.001 || Fill is not { } fill)
        {
            return;
        }
        var box = _box.Value;
        if (box.Y <= 0 || Bounds.Width <= 0)
        {
            return;
        }
        var radius = CornerRadius;
        var rounded = new RoundedRect(new Rect(0, box.X, Bounds.Width, box.Y), radius.TopLeft, radius.TopRight, radius.BottomRight, radius.BottomLeft);
        using (context.PushOpacity(_opacity.Value))
        {
            context.DrawRectangle(fill, null, rounded);
        }
    }
}

/// <summary>A group heading of the result list (Arc's <c>.groupHeading</c>).</summary>
internal sealed class CommandPaletteGroupHeading : TextBlock
{
    protected override Type StyleKeyOverride => typeof(CommandPaletteGroupHeading);

    /// <summary>The group's name (also the text).</summary>
    public string GroupName { get; private set; } = string.Empty;

    public void SetGroup(string name)
    {
        GroupName = name;
        Text = name;
        AutomationProperties.SetName(this, name);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new HeadingPeer(this);

    private sealed class HeadingPeer(CommandPaletteGroupHeading owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => ((CommandPaletteGroupHeading)Owner).GroupName;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// One result (Arc's <c>.result</c>): a 16px icon, the label with its description on the same baseline, and an
/// optional shortcut cap. The shared highlight draws the active row; the row itself only tints its icon.
/// </summary>
[PseudoClasses(ActivePseudoClass, NarrowPseudoClass, NarrowestPseudoClass)]
internal sealed class CommandPaletteRow : TemplatedControl
{
    public const string ActivePseudoClass = ":active";
    public const string NarrowPseudoClass = ":narrow";
    public const string NarrowestPseudoClass = ":narrowest";

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<CommandPaletteRow, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<CommandPaletteRow, string?>(nameof(Description));

    public static readonly StyledProperty<string?> ShortcutProperty =
        AvaloniaProperty.Register<CommandPaletteRow, string?>(nameof(Shortcut));

    public static readonly StyledProperty<Geometry?> IconGeometryProperty =
        AvaloniaProperty.Register<CommandPaletteRow, Geometry?>(nameof(IconGeometry));

    public static readonly StyledProperty<object?> IconControlProperty =
        AvaloniaProperty.Register<CommandPaletteRow, object?>(nameof(IconControl));

    /// <summary>Key caps use tabular figures (<c>font-variant-numeric: tabular-nums</c>).</summary>
    public static readonly FontFeatureCollection TabularNumbers = new() { FontFeature.Parse("tnum") };

    private object? _iconSource;
    private bool _iconSet;
    private bool _active;

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string? Shortcut { get => GetValue(ShortcutProperty); set => SetValue(ShortcutProperty, value); }
    public Geometry? IconGeometry { get => GetValue(IconGeometryProperty); set => SetValue(IconGeometryProperty, value); }
    public object? IconControl { get => GetValue(IconControlProperty); set => SetValue(IconControlProperty, value); }

    public CommandPaletteItem? Item { get; private set; }

    public bool IsActive => _active;

    static CommandPaletteRow()
    {
        FocusableProperty.OverrideDefaultValue<CommandPaletteRow>(false);
    }

    public void SetItem(CommandPaletteItem item)
    {
        Item = item;
        Title = item.Label;
        Description = string.IsNullOrEmpty(item.Description) ? null : item.Description;
        Shortcut = string.IsNullOrEmpty(item.Shortcut) ? null : item.Shortcut;
        IsEnabled = item.IsEnabled;
        if (!_iconSet || !ReferenceEquals(_iconSource, item.Icon))
        {
            _iconSet = true;
            _iconSource = item.Icon;
            switch (item.Icon)
            {
                case null:
                    IconControl = null;
                    IconGeometry = ArcIcons.Command;
                    break;
                case Geometry geometry:
                    IconControl = null;
                    IconGeometry = geometry;
                    break;
                default:
                    IconGeometry = null;
                    IconControl = item.Icon;
                    break;
            }
        }
        AutomationProperties.SetName(this, Description is null ? Title : $"{Title}, {Description}");
    }

    /// <summary>Lets go of a control icon so another row may show it.</summary>
    public void ReleaseIcon()
    {
        IconControl = null;
        _iconSet = false;
        _iconSource = null;
    }

    public void SetActive(bool active)
    {
        if (_active == active)
        {
            return;
        }
        _active = active;
        PseudoClasses.Set(ActivePseudoClass, active);
        if (ControlAutomationPeer.FromElement(this) is { } peer)
        {
            peer.RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, !active, active);
        }
    }

    public void SetWidthClass(bool narrow, bool narrowest)
    {
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(NarrowestPseudoClass, narrowest);
    }

    /// <summary>Raised when automation selects the row.</summary>
    public Action<CommandPaletteRow>? AutomationSelected { get; set; }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    private sealed class RowPeer(CommandPaletteRow owner) : ControlAutomationPeer(owner), ISelectionItemProvider
    {
        private CommandPaletteRow Row => (CommandPaletteRow)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public bool IsSelected => Row.IsActive;

        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection()
        {
        }

        public void RemoveFromSelection()
        {
        }

        public void Select() => Row.AutomationSelected?.Invoke(Row);
    }
}

/// <summary>
/// The copy of a row (Arc's <c>.resultCopy</c>): the title keeps its own width (up to the whole row), the description
/// takes what is left and truncates, and the two sit on one baseline.
/// </summary>
internal sealed class CommandPaletteRowCopy : Panel
{
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<CommandPaletteRowCopy, double>(nameof(Gap), 8);

    static CommandPaletteRowCopy()
    {
        AffectsMeasure<CommandPaletteRowCopy>(GapProperty);
    }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    private Control? Title => Children.Count > 0 ? Children[0] : null;

    private Control? Caption => Children.Count > 1 && Children[1].IsVisible ? Children[1] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Title is not { } title)
        {
            return default;
        }
        title.Measure(availableSize);
        var titleWidth = Math.Min(title.DesiredSize.Width, availableSize.Width);
        if (Caption is not { } caption)
        {
            return title.DesiredSize;
        }
        var rest = Math.Max(0, availableSize.Width - titleWidth - Gap);
        caption.Measure(new Size(rest, availableSize.Height));
        var (above, below) = Extent(title, caption);
        return new Size(titleWidth + Gap + caption.DesiredSize.Width, above + below);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Title is not { } title)
        {
            return finalSize;
        }
        var titleWidth = Math.Min(title.DesiredSize.Width, finalSize.Width);
        if (Caption is not { } caption)
        {
            title.Arrange(new Rect(0, (finalSize.Height - title.DesiredSize.Height) / 2, titleWidth, title.DesiredSize.Height));
            return finalSize;
        }
        var (above, below) = Extent(title, caption);
        var top = (finalSize.Height - above - below) / 2;
        title.Arrange(new Rect(0, top + above - Baseline(title), titleWidth, title.DesiredSize.Height));
        var x = titleWidth + Gap;
        caption.Arrange(new Rect(x, top + above - Baseline(caption), Math.Max(0, finalSize.Width - x), caption.DesiredSize.Height));
        return finalSize;
    }

    /// <summary>Height above and below the shared baseline.</summary>
    private static (double Above, double Below) Extent(Control title, Control caption)
    {
        var titleBase = Baseline(title);
        var captionBase = Baseline(caption);
        var above = Math.Max(titleBase, captionBase);
        var below = Math.Max(title.DesiredSize.Height - titleBase, caption.DesiredSize.Height - captionBase);
        return (above, below);
    }

    private static double Baseline(Control control)
    {
        if (control is TextBlock text && text.TextLayout is { TextLines.Count: > 0 } layout)
        {
            return text.Padding.Top + layout.TextLines[0].Baseline;
        }
        return control.DesiredSize.Height;
    }
}
