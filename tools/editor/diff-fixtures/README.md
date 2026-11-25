# Diff fixtures

`gen.ts` writes `tests/TKCode.Arc.Editor.Tests/Diff/TestData/diff-fixtures.json`: 406 diff inputs with the results of
vscode's own `DefaultLinesDiffComputer` (changes, inner changes, moves) and `LegacyLinesDiffComputer`, which
`DiffFixtureTests` compares the ported computers with. The inputs are the upstream legacy tests' inputs
(`legacy-inputs.json`, taken from `src/vs/editor/test/common/diff/diffComputer.test.ts`), a few crafted cases (moves,
sub-words, `\r`, surrogate pairs) and seeded random edits of code-like lines (insertions, deletions, character edits,
indentation changes, moved blocks), with random options; two large cases use the Myers path.

It runs vscode's TypeScript directly with [Bun](https://bun.sh) (which resolves the `.js` imports to `.ts` files):

```bash
git clone --depth 1 --filter=blob:none --sparse https://github.com/microsoft/vscode.git /tmp/vscode-ref/vscode
git -C /tmp/vscode-ref/vscode sparse-checkout set src/vs/base/common src/vs/editor/common
cd tools/editor/diff-fixtures
VSCODE_SRC=/tmp/vscode-ref/vscode/src bun run gen.ts ../../../tests/TKCode.Arc.Editor.Tests/Diff/TestData/diff-fixtures.json
```

The checked-in fixtures come from vscode commit `b57d5c9e`, the commit the diff engine is ported from.
