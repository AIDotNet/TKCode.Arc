using TKCode.Arc.DesignTokens;
using TKCode.Arc.Generated.TKCodeArcBlocks;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

public static class ArcBlocksBuilderExtensions
{
    private const string PackageId = "TKCode.Arc.Blocks";

    /// <summary>
    /// Enables all Arc blocks (page-level compositions such as the command palette). Depends on
    /// <see cref="ArcBuilderExtensions.UseArcControls"/> and <see cref="ArcMarkdownBuilderExtensions.UseArcMarkdown"/> (the
    /// AI and support blocks render Markdown). Code in those blocks is colored when the app also calls
    /// <c>UseArcHighlighting()</c> (TKCode.Arc.Highlighting); without it, it is plain text.
    /// </summary>
    public static ArcBuilder UseArcBlocks(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static package => ArcGeneratedAssets.AddTo(package
            .DependsOn(static b => b.UseArcControls().UseArcMarkdown())));

    /// <summary>
    /// Enables only the Arc blocks selected in <paramref name="configure"/>. Unlike the parameterless
    /// <see cref="UseArcBlocks(ArcBuilder)"/>, this overload never references the assembly-wide asset table unless
    /// <see cref="ArcBlocksBuilder.UseAll"/> is called, allowing NativeAOT and ILLink trimming to drop unused blocks,
    /// tokens and compiled XAML theme dictionaries. Depends on <see cref="ArcBuilderExtensions.UseArcControls"/>.
    /// </summary>
    /// <param name="builder">The Arc builder.</param>
    /// <param name="configure">Selects the blocks to register.</param>
    public static ArcBuilder UseArcBlocks(this ArcBuilder builder, Action<ArcBlocksBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.UsePackage(PackageId, package =>
        {
            package.DependsOn(static b => b.UseArcControls());
            configure(new ArcBlocksBuilder(package));
        });
    }
}

/// <summary>
/// Selects individual blocks from <c>TKCode.Arc.Blocks</c> for trim-safe and NativeAOT-friendly registration through
/// <see cref="ArcBlocksBuilderExtensions.UseArcBlocks(ArcBuilder, Action{ArcBlocksBuilder})"/>.
/// </summary>
public sealed class ArcBlocksBuilder
{
    private readonly ArcPackageBuilder _package;
    private readonly HashSet<string> _registered = new(StringComparer.Ordinal);
    private bool _all;

    internal ArcBlocksBuilder(ArcPackageBuilder package)
    {
        _package = package;
    }

    /// <summary>Registers <see cref="SettingsPage"/>, <see cref="SettingsSection"/>, <see cref="SettingsPageNavItem"/>, <see cref="SettingsGroup"/> and <see cref="SettingsRow"/>.</summary>
    public ArcBlocksBuilder UseSettings()
        => UseSettingsPage().UseSettingsGroup().UseSettingsRow();

    /// <summary>Registers <see cref="SettingsPage"/>, <see cref="SettingsSection"/> and <see cref="SettingsPageNavItem"/>.</summary>
    public ArcBlocksBuilder UseSettingsPage()
    {
        if (!_all && _registered.Add(nameof(SettingsPage)))
        {
            _package.AddTokens([SettingsPageTokens.Descriptor, SettingsPageNavItemTokens.Descriptor, SettingsSectionTokens.Descriptor])
                .AddThemes([ArcGeneratedThemeAssets.SettingsPageTheme()]);
        }
        return this;
    }

    /// <summary>Registers <see cref="SettingsGroup"/>.</summary>
    public ArcBlocksBuilder UseSettingsGroup()
    {
        if (!_all && _registered.Add(nameof(SettingsGroup)))
        {
            _package.AddTokens([SettingsGroupTokens.Descriptor])
                .AddThemes([ArcGeneratedThemeAssets.SettingsGroupTheme()]);
        }
        return this;
    }

