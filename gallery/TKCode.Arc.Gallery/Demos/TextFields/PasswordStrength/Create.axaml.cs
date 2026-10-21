using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PasswordStrengthCreateDemo : UserControl
{
    public PasswordStrengthCreateDemo()
    {
        InitializeComponent();
    }

    private void OnStrengthChanged(object? sender, PasswordStrengthChangedEventArgs e)
        => Result.Text = e.Level == 0
            ? "Strength: none yet"
            : $"Strength: {e.Label} (level {e.Level}, {e.Score.MetRuleIds.Count} rules met)";
}
