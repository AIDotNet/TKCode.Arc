using TKCode.Arc.DesignTokens;
using TKCode.Arc.Generated.TKCodeArcSpecial;

namespace TKCode.Arc;

public static class ArcSpecialBuilderExtensions
{
    private const string PackageId = "TKCode.Arc.Special";

    /// <summary>
    /// Enables all Arc special components (the animated AI explainers such as <c>AgentLoop</c>). Depends on
    /// <see cref="ArcBuilderExtensions.UseArcControls"/>. The AI components that show code (AskYourData, CodeSuggestion,
    /// DesignToCode, InstallSnippet) color it when the app also calls <c>UseArcHighlighting()</c> (TKCode.Arc.Highlighting);
    /// without it their code is plain text.
    /// </summary>
    public static ArcBuilder UseArcSpecial(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static package => ArcGeneratedAssets.AddTo(package
            .DependsOn(static b => b.UseArcControls())));

    /// <summary>
    /// Enables only the Arc special components selected in <paramref name="configure"/>. Unlike the parameterless
    /// <see cref="UseArcSpecial(ArcBuilder)"/>, this overload never references the assembly-wide asset table unless
    /// <see cref="ArcSpecialBuilder.UseAll"/> is called, allowing NativeAOT and ILLink trimming to drop unused controls,
    /// tokens and compiled XAML theme dictionaries. Depends on <see cref="ArcBuilderExtensions.UseArcControls"/>.
    /// </summary>
    /// <param name="builder">The Arc builder.</param>
    /// <param name="configure">Selects the special components to register.</param>
    public static ArcBuilder UseArcSpecial(this ArcBuilder builder, Action<ArcSpecialBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.UsePackage(PackageId, package =>
        {
            package.DependsOn(static b => b.UseArcControls());
            configure(new ArcSpecialBuilder(package));
        });
    }
}

/// <summary>
/// Selects individual components from <c>TKCode.Arc.Special</c> for trim-safe and NativeAOT-friendly registration through
/// <see cref="ArcSpecialBuilderExtensions.UseArcSpecial(ArcBuilder, Action{ArcSpecialBuilder})"/>.
/// </summary>
public sealed class ArcSpecialBuilder
{
    private readonly ArcPackageBuilder _package;
    private readonly HashSet<string> _registered = new(StringComparer.Ordinal);
    private bool _all;

    internal ArcSpecialBuilder(ArcPackageBuilder package)
    {
        _package = package;
    }

    /// <summary>Registers <see cref="ImageReveal"/>.</summary>
    public ArcSpecialBuilder UseImageReveal()
    {
        if (!_all && _registered.Add(nameof(ImageReveal)))
        {
            _package.AddTokens([ImageRevealTokens.Descriptor])
                .AddThemes([ArcGeneratedThemeAssets.ImageRevealTheme()]);
        }
        return this;
    }

    /// <summary>Registers the Glass family (<see cref="GlassPane"/>, <see cref="GlassCard"/>, <see cref="GlassMenu"/>, <see cref="GlassSegmented"/> and <see cref="GlassTabBar"/>).</summary>
    public ArcSpecialBuilder UseGlass()
    {
        if (!_all && _registered.Add("Glass"))
        {
            _package.AddTokens([
                    GlassPaneTokens.Descriptor,
                    GlassCardTokens.Descriptor,
                    GlassMenuTokens.Descriptor,
                    GlassMenuItemTokens.Descriptor,
                    GlassMenuSeparatorTokens.Descriptor,
                    GlassSegmentedTokens.Descriptor,
                    GlassSegmentTokens.Descriptor,
                    GlassTabBarTokens.Descriptor,
                    GlassTabTokens.Descriptor
                ])
                .AddThemes([
                    ArcGeneratedThemeAssets.GlassPaneTheme(),
                    ArcGeneratedThemeAssets.GlassCardTheme(),
                    ArcGeneratedThemeAssets.GlassMenuTheme(),
                    ArcGeneratedThemeAssets.GlassSegmentedTheme(),
                    ArcGeneratedThemeAssets.GlassTabBarTheme()
                ]);
        }
        return this;
    }

    /// <summary>Registers <see cref="ConfirmPopover"/>.</summary>
    public ArcSpecialBuilder UseConfirmPopover()
    {
        if (!_all && _registered.Add(nameof(ConfirmPopover)))
        {
            _package.AddTokens([ConfirmPopoverTokens.Descriptor])
                .AddThemes([ArcGeneratedThemeAssets.ConfirmPopoverTheme()]);
        }
        return this;
    }

    /// <summary>Registers <see cref="UndoToast"/>.</summary>
    public ArcSpecialBuilder UseUndoToast()
    {
        if (!_all && _registered.Add(nameof(UndoToast)))
        {
            _package.AddTokens([UndoToastTokens.Descriptor])
                .AddThemes([ArcGeneratedThemeAssets.UndoToastTheme()]);
        }
        return this;
    }

    /// <summary>Registers <see cref="EmojiPicker"/>.</summary>
    public ArcSpecialBuilder UseEmojiPicker()
    {
        if (!_all && _registered.Add(nameof(EmojiPicker)))
        {
            _package.AddTokens([EmojiPickerTokens.Descriptor])
                .AddThemes([ArcGeneratedThemeAssets.EmojiPickerTheme()]);
        }
        return this;
    }

    /// <summary>Registers <see cref="TocProgress"/>.</summary>
    public ArcSpecialBuilder UseTocProgress()
    {
        if (!_all && _registered.Add(nameof(TocProgress)))
        {
            _package.AddTokens([TocProgressTokens.Descriptor])
                .AddThemes([ArcGeneratedThemeAssets.TocProgressTheme()]);
        }
        return this;
    }

    /// <summary>Registers <see cref="ReasoningBlock"/>.</summary>
    public ArcSpecialBuilder UseReasoningBlock()
    {
        if (!_all && _registered.Add(nameof(ReasoningBlock)))
        {
            _package.AddTokens([ReasoningBlockTokens.Descriptor])
                .AddThemes([ArcGeneratedThemeAssets.ReasoningBlockTheme()]);
        }
        return this;
    }

    /// <summary>Registers <see cref="ToolCall"/> and <see cref="ToolCallGroup"/>.</summary>
    public ArcSpecialBuilder UseToolCall()
    {
        if (!_all && _registered.Add(nameof(ToolCall)))
        {
            _package.AddTokens([ToolCallTokens.Descriptor, ToolCallGroupTokens.Descriptor])
                .AddThemes([ArcGeneratedThemeAssets.ToolCallTheme(), ArcGeneratedThemeAssets.ToolCallGroupTheme()]);
        }
        return this;
    }

    /// <summary>Registers every component in <c>TKCode.Arc.Special</c> (equivalent to calling <see cref="ArcSpecialBuilderExtensions.UseArcSpecial(ArcBuilder)"/>).</summary>
    public ArcSpecialBuilder UseAll()
    {
        if (_all)
        {
            return this;
        }
        if (_registered.Count > 0)
        {
            throw new InvalidOperationException("Call UseAll() on its own rather than mixing it with individual component selections.");
        }
        _all = true;
        ArcGeneratedAssets.AddTo(_package);
        return this;
    }
}
