// Ported from vscode (src/vs/editor/contrib/rename/browser/rename.ts: RenameSkeleton, hasProvider, prepareRename,
// rawRename and rename, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - a RenameLocation & Rejection is a RenameLocationResult and a WorkspaceEdit & Rejection a RenameEdits (the types of
//    LanguageFeatures.Editing.cs); upstream's rejection of a rename without result is "No result.";
//  - the word at the position comes from ModelWords (the language's word definition);
//  - the controller, the commands and the _executeDocumentRenameProvider / _executePrepareRename API commands are in the
//    controls package or not ported.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Rename;

/// <summary>
/// Runs a rename at one position through the rename providers of a model, best first: the first provider that resolves
/// the location renames, and when it rejects the rename the next ones are asked (upstream's RenameSkeleton).
/// </summary>
internal sealed class RenameSkeleton
{
    /// <summary>Upstream's rejection when a provider returns nothing.</summary>
    public const string NoResultMessage = "No result.";

    private readonly TextModel _model;
    private readonly TextPosition _position;
    private readonly LanguageConfigurationService? _languageConfigurationService;
    private readonly IReadOnlyList<IRenameProvider> _providers;
    private int _providerRenameIdx;

    public RenameSkeleton(TextModel model, TextPosition position, LanguageFeatureRegistry<IRenameProvider> registry, LanguageConfigurationService? languageConfigurationService = null)
    {
        _model = model;
        _position = position;
        _languageConfigurationService = languageConfigurationService;
        _providers = registry.Ordered(model);
    }

    public bool HasProvider() => _providers.Count > 0;

    /// <summary>
    /// The range and text to rename: the first provider's that resolves it, else the word at the position (with the
    /// providers' rejections, if any).
    /// </summary>
    public async Task<RenameLocationResult?> ResolveRenameLocation(CancellationToken token)
    {
        var rejects = new List<string>();
        for (_providerRenameIdx = 0; _providerRenameIdx < _providers.Count; _providerRenameIdx++)
        {
            var provider = _providers[_providerRenameIdx];
            if (!provider.SupportsResolveRenameLocation)
            {
                break;
            }

            var res = await provider.ResolveRenameLocation(_model, _position, token).ConfigureAwait(true);
            if (res is null)
            {
                continue;
            }

            if (res.RejectReason is { } reason)
            {
                rejects.Add(reason);
                continue;
            }

            return res;
        }

        // no provider prepared a location: rely on the word under the cursor and start with the first provider
        _providerRenameIdx = 0;

        var rejectReason = rejects.Count > 0 ? string.Join('\n', rejects) : null;
        var word = ModelWords.GetWordAtPosition(_model, _position, _languageConfigurationService);
        if (word is null)
        {
            return new RenameLocationResult(new TextRange(_position), string.Empty) { RejectReason = rejectReason };
        }

        return new RenameLocationResult(new TextRange(_position.LineNumber, word.StartColumn, _position.LineNumber, word.EndColumn), word.Word)
        {
            RejectReason = rejectReason,
        };
    }

    /// <summary>The edits of the rename, from the provider that resolved the location on; the next providers when one rejects.</summary>
    public Task<RenameEdits> ProvideRenameEdits(string newName, CancellationToken token) =>
        ProvideRenameEdits(newName, _providerRenameIdx, [], token);

    private async Task<RenameEdits> ProvideRenameEdits(string newName, int i, List<string> rejects, CancellationToken token)
    {
        while (true)
        {
            if (i >= _providers.Count)
            {
                return new RenameEdits(new WorkspaceEdit([])) { RejectReason = string.Join('\n', rejects) };
            }

            var result = await _providers[i].ProvideRenameEdits(_model, _position, newName, token).ConfigureAwait(true);
            if (result is null)
            {
                rejects.Add(NoResultMessage);
                i++;
                continue;
            }

            if (result.RejectReason is { } reason)
            {
                rejects.Add(reason);
                i++;
                continue;
            }

            return result;
        }
    }
}

/// <summary>The rename functions of upstream's rename.ts.</summary>
internal static class RenameService
{
    public static bool HasProvider(LanguageFeatureRegistry<IRenameProvider> registry, TextModel model) => registry.Ordered(model).Count > 0;

    /// <summary>Upstream's prepareRename: the location a rename at the position would change.</summary>
    public static Task<RenameLocationResult?> PrepareRename(LanguageFeatureRegistry<IRenameProvider> registry, TextModel model, TextPosition position, CancellationToken token = default) =>
        new RenameSkeleton(model, position, registry).ResolveRenameLocation(token);

    /// <summary>Upstream's rawRename: the edits, without resolving the location first.</summary>
    public static Task<RenameEdits> RawRename(LanguageFeatureRegistry<IRenameProvider> registry, TextModel model, TextPosition position, string newName, CancellationToken token = default) =>
        new RenameSkeleton(model, position, registry).ProvideRenameEdits(newName, token);

    /// <summary>Upstream's rename: resolves the location, then asks for the edits; a rejected location rejects the rename.</summary>
    public static async Task<RenameEdits> Rename(LanguageFeatureRegistry<IRenameProvider> registry, TextModel model, TextPosition position, string newName)
    {
        var skeleton = new RenameSkeleton(model, position, registry);
        var loc = await skeleton.ResolveRenameLocation(CancellationToken.None).ConfigureAwait(true);
        if (loc?.RejectReason is { } reason)
        {
            return new RenameEdits(new WorkspaceEdit([])) { RejectReason = reason };
        }

        return await skeleton.ProvideRenameEdits(newName, CancellationToken.None).ConfigureAwait(true);
    }
}
