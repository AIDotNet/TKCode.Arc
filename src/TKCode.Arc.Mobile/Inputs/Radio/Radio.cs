using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace TKCode.Arc.Mobile;

/// <summary>
/// Mobile radio: a 22px circle with an accent dot that springs in when the item is chosen, and the label (the content)
/// beside it, in a row at least 44 high so the whole row is the touch target. Items that share a
/// <c>GroupName</c> are mutually exclusive: choosing one clears the others. The arrow keys move the choice
/// to the previous or next enabled item of the same group among the siblings. A finger on it dims it; there is no hover state.
/// </summary>
/// <remarks>
/// Derives from Avalonia's <c>RadioButton</c> rather than a <c>ListBox</c>: group coordination (<c>GroupName</c>), <c>IsChecked</c>,
/// binding and <c>Click</c> are the framework's, so a radio can sit in any row layout. Put the radios of one group in the same
/// panel and give them the same <c>GroupName</c>.
/// </remarks>
public class Radio : RadioButton
{
    protected override Type StyleKeyOverride => typeof(Radio);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var step = e.Key switch
        {
            Key.Down or Key.Right => 1,
            Key.Up or Key.Left => -1,
            _ => 0,
        };

        if (step != 0 && !e.Handled && MoveChoice(step))
        {
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private bool MoveChoice(int step)
    {
        if (string.IsNullOrEmpty(GroupName) || this.GetVisualParent() is not { } parent)
        {
            return false;
        }

        var members = parent.GetVisualChildren().OfType<Radio>().Where(r => r.GroupName == GroupName).ToList();
        var index = members.IndexOf(this);
        if (index < 0)
        {
            return false;
        }

        for (var i = 1; i < members.Count; i++)
        {
            var next = members[((index + step * i) % members.Count + members.Count) % members.Count];
            if (next.IsEnabled && next.IsVisible)
            {
                next.Focus(NavigationMethod.Directional);
                next.IsChecked = true;
                return true;
            }
        }

        return false;
    }
}
