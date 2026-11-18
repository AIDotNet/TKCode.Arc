using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using TKCode.Arc.DesignTokens;
using Avalonia.Media.Immutable;
using Avalonia.Automation.Provider;

namespace TKCode.Arc;

/// <summary>
/// The panel of one file: its header (path with a muted folder, the "New" pill for an added file, the rolling diffstat,
/// the Viewed checkbox at the right) over the diff. A 1px border above each; 52 tall header, padding 8 12 8 24.
/// </summary>
internal sealed class CodeReviewFilePanel : Border
{
    private readonly ICodeReviewHost _host;
    private readonly Border _head;
    private readonly RollingDigits _added;
    private readonly RollingDigits _deleted;
    private readonly StackPanel _stat;
    private readonly CodeReviewToggle _viewed;

    public CodeReviewFilePanel(ICodeReviewHost host, CodeReviewFileState file, Func<IEnumerable<CodeReviewThread>> threads)
    {
        _host = host;
        File = file;
        var labels = host.Labels;
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(this, BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessTop);
        AutomationProperties.SetControlTypeOverride(this, Avalonia.Automation.Peers.AutomationControlType.TabItem);

        var slash = file.File.Path.LastIndexOf('/');
        var path = new TextBlock
        {
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Inlines = new InlineCollection
            {
                Folder(slash >= 0 ? file.File.Path[..(slash + 1)] : string.Empty),
                new Run(slash >= 0 ? file.File.Path[(slash + 1)..] : file.File.Path) { FontWeight = FontWeight.Medium }
            }
        };
        ArcTokens.Bind(path, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        ArcTokens.Bind(path, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);

        _added = Digits(ArcDesignTokenKind.Success);
        _deleted = Digits(ArcDesignTokenKind.Danger);
        _stat = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        _stat.Children.Add(Signed("+", _added, ArcDesignTokenKind.Success));
        _stat.Children.Add(Signed("−", _deleted, ArcDesignTokenKind.Danger));

        _viewed = new CodeReviewToggle(labels.Viewed)
        {
            Height = 32,
            Padding = new Thickness(9, 0, 12, 0),
            LitWhenChecked = false,
            VerticalAlignment = VerticalAlignment.Center
        }.BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        AutomationProperties.SetName(_viewed, labels.ViewedName(file.File.Path));
        DockPanel.SetDock(_viewed, Dock.Right);

        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(path);
        if (file.File.Status == CodeReviewFileStatus.Added)
        {
            left.Children.Add(CodeReviewUi.Pill(labels.New));
        }
        left.Children.Add(_stat);

        var headRow = new DockPanel { LastChildFill = true };
        headRow.Children.Add(_viewed);
        headRow.Children.Add(left);
        _head = new Border { Height = 52, Child = headRow };

        Diff = new CodeReviewDiffView(host, file, threads);
        var diffFrame = new Border { Padding = new Thickness(0, 0, 0, 12), Child = Diff };
        ArcTokens.Bind(diffFrame, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        ArcTokens.Bind(diffFrame, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessTop);

        var stack = new StackPanel();
        stack.Children.Add(_head);
        stack.Children.Add(diffFrame);
        Child = stack;
        UpdateLayoutMode();
        UpdateStat(animate: false);
    }

    public CodeReviewFileState File { get; }

    public CodeReviewDiffView Diff { get; }

    public CodeReviewToggle ViewedToggle => _viewed;

    /// <summary>Re-reads the compact flag: paddings, one number column, the Viewed label.</summary>
    public void UpdateLayoutMode()
    {
        var compact = _host.IsCompactLayout;
        var palette = _host.Palette;
        _head.Padding = new Thickness(compact ? palette.CompactSidePadding : palette.SidePadding, 8, 12, 8);
        _viewed.ShowsLabel = !compact;
        _viewed.Padding = compact ? new Thickness(8, 0) : new Thickness(9, 0, 12, 0);
        Diff.IsCompact = compact;
    }

    public void UpdateStat(bool animate)
    {
        var (added, deleted) = CodeReviewDiff.Stat(File.Lines);
        _added.IsInstant = !animate;
        _deleted.IsInstant = !animate;
        _added.Value = added;
        _deleted.Value = deleted;
        AutomationProperties.SetName(_stat, _host.Labels.DiffstatName(added, deleted));
    }

    private static Run Folder(string text)
    {
        var run = new Run(text);
        run.Bind(TextElement.ForegroundProperty, run.GetResourceObservable(ArcDesignTokenKind.TextMuted));
        return run;
    }

    private static RollingDigits Digits(ArcDesignTokenKind color)
    {
        var digits = new RollingDigits { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(digits, RollingDigits.ForegroundProperty, color);
        ArcTokens.Bind(digits, RollingDigits.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        return digits;
    }

    private static Control Signed(string sign, RollingDigits digits, ArcDesignTokenKind color)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(CodeReviewUi.Mono(sign, color));
        row.Children.Add(digits);
        return row;
    }
}

/// <summary>
/// Holds the shown file panel. Choosing another file slides the old panel out and the new one in by 16px in the
/// direction of travel while they crossfade, and the height springs from one panel to the other; afterwards the height
/// simply follows the panel (its bands animate themselves).
/// </summary>
internal sealed class CodeReviewPanelSwitch : Panel
{
    private Control? _current;
    private readonly List<Control> _exiting = new();
    private MotionValue<double>? _blend;
    private double _blendT = 1;
    private double _fromHeight;

    public Control? Current => _current;

    /// <summary>Shows <paramref name="panel"/>; <paramref name="direction"/> is +1 moving right, −1 moving left, 0 no motion.</summary>
    public void Show(Control panel, int direction)
    {
        if (ReferenceEquals(panel, _current))
        {
            return;
        }
        var old = _current;
        _current = panel;
        var animate = old is not null && direction != 0 && IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        if (!Children.Contains(panel))
        {
            Children.Add(panel);
        }
        _exiting.Remove(panel);
        if (!animate)
        {
            if (old is not null)
            {
                Children.Remove(old);
            }
            foreach (var gone in _exiting)
            {
                Children.Remove(gone);
            }
            _exiting.Clear();
            panel.Opacity = 1;
            panel.RenderTransform = null;
            _blendT = 1;
            InvalidateMeasure();
            return;
        }

        _fromHeight = Bounds.Height;
        var full = ArcMotion.IsFull(this);
        _exiting.Add(old!);
        Slide(old!, 0, full ? -CodeReviewMotion.PanelSlide * direction : 0, 1, 0, CodeReviewMotion.FadeOut, () =>
        {
            _exiting.Remove(old!);
            Children.Remove(old!);
        });
        Slide(panel, full ? CodeReviewMotion.PanelSlide * direction : 0, 0, 0, 1, CodeReviewMotion.FadeIn, null);

        _blend ??= new MotionValue<double>(this, 1, MotionVectors.Double);
        _blend.Changed -= OnBlend;
        _blend.Changed += OnBlend;
        _blend.Jump(0);
        _blendT = 0;
        _ = _blend.AnimateTo(1, full ? MotionSpec.Spring(CodeReviewMotion.Spring) : MotionSpec.Tween(CodeReviewMotion.FadeIn, ArcEasings.Standard));
        InvalidateMeasure();
    }

    private void OnBlend(double value)
    {
        _blendT = value;
        InvalidateMeasure();
    }

    private void Slide(Control panel, double fromX, double toX, double fromOpacity, double toOpacity, TimeSpan fade, Action? done)
    {
        var translate = new TranslateTransform(fromX, 0);
        panel.RenderTransform = translate;
        panel.Opacity = fromOpacity;
        var x = new MotionValue<double>(panel, fromX, MotionVectors.Double);
        x.Changed += v => translate.X = v;
        var opacity = new MotionValue<double>(panel, fromOpacity, MotionVectors.Double);
        opacity.Changed += v => panel.Opacity = Math.Clamp(v, 0, 1);
        _ = x.AnimateTo(toX, MotionSpec.Spring(CodeReviewMotion.Spring));
        _ = Fade();

        async Task Fade()
        {
            await opacity.AnimateTo(toOpacity, MotionSpec.Tween(fade, ArcEasings.Standard));
            if (done is not null)
            {
                // Wait for the height and slide to settle before the old panel goes.
                await x.AnimateTo(toX, MotionSpec.Spring(CodeReviewMotion.Spring));
                done();
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var inner = new Size(availableSize.Width, double.PositiveInfinity);
        foreach (var child in Children)
        {
            child.Measure(inner);
        }
        var target = _current?.DesiredSize.Height ?? 0;
        var height = _blendT >= 1 ? target : _fromHeight + (target - _fromHeight) * _blendT;
        var width = _current?.DesiredSize.Width ?? 0;
        return new Size(width, Math.Max(0, height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(new Rect(0, 0, finalSize.Width, child.DesiredSize.Height));
        }
        ClipToBounds = true;
        return finalSize;
    }
}
