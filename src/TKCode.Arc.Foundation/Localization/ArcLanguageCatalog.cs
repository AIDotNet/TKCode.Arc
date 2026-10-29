namespace TKCode.Arc.Localization;

/// <summary>
/// Marks an enum as a language catalog: its members are text ids, and the <c>Localization/&lt;language&gt;.xlf</c> files of
/// the project (XLIFF 2, <c>&lt;file id&gt;</c> = the enum's full name, <c>&lt;unit id&gt;</c> = a member name) hold the text.
/// The Foundation generator turns it into <c>XxxLangResources</c> (C# lookup) and <c>XxxLangResourceExtension</c>
/// (<c>{arc:XxxLangResource Member}</c> in XAML), with <c>XxxLangResourceKind</c> → <c>XxxLangResource</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Enum, Inherited = false)]
public sealed class LanguageCatalogAttribute : Attribute
{
}

/// <summary>The text of one language of a catalog, aligned with <see cref="ArcLanguageCatalog.Keys"/> (null = missing).</summary>
public sealed class ArcLanguageTable
{
    /// <summary>Creates a table.</summary>
    /// <param name="language">The language.</param>
    /// <param name="texts">The text of every member in declaration order; null where the language has none.</param>
    public ArcLanguageTable(LanguageTag language, IReadOnlyList<string?> texts)
    {
        Language = language;
        Texts = texts;
    }

    /// <summary>The language.</summary>
    public LanguageTag Language { get; }

    /// <summary>The text of every member in declaration order; null where the language has none.</summary>
    public IReadOnlyList<string?> Texts { get; }
}

/// <summary>
/// One <c>[LanguageCatalog]</c> enum and its text in every language, as generated from its xlf files (the generated
/// <c>XxxLangResources.Catalog</c>). A package registers its catalogs with <see cref="ArcPackageBuilder.AddLanguages"/>.
/// </summary>
public sealed class ArcLanguageCatalog
{
    private readonly ArcLanguageTable? _source;

    /// <summary>Creates a catalog.</summary>
    /// <param name="id">The enum's full name (the xlf <c>file id</c>).</param>
    /// <param name="sourceLanguage">The language the text was written in (the xlf <c>srcLang</c>).</param>
    /// <param name="keys">The resource keys: the boxed enum members in declaration order.</param>
    /// <param name="names">The member names, aligned with <paramref name="keys"/>.</param>
    /// <param name="tables">The text of each language.</param>
    public ArcLanguageCatalog(string id, LanguageTag sourceLanguage, IReadOnlyList<object> keys, IReadOnlyList<string> names,
                              IReadOnlyList<ArcLanguageTable> tables)
    {
        if (names.Count != keys.Count)
        {
            throw new ArgumentException($"Catalog '{id}' has {keys.Count} keys but {names.Count} names.", nameof(names));
        }
        foreach (var table in tables)
        {
            if (table.Texts.Count != keys.Count)
            {
                throw new ArgumentException(
                    $"Catalog '{id}' has {keys.Count} keys but its '{table.Language}' table has {table.Texts.Count} entries.", nameof(tables));
            }
        }
        Id = id;
        SourceLanguage = sourceLanguage;
        Keys = keys;
        Names = names;
        Tables = tables;
        Languages = tables.Select(t => t.Language).ToArray();
        _source = FindTable(sourceLanguage);
    }

    /// <summary>The enum's full name.</summary>
    public string Id { get; }

    /// <summary>The language the text was written in; the last fallback.</summary>
    public LanguageTag SourceLanguage { get; }

    /// <summary>The resource keys: the boxed enum members in declaration order.</summary>
    public IReadOnlyList<object> Keys { get; }

    /// <summary>The member names, aligned with <see cref="Keys"/>.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>The text of each language.</summary>
    public IReadOnlyList<ArcLanguageTable> Tables { get; }

    /// <summary>The languages that have a table.</summary>
    public IReadOnlyList<LanguageTag> Languages { get; }

    /// <summary>The table of exactly <paramref name="language"/>, or null.</summary>
    /// <param name="language">The language.</param>
    public ArcLanguageTable? FindTable(LanguageTag language)
    {
        foreach (var table in Tables)
        {
            if (table.Language == language)
            {
                return table;
            }
        }
        return null;
    }

    /// <summary>
    /// The text of entry <paramref name="index"/> in <paramref name="language"/>: that language, then its parents
    /// (<c>zh-Hant-TW</c> → <c>zh-Hant</c> → <c>zh</c>), then <paramref name="fallback"/> and its parents, then the source
    /// language, then the member name.
    /// </summary>
    /// <param name="index">The member's declaration index.</param>
    /// <param name="language">The language.</param>
    /// <param name="fallback">The application's default language, or null.</param>
    public string Resolve(int index, LanguageTag language, LanguageTag? fallback = null)
    {
        if (index < 0 || index >= Keys.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return Lookup(index, language) ?? (fallback is null ? null : Lookup(index, fallback)) ?? _source?.Texts[index] ?? Names[index];
    }

    /// <summary>The entry text in <paramref name="language"/> or one of its parents, without other fallbacks.</summary>
    /// <param name="index">The member's declaration index.</param>
    /// <param name="language">The language.</param>
    public string? Lookup(int index, LanguageTag language)
    {
        for (var tag = language; tag is not null; tag = tag.Parent)
        {
            if (FindTable(tag)?.Texts[index] is { } text)
            {
                return text;
            }
        }
        return null;
    }

    /// <summary>The source-language text of entry <paramref name="index"/> (the member name when it has none).</summary>
    /// <param name="index">The member's declaration index.</param>
    public string SourceText(int index) => _source?.Texts[index] ?? Names[index];

    /// <inheritdoc />
    public override string ToString() => Id;
}
