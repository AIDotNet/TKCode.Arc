---
title: Vector search
summary: A question is embedded as a vector and answered with its nearest documents by cosine similarity.
---

<Demo name="HelpCentreSearch" />

## When to use

- Explaining how a semantic search finds answers that share no exact words with the question.
- Help-centre and product docs that show how a question is matched against articles.
- Teaching how embeddings, cosine similarity and a top-k list fit together.

## When not to use

- Answering real user questions in an app: the built-in embeddings are hashed words and trigrams, not a trained
  model, so use your own search service and show its results with `SearchField`.
- Exact or filtered lookups such as an order number: use `SearchField` with a normal index.
- Plotting real measurements: use `LineChart` from the Charts category.

## Usage

The story types a question, embeds it, finds its nearest documents and lists them with their similarity. The
built-in index has 32 help-centre articles in four topics and three example questions.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:VectorSearch />
</UserControl>
```

### Number of answers

`TopK` sets how many answers are listed (1 to 5, default 3). The list is best first, and the map circles the
farthest of the answers.

```xml
<arc:VectorSearch TopK="4" />
```

### Your own documents and questions

`Documents` takes `VectorSearchDocument` values (a title, a topic and optional keywords). `Queries` takes the
questions the story cycles through. Both replace the defaults when set.

<Demo name="YourDocuments" />

### Searching for a question

`Search(query)` shows a question at once, and the story carries on with its own questions afterwards. `Query` is
the question on screen and `Results` holds its answers. `ResultsChanged` is raised when the answers are known.

<Demo name="AskYourOwn" />

Each `VectorSearchResult` has `Document`, `Score` (the cosine similarity) and `Rank` (1 is the best match).

### Labels

`Labels` takes a `VectorSearchLabels` object with the visible words: the model name, the list header and the step
captions.

```csharp
search.Labels = new VectorSearchLabels { Model = "Text model", Header = k => $"Best {k} matches" };
```

### Reduced motion

Under reduced motion the illustration is a still frame of the answers to the first question.

<Demo name="ReducedMotion" />

## API reference

<!-- api: VectorSearch -->

<!-- api: VectorSearchLabels -->

## Accessibility

- `Label` is the accessible name, and the current caption is exposed as the help text.
- The illustration is focusable and has a 2px focus ring.
- Space or Enter plays and pauses the story, and R starts it over.
- Caption changes are not announced. The ranked answers are also available as text through `Results`, so show them
  in a list that users can read and navigate.

## Motion

- The question is typed letter by letter with a blinking caret, then the embedding model works on it.
- The vector bar fills one cell at a time, and the query point flies into the document map.
- A radius grows until it reaches the last answer, the answers light up in rank order and their rows rise into the list.
- Reduced motion and Off show the answers to the first question as a still frame, with no caret and no typing. The
  play button is hidden.

## Related

- `SearchField` for the question input in an app, and for showing your own results.
- `DataFlow` for how the documents that feed a search are collected and stored.
- `SortableDataTable` for showing a long list of results with their scores.
