using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The whole-row button of an <see cref="ApiKeyRow"/>: it opens and closes the row's details and reports itself to
/// screen readers as an expand / collapse button. Its keyboard focus ring is drawn inside its bounds, so the row's
/// clipping (while it grows or folds) never cuts it off (Arc clips it away).
/// </summary>
internal sealed class ApiKeyRowTrigger : AvaloniaButton
{
    /// <summary>Whether the row's details are open.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ApiKeyRowTrigger, bool>(nameof(IsExpanded));

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    protected override Type StyleKeyOverride => typeof(ApiKeyRowTrigger);

    protected override AutomationPeer OnCreateAutomationPeer() => new TriggerPeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            PseudoClasses.Set(":expanded", change.GetNewValue<bool>());
        }
    }

    private sealed class TriggerPeer(ApiKeyRowTrigger owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        private ApiKeyRowTrigger Trigger => (ApiKeyRowTrigger)Owner;

        public ExpandCollapseState ExpandCollapseState =>
            Trigger.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (!Trigger.IsExpanded)
            {
                Invoke();
            }
        }

        public void Collapse()
        {
            if (Trigger.IsExpanded)
            {
                Invoke();
            }
        }
    }
}

/// <summary>
/// The line struck through a revoked key's name or mask: 1.5px with round ends, 2px past the text on both sides, drawn
/// from left to right (480ms, enter curve). Code-driven because the progress is a scale whose start is delayed per line
/// (the mask follows the name by 80ms) and must be reversible when a revoke fails.
/// </summary>
internal sealed class ApiKeyStrike : Control
{
    /// <summary>Line color.</summary>
    public static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<ApiKeyStrike, IBrush?>(nameof(Brush));

    /// <summary>Line thickness (1.5).</summary>
    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<ApiKeyStrike, double>(nameof(Thickness), 1.5);

    private MotionValue<double>? _progress;

    static ApiKeyStrike()
    {
        AffectsRender<ApiKeyStrike>(BrushProperty, ThicknessProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ApiKeyStrike>(false);
        ClipToBoundsProperty.OverrideDefaultValue<ApiKeyStrike>(false);
    }

    /// <inheritdoc cref="BrushProperty"/>
    public IBrush? Brush { get => GetValue(BrushProperty); set => SetValue(BrushProperty, value); }

    /// <inheritdoc cref="ThicknessProperty"/>
    public double Thickness { get => GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    /// <summary>How much of the line is drawn (0–1).</summary>
    public double Progress => _progress?.Value ?? 0;

    /// <summary>Draws the line in (or takes it away at once). Full motion animates; otherwise it jumps.</summary>
    public void Set(bool on, TimeSpan delay)
    {
        _progress ??= CreateProgress();
        if (!on || !ArcMotion.IsFull(this) || !IsLoaded)
        {
            _progress.Jump(on ? 1 : 0);
            return;
        }
        _ = _progress.AnimateTo(1, MotionSpec.Tween(DeveloperSprings.Strike, ArcEasings.Enter, delay));
    }

    private MotionValue<double> CreateProgress()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    public override void Render(DrawingContext context)
    {
        var progress = Progress;
        if (progress <= 0 || Brush is not { } brush)
        {
            return;
        }
        var thickness = Thickness;
        var width = (Bounds.Width + 4) * Math.Clamp(progress, 0, 1);
        var top = Bounds.Height * 0.52 - thickness / 2;
        context.DrawRectangle(brush, null, new RoundedRect(new Rect(-2, top, width, thickness), thickness / 2));
    }
}

/// <summary>The form's scope checkboxes: equal columns (two, or one when the form is stacked), 16 between them.</summary>
internal sealed class ApiKeyScopeGrid : Panel
{
    /// <summary>How many columns.</summary>
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<ApiKeyScopeGrid, int>(nameof(Columns), 2);

    /// <summary>Between columns.</summary>
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<ApiKeyScopeGrid, double>(nameof(ColumnGap), 16);

    static ApiKeyScopeGrid()
    {
        AffectsMeasure<ApiKeyScopeGrid>(ColumnsProperty, ColumnGapProperty);
    }

    /// <inheritdoc cref="ColumnsProperty"/>
    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <inheritdoc cref="ColumnGapProperty"/>
    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    private double CellWidth(double width)
    {
        var columns = Math.Max(1, Columns);
        return Math.Max(0, (width - ColumnGap * (columns - 1)) / columns);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = Math.Max(1, Columns);
        var cell = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : CellWidth(availableSize.Width);
        double height = 0, rowHeight = 0, widest = 0;
        var visible = Children.Where(c => c.IsVisible).ToList();
        for (var i = 0; i < visible.Count; i++)
        {
            visible[i].Measure(new Size(cell, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, visible[i].DesiredSize.Height);
            widest = Math.Max(widest, visible[i].DesiredSize.Width);
            if (i % columns == columns - 1 || i == visible.Count - 1)
            {
                height += rowHeight;
                rowHeight = 0;
            }
        }
        var width = double.IsInfinity(availableSize.Width) ? widest * columns + ColumnGap * (columns - 1) : availableSize.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Math.Max(1, Columns);
        var cell = CellWidth(finalSize.Width);
        var visible = Children.Where(c => c.IsVisible).ToList();
        double top = 0;
        for (var start = 0; start < visible.Count; start += columns)
        {
            var count = Math.Min(columns, visible.Count - start);
            var rowHeight = 0.0;
            for (var j = 0; j < count; j++)
            {
                rowHeight = Math.Max(rowHeight, visible[start + j].DesiredSize.Height);
            }
            for (var j = 0; j < count; j++)
            {
                visible[start + j].Arrange(new Rect(j * (cell + ColumnGap), top, cell, rowHeight));
            }
            top += rowHeight;
        }
        return finalSize;
    }
}

/// <summary>Builds the scope chips of a row (22 high, pill, 1px border, mono 12 in the secondary text color).</summary>
internal static class ApiKeyChips
{
    public static void Fill(WrapPanel panel, IEnumerable<string> scopes)
    {
        panel.Children.Clear();
        foreach (var scope in scopes)
        {
            var chip = new Border
            {
                Height = 22,
                Padding = new Thickness(8, 0),
                BorderThickness = new Thickness(1),
                Background = Brushes.Transparent,
                Child = new TextBlock
                {
                    Text = scope,
                    FontSize = 12,
                    LineHeight = 16.8,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
            ArcTokens.Bind(chip, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
            ArcTokens.Bind(chip.Child!, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
            ArcTokens.Bind(chip.Child!, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            panel.Children.Add(chip);
        }
    }
}

/// <summary>Remembers whether the last input was the keyboard, so moved focus shows a ring only then.</summary>
internal static class ApiKeyFocus
{
    public static NavigationMethod Method(bool keyboard) => keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer;
}
