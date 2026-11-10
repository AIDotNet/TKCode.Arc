using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// An open <see cref="HelpCenter"/> article: the title with its date and reading time, the Markdown body, "Was this
/// helpful?", related articles, and (when wide) a table of contents built from the body's <c>##</c> and <c>###</c>
/// headings that scrolls to each section.
/// </summary>
internal sealed class HelpCenterArticlePage : Grid
{
    private readonly HelpCenter _owner;
    private readonly HelpCenterArticle _article;
    private readonly ArcContentSwap _feedback;
    private Textarea? _comment;

    public HelpCenterArticlePage(HelpCenter owner, HelpCenterArticle article, bool narrow)
    {
        _owner = owner;
        _article = article;
        ColumnDefinitions = ColumnDefinitions.Parse(narrow ? "*" : "*,Auto");
        ColumnSpacing = narrow ? 0 : 32;

        var meta = article.UpdatedAt is { } updated
            ? $"Updated {updated.ToString("MMM d, yyyy", HelpCenter.Culture)} · {article.ReadingMinutes} min read"
            : $"{article.ReadingMinutes} min read";
        Markdown = new Markdown { Text = article.Body };
        _feedback = new ArcContentSwap { Mode = ContentSwapMode.Lift, HorizontalContentAlignment = HorizontalAlignment.Stretch, Content = Question() };

        var related = owner.Articles.Where(a => a.CategoryId == article.CategoryId && !ReferenceEquals(a, article)).Take(3).ToList();
        var body = new StackPanel
        {
            Spacing = 16,
            Children =
            {
                new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        SupportKit.Heading(article.Title, ArcDesignTokenKind.Type2XL, 1),
                        SupportKit.Text(meta, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS),
                    },
                },
                Markdown,
                SupportKit.Panel(_feedback, ArcDesignTokenKind.RadiusStep16, ArcDesignTokenKind.SurfaceMuted, new Thickness(16), edge: false),
            },
        };
        if (related.Count > 0)
        {
            var heading = SupportKit.Text("Related articles", ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, FontWeight.SemiBold);
            AutomationProperties.SetHeadingLevel(heading, 2);
            body.Children.Add(new StackPanel { Spacing = 6, Children = { heading, owner.ArticleList(related, showCategory: false) } });
        }
        Scroller = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Children.Add(Scroller.At(0));

        Outline = Headings(article.Body);
        if (!narrow && Outline.Count > 0)
        {
            var toc = new StackPanel { Spacing = 2, Width = owner.TocWidth };
            var label = SupportKit.Text("On this page", ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, FontWeight.SemiBold);
            label.Margin = new Thickness(8, 0, 0, 6);
            toc.Children.Add(label);
            foreach (var (level, text) in Outline)
            {
                var link = new SupportKitRow
                {
                    Content = SupportKit.Text(text, level == 2 ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, wrap: true),
                    Item = text,
                    Padding = new Thickness(level == 2 ? 8 : 20, 5, 8, 5),
                };
                AutomationProperties.SetName(link, $"Go to {text}");
                link.Click += (_, _) => GoTo(text);
                toc.Children.Add(link);
            }
            AutomationProperties.SetName(toc, "On this page");
            Toc = toc;
            Children.Add(toc.At(1));
        }
    }

    public Markdown Markdown { get; }

    public ScrollViewer Scroller { get; }

    public StackPanel? Toc { get; }

    public IReadOnlyList<(int Level, string Text)> Outline { get; }

    public ArcContentSwap Feedback => _feedback;

    /// <summary>Scrolls to the section headed <paramref name="heading"/>.</summary>
    public bool GoTo(string heading) => Markdown.ScrollToAnchor(HelpCenter.Slug(heading));

    /// <summary>The <c>##</c> and <c>###</c> headings of <paramref name="markdown"/>, outside code fences.</summary>
    internal static List<(int Level, string Text)> Headings(string markdown)
    {
        var result = new List<(int, string)>();
        var fenced = false;
        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;
                continue;
            }
            if (fenced)
            {
                continue;
            }
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                result.Add((3, line[4..].Trim()));
            }
            else if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                result.Add((2, line[3..].Trim()));
            }
        }
        return result;
    }

    #region Was this helpful?

    private Control Question()
    {
        var yes = SupportKit.Button("Yes", ButtonVariant.Secondary, ArcIcons.ThumbsUp);
        var no = SupportKit.Button("No", ButtonVariant.Secondary, ArcIcons.ThumbsDown);
        yes.Click += (_, _) => Answer(true);
        no.Click += (_, _) => AskWhy();
        var text = SupportKit.Text("Was this article helpful?", ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, FontWeight.Medium);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { yes, no } };
        return new WrapPanel { ItemSpacing = 16, LineSpacing = 10, Children = { text, buttons } };
    }

    private void AskWhy()
    {
        _comment = new Textarea { Label = "What was missing?", Placeholder = "Tell us what you were looking for (optional)", FrameHeight = 72 };
        var send = SupportKit.Button("Send", ButtonVariant.Primary);
        var skip = SupportKit.Button("Skip", ButtonVariant.Ghost);
        send.Click += (_, _) => Answer(false, _comment.Text);
        skip.Click += (_, _) => Answer(false);
        _feedback.Content = new StackPanel
        {
            Spacing = 10,
            Children = { _comment, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { skip, send } } },
        };
        _comment.Focus(BlockFocusModality.For(this));
    }

    /// <summary>Records the answer and shows the thank-you (tests call it directly).</summary>
    public void Answer(bool helpful, string? comment = null)
    {
        var within = _feedback.IsKeyboardFocusWithin;
        var thanks = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                SupportKit.Glyph(ArcIcons.CircleCheck, 18, ArcDesignTokenKind.Success),
                SupportKit.Text(helpful ? "Thanks! Glad it helped." : "Thanks. We will use this to improve the article.", ArcDesignTokenKind.Foreground, wrap: true),
            },
        };
        AutomationProperties.SetLiveSetting(thanks, AutomationLiveSetting.Polite);
        _feedback.Content = thanks;
        if (within)
        {
            Scroller.Focus(NavigationMethod.Tab);
        }
        _owner.RaiseFeedback(_article, helpful, string.IsNullOrWhiteSpace(comment) ? null : comment.Trim());
    }

    #endregion
}
