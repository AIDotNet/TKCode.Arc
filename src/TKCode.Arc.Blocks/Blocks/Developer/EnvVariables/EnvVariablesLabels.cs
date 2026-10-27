namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="EnvVariables"/> shows or announces, in English by default. Replace the instance to translate or
/// reword; texts with parts are functions so that word order can change. Environment lists arrive already joined
/// ("Development and Staging").
/// </summary>
public sealed class EnvVariablesLabels
{
    // ---- Header and toolbar ----

    /// <summary>The heading, when <see cref="EnvVariables.Title"/> is null.</summary>
    public string Title { get; init; } = "Environment variables";

    /// <summary>The summary after the project name; the arguments are the number of keys and of environments.</summary>
    public Func<int, int, string> KeysSummary { get; init; } = (keys, envs) =>
        $"{keys} {(keys == 1 ? "key" : "keys")} in {envs} {(envs == 1 ? "environment" : "environments")}";

    /// <summary>The button that opens the add panel on the .env form.</summary>
    public string ImportButton { get; init; } = "Import .env";

    /// <summary>The button that opens the add panel on the single variable form.</summary>
    public string AddButton { get; init; } = "Add variable";

    /// <summary>The search box's placeholder and accessible name.</summary>
    public string SearchPlaceholder { get; init; } = "Search keys";

    /// <summary>The accessible name of the view switch.</summary>
    public string ViewSwitchName { get; init; } = "View";

    /// <summary>The variables view's name in the switch.</summary>
    public string VariablesView { get; init; } = "Variables";

    /// <summary>The compare view's name in the switch.</summary>
    public string CompareView { get; init; } = "Compare";

    // ---- Matrix ----

    /// <summary>The key column's heading.</summary>
    public string KeyColumn { get; init; } = "Key";

    /// <summary>The value column's heading.</summary>
    public string ValueColumn { get; init; } = "Value";

    /// <summary>The count under an environment's heading; the argument is how many rows cover it.</summary>
    public Func<int, string> SetCount { get; init; } = n => n == 1 ? "1 row" : $"{n} rows";

    /// <summary>The accessible name of the list of rows.</summary>
    public string VariablesListName { get; init; } = "Variables";

    /// <summary>The second line of a row; the arguments are the date (may be null) and the person (may be null).</summary>
    public Func<DateTimeOffset?, string?, string> Updated { get; init; } = (at, by) =>
        at is { } date
            ? by is { Length: > 0 } ? $"Updated {date:MMM d} by {by}" : $"Updated {date:MMM d}"
            : by is { Length: > 0 } ? $"Updated by {by}" : string.Empty;

    /// <summary>The second line of a row that changed since the last deployment.</summary>
    public string PendingRow { get; init; } = "Edited, not deployed yet";

    /// <summary>The accessible name of an environment switch; the arguments are the environment and the key.</summary>
    public Func<string, string, string> ScopeName { get; init; } = (env, key) => $"{env} for {key}";

    /// <summary>The accessible name of a row's group of switches; the argument is the key.</summary>
    public Func<string, string> ScopeGroupName { get; init; } = key => $"Environments for {key}";

    /// <summary>The accessible name of a row's edit button; the argument is the key.</summary>
    public Func<string, string> EditName { get; init; } = key => $"Edit {key}";

    /// <summary>The note under a row whose switch would cover an environment twice; the arguments are the key and the environment.</summary>
    public Func<string, string, string> ScopeConflict { get; init; } = (key, env) =>
        $"{key} is already set for {env} on another row. Turn it off there first.";

    /// <summary>The reveal button's accessible name; {0} is the key.</summary>
    public string RevealNameFormat { get; init; } = "Reveal {0}";

    /// <summary>The copy button's accessible name; {0} is the key.</summary>
    public string CopyNameFormat { get; init; } = "Copy {0}";

    /// <summary>What screen readers hear for a hidden value.</summary>
    public string HiddenValue { get; init; } = "Hidden value";

