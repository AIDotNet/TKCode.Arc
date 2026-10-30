---
title: Install snippet
summary: A quickstart card where framework tabs, three numbered steps and highlighted snippets take a developer from install to the first event, and Generate key wipes a fresh key into place.
---

<Demo name="Preview" />

## When to use

`InstallSnippet` is a "get started in 30 seconds" card. Framework tabs pick the reader's stack, and three numbered steps
(Install, Add your key, Ship) show a highlighted snippet each, with a copy button. Step 1 offers npm, pnpm, yarn and bun
when the framework installs packages. Generate key creates a key and wipes it into the placeholder in place, in every
snippet that uses it. Switching frameworks crossfades the snippets in space reserved for the longest of them, so the
card never changes size.

- **A quickstart for several frameworks** without a page of docs for each.
- **Onboarding where creating an API key is part of getting started**: the first screen after sign up, or an empty
  dashboard waiting for its first event.
- **Marketing pages for developer tools** that show how little setup it takes.

## When not to use

- **A single code sample with no steps**: use `CodeBlock`.
- **Trying requests against a live API**: an API playground fits better.
- **Long setup guides with many branches**: write docs pages and link to them.
- **Showing existing secret keys in full**: mask them in an API keys page instead (see `ProviderKeys`).

## Usage

With no frameworks and no simulation (below) the card shows its three steps over empty snippets (the Empty tab above).
With no key generator, Generate key invents a demo key and says so: nothing is sent. Give it your own frameworks and your
key endpoint:

```csharp
snippet.KeyGenerator = async token =>
{
    var response = await http.PostAsync("/api/keys", null, token);
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<NewKey>(token))!.Key;
};
snippet.Copied += (_, e) => analytics.Track("onboarding_copy", e.Step.ToString());
```

Return the key and it wipes into step 2. Throw and step 2 shows the exception's message (or "Could not create a key.")
with an alert icon, and the button reads Try again. The token is cancelled when the card leaves the window or the reader
asks for another key.

### Anatomy

1. **Header**: the title, one line under it and a muted version label at the top right (`Version`; empty hides it).
2. **Framework tabs**: a mark and a name per framework; a raised highlight slides to the selected tab.
3. **Steps**: a numbered marker on a rail, the title, a line that says what to do, and a code block with the file
   name, a copy button and the snippet. A step whose snippet was copied swaps its number for a check.
4. **Step 1** shows the package manager tabs at the right of its title when the framework installs packages.
5. **Step 2** has Generate key at the right of its title and a status line under its code.

### Your frameworks

<Demo name="YourFrameworks" />

Each `InstallSnippetFramework` has an `Id`, a `Label` and three snippets. Set `Packages` (package names separated by
spaces) for an install through npm, pnpm, yarn or bun, or `Install` for a command or a script tag. Write `{{key}}` where
the key goes; until a key exists the placeholder (`KeyPlaceholder`) shows there, tinted, and copies as written:

```xml
<arc:InstallSnippet KeyPlaceholder="YOUR_CONTOSO_KEY">
    <arc:InstallSnippetFramework Id="ruby" Label="Ruby">
        <arc:InstallSnippetFramework.Install>
            <arc:InstallSnippetFile FileName="Terminal" Code="gem install contoso-labs" />
        </arc:InstallSnippetFramework.Install>
        <arc:InstallSnippetFramework.Key>
            <arc:InstallSnippetFile FileName=".env" Code="CONTOSO_KEY={{key}}" />
        </arc:InstallSnippetFramework.Key>
    </arc:InstallSnippetFramework>
</arc:InstallSnippet>
```

The ids `node`, `python`, `go` and `html` get a mark of their own; any other id shows a text-only tab unless you set
`Mark` to a 24 × 24 stroked glyph. The language for highlighting follows the file name (`Terminal` and `.env` read as
shell) unless you set `Language`. Keep snippets under ten lines so the card stays easy to scan.

### Your key service

<Demo name="YourKeyService" />

`SelectedFramework` and `PackageManager` are two-way: bind them to remember the reader's choice across pages.
`FrameworkChanged`, `PackageManagerChanged`, `KeyGenerated` and `Copied` report what the reader did, with the exact text
a copy put on the clipboard. Pass `ApiKey` for a signed-in reader who already has a key: every snippet shows it and
Generate key is hidden.

### A simulated SDK

