using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Developer;

public class EnvVariablesTests
{
    private const string Dev = "development";
    private const string Stg = "staging";
    private const string Prod = "production";

    private static EnvVariables Block(bool withSample = true)
    {
        var block = new EnvVariables { ProjectName = "lumen-console", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        block.Environments.Add(new EnvEnvironment(Dev, "Development"));
        block.Environments.Add(new EnvEnvironment(Stg, "Staging"));
        block.Environments.Add(new EnvEnvironment(Prod, "Production"));
        if (withSample)
        {
            block.Variables.AddRange(
            [
                new EnvVariable("CACHE_TTL", "30", Dev, Stg),
                new EnvVariable("CACHE_TTL", "300", Prod),
                new EnvVariable("API_ORIGIN", "https://staging.lumen.example", Stg),
                new EnvVariable("API_ORIGIN", "https://lumen.example", Prod),
                new EnvVariable("DB_DSN", null, Prod) { IsSensitive = true, Fingerprint = "5e0c2a" },
                new EnvVariable("ONLY_DEV", "x", Dev),
            ]);
            block.SampleDotenv = "NEW_KEY=1\nCACHE_TTL=45\n9BAD=2\nCACHE_TTL=60\n";
        }
        return block;
    }

    private static Window Show(Control control, double width = 1200, double height = 1400, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = height;
        Pump(window);
        return window;
    }

    private static void Pump(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static List<string> RowKeys(EnvVariables block) =>
        block.VariablesPane!.Rows.Select(r => $"{r.Variable.Key}:{string.Join('+', r.Variable.Environments)}").ToList();

    private static EnvVariable Row(EnvVariables block, string key, string env) =>
        block.Variables.Single(v => v.Key == key && v.Environments.Contains(env));

    [AvaloniaFact]
    public void Shows_rows_sorted_with_summary_and_counts()
    {
        var block = Block();
        var window = Show(block);
        RowKeys(block).ShouldBe(["API_ORIGIN:staging", "API_ORIGIN:production", "CACHE_TTL:development+staging",
            "CACHE_TTL:production", "DB_DSN:production", "ONLY_DEV:development"]);
        TestHelpers.Descendant<TextBlock>(block, "PART_Summary").Text.ShouldBe("4 keys in 3 environments");
        TestHelpers.Descendant<TextBlock>(block, "PART_Project").Text.ShouldBe("lumen-console");
        block.IsCompact.ShouldBeFalse();
        block.RedeployState.ShouldBe(EnvRedeployState.None);
        block.VariablesPane!.Rows.All(r => r.Toggles.All(t => !t.IsPill)).ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Default_environments_are_used_when_none_are_given()
    {
        var block = new EnvVariables();
        var window = Show(block);
        block.EffectiveEnvironments.Select(e => e.Name).ShouldBe(["Development", "Preview", "Production"]);
        TestHelpers.Descendant<TextBlock>(block, "PART_Title").Text.ShouldBe("Environment variables");
        window.Close();
    }

    [AvaloniaFact]
    public void Toggling_a_switch_counts_a_change_and_marks_the_row()
    {
        var block = Block();
        var window = Show(block);
        var toggled = new List<EnvScopeEventArgs>();
        block.ScopeToggled += (_, e) => toggled.Add(e);
        var row = block.VariablesPane!.RowFor(Row(block, "ONLY_DEV", Dev))!;
        var staging = row.Toggles.Single(t => t.Environment == Stg);
        staging.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Pump(window);
        row.Variable.Environments.ShouldBe([Dev, Stg]);
        staging.IsChecked.ShouldBe(true);
        toggled.Single().IsOn.ShouldBeTrue();
        block.PendingChangeCount.ShouldBe(1);
        block.PendingEnvironments.ShouldBe([Stg]);
        block.RedeployState.ShouldBe(EnvRedeployState.Pending);
        row.Variable.IsPendingDeploy.ShouldBeTrue();
        block.Announcements.Last().ShouldBe("ONLY_DEV now on in Staging");
        block.Banner!.TitleText.ShouldBe("Deploy to apply 1 change");
        block.Banner.DetailText.ShouldBe("Staging still serves the previous values until you deploy.");
        // Back off again: a second change, not a cancellation.
        block.RequestToggle(row.Variable, Stg).ShouldBeTrue();
        block.PendingChangeCount.ShouldBe(2);
        block.Banner.TitleText.ShouldBe("Deploy to apply 2 changes");
        window.Close();
    }

    [AvaloniaFact]
    public void A_switch_that_would_cover_an_environment_twice_is_refused()
    {
        var block = Block();
        var window = Show(block);
        var conflicts = new List<EnvScopeEventArgs>();
        block.ScopeConflict += (_, e) => conflicts.Add(e);
        var prodRow = Row(block, "CACHE_TTL", Prod);
        block.RequestToggle(prodRow, Stg).ShouldBeFalse();
        Pump(window);
        prodRow.Environments.ShouldBe([Prod]);
        conflicts.Single().BlockingVariable.ShouldBe(Row(block, "CACHE_TTL", Dev));
        var row = block.VariablesPane!.RowFor(prodRow)!;
        row.IsConflictShown.ShouldBeTrue();
        AutomationProperties.GetName(row.ConflictNote!).ShouldBe("CACHE_TTL is already set for Staging on another row. Turn it off there first.");
        block.PendingChangeCount.ShouldBe(0);
        row.Toggles.Single(t => t.Environment == Stg).IsChecked.ShouldBe(false);
        row.DismissConflict();
        Pump(window);
        row.IsConflictShown.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Values_enter_the_tree_only_while_revealed_and_sensitive_ones_never()
    {
        var block = Block();
        var window = Show(block);
        var revealed = new List<(string, bool)>();
        block.ValueRevealed += (_, e) => revealed.Add((e.Variable.Key, e.IsRevealed));
        string[] Texts() => block.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToArray();
        Texts().ShouldNotContain("https://lumen.example");
        var row = block.VariablesPane!.RowFor(Row(block, "API_ORIGIN", Prod))!;
        row.ValueField.IsRevealed = true;
        Pump(window);
        Texts().ShouldContain("https://lumen.example");
        revealed.ShouldBe([("API_ORIGIN", true)]);
        var reveal = TestHelpers.Descendant<DeveloperIconToggle>(row.ValueField, "PART_Reveal");
        reveal.IsChecked = false;
        Pump(window);
        Texts().ShouldNotContain("https://lumen.example");
        revealed.Last().ShouldBe(("API_ORIGIN", false));
        var secret = block.VariablesPane.RowFor(Row(block, "DB_DSN", Prod))!;
        secret.ValueField.IsSensitive.ShouldBeTrue();
        secret.ValueField.IsRevealed = true;
        secret.ValueField.IsRevealed.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Editing_saves_with_enter_and_returns_focus_to_the_edit_button()
    {
        var block = Block();
        var window = Show(block);
        var saved = new List<EnvVariableEventArgs>();
        block.VariableSaved += (_, e) => saved.Add(e);
        var variable = Row(block, "CACHE_TTL", Prod);
        var row = block.VariablesPane!.RowFor(variable)!;
        row.EditButton.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump(window);
        row.IsEditing.ShouldBeTrue();
        var editor = row.Editor!;
        editor.ValueInput.Label.ShouldBe("Value in Production");
        editor.ValueInput.Text.ShouldBe("300");
        editor.SaveButton.IsEnabled.ShouldBeFalse();
        editor.ValueInput.IsKeyboardFocusWithin.ShouldBeTrue();
        editor.ValueInput.Text = "600";
        editor.SaveButton.IsEnabled.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump(window);
        variable.Value.ShouldBe("600");
        saved.Single().ValueChanged.ShouldBeTrue();
        row.IsEditing.ShouldBeFalse();
        row.EditButton.IsFocused.ShouldBeTrue();
        block.Announcements.Last().ShouldBe("CACHE_TTL updated");
        block.PendingChangeCount.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Empty_value_shows_an_error_and_escape_cancels()
    {
        var block = Block();
        var window = Show(block);
        var variable = Row(block, "CACHE_TTL", Prod);
        block.BeginEdit(variable);
        Pump(window);
        var row = block.VariablesPane!.RowFor(variable)!;
        var editor = row.Editor!;
        editor.ValueInput.Text = string.Empty;
        editor.Submit();
        editor.ValueInput.IsInvalid.ShouldBeTrue();
        editor.ValueInput.ErrorMessage.ShouldBe("Values can't be blank; delete the variable instead.");
        variable.Value.ShouldBe("300");
        editor.ValueInput.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump(window);
        row.IsEditing.ShouldBeFalse();
        row.EditButton.IsFocused.ShouldBeTrue();
        block.PendingChangeCount.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Only_one_editor_is_open_and_the_add_panel_closes_it()
    {
        var block = Block();
        var window = Show(block);
        block.BeginEdit(Row(block, "CACHE_TTL", Prod));
        block.BeginEdit(Row(block, "ONLY_DEV", Dev));
        Pump(window);
        block.VariablesPane!.Rows.Count(r => r.IsEditing).ShouldBe(1);
        block.EditingVariable!.Key.ShouldBe("ONLY_DEV");
        block.OpenAddPanel();
        Pump(window);
        block.EditingVariable.ShouldBeNull();
        block.VariablesPane.Rows.Any(r => r.IsEditing).ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Sensitive_rows_edit_empty_and_cannot_be_made_readable()
    {
        var block = Block();
        var window = Show(block);
        var variable = Row(block, "DB_DSN", Prod);
        block.BeginEdit(variable);
        Pump(window);
        var editor = block.VariablesPane!.RowFor(variable)!.Editor!;
        editor.ValueInput.Text.ShouldBe(string.Empty);
        editor.ValueInput.Placeholder.ShouldBe("A new value replaces the stored one");
        editor.SensitiveBox.IsChecked.ShouldBe(true);
        editor.SensitiveBox.IsEnabled.ShouldBeFalse();
        editor.ValueInput.Text = "new-secret";
        editor.Submit();
        variable.Value.ShouldBe("new-secret");
        variable.IsSensitive.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Saving_as_sensitive_drops_the_shown_value()
    {
        var block = Block();
        var window = Show(block);
        var variable = Row(block, "ONLY_DEV", Dev);
        var row = block.VariablesPane!.RowFor(variable)!;
        row.ValueField.IsRevealed = true;
        block.BeginEdit(variable);
        Pump(window);
        var saved = new List<EnvVariableEventArgs>();
        block.VariableSaved += (_, e) => saved.Add(e);
        row.Editor!.SensitiveBox.IsChecked = true;
        row.Editor.SaveButton.IsEnabled.ShouldBeTrue();
        row.Editor.Submit();
        Pump(window);
        saved.Single().BecameSensitive.ShouldBeTrue();
        row.ValueField.IsSensitive.ShouldBeTrue();
        row.ValueField.IsRevealed.ShouldBeFalse();
        block.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "x").ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Remove_takes_two_steps_and_moves_focus_to_the_next_row()
    {
        var block = Block();
        var window = Show(block);
        var removed = new List<EnvVariableEventArgs>();
        block.VariableRemoved += (_, e) => removed.Add(e);
        var variable = Row(block, "CACHE_TTL", Prod);
        block.BeginEdit(variable);
        Pump(window);
        var editor = block.VariablesPane!.RowFor(variable)!.Editor!;
        editor.RemoveButton.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump(window);
        editor.IsConfirmingRemove.ShouldBeTrue();
        editor.RemoveButton.Content.ShouldBe("Really delete");
        editor.RemoveButton.Variant.ShouldBe(ButtonVariant.Danger);
        // Losing focus resets the confirmation.
        editor.ValueInput.Focus(NavigationMethod.Tab);
        Pump(window);
        editor.IsConfirmingRemove.ShouldBeFalse();
        editor.RemoveButton.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump(window);
        block.Variables.ShouldNotContain(variable);
        removed.Single().Variable.ShouldBe(variable);
        var next = block.VariablesPane.RowFor(Row(block, "DB_DSN", Prod))!;
        next.EditButton.IsFocused.ShouldBeTrue();
        block.Announcements.Last().ShouldBe("CACHE_TTL deleted from Production");
        block.PendingEnvironments.ShouldBe([Prod]);
        window.Close();
    }

    [AvaloniaFact]
    public void Add_validates_in_order_and_inserts_sorted()
    {
        var block = Block();
        var window = Show(block);
        block.OpenAddPanel();
        Pump(window);
        var panel = block.AddPanel!;
        panel.KeyInput.IsKeyboardFocusWithin.ShouldBeTrue();
        panel.SinglePills.Where(p => p.IsChecked == true).Select(p => p.Environment).ShouldBe([Dev, Stg]);
        panel.SubmitSingle();
        panel.KeyInput.ErrorMessage.ShouldBe("Key is required");
        panel.ValueInput.ErrorMessage.ShouldBe("Value is required");
        panel.KeyInput.Text = "2FA";
        panel.KeyInput.ErrorMessage.ShouldBe("A key can't begin with a digit");
        panel.KeyInput.Text = "HAS-DASH";
        panel.KeyInput.ErrorMessage.ShouldBe("Only letters, digits and underscores");
        panel.KeyInput.Text = "CACHE_TTL";
        panel.KeyInput.ErrorMessage.ShouldBe("Already defined for Development and Staging; edit that row or deselect them.");
        foreach (var pill in panel.SinglePills)
        {
            pill.IsChecked = false;
        }
        panel.EnvironmentError.IsVisible.ShouldBeTrue();
        panel.SinglePills.Single(p => p.Environment == Prod).IsChecked = true;
        panel.KeyInput.ErrorMessage.ShouldBe("Already defined for Production; edit that row or deselect them.");
        panel.KeyInput.Text = "BETA_FLAG";
        panel.ValueInput.Text = "on";
        panel.KeyInput.IsInvalid.ShouldBeFalse();
        var added = new List<EnvVariableEventArgs>();
        block.VariableAdded += (_, e) => added.Add(e);
        panel.SubmitSingle();
        Pump(window);
        added.Single().Variable.Key.ShouldBe("BETA_FLAG");
        RowKeys(block).IndexOf("BETA_FLAG:production").ShouldBe(2);
        block.IsAddPanelOpen.ShouldBeFalse();
        block.AddButton!.IsFocused.ShouldBeTrue();
        block.Announcements.Last().ShouldBe("BETA_FLAG created in Production");
        block.PendingChangeCount.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Import_previews_lines_and_replaces_existing_environments()
    {
        var block = Block();
        var window = Show(block);
        var imported = new List<EnvVariablesImportedEventArgs>();
        block.VariablesImported += (_, e) => imported.Add(e);
        block.OpenAddPanel(EnvAddMode.Dotenv);
        Pump(window);
        var panel = block.AddPanel!;
        panel.Mode.ShouldBe(EnvAddMode.Dotenv);
        panel.DotenvText.Text.ShouldBe(block.SampleDotenv);
        panel.Parsed.ImportCount.ShouldBe(2);
        panel.PreviewLineCount.ShouldBe(4);
        panel.ImportButton.Content.ShouldBe("Add 2 variables");
        foreach (var pill in panel.ImportPills)
        {
            pill.IsChecked = false;
        }
        panel.ImportButton.IsEnabled.ShouldBeFalse();
        panel.ImportPills.Single(p => p.Environment == Dev).IsChecked = true;
        panel.ImportPills.Single(p => p.Environment == Stg).IsChecked = true;
        panel.ImportButton.IsEnabled.ShouldBeTrue();
        var oldCache = Row(block, "CACHE_TTL", Dev);
        panel.SubmitImport();
        Pump(window);
        block.Variables.ShouldNotContain(oldCache);
        Row(block, "CACHE_TTL", Dev).Value.ShouldBe("60");
        Row(block, "NEW_KEY", Stg).Value.ShouldBe("1");
        imported.Single().Added.Count.ShouldBe(2);
        imported.Single().Replaced.ShouldBe([oldCache]);
        imported.Single().SkippedLines.ShouldBe([2, 3]);
        block.PendingChangeCount.ShouldBe(1);
        block.Announcements.Last().ShouldBe("Imported 2 variables into Development and Staging");
        block.ImportButton!.IsFocused.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Multi_line_paste_switches_to_the_dotenv_form()
    {
        var block = Block();
        var window = Show(block);
        block.OpenAddPanel();
        Pump(window);
        block.AddPanel!.PasteDotenv("A=1\nB=2");
        Pump(window);
        block.AddPanel.Mode.ShouldBe(EnvAddMode.Dotenv);
        block.AddPanel.Parsed.ImportCount.ShouldBe(2);
        block.AddPanel.DotenvText.IsKeyboardFocusWithin.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Escape_closes_the_add_panel_and_focus_returns_to_its_button()
    {
        var block = Block();
        var window = Show(block);
        block.ImportButton!.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump(window);
        block.IsAddPanelOpen.ShouldBeTrue();
        block.AddPanel!.DotenvText.IsKeyboardFocusWithin.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump(window);
        block.IsAddPanelOpen.ShouldBeFalse();
        block.ImportButton.IsFocused.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Search_filters_keys_and_clear_search_keeps_focus()
    {
        var block = Block();
        var window = Show(block);
        block.SearchText = "cache";
        Pump(window);
        RowKeys(block).ShouldBe(["CACHE_TTL:development+staging", "CACHE_TTL:production"]);
        block.SearchText = "secret";
        Pump(window);
        block.VariablesPane!.Empty.IsVisible.ShouldBeTrue();
        block.VariablesPane.Empty.ActionButton.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump(window);
        block.SearchText.ShouldBe(string.Empty);
        block.Search!.IsKeyboardFocusWithin.ShouldBeTrue();
        block.VariablesPane.Rows.Count().ShouldBe(6);
        window.Close();
    }

    [AvaloniaFact]
    public void Compare_lists_differences_and_copies_a_missing_value()
    {
        var block = Block();
        var window = Show(block);
        block.ViewMode = EnvVariablesView.Compare;
        Pump(window);
        var pane = block.ComparePane!;
        block.CompareSides.ShouldBe((Stg, Prod));
        pane.Rows.Select(r => (r.Entry.Key, r.Entry.Row.Result)).ShouldBe([
            ("API_ORIGIN", EnvCompareResult.Different),
            ("CACHE_TTL", EnvCompareResult.Different),
            ("DB_DSN", EnvCompareResult.MissingLeft),
        ]);
        pane.CountsText.ShouldBe("1 missing · 2 different · 0 identical");
        var copied = new List<EnvValueCopiedEventArgs>();
        block.ValueCopied += (_, e) => copied.Add(e);
        var dbRow = pane.Rows.Single(r => r.Entry.Key == "DB_DSN");
        dbRow.CopyButton!.Content.ShouldBe("Copy to Staging");
        dbRow.CopyButton.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump(window);
        copied.Single().To.ShouldBe(Stg);
        Row(block, "DB_DSN", Stg).IsSensitive.ShouldBeTrue();
        pane.CountsText.ShouldBe("0 missing · 2 different · 1 identical");
        pane.Rows.Any(r => r.Entry.Key == "DB_DSN").ShouldBeFalse();
        pane.ShowMatchingSwitch.IsFocused.ShouldBeTrue();
        block.PendingChangeCount.ShouldBe(1);
        block.ShowMatching = true;
        Pump(window);
        pane.Rows.Single(r => r.Entry.Key == "DB_DSN").Entry.Row.Result.ShouldBe(EnvCompareResult.Same);
        window.Close();
    }

    [AvaloniaFact]
    public void Compare_swaps_sides_and_shows_the_in_sync_state()
    {
        var block = Block();
        var window = Show(block);
        block.ViewMode = EnvVariablesView.Compare;
        Pump(window);
        block.ComparePane!.Swap();
        Pump(window);
        block.CompareSides.ShouldBe((Prod, Stg));
        block.ComparePane.Rows.Single(r => r.Entry.Key == "DB_DSN").Entry.Row.Result.ShouldBe(EnvCompareResult.MissingRight);
        block.SearchText = "ONLY";
        Pump(window);
        block.ComparePane.Empty.IsVisible.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Redeploy_clears_the_pending_marks()
    {
        var block = Block();
        var window = Show(block);
        var gate = new TaskCompletionSource();
        EnvRedeployRequest? request = null;
        block.RedeployAction = (r, _) =>
        {
            request = r;
            return gate.Task;
        };
        var variable = Row(block, "ONLY_DEV", Dev);
        block.RequestToggle(variable, Prod);
        var run = block.RedeployAsync();
        block.RedeployState.ShouldBe(EnvRedeployState.Running);
        block.Banner!.ActionButton!.IsLoading.ShouldBeTrue();
        request!.Environments.ShouldBe([Prod]);
        request.Changed.ShouldBe([variable]);
        // A change made while it runs waits for the next deployment.
        block.RequestToggle(Row(block, "API_ORIGIN", Stg), Dev);
        gate.SetResult();
        TestHelpers.PumpUntil(() => run.IsCompleted).ShouldBeTrue();
        run.Result.ShouldBeTrue();
        Pump(window);
        block.RedeployState.ShouldBe(EnvRedeployState.Done);
        block.Banner.TitleText.ShouldBe("Deployed Production");
        variable.IsPendingDeploy.ShouldBeFalse();
        Row(block, "API_ORIGIN", Stg).IsPendingDeploy.ShouldBeTrue();
        block.PendingChangeCount.ShouldBe(1);
        block.PendingEnvironments.ShouldBe([Dev]);
        block.EndDone();
        block.RedeployState.ShouldBe(EnvRedeployState.Pending);
        window.Close();
    }

    [AvaloniaFact]
    public void Failed_redeploy_keeps_the_changes_and_offers_retry()
    {
        var block = Block();
        var window = Show(block);
        var attempts = 0;
        block.RedeployAction = (_, _) =>
        {
            attempts++;
            return attempts == 1 ? Task.FromException(new InvalidOperationException("The build queue is full.")) : Task.CompletedTask;
        };
        var failures = new List<EnvRedeployEventArgs>();
        block.RedeployFailed += (_, e) => failures.Add(e);
        var variable = Row(block, "ONLY_DEV", Dev);
        block.RequestToggle(variable, Prod);
        var first = block.RedeployAsync();
        TestHelpers.PumpUntil(() => first.IsCompleted).ShouldBeTrue();
        first.Result.ShouldBeFalse();
        Pump(window);
        block.RedeployState.ShouldBe(EnvRedeployState.Failed);
        failures.Single().Exception!.Message.ShouldBe("The build queue is full.");
        block.PendingChangeCount.ShouldBe(1);
        variable.IsPendingDeploy.ShouldBeTrue();
        block.Banner!.TitleText.ShouldBe("The deployment failed");
        block.Banner.DetailText.ShouldBe("The build queue is full.");
        AutomationProperties.GetName(block.Banner.ActionButton!).ShouldBe("Retry");
        block.Banner.ActionButton!.IsFocused.ShouldBeTrue();
        block.Announcements.Last().ShouldBe("Deployment failed. The build queue is full.");
        var second = block.RedeployAsync();
        TestHelpers.PumpUntil(() => second.IsCompleted).ShouldBeTrue();
        second.Result.ShouldBeTrue();
        block.RedeployState.ShouldBe(EnvRedeployState.Done);
        variable.IsPendingDeploy.ShouldBeFalse();
        block.EndDone();
        block.RedeployState.ShouldBe(EnvRedeployState.None);
        window.Close();
    }

    [AvaloniaFact]
    public void Redeploy_can_be_cancelled_by_the_requested_event()
    {
        var block = Block();
        var window = Show(block);
        block.RedeployRequested += (_, e) => e.Cancel = true;
        block.RequestToggle(Row(block, "ONLY_DEV", Dev), Prod);
        block.RedeployAsync().Result.ShouldBeFalse();
        block.RedeployState.ShouldBe(EnvRedeployState.Pending);
        window.Close();
    }

    [AvaloniaFact]
    public void Narrow_containers_stack_rows_with_labelled_pills()
    {
        var block = Block();
        var window = Show(block, width: 700);
        block.IsCompact.ShouldBeTrue();
        block.IsNarrow.ShouldBeFalse();
        var row = block.VariablesPane!.Rows.First();
        row.Toggles.All(t => t.IsPill).ShouldBeTrue();
        window.Width = 400;
        Pump(window);
        block.IsNarrow.ShouldBeTrue();
        TestHelpers.Descendant<EnvSplitPanel>(block, "PART_Toolbar").IsStacked.ShouldBeTrue();
        window.Width = 1200;
        Pump(window);
        block.IsCompact.ShouldBeFalse();
        block.VariablesPane.Rows.First().Toggles.Any(t => t.IsPill).ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Pill_columns_follow_css_auto_fit()
    {
        EnvRowPanel.PillColumns(652, 3).ShouldBe(3);
        EnvRowPanel.PillColumns(358, 3).ShouldBe(2);
        EnvRowPanel.PillColumns(100, 3).ShouldBe(1);
        EnvRowPanel.PillColumns(2000, 2).ShouldBe(2);
        var (key, value) = EnvRowPanel.FlexColumns(1312 - 48, 3, 92, 52);
        key.ShouldBe(383.2, 0.5);
        value.ShouldBe(492.75, 0.5);
    }

    [AvaloniaFact]
    public void Read_only_hides_editing()
    {
        var block = Block();
        block.IsReadOnly = true;
        var window = Show(block);
        block.AddButton!.IsVisible.ShouldBeFalse();
        var row = block.VariablesPane!.Rows.First();
        row.EditButton.IsVisible.ShouldBeFalse();
        row.Toggles.All(t => !t.Focusable).ShouldBeTrue();
        block.RequestToggle(row.Variable, Prod).ShouldBeFalse();
        block.OpenAddPanel();
        block.IsAddPanelOpen.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Accessible_names_follow_the_labels()
    {
        var block = Block();
        var window = Show(block);
        var row = block.VariablesPane!.RowFor(Row(block, "CACHE_TTL", Prod))!;
        AutomationProperties.GetName(row.Toggles[0]).ShouldBe("Development for CACHE_TTL");
        AutomationProperties.GetName(row.EditButton).ShouldBe("Edit CACHE_TTL");
        AutomationProperties.GetName(TestHelpers.Descendant<DeveloperIconToggle>(row.ValueField, "PART_Reveal")).ShouldBe("Reveal CACHE_TTL");
        block.Labels = new EnvVariablesLabels { ScopeName = (env, key) => $"{key} en {env}", Title = "Variables" };
        Pump(window);
        TestHelpers.Descendant<TextBlock>(block, "PART_Title").Text.ShouldBe("Variables");
        row = block.VariablesPane!.RowFor(Row(block, "CACHE_TTL", Prod))!;
        AutomationProperties.GetName(row.Toggles[0]).ShouldBe("CACHE_TTL en Development");
        window.Close();
    }

    [AvaloniaFact]
    public void Host_changes_to_the_collection_are_followed()
    {
        var block = Block();
        var window = Show(block);
        block.Variables.Add(new EnvVariable("AAA_FIRST", "1", Dev));
        TestHelpers.PumpUntil(() => block.VariablesPane!.Rows.Count() == 7).ShouldBeTrue();
        RowKeys(block)[0].ShouldBe("AAA_FIRST:development");
        Row(block, "AAA_FIRST", Dev).Environments.Add(Stg);
        TestHelpers.PumpUntil(() => block.VariablesPane!.Rows.First().Toggles[1].IsChecked == true).ShouldBeTrue();
        block.PendingChangeCount.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Switch_draws_its_box_and_springs_under_full_motion()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var block = Block();
        var window = Show(block, level: ArcMotionLevel.Full);
        var row = block.VariablesPane!.RowFor(Row(block, "ONLY_DEV", Dev))!;
        var glyph = row.Toggles[1].GetVisualDescendants().OfType<EnvCheckGlyph>().Single();
        glyph.FillProgress.ShouldBe(0);
        block.RequestToggle(row.Variable, Stg);
        clock.Advance(TimeSpan.FromMilliseconds(40));
        glyph.FillProgress.ShouldBeInRange(0.01, 0.99);
        for (var i = 0; i < 40; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(16));
        }
        glyph.FillProgress.ShouldBe(1, 0.001);
        window.Close();
    }
}
