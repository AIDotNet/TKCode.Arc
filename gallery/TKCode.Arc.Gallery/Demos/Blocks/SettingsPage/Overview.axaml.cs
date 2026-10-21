using Avalonia.Controls;
using TKCode.Arc;

namespace TKCode.Arc.Gallery.Demos;

public partial class SettingsPageOverviewDemo : UserControl
{
    private (string Name, string Email, string Bio) _saved;

    public SettingsPageOverviewDemo()
    {
        InitializeComponent();
        _saved = (FullName.Text ?? string.Empty, Email.Text ?? string.Empty, Bio.Text ?? string.Empty);

        // Editing the profile marks its section dirty; the page then offers Save and Discard.
        FullName.TextChanged += (_, _) => UpdateDirty();
        Email.TextChanged += (_, _) => UpdateDirty();
        Bio.TextChanged += (_, _) => UpdateDirty();
        Page.SaveRequested += (_, _) =>
        {
            _saved = (FullName.Text ?? string.Empty, Email.Text ?? string.Empty, Bio.Text ?? string.Empty);
            UpdateDirty();
            Toast.Show(this, "Profile saved", ToastKind.Success);
        };
        Page.DiscardRequested += (_, _) =>
        {
            FullName.Text = _saved.Name;
            Email.Text = _saved.Email;
            Bio.Text = _saved.Bio;
        };

        // Yearly billing is $12 a seat a month instead of $15.
        Period.ValueChanged += (_, e) =>
        {
            var yearly = e.NewValue == "yearly";
            Price.Amount = yearly ? 72 : 90;
            Price.Period = yearly ? "per month, billed yearly" : "per month";
            PeriodRow.Description = yearly ? "You save $216 a year." : "Save $216 a year with yearly billing.";
        };
    }

    private void UpdateDirty()
        => Profile.IsDirty = FullName.Text != _saved.Name || Email.Text != _saved.Email || Bio.Text != _saved.Bio;
}