For a demo, leave `Frameworks` empty and give the card an `InstallSnippetSimulation`: its framework tabs and the
`KeyPrefix` of the keys its Generate key invents. Your own `Frameworks` set it aside, and setting `Simulation` to null
empties the card. The previews here show this library's own install steps that way: `TKCode.Arc.Special` added with the
.NET CLI, a PackageReference or central package management, a model provider key kept in user secrets, development
settings or an environment variable (for your app's own model calls; Arc's components never see it), and the
registration in `Program.cs`. The keys are invented demo keys.

```csharp
snippet.Simulation = new InstallSnippetSimulation
{
    Frameworks =
    [
        new InstallSnippetFramework("dotnet", ".NET CLI")
        {
            Install = new InstallSnippetFile("Terminal", "dotnet add package TKCode.Arc.Special --version 0.3.2-preview.1"),
            Key = new InstallSnippetFile("Terminal", "dotnet user-secrets set \"Chat:ApiKey\" \"{{key}}\""),
        },
    ],
    KeyPrefix = "demo_",
};
```

### Narrow

<Demo name="Narrow" />

The card fills its container up to 640px. Below 460px the padding tightens and the package manager tabs show their marks
only (their names stay for screen readers and in a tooltip). Below 380px the framework tabs drop their marks and the
version label hides. Long lines scroll sideways inside their code block, never the page.

### Your own words

`Labels` holds every word the card shows or says, apart from `Title`, `Description` and `Version`:

```csharp
snippet.Labels = new InstallSnippetLabels
{
    InstallTitle = "Installer",
    KeyTitle = "Ajouter votre clé",
    CodeTitle = "Livrer",
    GenerateKey = "Générer une clé",
};
```

### Content guidelines

- Step lines say what to do with the snippet in a few words ("Put your key in .env. Never commit it.").
- File names are the reader's real files (`.env`, `app.py`); a command shows as Terminal.
- Keep each snippet short and runnable as is, with `{{key}}` exactly where the key goes.

### Best practices

- Wire `KeyGenerator` to your key endpoint, and pass `ApiKey` for readers who already have one.
- Track `Copied` to see where onboarding stalls.
- Keep simulated keys out of production: without `KeyGenerator` the card invents keys on purpose.

## API reference

<!-- api: InstallSnippet -->

<!-- api: InstallSnippetFramework -->

<!-- api: InstallSnippetFile -->

<!-- api: InstallSnippetSimulation -->

<!-- api: InstallSnippetLabels -->

## Accessibility

- The framework and package manager tabs are tab lists with roving focus: only the selected tab is a Tab stop, and each
  tab reports whether it is selected. The steps are the framework tabs' panel.
- The steps are a list of three items; each title is a heading. A finished step reads ", done" and shows a check
  instead of its number, so progress is never shown by color alone.
- A polite live region announces framework changes, new keys and errors, and says when a key is simulated. Copy buttons
  announce Copied on their own.
- An error shows an alert icon and text, and the button changes to Try again.
- Marks are decorative; every tab has a name, and the package manager tabs keep theirs when the narrow layout hides it.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves between the framework tabs, the package manager tabs, the copy buttons and Generate key |
| Left Arrow, Right Arrow | In a tab list, moves to and selects the previous or next tab (wrapping) |
| Home, End | In a tab list, selects the first or last tab |
| Enter, Space | Copies a snippet or generates a key |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Tab highlight | Slides to the measured tab on a bounce-free spring (0.24s) | Moves at once | At once |
| Snippets, file names, step lines | Old fades out over 100ms, new fades in over 160ms rising 2px, in one shared cell | Crossfade over 120ms, no rise | At once |
| A new key | The slot eases from the placeholder's width to the key's (exact, in characters) while the key wipes in from the left over 340ms, then its tint fades over 480ms | The key appears at once; the tint still fades | At once |
| A finished step | The number fades to a check as the marker fills with the accent | The check fades in over 120ms | At once |

Every code block reserves the height of its step's longest snippet and every step line the height of its longest line,
so switching frameworks moves nothing. Only opacity, clips and one measured width animate, and nothing runs when idle.

## Related

- `CodeBlock` for a single code sample with copy.
- `CopyButton` for copying one value.
- `ProviderKeys` for showing and rotating keys a person already has.
- `Tabs` for switching related content in one place.
