using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The orbit itself: a square holding the centre disc, the two rings of logo chips, the connector and the name tag. It
/// owns the <see cref="OrbitLogosSimulation"/> and one <see cref="LiveLoop"/> that moves the chips with render
/// transforms and sleeps whenever nothing turns, the orbit is out of view or the tab is hidden.
/// </summary>
internal sealed class OrbitLogosStage : Panel
{
    public const double MaxSize = 460;

    /// <summary>Below this stage width a ring chip is 44px; above it a chip is 12% of the orbit, between 48 and 56px.</summary>
    public const double PhoneWidth = 420;

    private readonly OrbitLogosSimulation _sim = new();
    private readonly Guides _guides;
    private readonly LiveLoop _loop;
    private readonly List<OrbitLogosChip> _chips = [];
    private MotionValue<double>? _connect;
    private Control? _disc;
    private Border? _tag;
    private readonly TranslateTransform _tagTranslate = new();
    private TextBlock? _tagText;
    private OrbitLogosChip? _tagChip;
    private int _connectorIndex = -1;
    private int _selected = -1;
    private bool _externalHold;
    private double _chipSize = 52;
    private double _innerRadius;
    private double _outerRadius;
    private double _discSize;
    private IImmutableBrush _ringBrush = new ImmutableSolidColorBrush(Colors.Gray);
    private IImmutableBrush _connectorBrush = new ImmutableSolidColorBrush(Colors.Gray);

    public OrbitLogosStage()
    {
        _guides = new Guides(this);
        Children.Add(_guides);
        _loop = new LiveLoop(this, Step);
        AutomationProperties.SetAccessibilityView(_guides, AccessibilityView.Raw);
    }

    internal OrbitLogosSimulation Simulation => _sim;

    internal LiveLoop Loop => _loop;

    internal IReadOnlyList<OrbitLogosChip> Chips => _chips;

    internal double ChipSize => _chipSize;

    internal double InnerRadius => _innerRadius;

    internal double OuterRadius => _outerRadius;

    /// <summary>The connector's progress from the centre to the chosen chip, 0 to 1.</summary>
    internal double ConnectProgress => _connect?.Value ?? 0;

    /// <summary>The tag that follows the hovered or focused chip.</summary>
    internal Border? NameTag => _tag;

    /// <summary>The orbit is held by something outside it: a category is chosen or a card is open.</summary>
    public bool ExternalHold
    {
        get => _externalHold;
        set
        {
            if (_externalHold == value)
            {
                return;
            }
            _externalHold = value;
            UpdateHeld();
        }
    }

    /// <summary>Where the chosen logo parks: true to the right of the orbit, false under it.</summary>
    public bool IsSideBySide
    {
        get => _sim.DetailAngle == 0;
        set => _sim.DetailAngle = value ? 0 : Math.PI / 2;
    }

    public void Attach(Control disc, Border tag, TextBlock tagText)
    {
        _disc = disc;
        _tag = tag;
        _tagText = tagText;
        tag.IsHitTestVisible = false;
        tag.RenderTransform = _tagTranslate;
        tag.Opacity = 0;
    }

    public void SetBrushes(IBrush? ring, IBrush? connector)
    {
        _ringBrush = Freeze(ring);
        _connectorBrush = Freeze(connector);
        _guides.InvalidateVisual();
    }

    public void SetChips(IReadOnlyList<OrbitLogosChip> chips)
    {
        foreach (var chip in _chips)
        {
            chip.TagChanged -= OnTagChanged;
            Children.Remove(chip);
        }
        _chips.Clear();
        _selected = -1;
        _connectorIndex = -1;
        _tagChip = null;
        HideTag();
        foreach (var chip in chips)
        {
            chip.TagChanged += OnTagChanged;
            _chips.Add(chip);
            Children.Add(chip);
        }
        if (_tag is not null)
        {
            // The tag floats above every chip.
            Children.Remove(_tag);
            Children.Add(_tag);
        }
        _sim.Build(chips.Count);
        _sim.CanMove = ArcMotion.IsFull(this);
        InvalidateMeasure();
        ApplyPositions();
    }

