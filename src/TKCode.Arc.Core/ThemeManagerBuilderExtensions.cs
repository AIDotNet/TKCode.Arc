using TKCode.Arc.Generated.TKCodeArcCore;

namespace TKCode.Arc;

public static class ArcCoreBuilderExtensions
{
    /// <summary>
    /// Enables the Arc core package (design tokens, motion, primitives). At startup it also registers
    /// <see cref="ArcBaseTheme"/>, the themes of the Avalonia built-in controls Arc's templates use (ScrollViewer,
    /// ItemsControl, popup hosts, ...), unless the application already has a theme for them (FluentTheme, SimpleTheme).
    /// </summary>
    public static ArcBuilder UseArcCore(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static package => ArcGeneratedAssets.AddTo(package)
            .OnCommit(static context => ArcBaseTheme.TryRegister(context.Application)));
}
