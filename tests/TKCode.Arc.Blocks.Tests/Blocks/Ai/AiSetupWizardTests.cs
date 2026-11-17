using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary><see cref="AiSetupWizard"/>: validation, the steps, the test (pass, fail, invalidation) and completion.</summary>
public class AiSetupWizardTests
{
    private static (AiSetupWizard Wizard, Window Window) Show()
    {
        var wizard = new AiSetupWizard { Delay = static (_, _) => Task.CompletedTask };
        var window = TestHelpers.Show(wizard);
        Dispatcher.UIThread.RunJobs();
        return (wizard, window);
    }

    private static void ChooseProvider(AiSetupWizard wizard, string id, string key)
    {
        wizard.ProviderCards!.SelectedValue = id;
        Dispatcher.UIThread.RunJobs();
        if (wizard.KeyField is { IsVisible: true } field)
        {
            field.Text = key;
        }
    }

    [AvaloniaFact]
    public void Continue_needs_a_provider_and_a_key()
    {
        var (wizard, window) = Show();
        try
        {
            wizard.Next().ShouldBeFalse();
            wizard.ProviderError!.IsVisible.ShouldBeTrue();
            wizard.ProviderCards!.SelectedValue = "northwind";
            Dispatcher.UIThread.RunJobs();
            wizard.Configuration.Model.ShouldBe("Northwind Large 3");
            wizard.Next().ShouldBeFalse();
            wizard.KeyField!.ErrorMessage.ShouldBe("Enter the key to continue.");
            wizard.KeyField.Text = "nw_123";
            wizard.Next().ShouldBeTrue();
            wizard.CurrentStep.ShouldBe(AiSetupStep.DataAccess);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_provider_without_a_key_skips_the_field_and_steps_cannot_jump_ahead()
    {
        var (wizard, window) = Show();
        try
        {
            ChooseProvider(wizard, "inhouse", "");
            wizard.KeyField!.IsVisible.ShouldBeFalse();
            wizard.GoTo(AiSetupStep.Privacy).ShouldBeFalse();
            wizard.Next().ShouldBeTrue();
            wizard.Configuration.DataSourceIds.ShouldBe(["docs", "tickets"]);
            wizard.Back().ShouldBeTrue();
            wizard.GoTo(AiSetupStep.DataAccess).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_test_must_pass_and_a_change_asks_for_a_new_one()
    {
        var (wizard, window) = Show();
        try
        {
            ChooseProvider(wizard, "cobalt", "fail");
            wizard.Next().ShouldBeTrue();
            wizard.Next().ShouldBeTrue();
            wizard.Next().ShouldBeTrue();
            wizard.CurrentStep.ShouldBe(AiSetupStep.Test);
            wizard.Next().ShouldBeFalse();

            var failed = await wizard.RunTestAsync();
            failed.ShouldNotBeNull().IsSuccess.ShouldBeFalse();
            wizard.CheckRows.Select(r => r.State).ShouldBe([AiSetupWizardCheckState.Passed, AiSetupWizardCheckState.Failed,
                AiSetupWizardCheckState.Pending, AiSetupWizardCheckState.Pending]);
            wizard.Next().ShouldBeFalse();

            wizard.GoTo(AiSetupStep.Provider).ShouldBeTrue();
            wizard.KeyField!.Text = "cb_good";
            wizard.GoTo(AiSetupStep.Test).ShouldBeTrue();
            (await wizard.RunTestAsync()).ShouldNotBeNull().IsSuccess.ShouldBeTrue();
            wizard.CheckRows.ShouldAllBe(r => r.State == AiSetupWizardCheckState.Passed);
            wizard.Next().ShouldBeTrue();

            // Going back and changing a privacy choice needs a new test before the summary.
            wizard.GoTo(AiSetupStep.Privacy).ShouldBeTrue();
            var training = wizard.CurrentPage!.GetVisualDescendantsOfType<Switch>().First();
            training.IsChecked = false;
            wizard.TestResult.ShouldBeNull();
            wizard.FurthestStep.ShouldBe(AiSetupStep.Test);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Finishing_raises_Completed_with_the_configuration()
    {
        var (wizard, window) = Show();
        AiSetupConfiguration? done = null;
        wizard.Completed += (_, e) => done = e.Configuration;
        try
        {
            ChooseProvider(wizard, "northwind", "nw_key");
            wizard.Next();
            wizard.Next();
            wizard.Next();
            await wizard.RunTestAsync();
            wizard.Next().ShouldBeTrue();
            wizard.CurrentStep.ShouldBe(AiSetupStep.Done);
            wizard.Next().ShouldBeTrue();
            wizard.IsCompleted.ShouldBeTrue();
            done.ShouldNotBeNull().ProviderId.ShouldBe("northwind");
            done.ApiKey.ShouldBe("nw_key");
            done.HistoryDays.ShouldBe(30);
            wizard.StepperControl!.CurrentIndex.ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }
}

internal static class AiSetupWizardTestExtensions
{
    public static IEnumerable<T> GetVisualDescendantsOfType<T>(this Avalonia.Visual visual)
        => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(visual).OfType<T>();
}
