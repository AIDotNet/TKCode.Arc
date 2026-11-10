---
title: AI setup wizard
summary: A five-step setup that turns on AI for a workspace: provider, model and key, data access, privacy rules, a connection test and a summary before it goes live.
---

<Demo name="Preview" />

## When to use

`AiSetupWizard` walks a workspace admin through turning on AI features. Five steps, each on its own page: choose the
provider and the model and enter the key; choose which kinds of workspace data the assistant may read; decide the privacy
rules (keep data out of training, mask personal information, how long chat history is kept); run a short test with those
settings; and review everything before turning it on. A stepper beside the page shows where the admin is and lets them go
back to any finished step. Each step checks its own fields, and the summary only opens after a test has passed.

- **Admin settings.** The first time a workspace enables an assistant, or when it moves to another provider.
- **Self-hosted or bring-your-own-key setups.** A provider without a key (your own server) skips the key field.
- **Compliance reviews.** The summary shows every choice in one place before anything changes.

## When not to use

- **Per-person preferences.** A single switch or the `Settings page` block is enough when nothing needs testing.
- **Choosing a model only.** Use the `Model picker` block.
- **Long forms.** For many unrelated fields, use a settings page with sections rather than steps.

## Usage

The block derives from Avalonia's `TemplatedControl`. Without any data it offers invented providers and data sources and a
simulated test (`AiSetupWizardSamples`; a key of "fail" shows the failure path).

```xml
<arc:AiSetupWizard Providers="{Binding Providers}" DataSources="{Binding DataSources}" />
```

```csharp
wizard.TestRunner = async (configuration, progress, cancellation) =>
{
    await client.PingAsync(configuration.ProviderId, cancellation);   progress.Report(1);
    await client.CheckKeyAsync(configuration.ApiKey, cancellation);   progress.Report(2);
    var reply = await client.AskAsync(configuration.Model, "Say hi", cancellation); progress.Report(3);
    await index.ReadOneAsync(configuration.DataSourceIds, cancellation); progress.Report(4);
    return new AiSetupTestResult(true, $"The model answered in {reply.Elapsed.TotalMilliseconds:0} ms.");
};
wizard.Completed += (_, e) => workspace.EnableAi(e.Configuration);
```

### The steps

1. **Provider and model**: `Providers` as radio cards; a model menu with the chosen provider's `Models`; a key field when
   `RequiresKey`, with `KeyHint` under it. Continue needs a provider and, when asked for, a key.
2. **Data access**: one checkbox per `DataSources` entry; `IsEnabledByDefault` decides the start.
3. **Privacy**: two switches (training, masking) and the history choices from `HistoryChoices` (days; 0 is off).
4. **Test**: four checks that turn from a circle into a spinner and then a check or a cross, the result's message, and a
   button to run it again. Any change in an earlier step asks for a new test.
5. **Done**: a summary of every choice and the button that raises `Completed` with the `AiSetupConfiguration`.

`CurrentStep` (two-way) only moves to steps already reached; `Next()`, `Back()`, `GoTo(step)`, `RunTestAsync()` and
`Reset()` do from code what the buttons do. `Configuration`, `TestResult`, `IsTesting` and `IsCompleted` report where it
stands.

### Narrow

<Demo name="Narrow" />

### Words

`Labels` (`AiSetupWizardLabels`) holds the step names and descriptions, the buttons, the field labels, the errors, the
switch texts, the test checks, the summary rows and the step announcement.

## API reference

<!-- api: AiSetupWizard -->

<!-- api: AiSetupProvider -->

<!-- api: AiSetupDataSource -->

<!-- api: AiSetupConfiguration -->

<!-- api: AiSetupTestResult -->

<!-- api: AiSetupStep -->

<!-- api: AiSetupWizardLabels -->

## Accessibility

- The wizard is a form landmark; its title is a level 2 heading and each page's title a level 3 heading.
- The stepper reads each step's name and state; finished steps can be chosen with the keyboard.
- Moving to a step announces "Step 2 of 5: Data access". A missing provider or key is announced, shown under the field and
  focuses it.
- Each test check reports its state; the result's message is announced politely.
- The switches are named by their text and described by the line under them.

## Motion

- Pages slide 12 px in the direction of travel with a crossfade; the stepper fills its connectors and moves its halo.
- Check rows swap their glyph at once; the test button shows its spinner while the test runs.
- Reduced motion crossfades the pages without sliding; Off changes them at once.

## Responsive behavior

- The wizard is at most 860 wide. Wide, the stepper is a 220 wide column beside the page. At 640 or less (`:compact`) the
  stepper turns horizontal above the page and the padding tightens from 24 to 16.
- The page keeps a minimum height of 360 so the footer does not jump between steps.
- Radio cards and checkboxes wrap their text; nothing depends on hover.

## Related

- `Stepper`, `RadioCards`, `Select`, `PasswordField`, `Checkbox`, `Switch` and `SegmentedControl`, the controls it is made of.
- `Multi-step form`, a general step-by-step form.
- `Model picker` for choosing a model in the product itself.
