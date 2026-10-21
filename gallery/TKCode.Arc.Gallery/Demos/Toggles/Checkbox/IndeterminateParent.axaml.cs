using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CheckboxIndeterminateParentDemo : UserControl
{
    private bool _syncing;

    public CheckboxIndeterminateParentDemo()
    {
        InitializeComponent();
        Email.IsChecked = true;
    }

    private Checkbox[] Children => [Email, Push, Sms];

    // The parent reflects its children: checked, unchecked or indeterminate (the check morphs into a dash).
    private void OnChildChanged(object? sender, CheckboxStateChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }
        _syncing = true;
        var on = Children.Count(c => c.IsChecked == true);
        All.CheckedState = on == 0
            ? CheckboxState.Unchecked
            : on == Children.Length ? CheckboxState.Checked : CheckboxState.Indeterminate;
        _syncing = false;
    }

    // A click on the parent sets every child.
    private void OnAllChanged(object? sender, CheckboxStateChangedEventArgs e)
    {
        if (_syncing || e.NewState == CheckboxState.Indeterminate)
        {
            return;
        }
        _syncing = true;
        foreach (var child in Children)
        {
            child.IsChecked = e.NewState == CheckboxState.Checked;
        }
        _syncing = false;
    }
}
