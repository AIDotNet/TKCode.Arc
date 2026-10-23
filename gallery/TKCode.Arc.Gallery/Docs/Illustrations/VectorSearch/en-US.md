---
title: Vector search
summary: A question is embedded as a vector and answered with its nearest documents by cosine similarity.
---

<Demo name="HelpCentreSearch" />

## When to use

`VectorSearch` is an animated illustration of semantic search. It types a question, turns it into a vector, places it on
a map of document vectors and lists the documents closest to it as ranked answers. Put it beside the copy that explains
a search feature, where a reader needs to see why an answer matched a question that shares no exact words with it.

- **Help-centre landing pages for a billing product**: a visitor asks "why was I charged twice" and sees the article on
  duplicate payments rank first, although the words differ. The picture sets expectations before the visitor uses the
  real search box.
- **Onboarding for an AI-assisted support console**: a new support agent sees how the assistant chose its suggested
  articles, which builds trust in the ranking and explains why a rephrased question can change the answers.
- **Product tours for an analytics workspace**: a tour step that introduces "Ask your data" shows how a plain question
  finds the saved reports it is about.
- **Developer documentation for a search feature**: a tutorial on embeddings, cosine similarity and top-k results uses
  the live map and the ranked list as the reference picture for the text beside it.
- **Training for knowledge-base authors**: the ranked list and its similarity scores show authors which titles and
  keywords carry a match, so they can improve the articles that rank poorly.

## When not to use

- **Answering real customer questions**: the built-in index uses hashed words and character trigrams, not a trained
  model, so it is a teaching tool. Use your own search service and show its results with `SearchField`.
- **Exact or filtered lookups**: an invoice number, an order ID or a date range needs an exact index with filters. Use
  `SearchField` with a normal query, not a similarity ranking.
- **Plotting real measurements**: the map is a two-dimensional projection of text similarity, with no units or axes.
  Use `LineChart` or `BarChart` for values that people compare.
- **Long result lists people act on**: rank, score and document metadata in columns are easier to scan, sort and export
  in `SortableDataTable`.
- **Explaining a pipeline rather than a search**: when the picture should show how documents are collected, validated
  and stored, use `DataFlow`, which is built for that story.
- **Dense dashboards or pages with several animated pictures**: each illustration redraws its canvas every frame while
  it plays. Use a `Card` with a short static explanation instead.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:VectorSearch />
