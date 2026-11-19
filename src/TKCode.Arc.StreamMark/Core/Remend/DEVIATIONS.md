# Remend port: deviations from the TypeScript original

Ported from vercel/streamdown `packages/remend` (remend 1.4.0), Copyright 2023 Vercel, Inc., Apache-2.0; modified.

Every case of the 29 TS test files passes (recorded as JSON fixtures by
`tools/remend-fixtures/extract.mjs`; calls with handler functions are ported by
hand). No TS test is excluded. Behavior on string input is identical to the TS
implementation, apart from the intentional differences below.

## Intentional differences

| Area | TypeScript | C# | Why |
| --- | --- | --- | --- |
| Sentinel URLs | `streamdown:incomplete-link` / `streamdown:incomplete-image` | `streammark:incomplete-link` / `streammark:incomplete-image` (`Remend.IncompleteLinkUrl` / `IncompleteImageUrl`) | Project's own scheme. Same length, so offsets are unchanged. |
| Non-string input | `remend(null)`, `remend(123)` return the input | `Complete(null!)` returns null; only strings type-check | C# is statically typed. |
| Scan cache | Module-level single-entry cache keyed by string value, cleared after each run | Thread-static single-entry cache keyed by string reference, scoped to one `Complete` call (nested calls from custom handlers are supported); region/mask buffers come from `ArrayPool<byte>` and are returned when the outermost call ends | Thread safety for concurrent calls; a value comparison would cost O(n) per query. A cache miss only rescans, so results are unaffected. |
| Comparison-operator regex | One `/^…/gm` regex passed to `String.replace` | The same pattern anchored with `\G`, tried at each JS line start (after `\n`, `\r`, U+2028, U+2029) in a manual replace loop | .NET `Multiline` `^` only follows `\n`; the loop reproduces the JS `m` flag exactly and skips lines that cannot start a list item. |
| Single-tilde regex | `/([\p{L}\p{N}_])~(?!~)(?=[\p{L}\p{N}_])/gu` | Candidate regex that also admits a surrogate on either side, with the full code point checked in the evaluator | .NET character classes see UTF-16 units; the `u` flag makes JS see code points (astral letters such as `𝐀`). |

JS regex semantics are otherwise reproduced explicitly: `$` without the `m` flag
becomes `\z`, JS `\s` is spelled out (`JsText.WhitespaceClass`: includes U+FEFF,
excludes U+0085), JS `\d` becomes `[0-9]`, and `.` excludes all four JS line
terminators. `trim`/`trimEnd`, `lastIndexOf` and `substring` follow JS rules
(`JsText`).

## Test-harness differences

- `streaming-properties.test.ts` uses fast-check and `mdast-util-from-markdown`.
  The C# port (`RemendStreamingPropertyTests`) uses a seeded `System.Random`
  generator with the same document shapes and Markdig (CommonMark pipeline) as
  the code oracle. The extractor also records a bounded sample (600 calls per
  property test, the full exhaustive prefix sweep) of the TS run with a fixed
  fast-check seed as exact-output fixtures.
- `trailing-patterns.test.ts` compares `matchTrailing` with a full-text match over
  5000 random strings per pattern; the fixtures keep a 600-call sample per
  pattern with the TS results.
