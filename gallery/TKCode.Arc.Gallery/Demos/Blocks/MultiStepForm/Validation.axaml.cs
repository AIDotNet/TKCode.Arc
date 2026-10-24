using System.Text.RegularExpressions;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MultiStepFormValidationDemo : UserControl
{
    public MultiStepFormValidationDemo()
    {
        InitializeComponent();
        // The account step checks its own fields: an error message on the field, and false keeps the form on the step.
        Form.Steps.First(s => s.Id == "account").ValidateAsync = _ =>
        {
            var email = Email.Text?.Trim() ?? string.Empty;
            var name = FullName.Text?.Trim() ?? string.Empty;
            Email.ErrorMessage = email.Length == 0 ? "Enter your work email."
                : Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$") ? null
                : "That does not look like an email address.";
            FullName.ErrorMessage = name.Length == 0 ? "Tell us what to call you." : null;
            if (Email.ErrorMessage is not null)
            {
                Email.Focus();
            }
            else if (FullName.ErrorMessage is not null)
            {
                FullName.Focus();
            }
            return Task.FromResult(Email.ErrorMessage is null && FullName.ErrorMessage is null);
        };

        // A synchronous check through the event: the plan step needs a choice.
        Form.StepValidating += (_, e) =>
        {
            if (e.Step.Id == "plan" && Plan.SelectedValue is null)
            {
                e.Cancel = true;
                Log.Text = "Pick a plan to finish.";
            }
        };
        Form.StepChanged += (_, e) => Log.Text = $"Step {e.OldIndex + 1} → {e.NewIndex + 1}";
        Form.Completed += (_, _) => Log.Text = $"Invite for {Email.Text} on the {Plan.SelectedValue} plan.";
    }
}