</UserControl>
```

`VectorSearch` derives from `Illustration`, which derives from `TemplatedControl`. The base class runs the looping
story on a scene clock, gives the control focus, adds the play and pause button and the caption line, and raises
`StepChanged` as the story moves between its four steps. The clock pauses while the control is off-screen, hidden or
detached, so a story below the fold costs nothing until it scrolls into view. `VectorSearch` adds the search itself: it
builds an in-memory index of its documents, embeds each question, ranks the documents by cosine similarity and draws
the result. The default story cycles through three example questions, so one control shows the whole path from a
question to its answers.

### Anatomy

The diagram is drawn on one canvas that scales to the width the control is given, so the proportions below hold at any
size. The padding of the frame is 20 px on the sides and top and 14 px at the bottom.

1. **Query box** (`Query` at run time): the question at the top left, typed one character at a time with a caret.
2. **Embedding model node** (`Labels.Model`, with `Labels.ModelDetail` under it): a card with a brain icon. It shows a
   busy ring while the model works on the question.
3. **Vector strip**: 16 cells that show the question's vector as a fixed projection of the whole vector. The cells
   fill from the query's real embedding, so the same question always fills them the same way.
4. **Document map**: each document is a point, grouped by its `Topic` with the topic name drawn faintly in the group.
   The query is a point placed among its nearest documents, with a ring that reaches the farthest answer.
5. **Result list**: a header (`Labels.Header`, "Top 3 · cosine similarity" by default), then one row per answer with
   its rank, title, similarity score to two decimals and a bar. Titles that are too long for the row are cut with an
   ellipsis on a single line.
6. **Caption line** (`Caption`): one line under the diagram in the secondary text color at the small type size. It
   names the current step. Hide it with `IsCaptionVisible="False"`.
7. **Play button** (`IsPlaybackButtonVisible`): a 28 px ghost circle at the bottom right that swaps between a play and
   a pause icon. It is hidden under the still frame.
8. **Frame**: a Surface card with a 1 px Border line, the surface corner radius and a dotted backdrop. A 2 px focus ring
   in the focus color appears around the frame when the control has keyboard focus.

Node cards use a corner radius of 12 px. The colors all come from ArcDesign tokens (the surface, border, text, accent
and status colors), so the control follows the light and dark themes without any color setting.

### Number of answers

`TopK` sets how many answers the list shows. The value is clamped to the range 1 to 5, and the default is 3. The list is
always best first, and the map draws the ring to the farthest answer, capped at 110 scene units so that one outlier
does not swallow the map.

```xml
<arc:VectorSearch TopK="4" />
```

Use 3 on a help page. It fits a reading column, and the best match stands out from the rest. Use 5 only when the list
itself is the point of the explanation, because the ring grows with every answer. Changing `TopK` rebuilds the scene,
so set it once when the page is built rather than on every interaction.

### Your own documents and questions

`Documents` takes `VectorSearchDocument` values. The record has three parts: `Title` is the text the result list shows,
`Topic` groups the document on the map and labels its cluster, and `Keywords` (optional) adds words to the document's
embedding without showing them. `Queries` takes the questions the story cycles through. Both replace the defaults when
they are set and have at least one item.

<Demo name="YourDocuments" />

```csharp
search.Documents =
[
    new VectorSearchDocument("Cancel and refund an annual plan", "Billing", "money back refund"),
    new VectorSearchDocument("Reset two-factor authentication", "Security", "phone lost new device"),
    new VectorSearchDocument("Track a delayed parcel", "Shipping", "late package courier"),
];
search.Queries = ["get my money back", "I lost my phone"];
```

Write titles as the answer a reader would click, and put the words readers actually type in `Keywords`. Keywords
count toward the match, so a title can stay short and plain. Keep the topic names few and broad (four to six is a
good range): each topic becomes one cluster on the map, and many small clusters make the map hard to read.

### Searching for a question

`Search(query)` shows a question at once: it cancels the scheduled steps, embeds the question and starts its story. A
blank or white-space string does nothing. After the answers of that question, the story carries on with the questions
in `Queries`. `Query` is the question on screen, `Results` holds its answers, best first, and `ResultsChanged` is raised
each time the answers of a question are known, including for the questions the story plays on its own.

<Demo name="AskYourOwn" />

Each `VectorSearchResult` has `Document`, `Score` (the cosine similarity, from −1 to 1) and `Rank` (1 is the best match).
`Results` is a plain property and does not notify, so read it in the `ResultsChanged` handler rather than binding to it.

### Labels

`Labels` takes a `VectorSearchLabels` object with the visible words: the model name and its detail line, the list
header, and the four step captions (`Typing`, `Embedding`, `Comparing` and `Answers`). Its `Summary` is the caption of
the still frame. Replace the whole object to change the words, because the header and the model node are read when the
scene is built.

```csharp
search.Labels = new VectorSearchLabels { Model = "Text model", Header = k => $"Best {k} matches" };
```

### Playback, speed and captions

The illustration plays by default. `IsPlaying` is two-way: bind it to a toggle that the page shows, or call `Play()` and
`Pause()`. `Restart()` starts the story from its first step. The preview at the top of this page uses speed buttons, and
the same settings are available to you in code.

| Member | Use it to |
|---|---|
| `Speed` | Scale the story clock from 0.25 to 4 (default 1). Slow it for a tour that explains each step, and speed it up for a teaser. |
| `IsCaptionVisible` | Hide the caption line when the page already explains the steps in its own text. |
| `IsPlaybackButtonVisible` | Hide the play and pause button on a page where the picture must stay still. |
| `Caption` and `Step` | Read the current caption and its step (0 to 3). The read-only values also back the accessible help text. |
| `StepChanged` | React to each step, for example to highlight the matching sentence in the text beside the picture. |
| `Label` | Name the illustration for assistive technology (default "Vector search illustration"). |

### Reduced motion

Under reduced motion the illustration is a still frame of the answers to the first question. The caption summarizes the
whole story, the play button is hidden, and no caret blinks. Set the level on the control, as the demo does, or on a
parent that holds the page's motion setting.

<Demo name="ReducedMotion" />

### Using motion with restraint

The story helps when a reader has to see a path: the question becomes a vector, the vector finds its neighbours, and
the neighbours become the answers. Keep it on a page where the reader has time to watch one question finish, which takes
about 6.5 seconds in Full motion at speed 1. It distracts when it sits beside a form, a checkout step or a live
dashboard, where the eye should stay on the field the reader is filling in. Place at most one or two illustrations on a
page, and let the story run only while the picture is on screen. The clock already pauses off-screen, so a long page
does not pay for an illustration that the reader has not reached yet.

### Binding to a view model

In an MVVM app the view model owns the documents and the questions, and the control only shows them. The view model
below uses CommunityToolkit.Mvvm. The control's `Documents`, `Queries`, `TopK` and `Labels` are one-way inputs: a new
value rebuilds the index and the map. `IsPlaying` is the only two-way value.

```csharp
public partial class HelpCentreViewModel : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<VectorSearchDocument> _articles = [];

    [ObservableProperty]
    private IReadOnlyList<string> _sampleQuestions = [];

    [ObservableProperty]
    private string _topMatchTitle = string.Empty;

    [ObservableProperty]
    private bool _isStoryPlaying = true;

    public void Load()
    {
        // Assigning a new list raises PropertyChanged, so the control rebuilds its index once.
        Articles =
        [
            new VectorSearchDocument("Cancel and refund an annual plan", "Billing", "money back refund"),
            new VectorSearchDocument("Reset two-factor authentication", "Security", "phone lost new device"),
            new VectorSearchDocument("Track a delayed parcel", "Shipping", "late package courier"),
        ];
        SampleQuestions = ["get my money back", "I lost my phone"];
    }
}
```

```xml
<arc:VectorSearch Documents="{Binding Articles}"
                  Queries="{Binding SampleQuestions}"
                  IsPlaying="{Binding IsStoryPlaying, Mode=TwoWay}"
                  TopK="3"
                  Label="Help centre search illustration" />
