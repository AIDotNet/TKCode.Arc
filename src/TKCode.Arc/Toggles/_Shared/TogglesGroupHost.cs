using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The behavior every toggle group shares, composed into each public group control: roving tab stop, arrow / Home /
/// End selection, the gliding indicator on the selected item, and following option changes (an option's properties
/// may be bound in XAML, so their values can arrive after its container was prepared, or change later).
/// </summary>
internal sealed class TogglesGroupHost
{
    private readonly SelectingItemsControl _owner;
    private readonly bool _flipInRtl;
    private readonly Func<int, bool> _usable;
    private readonly Action<Control, object?>? _apply;
    private readonly ObservedItems _options;
    private TogglesIndicatorTracker? _tracker;

    /// <param name="owner">The group control.</param>
    /// <param name="flipInRtl">Whether the arrow keys flip in right-to-left layouts.</param>
    /// <param name="apply">
    /// Copies an option's properties onto its container; called again when a property of a shown option changes.
    /// </param>
    public TogglesGroupHost(SelectingItemsControl owner, bool flipInRtl, Action<Control, object?>? apply = null)
    {
        _owner = owner;
        _flipInRtl = flipInRtl;
        _apply = apply;
        _usable = index => TogglesNavigation.IsUsable(_owner, index);
        // The options may be a view model's: they are observed only while the group is shown.
        _options = new ObservedItems(OnOptionChanged);
        owner.AttachedToVisualTree += (_, _) => OnAttached();
        owner.DetachedFromVisualTree += (_, _) => _options.Observe(null);
    }

    private void OnAttached()
    {
        _options.Observe(_owner.Items);
        // Catch up with what changed while the group was away.
        if (_apply is not null)
        {
            foreach (var container in _owner.GetRealizedContainers())
            {
                _apply(container, _owner.ItemFromContainer(container));
            }
        }
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

    private void OnOptionChanged(AvaloniaObject? option)
    {
        // A list change is the items control's own business: it prepares new containers itself.
        if (option is null)
        {
            return;
        }
        if (_apply is not null && _owner.ContainerFromItem(option) is { } container)
        {
            _apply(container, option);
        }
        if (option is ITogglesValueSource source)
        {
            // The selection reports the value it has now (the items control reads it only when the selection changes).
            if (ReferenceEquals(_owner.SelectedItem, option) && !Equals(_owner.SelectedValue, source.ToggleValue))
            {
                _owner.SetCurrentValue(SelectingItemsControl.SelectedValueProperty, source.ToggleValue);
            }
        }
        UpdateTabStop();
    }

    /// <summary>Handles a navigation key from an item. Returns true when it moved the selection.</summary>
    public bool HandleKey(KeyEventArgs e)
    {
        var flip = _flipInRtl && _owner.FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;
        return TogglesNavigation.HandleKey(_owner, e, _usable, flip) >= 0;
    }
}
