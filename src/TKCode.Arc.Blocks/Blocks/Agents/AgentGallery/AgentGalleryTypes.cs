using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How <see cref="AgentGallery"/> orders the cards.</summary>
public enum AgentGallerySort
{
    /// <summary>Most installs first (ties by name).</summary>
    Popular,

    /// <summary>Most recently added first (ties by name).</summary>
    Newest,

    /// <summary>Highest rating first, then the most ratings (ties by name).</summary>
    TopRated
}

/// <summary>
/// The color family of an agent's mark: one of the four chart series colors of the theme, the accent, or a neutral
/// tile. The colors follow the theme and the accent; the mark never carries a brand color of its own.
/// </summary>
public enum AgentGalleryTint
{
    /// <summary>The accent color.</summary>
    Accent,

    /// <summary>The first series color.</summary>
    Series1,

    /// <summary>The second series color.</summary>
    Series2,

    /// <summary>The third series color.</summary>
    Series3,

    /// <summary>The fourth series color.</summary>
    Series4,

    /// <summary>A neutral tile in the text color.</summary>
    Neutral
}

/// <summary>Where an agent stands in <see cref="AgentGallery"/>: available, being installed, installed, or a failed install.</summary>
public enum AgentGalleryInstallState
{
    /// <summary>Not installed.</summary>
    Available,

    /// <summary>An install is running.</summary>
    Installing,

    /// <summary>Installed (its id is in <see cref="AgentGallery.InstalledIds"/>).</summary>
    Installed,

    /// <summary>The last install failed; the card offers a retry and the detail view shows the message.</summary>
    Failed
}

/// <summary>A category of <see cref="AgentGallery"/>: the key agents refer to and the label of its filter pill.</summary>
public sealed class AgentGalleryCategory
{
    /// <summary>Creates an empty category (for XAML).</summary>
    public AgentGalleryCategory()
    {
    }

    /// <summary>Creates a category.</summary>
    /// <param name="key">The key agents refer to.</param>
    /// <param name="label">The label of its filter pill.</param>
    public AgentGalleryCategory(string key, string label)
    {
        Key = key;
        Label = label;
    }

    /// <summary>The key agents refer to in <see cref="AgentGalleryItem.CategoryKey"/>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The label of its filter pill and of the category line in the detail view.</summary>
    public string Label { get; set; } = string.Empty;
}

/// <summary>Something an agent needs access to before it can work, listed in the detail view.</summary>
public sealed class AgentGalleryPermission
{
    /// <summary>Creates an empty permission (for XAML).</summary>
    public AgentGalleryPermission()
    {
    }

    /// <summary>Creates a permission.</summary>
    /// <param name="name">What is accessed ("Read your calendar").</param>
    /// <param name="detail">Why, in one short sentence; null shows none.</param>
    /// <param name="isSensitive">True marks it with a warning icon and the word "Sensitive".</param>
    public AgentGalleryPermission(string name, string? detail = null, bool isSensitive = false)
    {
        Name = name;
        Detail = detail;
        IsSensitive = isSensitive;
    }

    /// <summary>What is accessed, as a short phrase ("Read your calendar").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Why the agent needs it, in one short sentence; null shows none.</summary>
    public string? Detail { get; set; }

    /// <summary>True for access people should think twice about (sending mail, changing files): a warning icon and a word, not color alone.</summary>
    public bool IsSensitive { get; set; }
}

/// <summary>
/// One ready-made agent in <see cref="AgentGallery"/>: what the card shows (mark, name, maker, pitch, rating, installs,
/// tags) and what the detail view adds (description, abilities, permissions, sample prompts). Data only; replace the
/// list in <see cref="AgentGallery.Agents"/> (or change an observable one) to update the gallery.
/// </summary>
public sealed class AgentGalleryItem
{
    /// <summary>Stable id, reported in events and kept in <see cref="AgentGallery.InstalledIds"/>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The agent's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Who made it ("Fernhill Studio"); null shows none.</summary>
    public string? Maker { get; set; }

    /// <summary>True adds a check after the maker: the maker is known to the gallery's owner.</summary>
    public bool IsVerified { get; set; }

    /// <summary>One line on the card: what the agent does.</summary>
    public string Pitch { get; set; } = string.Empty;

    /// <summary>A paragraph for the detail view; null repeats the pitch.</summary>
    public string? Description { get; set; }

    /// <summary>The <see cref="AgentGalleryCategory.Key"/> it is filed under.</summary>
    public string CategoryKey { get; set; } = string.Empty;

    /// <summary>A 24 × 24 stroke glyph for its mark (such as an <c>ArcIcons</c> geometry); null shows its initial.</summary>
    public Geometry? Glyph { get; set; }

    /// <summary>The color family of its mark.</summary>
    public AgentGalleryTint Tint { get; set; } = AgentGalleryTint.Accent;

    /// <summary>Average rating out of five; zero shows "New" instead of a rating.</summary>
    public double Rating { get; set; }

    /// <summary>How many ratings the average is made of.</summary>
    public int RatingCount { get; set; }

    /// <summary>How many workspaces installed it (shown compact: 12.4K).</summary>
    public long Installs { get; set; }

    /// <summary>When it joined the gallery (the Newest order).</summary>
    public DateOnly AddedOn { get; set; }

    /// <summary>True shows a Featured badge on its card.</summary>
    public bool IsFeatured { get; set; }

    /// <summary>Short capability tags; the card shows the first two, search matches all of them.</summary>
    public IReadOnlyList<string> Tags { get; set; } = [];

    /// <summary>What it can do, one short sentence each, listed in the detail view.</summary>
    public IReadOnlyList<string> Abilities { get; set; } = [];

    /// <summary>What it needs access to, listed in the detail view.</summary>
    public IReadOnlyList<AgentGalleryPermission> Permissions { get; set; } = [];

    /// <summary>Prompts to try, shown as buttons in the detail view (they raise <see cref="AgentGallery.PromptChosen"/>).</summary>
    public IReadOnlyList<string> SamplePrompts { get; set; } = [];
}