    /// <summary>The visible word on a write-only value.</summary>
    public string Sensitive { get; init; } = "Sensitive";

    /// <summary>What screen readers hear for a write-only value.</summary>
    public string SensitiveValue { get; init; } = "Write-only value";

    // ---- Inline editor ----

    /// <summary>The editor's label; the argument is the row's environments, joined.</summary>
    public Func<string, string> EditorLabel { get; init; } = envs => envs.Length > 0 ? $"Value in {envs}" : "Value";

    /// <summary>The editor's placeholder on a write-only row (its value is never filled in).</summary>
    public string ReplacePlaceholder { get; init; } = "A new value replaces the stored one";

    /// <summary>The sensitive checkbox's text.</summary>
    public string SensitiveOption { get; init; } = "Sensitive";

    /// <summary>The sensitive checkbox's explanation in the editor.</summary>
    public string SensitiveEditorHint { get; init; } = "Once saved it is write-only: you can replace it, but nobody can reveal it again.";

    /// <summary>The sensitive checkbox's explanation on a row that is already write-only.</summary>
    public string SensitiveLockedHint { get; init; } = "This value is write-only. To make it readable, remove it and add it again.";

    /// <summary>The first step of removing a row.</summary>
    public string RemoveButton { get; init; } = "Delete variable";

    /// <summary>The second step of removing a row (it resets when the button loses focus).</summary>
    public string ConfirmRemoveButton { get; init; } = "Really delete";

    /// <summary>Cancel buttons.</summary>
    public string CancelButton { get; init; } = "Cancel";

    /// <summary>The editor's save button.</summary>
    public string SaveValueButton { get; init; } = "Save";

    /// <summary>The error of an empty value in the editor.</summary>
    public string EmptyValueError { get; init; } = "Values can't be blank; delete the variable instead.";

    // ---- Add panel ----

    /// <summary>The add panel's accessible name on the single variable form.</summary>
    public string AddPanelName { get; init; } = "Add a variable";

    /// <summary>The add panel's accessible name on the .env form.</summary>
    public string ImportPanelName { get; init; } = "Import a .env file";

    /// <summary>The accessible name of the form switch.</summary>
    public string AddModeName { get; init; } = "How to add";

    /// <summary>The single variable form in the switch.</summary>
    public string SingleMode { get; init; } = "Single";

    /// <summary>The .env form in the switch.</summary>
    public string DotenvMode { get; init; } = "From .env";

    /// <summary>The key field's label.</summary>
    public string KeyLabel { get; init; } = "Key";

    /// <summary>The key field's placeholder.</summary>
    public string KeyPlaceholder { get; init; } = "e.g. API_BASE_URL";

    /// <summary>The key field's hint.</summary>
    public string KeyHint { get; init; } = "Tip: paste a whole .env file here.";

    /// <summary>The value field's label.</summary>
    public string ValueLabel { get; init; } = "Value";

    /// <summary>The legend of the environment pills on the single variable form.</summary>
    public string EnvironmentsLegend { get; init; } = "Environments";

    /// <summary>The sensitive checkbox's explanation on the add form.</summary>
    public string SensitiveAddHint { get; init; } = "Can't be revealed later";

    /// <summary>The add form's save button.</summary>
    public string SaveVariableButton { get; init; } = "Save";

    /// <summary>The error of an empty key.</summary>
    public string KeyEmptyError { get; init; } = "Key is required";

    /// <summary>The error of a key that starts with a digit.</summary>
    public string KeyDigitError { get; init; } = "A key can't begin with a digit";

    /// <summary>The error of a key with other characters.</summary>
    public string KeyCharactersError { get; init; } = "Only letters, digits and underscores";

    /// <summary>The error of a key already set in chosen environments; the argument is those environments, joined.</summary>
    public Func<string, string> KeyTakenError { get; init; } = envs =>
        $"Already defined for {envs}; edit that row or deselect them.";

    /// <summary>The error of an empty value on the add form.</summary>
    public string ValueEmptyError { get; init; } = "Value is required";