    /// <summary>Registers <see cref="SettingsRow"/>.</summary>
    public ArcBlocksBuilder UseSettingsRow()
    {
        if (!_all && _registered.Add(nameof(SettingsRow)))
        {
            _package.AddTokens([SettingsRowTokens.Descriptor])
                .AddThemes([ArcGeneratedThemeAssets.SettingsRowTheme()]);
        }
        return this;
    }

    /// <summary>Registers <see cref="Sidebar"/> (including its design tokens, theme and built-in localization catalog).</summary>
    public ArcBlocksBuilder UseSidebar()
    {
        if (!_all && _registered.Add(nameof(Sidebar)))
        {
            _package.AddTokens([SidebarTokens.Descriptor])
                .AddThemes([ArcGeneratedThemeAssets.SidebarTheme()])
                .AddLanguages([SidebarLangResources.Catalog]);
        }
        return this;
    }

    /// <summary>Registers <see cref="CommandPalette"/> and <see cref="CommandPaletteDialog"/>.</summary>
    public ArcBlocksBuilder UseCommandPalette()
    {
        if (!_all && _registered.Add(nameof(CommandPalette)))
        {
            _package.AddTokens([CommandPaletteTokens.Descriptor, CommandPaletteDialogTokens.Descriptor])
                .AddThemes([ArcGeneratedThemeAssets.CommandPaletteTheme(), ArcGeneratedThemeAssets.CommandPaletteDialogTheme()]);
        }
        return this;
    }

    /// <summary>Registers <see cref="PageHeader"/> and <see cref="PageHeaderTab"/>.</summary>
    public ArcBlocksBuilder UsePageHeader()
    {
        if (!_all && _registered.Add(nameof(PageHeader)))
        {
            _package.AddTokens([PageHeaderTokens.Descriptor, PageHeaderTabTokens.Descriptor])
                .AddThemes([ArcGeneratedThemeAssets.PageHeaderTheme(), ArcGeneratedThemeAssets.PageHeaderTabTheme()]);
        }
        return this;
    }

    /// <summary>Registers <see cref="NotificationCenter"/>.</summary>
    public ArcBlocksBuilder UseNotificationCenter()
    {
        if (!_all && _registered.Add(nameof(NotificationCenter)))
        {
            _package.AddTokens([NotificationCenterTokens.Descriptor])
                .AddThemes([ArcGeneratedThemeAssets.NotificationCenterTheme()]);
        }
        return this;
    }

    /// <summary>Registers <see cref="EmptyStates"/>.</summary>
    public ArcBlocksBuilder UseEmptyStates()
    {
        if (!_all && _registered.Add(nameof(EmptyStates)))
        {
            _package.AddTokens([EmptyStatesTokens.Descriptor])
                .AddThemes([ArcGeneratedThemeAssets.EmptyStatesTheme()]);
        }
        return this;
    }

    /// <summary>Registers <see cref="SelectionToolbar"/>.</summary>
    public ArcBlocksBuilder UseSelectionToolbar()
    {
        if (!_all && _registered.Add(nameof(SelectionToolbar)))
        {
            _package.AddTokens([SelectionToolbarTokens.Descriptor])
                .AddThemes([ArcGeneratedThemeAssets.SelectionToolbarTheme()]);
        }
        return this;
    }

    /// <summary>Registers every block in <c>TKCode.Arc.Blocks</c> (equivalent to calling <see cref="ArcBlocksBuilderExtensions.UseArcBlocks(ArcBuilder)"/>).</summary>
    public ArcBlocksBuilder UseAll()
    {
        if (_all)
        {
            return this;
        }
        if (_registered.Count > 0)
        {
            throw new InvalidOperationException("Call UseAll() on its own rather than mixing it with individual block selections.");
        }
        _all = true;
        _package.DependsOn(static b => b.UseArcMarkdown());
        ArcGeneratedAssets.AddTo(_package);
        return this;
    }
}
