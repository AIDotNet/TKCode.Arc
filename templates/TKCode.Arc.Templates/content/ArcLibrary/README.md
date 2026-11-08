# ArcLibraryDisplayName

Controls for [TKCode.Arc](https://github.com/AIDotNet/TKCode.Arc) apps.

```text
src/ArcLibrary/            the library (dotnet pack makes its NuGet package)
tests/ArcLibrary.Tests/    headless tests (dotnet test)
samples/ArcLibrary.Sample/ an app that shows the controls (dotnet run --project samples/ArcLibrary.Sample)
```

## Use it in an app

```csharp
builder.UseArc(arc => arc.UseArcLibraryIdentifier());   // registers Arc's controls too
```

```xml
<Window xmlns:lib="using:ArcLibrary">
    <lib:StatTile Title="Revenue" Value="$48,210" Caption="+8.2% vs. last month" />
</Window>
```

## Add a control

Each control has three files, as `src/ArcLibrary/Controls/StatTile/` shows (inside the library folder,
`dotnet new tkcode-arc-control -n Gauge --namespace ArcLibrary` writes them under `Controls/`):

- `<Control>.cs`: the control, a `TemplatedControl` with styled properties.
- `<Control>Token.cs`: its design tokens, `[ControlDesignToken]`, geometry only (colors come from Arc's tokens).
  **Give it an identity with the library's prefix** (`Identity = "ArcLibraryIdentifier<Control>"`): an identity must be
  unique among every package an app registers, Arc's included, and a clash stops the app at start-up (ARCF004).
- `Themes/<Control>Theme.axaml`: its `ControlTheme`, with `x:Class="ArcLibrary.<Control>Theme"` and
  `x:ClassModifier="internal"`; transitions under `^:motion-full` / `^:motion-reduced` with `arc:ArcMotion.Tracks`.

The Foundation generator, which comes with the TKCode.Arc package, generates the token types and registers the themes
at build time (no reflection: apps that use the library can be trimmed and compiled ahead of time), and reports the
rules it checks as ARCF warnings and errors. `UseArcLibraryIdentifier()` registers whatever it generated, so a new control
needs no other registration.

## Publish

```bash
dotnet pack src/ArcLibrary -c Release -o artifacts
```

Set the package id, version and description in `src/ArcLibrary/ArcLibrary.csproj`.
