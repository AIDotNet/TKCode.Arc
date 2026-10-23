using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A button that opens and closes something (a row's panel, a group's list, a fold): screen readers get its expanded
/// state through the expand / collapse pattern (Arc's <c>aria-expanded</c>). Enter and Space press it.
/// </summary>
internal abstract class ToolCallDisclosureButton : AvaloniaButton
{
    /// <summary>Whether what it controls is open.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ToolCallDisclosureButton, bool>(nameof(IsExpanded));

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty && ControlAutomationPeer.FromElement(this) is Peer peer)
        {
            peer.RaiseExpanded(change.GetOldValue<bool>(), change.GetNewValue<bool>());
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer(ToolCallDisclosureButton owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        private ToolCallDisclosureButton Button => (ToolCallDisclosureButton)Owner;

        public ExpandCollapseState ExpandCollapseState =>
            Button.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (!Button.IsExpanded)
            {
                Invoke();
            }
        }

        public void Collapse()
        {
            if (Button.IsExpanded)
            {
                Invoke();
            }
        }

        public void RaiseExpanded(bool was, bool now) =>
            RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                was ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                now ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
    }
}

/// <summary>
/// The whole-row button of a call and the group header's button: a 12px rounded area, transparent at rest, SurfaceMuted at
/// 75% under the pointer (160ms), and a 2px keyboard focus ring drawn inside its edge so a clipping parent never cuts it.
/// </summary>
internal sealed class ToolCallHeadButton : ToolCallDisclosureButton
{
    protected override Type StyleKeyOverride => typeof(ToolCallHeadButton);
}

/// <summary>"Show 25 more lines" / "Show less" under a folded output: a 14px chevron that turns over and quiet 12px text.</summary>
internal sealed class ToolCallFoldButton : ToolCallDisclosureButton
{
    protected override Type StyleKeyOverride => typeof(ToolCallFoldButton);
}

/// <summary>
/// The icon-only copy button of an Input or Output section (28 × 28, radius 9): Lucide copy in TextSecondary, SurfaceMuted
/// and Foreground under the pointer. A copy trades the icon for a check in Success (the copy icon fades over 100ms, the
/// check fades in over 120ms while scaling 0.85 → 1 on a quick spring), holds it 1.9s and trades back, without changing
/// width; it also says "Copy input: Copied" through the row's live region. The swap is code-driven
/// (<see cref="MotionValue{T}"/>) because it is a timed sequence on two overlapping glyphs.
/// </summary>
internal sealed class ToolCallCopyButton : AvaloniaButton
{
    /// <summary>How long the check stays.</summary>
    internal static readonly TimeSpan HoldFor = TimeSpan.FromMilliseconds(1900);

    private readonly ArcGlyph _copy = new() { Data = ArcIcons.Copy, Size = 16, IsHitTestVisible = false };
    private readonly ArcGlyph _check = new() { Data = ArcIcons.Check, Size = 16, IsHitTestVisible = false, Opacity = 0 };
    private readonly ScaleTransform _checkScale = new(0.85, 0.85);
    private MotionValue<double>? _swap;
    private DispatcherTimer? _reset;
    private string _label = string.Empty;

    public ToolCallCopyButton()
    {
        var cell = new Panel { Width = 16, Height = 16 };
        cell.Children.Add(_copy);
        cell.Children.Add(_check);
        Content = cell;
        _check.RenderTransformOrigin = RelativePoint.Center;
        _check.RenderTransform = _checkScale;
        ArcTokens.Bind(_check, ArcGlyph.ForegroundProperty, DesignTokens.ArcDesignTokenKind.Success);
    }

    protected override Type StyleKeyOverride => typeof(ToolCallCopyButton);

    /// <summary>The text to copy, read on each press.</summary>
    public Func<string?>? Text { get; set; }

    /// <summary>The button's name ("Copy input").</summary>
    public string Label
    {
        get => _label;
        set
        {
            _label = value;
            AutomationProperties.SetName(this, value);
        }
    }

    /// <summary>The words said after a copy.</summary>
    public string CopiedLabel { get; set; } = "Copied";

    /// <summary>The words said when the clipboard refused.</summary>
    public string FailedLabel { get; set; } = "Copy failed";

    /// <summary>Says a sentence through the row's live region.</summary>
    public Action<string>? Announce { get; set; }

    /// <summary>Whether the check shows now.</summary>
    internal bool IsCopied { get; private set; }

    /// <summary>The last text copied (tests read it; the clipboard may be missing headless).</summary>
    internal string? LastCopied { get; private set; }

    protected override async void OnClick()
    {
        base.OnClick();
        var text = Text?.Invoke() ?? string.Empty;
        LastCopied = text;
        var copied = await ArcClipboard.CopyTextAsync(this, text);
        Announce?.Invoke($"{Label}: {(copied ? CopiedLabel : FailedLabel)}");
        if (copied)
        {
            ShowCopied(true);
            _reset?.Stop();
            _reset = new DispatcherTimer { Interval = HoldFor };
            _reset.Tick += (_, _) =>
            {
                _reset?.Stop();
                ShowCopied(false);
            };
            _reset.Start();
        }
    }

    private void ShowCopied(bool copied)
    {
        IsCopied = copied;
        _swap ??= CreateSwap();
        if (!IsLoaded || ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            _swap.Jump(copied ? 1 : 0);
            return;
        }
        _ = _swap.AnimateTo(copied ? 1 : 0, MotionSpec.Spring(ArcSpring.Quick));
    }

    private MotionValue<double> CreateSwap()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += p =>
        {
            // One progress drives both glyphs: the copy icon is gone in the first third, the check fades in over the first
            // half and grows 0.85 → 1 along the spring.
            var q = Math.Clamp(p, 0, 1);
            _copy.Opacity = Math.Clamp(1 - q * 3, 0, 1);
            _check.Opacity = Math.Clamp(q * 2, 0, 1);
            var s = 0.85 + 0.15 * Math.Clamp(p, 0, 1.05);
            _checkScale.ScaleX = s;
            _checkScale.ScaleY = s;
        };
        return value;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _reset?.Stop();
        IsCopied = false;
        _swap?.Jump(0);
        _copy.Opacity = 1;
        _check.Opacity = 0;
    }
}