```

`Search` is a method and `Results` is a plain property, so they stay in code-behind. Forward the top match to the view
model from the `ResultsChanged` handler, and call `Search` from the handler of the page's search field:

```csharp
// In the code-behind of the view that hosts the control.
Search.ResultsChanged += (_, _) =>
{
    ViewModel.TopMatchTitle = Search.Results.Count > 0 ? Search.Results[0].Document.Title : string.Empty;
};

private void OnAskSubmitted(object? sender, SearchSubmittedEventArgs e)
{
    Search.Search(e.Query);
}
```

### Data shape and performance

- **The item model**: a `VectorSearchDocument` is a record of `Title`, `Topic` and `Keywords`. The index embeds each
  document once, when the index is built. Each question is embedded when it is searched, so a question costs one
  embedding and one ranking pass over the documents.
- **Size**: the default index holds 32 help-centre articles in four topics. The index is in memory and has no paging or
  persistence. The map runs a fixed number of stress-minimization steps (300) over the document set. It is recomputed
  only when the documents change, not on each question. The illustration is designed for a teaching-sized index: a real
  help centre with thousands of articles belongs in your search service, and this control explains how it works.
- **Updates**: replacing `Documents`, `Queries`, `TopK` or `Labels` rebuilds the scene. Editing the items of a list in
  place does not, because the control does not observe the list. Replace the list with a new one instead.
- **Formatting**: the rank and the score text use the invariant culture with two decimals (for example "0.87"), so the
  numbers read the same in every locale. Translate the words through `Labels`, not the numbers.

### Example: help-centre search for a billing question

A help centre landing page for a subscription product. The search field asks the visitor to type a question, and the
illustration beneath it explains how the articles are ranked. The text is short and says what the picture means, the
search field has a placeholder that shows the kind of question to ask, and the illustration keeps the default story so
the reader sees three different questions match three different topics. The width of 720 px suits a reading column
on a desktop page, and the frame keeps its padding at every width.

![A help-centre search illustration beside a search field and a short explanation of the ranked articles](images/HelpCentreSearch.png)

```xml
<StackPanel Width="720" Spacing="16">
    <TextBlock Text="Help centre"
               FontSize="20"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:SearchField Placeholder="Ask the help centre" />
    <arc:VectorSearch TopK="3" />
    <TextBlock Text="Articles are ranked by how closely their meaning matches your question."
               FontSize="13"
               TextWrapping="Wrap"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