    /// <summary>Points the rings at the chosen logo (or the category) and starts the connector toward it.</summary>
    public void Retarget(int selected, IReadOnlyList<bool>? matches, bool snap = false)
    {
        _sim.CanMove = ArcMotion.IsFull(this);
        _sim.Retarget(selected, matches, snap);
        SetSelected(selected, snap);
        if (_sim.CanMove && !snap)
        {
            _loop.Start();
        }
        else
        {
            ApplyPositions();
        }
        UpdateHeld();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : MaxSize;
        var side = Math.Max(0, Math.Min(MaxSize, width));
        if (double.IsFinite(availableSize.Height))
        {
            side = Math.Min(side, Math.Max(availableSize.Height, 0));
        }
        _chipSize = width < PhoneWidth ? 44 : Math.Clamp(side * 0.12, 48, 56);
        _discSize = Math.Max(64, side * 0.26);
        _outerRadius = Math.Max(0, side / 2 - _chipSize / 2 - 6);
        var discRadius = _discSize / 2;
        _innerRadius = discRadius + (_outerRadius - discRadius) * 0.48;

        var square = new Size(side, side);
        _guides.Measure(square);
        _disc?.Measure(new Size(_discSize, _discSize));
        var chip = new Size(_chipSize, _chipSize);
        var padding = new Thickness(_chipSize * 0.26);
        foreach (var item in _chips)
        {
            item.Padding = padding;
            item.Measure(chip);
        }
        _tag?.Measure(Size.Infinity);
        return square;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var center = new Point(finalSize.Width / 2, finalSize.Height / 2);
        _guides.Arrange(new Rect(finalSize));
        _disc?.Arrange(new Rect(center.X - _discSize / 2, center.Y - _discSize / 2, _discSize, _discSize));
        foreach (var chip in _chips)
        {
            chip.Arrange(new Rect(center.X - _chipSize / 2, center.Y - _chipSize / 2, _chipSize, _chipSize));
        }
        if (_tag is not null)
        {
            _tag.Arrange(new Rect(_tag.DesiredSize));
        }
        ApplyPositions();
        return finalSize;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _sim.CanMove = ArcMotion.IsFull(this);
        UpdateHeld();
        if (_sim.CanMove)
        {
            _loop.Start();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPointerOverProperty || change.Property == IsKeyboardFocusWithinProperty)
        {
            UpdateHeld();
        }
        else if (change.Property == ArcMotion.LevelProperty)
        {
            _sim.CanMove = ArcMotion.IsFull(this);
            if (_sim.CanMove)
            {
                _loop.Start();
            }
            else
            {
                _sim.Retarget(_selected, null, snap: true);
                ApplyPositions();
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new StageAutomationPeer(this);

    /// <summary>Re-reads the holds and wakes the loop so the spin eases to rest or back up.</summary>
    internal void UpdateHeld()
    {
        var held = IsPointerOver || IsKeyboardFocusWithin || _externalHold;
        _sim.Held = held;
        if (_sim.CanMove)
        {
            _loop.Start();
        }
    }

    /// <summary>Speed and pause come from the control; waking lets a resumed orbit turn again.</summary>
    internal void Wake()
    {
        _sim.CanMove = ArcMotion.IsFull(this);
        if (_sim.CanMove)
        {
            _loop.Start();
        }
    }

    /// <summary>The chip's rectangle in the coordinates of <paramref name="relativeTo"/>.</summary>
    internal Rect? ChipRect(int index, Visual relativeTo)
    {
        if (index < 0 || index >= _chips.Count)
        {
            return null;
        }
        var chip = _chips[index];
        return chip.TranslatePoint(default, relativeTo) is { } origin ? new Rect(origin, chip.Bounds.Size) : null;
    }

    /// <summary>Runs one frame by hand (tests step the loop with a manual clock; this is the same body).</summary>
    internal bool Step(double dt)
    {
        var moving = _sim.Step(dt);
        ApplyPositions();
        return moving;
    }

    private void SetSelected(int index, bool snap)
    {
        _selected = index;
        _connect ??= new MotionValue<double>(this, 0, MotionVectors.Double);
        _connect.Changed -= OnConnectChanged;
        _connect.Changed += OnConnectChanged;
        if (index >= 0)
        {
            _connectorIndex = index;
            if (snap || !ArcMotion.IsFull(this))
            {
                _connect.Jump(1);
            }
            else if (_connect.Target < 1)
            {
                _ = _connect.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, ArcMotionPresets.Fast));
            }
        }
        else if (snap || !ArcMotion.IsFull(this))
        {
            _connect.Jump(0);
            _connectorIndex = -1;
        }
        else
        {
            _ = _connect.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Exit));
        }
        _guides.InvalidateVisual();
    }

    private void OnConnectChanged(double value)
    {
        if (value <= 0.001 && _selected < 0)
        {
            _connectorIndex = -1;
        }
        _guides.InvalidateVisual();
    }

    private void ApplyPositions()
    {
        for (var i = 0; i < _chips.Count; i++)
        {
            var (x, y) = _sim.Offset(i, _innerRadius, _outerRadius);
            _chips[i].X = x;
            _chips[i].Y = y;
        }
        UpdateTag();
        _guides.InvalidateVisual();
    }

    private void OnTagChanged(OrbitLogosChip chip, bool show)
    {
        if (show)
        {
            _tagChip = chip;
            if (_tagText is not null)
            {
                _tagText.Text = chip.Logo.Name;
            }
            _tag?.Measure(Size.Infinity);
            _tag?.Arrange(new Rect(_tag.DesiredSize));
            UpdateTag();
            if (_tag is not null)
            {
                _tag.Opacity = 1;
            }
        }
        else if (_tagChip == chip)
        {
            HideTag();
        }
    }

    private void HideTag()
    {
        _tagChip = null;
        if (_tag is not null)
        {
            _tag.Opacity = 0;
        }
    }

    private void UpdateTag()
    {
        if (_tag is null || _tagChip is null)
        {
            return;
        }
        var width = _tag.DesiredSize.Width;
        var center = new Point(Bounds.Width / 2 + _tagChip.X, Bounds.Height / 2 + _tagChip.Y);
        var x = Math.Clamp(center.X - width / 2, 0, Math.Max(0, Bounds.Width - width));
        var y = center.Y + _chipSize / 2 + 6;
        _tagTranslate.X = x;
        _tagTranslate.Y = y;
    }

    private static IImmutableBrush Freeze(IBrush? brush) => brush switch
    {
        IImmutableBrush immutable => immutable,
        ISolidColorBrush solid => new ImmutableSolidColorBrush(solid.Color, solid.Opacity),
        not null => brush.ToImmutable(),
        _ => new ImmutableSolidColorBrush(Colors.Gray)
    };

    /// <summary>The faint ring lines and the accent connector, drawn under the chips.</summary>
    private sealed class Guides : Control
    {
        private readonly OrbitLogosStage _stage;
        private Pen? _ringPen;
        private Pen? _connectorPen;
        private IImmutableBrush? _penBrush;
        private IImmutableBrush? _connectorPenBrush;

        public Guides(OrbitLogosStage stage)
        {
            _stage = stage;
            IsHitTestVisible = false;
        }

        public override void Render(DrawingContext context)
        {
            var sim = _stage._sim;
            if (_stage.Bounds.Width <= 0 || sim.Count == 0)
            {
                return;
            }
            var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
            if (_ringPen is null || !ReferenceEquals(_penBrush, _stage._ringBrush))
            {
                _penBrush = _stage._ringBrush;
                _ringPen = new Pen(_penBrush, 1);
            }
            if (sim.RingCount(0) > 0)
            {
                context.DrawEllipse(null, _ringPen, center, _stage._innerRadius, _stage._innerRadius);
            }
            if (sim.RingCount(1) > 0)
            {
                context.DrawEllipse(null, _ringPen, center, _stage._outerRadius, _stage._outerRadius);
            }

            var index = _stage._connectorIndex;
            var progress = _stage.ConnectProgress;
            if (index < 0 || index >= sim.Count || progress <= 0.001)
            {
                return;
            }
            if (_connectorPen is null || !ReferenceEquals(_connectorPenBrush, _stage._connectorBrush))
            {
                _connectorPenBrush = _stage._connectorBrush;
                _connectorPen = new Pen(_connectorPenBrush, 2, lineCap: PenLineCap.Round);
            }
            var (x, y) = sim.Offset(index, _stage._innerRadius, _stage._outerRadius);
            var end = new Point(center.X + x * Math.Min(1, progress), center.Y + y * Math.Min(1, progress));
            context.DrawLine(_connectorPen, center, end);
        }
    }

    private sealed class StageAutomationPeer(OrbitLogosStage owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override bool IsControlElementCore() => true;
    }
}
