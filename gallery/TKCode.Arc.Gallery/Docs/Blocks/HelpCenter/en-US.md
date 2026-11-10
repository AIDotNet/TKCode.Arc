---
title: Help center
summary: A help center home with search, topics and popular articles, and an article view with a breadcrumb, a table of contents that jumps to each section, related articles and a was-this-helpful question.
---

<Demo name="Preview" />

## When to use

`HelpCenter` is the home of a product's help articles. A search field finds articles as you type, topics group them,
and the most read ones are listed up front. An article opens with a breadcrumb back to its topic, a table of contents
that jumps to each section, related articles, and a "Was this article helpful?" question that asks what was missing
after a No.

- **A help site or an in-app help page** with tens to a few hundred articles.
- **A product knowledge base** for customers or for your own team.
- **Onboarding guides** grouped by task.

## When not to use

- **A small help panel in the corner of a page**: use `SupportWidget`.
- **A single long document**: use `Markdown` with your own outline.
- **Full-text search over thousands of documents**: run the search on a server and show its results in your own list.

## Usage

```xml
<arc:HelpCenter x:Name="Help" Title="How can we help?" Height="680" />
```

```csharp
Help.FeedbackGiven += (_, e) => api.Rate(e.Article.Id, e.IsHelpful, e.Comment);
Help.Categories.Add(new HelpCenterCategory("billing", "Billing", "Invoices and plans", ArcIcons.CreditCard));
Help.Articles.Add(new HelpCenterArticle("refunds", "billing", "How refunds work", "When the money comes back.", markdown)
{
    IsPopular = true,
    UpdatedAt = DateTimeOffset.Now,
});
```

Articles are Markdown. Their `##` and `###` headings make the table of contents. `IsPopular` lists an article on the home,
and the reading time is counted from the body. Search matches every word of the query in the title, the summary or the
body, titles first; `Enter` opens the first result. A help center with no articles when it first loads shows a sample.

`SearchText`, `CurrentCategory` and `CurrentArticle` are two-way, and `View` says which page shows. `OpenArticle`,
`OpenCategory`, `GoHome` and `GoBack` navigate from code. Give the block a height so its pages scroll inside it.

### Narrow

<Demo name="Narrow" />

## API reference

<!-- api: HelpCenter -->

<!-- api: HelpCenterView -->

<!-- api: HelpCenterCategory -->

<!-- api: HelpCenterArticle -->

<!-- api: HelpCenterArticleEventArgs -->

<!-- api: HelpCenterFeedbackEventArgs -->

## Accessibility

- The home title is the page heading; topics, lists and the table of contents are named groups, and every row is a button named by its title and reading time.
- The number of search results and the opened article are announced politely; the breadcrumb is a row of buttons.
- The feedback question keeps focus in place and announces the thanks.

### Keyboard

| Keys | Action |
|---|---|
| `/` | Focus the search field (home and results). |
| `Enter` in the search field | Open the first result. |
| `Escape` | From an article or a topic, go up one level. In the search field, clear it. |
| `Alt+←` | Go up one level. |

## Motion

- Pages slide sideways: forward when you go deeper, back when you go up.
- The feedback box swaps its content with a short lift.
- Reduced motion turns the slide into a fade; with motion off pages change at once.

## Responsive behavior

- Below 720 px (`:narrow`, `NarrowWidth`) topics take one column and the table of contents hides; the breadcrumb wraps.
- Wider, topics take up to three columns and the table of contents sits beside the article (200 px, `TocWidth`).

## Related

- `SupportWidget` for help in the corner of any page.
- `Markdown` for rendering a single article.
- `SearchResults` for a workspace search page with filters.