    /// <summary>The error when no environment is chosen.</summary>
    public string EnvironmentsError { get; init; } = "Pick one or more environments";

    /// <summary>The .env text box's label.</summary>
    public string DotenvLabel { get; init; } = ".env text";

    /// <summary>The legend of the environment pills on the .env form.</summary>
    public string ImportIntoLegend { get; init; } = "Import to";

    /// <summary>The hint shown while the .env text is empty.</summary>
    public string DotenvEmptyHint { get; init; } = "Paste KEY=value lines. Comments and blank lines are ignored.";

    /// <summary>The live summary of the parse; the arguments are the lines imported and skipped.</summary>
    public Func<int, int, string> DotenvSummary { get; init; } = (import, skipped) => $"{import} ready, {skipped} skipped";

    /// <summary>The accessible name of the parsed lines.</summary>
    public string DotenvPreviewName { get; init; } = "Parsed lines";

    /// <summary>A new key line.</summary>
    public string DotenvNew { get; init; } = "New";

    /// <summary>A line that replaces existing values; the argument is the environments, joined.</summary>
    public Func<string, string> DotenvReplaces { get; init; } = envs => $"Overwrites {envs}";

    /// <summary>A line set again later; the argument is the later line's number.</summary>
    public Func<int, string> DotenvDuplicate { get; init; } = line => $"Skipped: set again on line {line}";

    /// <summary>A line whose key starts with a digit.</summary>
    public string DotenvDigit { get; init; } = "Skipped: begins with a digit";

    /// <summary>A line whose key has other characters.</summary>
    public string DotenvCharacters { get; init; } = "Skipped: invalid characters";

    /// <summary>A line without an equals sign.</summary>
    public string DotenvMissingEquals { get; init; } = "Skipped: no equals sign";

    /// <summary>The import button; the argument is how many lines are imported.</summary>
    public Func<int, string> ImportCountButton { get; init; } = n => n == 1 ? "Add 1 variable" : $"Add {n} variables";

    // ---- Redeploy banner ----

    /// <summary>The banner's title; the argument is the number of changes.</summary>
    public Func<int, string> BannerTitle { get; init; } = n => n == 1 ? "Deploy to apply 1 change" : $"Deploy to apply {n} changes";

    /// <summary>The banner's line; the arguments are the environments (joined) and how many there are.</summary>
    public Func<string, int, string> BannerDetail { get; init; } = (envs, count) =>
        count == 1 ? $"{envs} still serves the previous values until you deploy." : $"{envs} still serve the previous values until you deploy.";

    /// <summary>The banner's button.</summary>
    public string RedeployButton { get; init; } = "Redeploy";

    /// <summary>The banner's button after a failure.</summary>
    public string RetryButton { get; init; } = "Retry";

    /// <summary>The banner's title after a deployment; the argument is the environments, joined.</summary>
    public Func<string, string> DoneTitle { get; init; } = envs => $"Deployed {envs}";

    /// <summary>The banner's line after a deployment.</summary>
    public string DoneDetail { get; init; } = "The new build is using your changes.";

    /// <summary>The banner's title after a failed deployment.</summary>
    public string FailedTitle { get; init; } = "The deployment failed";

    /// <summary>The banner's line after a failed deployment when the error has no message; your changes are kept.</summary>
    public string FailedDetail { get; init; } = "Your changes are kept. Try again in a moment.";

    // ---- Compare ----

    /// <summary>The left select's label.</summary>
    public string CompareLeftLabel { get; init; } = "Environment";

    /// <summary>The right select's label.</summary>
    public string CompareRightLabel { get; init; } = "Against";

    /// <summary>The swap button's accessible name.</summary>
    public string SwapName { get; init; } = "Swap environments";

    /// <summary>The result column's heading.</summary>
    public string ResultColumn { get; init; } = "Result";

    /// <summary>The count of keys one side lacks.</summary>
    public Func<int, string> MissingCount { get; init; } = n => $"{n} missing";

