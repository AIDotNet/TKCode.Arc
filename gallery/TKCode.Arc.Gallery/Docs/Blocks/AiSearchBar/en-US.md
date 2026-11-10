---
title: AI search bar
summary: A search field that answers in plain language above the classic results, with suggested questions, numbered citations, source cards and questions to ask next.
---

<Demo name="Preview" />

## When to use

`AiSearchBar` is a search box that can also answer. Before anything is typed it offers a few suggested questions. When a
question is asked, a short answer in plain language appears first, with small numbered citations in the text that point to
the sources it used; the sources follow as cards, then questions to ask next, then the classic list of results. People get
the answer and can still check where it came from or open a result themselves.

- **Help centers and handbooks.** Questions such as "How many vacation days do new hires get?" deserve a sentence, not ten
  links.
- **Workspace search.** Search across documents, tickets and wikis, with the answer on top and the documents below.
- **Product docs.** Answer how-to questions and keep the pages one click away.

## When not to use

- **A conversation.** For follow-ups that build on each other, use the `AI chat` block.
- **Plain filtering.** To narrow a list by name or tag, use `SearchField` or `Search results`.
- **Answers without sources.** The block is built around citations; if your answers cannot cite anything, a chat reply is
  more honest.

## Usage

The block derives from Avalonia's `TemplatedControl`. Answers come from `AnswerProvider`; without one the bar answers from
an invented handbook (`AiSearchBarSamples`), so the demo works on its own.

```xml
<arc:AiSearchBar SuggestedQuestions="{Binding Questions}" />
```

```csharp
search.AnswerProvider = async (query, cancellation) =>
{
    var result = await assistant.AskAsync(query, cancellation);
    return new AiSearchAnswer
    {
        Text = result.Text,                 // "... from their first day [1]."
        Sources = result.Sources.Select(s => new AiSearchSource { Title = s.Title, Location = s.Path, Link = s.Url }).ToArray(),
        Results = result.Documents.Select(d => new AiSearchResult { Title = d.Title, Snippet = d.Snippet, Link = d.Url }).ToArray(),
        FollowUps = result.FollowUps,
    };
};
search.SourceInvoked += (_, e) => Open(e.SearchSource.Link);
search.ResultInvoked += (_, e) => Open(e.Result.Link);
```

### Anatomy

- **Field**: `Query` (two-way), the Ask button and a clear button that appears once there is something to clear.
- **Suggestions** (`:idle`): `SuggestedQuestions` as chips; pressing one asks it.
- **Searching**: a shimmering line and placeholder lines.
- **Answer** (`:answered`): the text with citation buttons where it says `[1]`, `[2]`; the sources as numbered cards; the
  follow-up questions as chips; the results as rows with title, location and snippet.
- **Failure** (`:failed`): the exception's message and a retry button.

### Your own answers

<Demo name="OwnAnswers" />

`State`, `Answer`, `AskedQuery` and `ErrorText` tell where the bar stands. `SearchAsync(query)` asks from code, `Cancel()`
stops a running search and `Clear()` empties everything. A newer question cancels the one still running, and only the
latest answer is shown.

### Words

`Labels` (`AiSearchBarLabels`) holds the placeholder, the buttons, the headings, the searching and error lines and the
screen reader names of the citations and the result count.

## API reference

<!-- api: AiSearchBar -->

<!-- api: AiSearchAnswer -->

<!-- api: AiSearchSource -->

<!-- api: AiSearchResult -->

<!-- api: AiSearchBarState -->

<!-- api: AiSearchBarLabels -->

## Accessibility

- The bar is a search landmark; the field is named by `Labels.Field`.
- **Enter** asks, **Escape** stops a running search or clears the bar, and **Down** moves from the field to the first
  suggestion, source or result.
- Each citation is a button named "Source 2: Travel and expenses"; each source card has the same name, each result is named
  by its title and described by its snippet.
- "Reading your workspace…", "Answer ready, 4 results" and errors are announced politely.

## Motion

- The answer and the suggestions fade in over 160 ms when they appear; the searching line shimmers while it waits.
- Chips, cards and rows tint on hover in 160 ms.
- Reduced motion and Off show each state at once; the shimmer follows its own motion rules.

## Responsive behavior

- The bar is at most 720 wide and fills narrower containers. At 440 or less (`:compact`) its padding tightens from 20 to
  16.
- Chips and source cards wrap onto new lines; source cards are at most 260 wide and shorten their text with an ellipsis.
- Result rows keep their title and location on one line and wrap the snippet.

## Related

- `SearchField` and `ExpandingSearch` for search without answers.
- `AI answer` for a long, article-like answer to one question.
- `Search results` for a full results page with filters.
