using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The behavior every toggle group shares, composed into each public group control: roving tab stop, arrow / Home /
/// End selection, and the gliding indicator on the selected item.
/// </summary>
internal sealed class TogglesGroupHost
{
    private readonly SelectingItemsControl _owner;
    private readonly bool _flipInRtl;
    private readonly Func<int, bool> _usable;
    private TogglesIndicatorTracker? _tracker;

    public TogglesGroupHost(SelectingItemsControl owner, bool flipInRtl)
    {
        _owner = owner;
        _flipInRtl = flipInRtl;
        _usable = index => TogglesNavigation.IsUsable(_owner, index);
    }

    public Func<int, bool> Usable => _usable;

    public ITogglesIndicator? Indicator => _tracker?.Indicator;

    /// <summary>
    /// Uses <paramref name="indicator"/> (a template part) as the selection indicator. It glides on
    /// <paramref name="spring"/> when the component's Arc source has its own spring, otherwise on its Spring preset.
    /// </summary>
    public void AttachIndicator(GlidingIndicator? indicator, double inset, bool fadeInFirst, SpringSpec? spring = null)
        => AttachIndicator(indicator is null ? null : new TogglesGlidingIndicator(indicator, spring), inset, fadeInFirst);

    /// <summary>Uses a custom indicator.</summary>
    public void AttachIndicator(ITogglesIndicator? indicator, double inset, bool fadeInFirst)
    {
        _tracker?.Detach();
        _tracker = indicator is null ? null : new TogglesIndicatorTracker(_owner, indicator, inset, fadeInFirst);
        _tracker?.Place();
    }

    /// <summary>Call when the selection changed: the indicator glides and the tab stop follows.</summary>
    public void OnSelectionChanged()
    {
        _tracker?.Glide();
        UpdateTabStop();
    }

    public void UpdateTabStop() => TogglesNavigation.UpdateTabStop(_owner, _usable);

    /// <summary>Handles a navigation key from an item. Returns true when it moved the selection.</summary>
    public bool HandleKey(KeyEventArgs e)
    {
        var flip = _flipInRtl && _owner.FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;
        return TogglesNavigation.HandleKey(_owner, e, _usable, flip) >= 0;
    }
}
