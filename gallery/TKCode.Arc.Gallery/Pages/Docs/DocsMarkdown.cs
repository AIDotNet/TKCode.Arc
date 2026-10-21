namespace TKCode.Arc.Gallery.Pages;

/// <summary>The text of the Docs pages. Only facts the repository states are written here; pages not yet written say so.</summary>
internal static class DocsMarkdown
{
    public const string Introduction = """
        # Introduction

        TKCode.Arc is an Arc-style component library for Avalonia, built on AtomUI 6.2.6. Arc's look and behavior follow
        [uiarc.dev](https://uiarc.dev): the same component groups, motion and design tokens.

        The library is split into packages:

        - `TKCode.Arc.Core`: design tokens, the palette, the motion engine and shared primitives.
        - `TKCode.Arc`: the controls, grouped by Arc's sidebar categories.
        - `TKCode.Arc.Blocks`: page-level compositions built from Arc's controls.
        - `TKCode.Arc.AdvancedCharts`: the spreadsheet-style `DataGrid`, an optional package with its own built-in text.
        - Illustrations: animated scene controls, in `TKCode.Arc`.
        - `TKCode.Arc.Icons.Lucide` and `TKCode.Arc.Fonts`: icons and fonts.

        The gallery in this app shows every component. Its Code tab shows each component's Markdown documentation.
        """;

    public const string Installation = """
        # Installation

        Add the package to an Avalonia application:

        ```bash
        dotnet add package TKCode.Arc --prerelease
        ```

        Then call `UseArc()` in the `UseAtomUI(...)` setup of your host. Hosts that show the gallery also call
        `UseArcGallery()` next to it, so the gallery's shell text is translated.

        Build and test the library from the repository root:

        ```bash
        dotnet build TKCode.Arc.slnx
        dotnet test tests/TKCode.Arc.Tests
        ```
        """;

    public const string Theming = """
        # Theming

        Colors, radii, shadows and fonts come from Arc's design tokens. In XAML, read them with `ArcDesignTokenResource`:

        ```xml
        <Setter Property="Background" Value="{arc:ArcDesignTokenResource Surface}" />
        ```

        In code, bind a property to a token with `ArcTokens.Bind`, so the value follows the light and dark appearance and the
        accent color:

        ```cs
        ArcTokens.Bind(control, TemplatedControl.BackgroundProperty, ArcDesignTokenKind.Surface);
        ```

        Control tokens hold geometry and colors that do not depend on the accent. A control's own token is read with
        `{arc:<Control>TokenResource}`.
        """;

    public const string Motion = """
        # Motion

        Arc's transitions are declared in each control's theme, not in code. A transition setter sits under one of two
        selectors, so reduced-motion settings are respected:

        - `^:motion-full` for the full motion.
        - `^:motion-reduced` for the reduced motion.

        A control that uses transitions sets `arc:ArcMotion.Tracks="True"`.

        Motion that a theme cannot express is driven in code with `MotionValue<T>`. The code must say why the theme cannot
        express it.
        """;

    public const string AiAndMcp = """
        # AI and MCP

        This page is not written yet. It will describe how AI assistants can work with the library through MCP.
        """;

    public const string Changelog = """
        # Changelog

        No release notes are published yet. Release notes will be listed here, newest first.
        """;
}