    /// <summary>The count of keys with different values.</summary>
    public Func<int, string> DifferentCount { get; init; } = n => $"{n} different";

    /// <summary>The count of keys with the same value.</summary>
    public Func<int, string> MatchingCount { get; init; } = n => $"{n} identical";

    /// <summary>The switch that lists matching keys too.</summary>
    public string ShowMatching { get; init; } = "Show identical";

    /// <summary>The compare list's accessible name; the arguments are the two environments.</summary>
    public Func<string, string, string> CompareListName { get; init; } = (a, b) => $"{a} compared with {b}";

    /// <summary>The compare result of different values.</summary>
    public string DifferentResult { get; init; } = "Values differ";

    /// <summary>The compare result of a key one side lacks; the argument is that side.</summary>
    public Func<string, string> MissingResult { get; init; } = env => $"Not in {env}";

    /// <summary>The compare result of equal values.</summary>
    public string SameResult { get; init; } = "Identical";

    /// <summary>A side that has no value.</summary>
    public string NotSet { get; init; } = "Not set";

    /// <summary>What screen readers hear before a fingerprint.</summary>
    public string FingerprintName { get; init; } = "Fingerprint";

    /// <summary>The button that copies a value to the side that lacks it; the argument is that side.</summary>
    public Func<string, string> CopyTo { get; init; } = env => $"Copy to {env}";

    /// <summary>The empty compare view's title; the arguments are the two environments.</summary>
    public Func<string, string, string> InSyncTitle { get; init; } = (a, b) => $"{a} and {b} are in sync";

    /// <summary>The empty compare view's line.</summary>
    public string InSyncDetail { get; init; } = "Every key has the same value on both sides.";

    /// <summary>The empty compare view's button.</summary>
    public string ShowMatchingButton { get; init; } = "Show identical keys";

    // ---- Search ----

    /// <summary>The empty search's title; the argument is the query.</summary>
    public Func<string, string> NoMatchTitle { get; init; } = q => $"Nothing matches “{q}”";

    /// <summary>The empty search's line.</summary>
    public string NoMatchDetail { get; init; } = "Only key names are searched; values stay hidden.";

    /// <summary>The empty search's button.</summary>
    public string ClearSearchButton { get; init; } = "Clear search";

    // ---- Announcements ----

    /// <summary>A switch turned; the arguments are the environment, the key and the new state.</summary>
    public Func<string, string, bool, string> ScopeAnnouncement { get; init; } = (env, key, on) =>
        on ? $"{key} now on in {env}" : $"{key} now off in {env}";

    /// <summary>A value saved; the argument is the key.</summary>
    public Func<string, string> SavedAnnouncement { get; init; } = key => $"{key} updated";

    /// <summary>A variable added; the arguments are the key and the environments, joined.</summary>
    public Func<string, string, string> AddedAnnouncement { get; init; } = (key, envs) => $"{key} created in {envs}";

    /// <summary>A variable removed; the arguments are the key and the environments, joined.</summary>
    public Func<string, string, string> RemovedAnnouncement { get; init; } = (key, envs) =>
        envs.Length > 0 ? $"{key} deleted from {envs}" : $"{key} deleted";

    /// <summary>An import; the arguments are the count and the environments, joined.</summary>
    public Func<int, string, string> ImportedAnnouncement { get; init; } = (n, envs) =>
        n == 1 ? $"Imported 1 variable into {envs}" : $"Imported {n} variables into {envs}";

    /// <summary>A copy across environments; the arguments are the key and the target.</summary>
    public Func<string, string, string> CopiedAnnouncement { get; init; } = (key, env) => $"{key} now set in {env}";

    /// <summary>A failed deployment; the argument is the error text.</summary>
    public Func<string, string> FailedAnnouncement { get; init; } = error => $"Deployment failed. {error}";

    /// <summary>The word joining the last two names of a list.</summary>
    public string AndWord { get; init; } = "and";
}