</StackPanel>
```

### Content guidelines

- **Label**: name what the picture shows, in sentence case: "Help centre search illustration", not "Vector search".
  Screen readers read it first, so it should make sense without the picture.
- **Captions and headers**: write for the reader, not the mathematics. The default header uses the term "cosine
  similarity", which suits developers. Customer pages usually say what the number means.
- **Document titles**: write the answer a reader would click, in sentence case, and keep the title short enough to fit
  one line of the result list. Put synonyms and misspellings readers use in `Keywords`.
- **Topics**: a short noun in sentence case, the same word the navigation uses: "Billing", "Security", "Shipping".

| Instead of | Write |
|---|---|
| Top 3 · cosine similarity | Best 3 matches |
| Vector search illustration | Help centre search illustration |
| Embedding model | Text model |
| Query | Question |
| The vector is compared with 32 document vectors | Comparing 32 articles |

### Best practices

**Do**

- Put the illustration next to the real search field, so readers can connect the picture with their own question. The
  picture explains the ranking; it does not perform the search for the page.
- Keep `TopK` at 3 on help and marketing pages. A short list fits the column and makes the best match easy to see.
- Put the words readers type into `Keywords`. They improve the match without adding clutter to the titles the reader sees.
- Expose the same answers as text when the page needs them for accessibility, using `Results`, in a list the reader can
  navigate. The drawn list is not an accessible list.

**Avoid**

- Presenting the built-in ranking as the quality of your product's search. The embeddings are hashed words and
  trigrams, so the examples teach the method rather than measure your content.
- Several story illustrations on one screen. Each redraws its canvas every frame while it plays, so the page becomes
  heavy and the reader cannot follow more than one story at once.
- Editing items in place in a document list. The control rebuilds only when the list is replaced, so the map and the
  results keep the old documents.
- Long titles that rely on the full text. The result list cuts a long title with an ellipsis on one line, so the
  important words must come first.

### Common questions

**What happens when I call `Search` while the story is running?** The scheduled steps are cancelled and the question is
shown at once. The story then carries on with the `Queries` list from the point where it was, so a single search does
not restart the loop.

**Why does the map move when I add one document?** The map is laid out from all the documents at once, so one new
document changes the positions of the others. The ranking does not change in the same way: the order of the answers
depends only on the cosine similarity between the question and each document.

**Which words count toward a match?** Words are lowercased, common words such as "the", "my" and "how" are dropped, and
plural, `-ing` and `-ed` endings are trimmed. Each word and its character trigrams go into the vector, and trigrams
count for 0.3 of a word, so "bill" and "billing" meet part way. `Keywords` count the same way but are never shown.

**Can I stop the story and still read the answers?** Yes. Set `IsPlaying="False"` to hold the current frame, and read
`Results` at any time. Under reduced motion the control shows the answers to the first question as a still frame.

## API reference

<!-- api: VectorSearch -->

<!-- api: VectorSearchLabels -->

The two records that the page uses are public companion types in the same namespace. Their parameters come from their
source, in the order of the primary constructor.

| Record | Parameters | Meaning |
|---|---|---|
| `VectorSearchDocument` | `Title`, `Topic`, `Keywords` (optional) | A document to search. `Title` is shown in the list, `Topic` names its cluster, and `Keywords` count toward its embedding but are not shown. |
| `VectorSearchResult` | `Document`, `Score`, `Rank` | One answer. `Score` is the cosine similarity from −1 to 1, and `Rank` is 1 for the best match. |

## Accessibility

- `Label` is the accessible name, and the current caption is exposed as the help text.
- The illustration takes keyboard focus and shows a 2 px focus ring around its frame.
- The caption and the drawn result list are not announced when they change. The ranked answers are also available as
  text through `Results`, so show them in a list that users can read and navigate.
- Under reduced motion the control shows its still frame, so no content depends on the motion to be understood.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move focus to or from the illustration |
| `Space` or `Enter` | Play or pause the story (when the illustration itself has focus) |
| `R` | Start the story over from its first step |

## Motion

- The question is typed letter by letter with a blinking caret, then the embedding model works on it.
- The vector strip fills one cell at a time, and the query point flies into the document map.
- A radius grows until it reaches the last answer, the answers light up in rank order and their rows rise into the list.
- A question takes about 6.5 seconds in Full motion at speed 1, then the next question of the story begins.
- Reduced motion and Off show the answers to the first question as a still frame, with no caret and no typing. The
  play button is hidden.

| Change | Full | Reduced | Off |
|---|---|---|---|
| The question appears | Typed letter by letter with a blinking caret | Shown in full, no caret | Shown in full, no caret |
| The model works on the question | The model node shows a busy ring | No busy ring | No busy ring |
| The vector strip | Cells fill one at a time | Final values | Final values |
| The query point | Flies from the strip into the map | Placed at its final position | Placed at its final position |
| The answers | The radius grows to the last answer and the neighbours light up in rank order | Final ring and answers | Final ring and answers |
| The result rows | Rise into the list in rank order | Listed at once | Listed at once |

## Related

- `SearchField` for the question input in an app, and for showing your own results.
- `DataFlow` for how the documents that feed a search are collected and stored.
- `SortableDataTable` for showing a long list of results with their scores.

| Component | Choose it when |
|---|---|
| `SearchField` | The reader types a question into your product and your own search service answers it |
| `ExpandingSearch` | Search sits in a page header and opens on demand |
| `SortableDataTable` | The results are a long list that people sort, filter or export |
| `DataFlow` | The story is how documents are collected, validated and stored |
| `LineChart` | The values are measurements that people compare over time |
| `Card` | A static explanation is enough and an illustration would compete with the page |
