---
title: Branch graph
summary: A git-style history drawn commit by commit; branches split off into their own lanes and merge back, with branch and tag labels and each commit's history on selection.
---

<Demo name="History" />

## When to use

`BranchGraph` draws a version history the way git tools do: one row per commit, each branch in its own colored lane,
a curve where a branch splits off and another where it merges back, merge commits drawn as rings, and pills for each
branch's newest commit and for tags. As a story, it draws the history commit by commit (the edges draw in, the dot pops,
the branch label moves up) while a caption says what each commit did: a first commit, a commit on a branch, a new
branch, a merge with its two parents. Selecting a commit lights everything it descends from and dims the rest, which
answers "is this fix in that release?" at a glance.

- **Docs and courses about version control**: branching, merging and release branches, shown rather than told.
- **Release notes and changelogs**: how a release came together, with its tag.
- **Developer tools**: a compact history next to a pull request or a deploy.

## When not to use

- **Long histories**: every commit is a row and nothing is virtualized; keep it to a few dozen commits. Use a `Table`
  or a dedicated log view for full repositories.
- **Diffs and file changes**: the graph shows the shape of the history, not what changed.
- **Many cards on a page**: each animates while it plays. Set `StartsFinished` on the ones that only need the picture.

## Usage

The card needs no data: it plays an invented history of nine commits on `main`, a feature branch and a hotfix branch,
with two merges and a release tag.

```xml
<arc:BranchGraph />
```

`BranchGraph` derives from `Explainer`, which supplies the story clock (`IsPlaying`, `Speed`, `IsLooping`, `Play()`,
`Pause()`, `StepForward()`, `Restart()`), start on view, the playback buttons and the polite live region; this control
adds the speed control and the graph.

### Your own history

Add `BranchGraphCommit` items oldest first. `Branch` picks the lane (default `main`). Without `Parents` a commit's
parent is the previous commit on its branch, or, for a branch's first commit, the previous commit of any branch; give
`Parents` to branch from somewhere else or, with two ids (the branch's own first), to merge. Lanes are given in the
order branches appear and reused once a branch has been merged.

<Demo name="PlainGraph" />

```xml
<arc:BranchGraph Title="Release branch" StartsFinished="True" IsNewestFirst="True" AreControlsVisible="False">
    <arc:BranchGraphCommit Id="5e1a20" Branch="main" Message="Start the 2.0 cycle" Author="Rhea" />
    <arc:BranchGraphCommit Id="6f2b31" Branch="release/2.0" Message="Freeze features for 2.0" Author="Rhea" />
    <arc:BranchGraphCommit Id="7a3c42" Branch="main" Message="Add dark mode settings" Author="Sam" />
    <arc:BranchGraphCommit Id="8b4d53" Branch="release/2.0" Message="Fix crash on startup" Author="Rhea" Tag="v2.0.0" />
    <arc:BranchGraphCommit Id="9c5e64" Branch="main" Message="Merge release/2.0" Parents="7a3c42, 8b4d53" Author="Sam" />
</arc:BranchGraph>
```

`StartsFinished` opens on the whole history (Play still replays it), `IsNewestFirst` puts the newest commit on top
as `git log` does, and `ItemsSource` takes the commits from a view model. `RevealedCount` reports how far the story is
and `CommitDrawn` is raised for each commit with its `Kind`: `Commit`, `Branch` or `Merge`.

### Selection

A click on a commit, or the arrow keys on the focused graph, sets `SelectedCommit` (two way). The commit's row is
highlighted, everything it descends from stays lit and the rest dims. Click it again or press Escape to clear.

<Demo name="Selection" />

`Labels` holds every word the card shows or speaks; replace it to translate or reword.

### Reduced motion

Under reduced motion the card opens on the whole history. Play and Next step still work, and each commit appears in
place.

<Demo name="ReducedMotion" />

## API reference

<!-- api: BranchGraph -->

<!-- api: BranchGraphCommit -->

<!-- api: BranchGraphLabels -->

<!-- api: BranchGraphCommitEventArgs -->

`BranchGraphSamples.CreateCommits()` returns a fresh copy of the sample history.

## Accessibility

- The card is a group named by `Title` ("Branch history"). The status line says Ready, Drawing, Paused or Up to date.
- The graph is one tab stop. Up and Down move the selection from commit to commit, Home and End go to the first and
  last row, Escape clears it. Its accessible name is the selected commit ("Merge feature/search, 2c8d0e on main by
  Mira, v1.2.0"), or the newest one drawn.
- The live region speaks the caption only when the reader drives the story (a step, a pause, a restart).
- Lanes are told apart by position as well as color, merge commits by their ring, and branch names are written in the
  pills.

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Edges into a new commit | Draw in over 0.5 s | Appear | Appear |
| Commit dot | Pops in with a small overshoot | Appears | Appears |
| Row text and pills | Fade in | Appear | Appear |
| Caption | Crossfade | Changes in place | Changes in place |

Each commit takes 1.6 seconds at speed 1. Everything runs on the story clock, so it pauses, steps and scales with
`Speed`.

## Responsive behavior

- The lanes take 18 px each; the message takes the rest of the row and is cut with an ellipsis.
- From 480 px the author and short id show on the right of each row; narrower, they are left out (screen readers still
  hear them).
- The card is at most 760 px wide; under 560 px its padding tightens and under 420 px the "Speed" words hide.
- A tap selects a commit, as a click does.

## Related

| Component | Choose it when |
|---|---|
| `Timeline` | Events in order, without branches |
| `ReasoningTree` | The story is a search that branches and prunes |
| `AgentTimeline` | Several agents working over time |
