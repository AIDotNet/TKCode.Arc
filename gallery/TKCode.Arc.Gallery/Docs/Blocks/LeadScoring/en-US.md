---
title: Lead scoring
summary: Leads ranked by score with stage tabs, a search and score bands; choose a lead to see the fit, engagement and intent factors behind its score, and move one or many leads between stages.
---

<Demo name="Preview" />

## When to use

`LeadScoring` is a sales team's working list of leads. Each lead has a score from 0 to 100 made of factors (a fit,
engagement or intent signal worth some points, sometimes negative), so the number is never a black box: choose a row and
the panel beside the table lists what added and what took away. Stage tabs, a search and score bands (hot, warm, cold)
narrow the table; the stage switch in the panel moves one lead, and the bar over the table moves every selected row.

- **Daily triage.** Sort by score, open the hot leads and move them to Working.
- **Explaining a score.** Show a manager or a rep why a lead ranks where it does.
- **Cleaning up a stage.** Select the cold leads in New and move them to Nurture at once.

## When not to use

- **A full CRM record.** The panel explains the score; use a profile page for contact history and notes.
- **Boards with drag and drop between columns.** Use `Kanban board` when stages are the main view.
- **Scores you cannot explain.** Without factors the panel has nothing to show; set `ScoreOverride` only as an exception.

## Usage

The block derives from `TemplatedControl`. Without leads it shows an invented pipeline. Give it `Stages` and `Leads`; a
lead's `Score` adds up its `Factors` (clamped to 0–100) unless `ScoreOverride` is set.

```xml
<arc:LeadScoring Title="Event sign-ups" HotThreshold="70" WarmThreshold="40" />
```

```csharp
board.Stages.Add(new LeadScoringStage("met", "Met at booth"));
board.Leads.Add(new LeadScoringLead("a", "Paula Ito", "Fernway Labs", "met")
{
    Factors = [new("Decision maker", 25, LeadScoringFactorKind.Fit), new("Asked for pricing", 30, LeadScoringFactorKind.Intent)],
});
board.LeadsMoved += (_, e) => crm.SaveStage(e.Leads, e.Stage.Key);
```

### Filters and moves

`StageFilter`, `Band` and `SearchText` are two-way, so the filters can be driven from the page. `ActiveLead` is the lead in
the panel; choosing or focusing a row sets it. `MoveLeads` moves leads from code; every move writes `StageKey`, raises
`LeadsMoved` with the stages they came from, and is announced. Words are in `Labels` (`LeadScoringLabels`).

<Demo name="OwnLeads" />

## API reference

<!-- api: LeadScoring -->

<!-- api: LeadScoringLead -->

<!-- api: LeadScoringFactor -->

<!-- api: LeadScoringStage -->

## Accessibility

- The table is the Arc `Table`: its caption names it, rows are selectable with the keyboard and the header sorts.
- The stage tabs and score bands are segmented controls named "Stage" and "Score".
- The panel is named by the lead; each factor reads its kind, label and points, and the ring reads the score.
- Moves are announced ("Moved Paula Ito to Qualified").

## Motion

- The score ring chases its new value when another lead is chosen (Full motion only).
- The bulk bar fades in and out in 160 ms; Reduced motion makes it instant.
- The table's own sorting and selection motion applies.

## Responsive behavior

- At `CompactWidth` (860 px) or less the panel moves under the table and the padding tightens from 24 to 16.
- The filters wrap onto a second line when they do not fit.
- The table folds its rows below 620 px as the Arc `Table` does.

## Related

- `Table`, `SegmentedControl` and `Select`, the controls the block is made of.
- `Kanban board` for stage columns you drag between.
- `Call insights` and `Account health`.
