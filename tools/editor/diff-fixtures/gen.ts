// Writes the diff fixtures of tests/TKCode.Arc.Editor.Tests/Diff/TestData: inputs (the upstream legacy tests' inputs,
// crafted cases and seeded random edits of code-like lines) with the results of vscode's own DefaultLinesDiffComputer
// and LegacyLinesDiffComputer. Run with Bun (it loads vscode's TypeScript directly); see README.md.
import { readFileSync, writeFileSync } from 'node:fs';

const src = process.env.VSCODE_SRC ?? '/tmp/vscode-ref/vscode/src';
const { DefaultLinesDiffComputer } = await import(src + '/vs/editor/common/diff/defaultLinesDiffComputer/defaultLinesDiffComputer.ts');
const { LegacyLinesDiffComputer } = await import(src + '/vs/editor/common/diff/legacyLinesDiffComputer.ts');

// deterministic PRNG (mulberry32)
function rng(seed: number) { return () => { seed |= 0; seed = seed + 0x6D2B79F5 | 0; let t = Math.imul(seed ^ seed >>> 15, 1 | seed); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }

const words = ['foo', 'bar', 'baz', 'value', 'count', 'item', 'result', 'index', 'name', 'x', 'y', 'getValue', 'setValue', 'fooBar', 'BarBaz', 'return', 'if', 'else', 'for', 'const', 'let', '42', '0', '1'];
const puncts = [' ', ' ', ' ', '(', ')', ';', ',', '.', ' = ', ' + ', '{', '}', '[', ']', '"', '\t'];
function randomLine(r: () => number): string {
  const indent = '    '.repeat(Math.floor(r() * 4));
  let s = indent;
  const n = Math.floor(r() * 7);
  for (let i = 0; i < n; i++) { s += words[Math.floor(r() * words.length)]; if (r() < 0.6) { s += puncts[Math.floor(r() * puncts.length)]; } }
  if (r() < 0.1) { s += '   '; }
  return s;
}
function mutate(lines: string[], r: () => number, edits: number): string[] {
  const result = lines.slice();
  for (let e = 0; e < edits; e++) {
    const kind = r();
    const at = Math.floor(r() * (result.length + 1));
    if (kind < 0.2) { result.splice(at, 0, ...Array.from({ length: 1 + Math.floor(r() * 4) }, () => randomLine(r))); }
    else if (kind < 0.4 && result.length > 1) { result.splice(Math.min(at, result.length - 1), 1 + Math.floor(r() * 3)); }
    else if (kind < 0.7 && result.length > 0) {
      const i = Math.min(at, result.length - 1);
      const line = result[i];
      const p = Math.floor(r() * (line.length + 1));
      const op = r();
      if (op < 0.33) { result[i] = line.slice(0, p) + words[Math.floor(r() * words.length)] + line.slice(p); }
      else if (op < 0.66) { result[i] = line.slice(0, p) + line.slice(p + 1 + Math.floor(r() * 5)); }
      else { result[i] = '  ' + line + (r() < 0.5 ? ' ' : ''); }
    } else if (kind < 0.85 && result.length > 6) {
      // move a block
      const len = 3 + Math.floor(r() * 4);
      const from = Math.floor(r() * (result.length - len));
      const block = result.splice(from, len);
      const to = Math.floor(r() * (result.length + 1));
      result.splice(to, 0, ...block);
    } else if (result.length > 0) {
      const i = Math.min(at, result.length - 1);
      result[i] = randomLine(r);
    }
  }
  if (result.length === 0) { result.push(''); }
  return result;
}

type Opt = { ignoreTrimWhitespace: boolean; maxComputationTimeMs: number; computeMoves: boolean; extendToSubwords?: boolean };
function ser(changes: any[]): any[] {
  return changes.map(c => [c.original.startLineNumber, c.original.endLineNumberExclusive, c.modified.startLineNumber, c.modified.endLineNumberExclusive,
    c.innerChanges ? c.innerChanges.map((m: any) => [m.originalRange.startLineNumber, m.originalRange.startColumn, m.originalRange.endLineNumber, m.originalRange.endColumn,
      m.modifiedRange.startLineNumber, m.modifiedRange.startColumn, m.modifiedRange.endLineNumber, m.modifiedRange.endColumn]) : null]);
}
const cases: any[] = [];
function add(name: string, original: string[], modified: string[], options: Opt) {
  const def = new DefaultLinesDiffComputer().computeDiff(original, modified, options);
  const leg = new LegacyLinesDiffComputer().computeDiff(original, modified, options);
  cases.push({
    name, original, modified, options,
    expected: { changes: ser(def.changes as any), moves: def.moves.map(m => [m.lineRangeMapping.original.startLineNumber, m.lineRangeMapping.original.endLineNumberExclusive, m.lineRangeMapping.modified.startLineNumber, m.lineRangeMapping.modified.endLineNumberExclusive, ser(m.changes as any)]), hitTimeout: def.hitTimeout },
    legacy: { changes: ser(leg.changes as any), hitTimeout: leg.hitTimeout },
  });
}

// the inputs of the upstream legacy tests
const legacyInputs: [string, string[], string[]][] = JSON.parse(readFileSync(new URL('./legacy-inputs.json', import.meta.url), 'utf8'));
for (const [name, o, m] of legacyInputs) {
  add(name + ' (whitespace)', o, m, { ignoreTrimWhitespace: false, maxComputationTimeMs: 0, computeMoves: false });
  add(name + ' (ignore trim whitespace, moves)', o, m, { ignoreTrimWhitespace: true, maxComputationTimeMs: 0, computeMoves: true });
}

// crafted
add('import list', ['import { Baz, Bar } from "foo";'], ['import { Baz, Bar, Foo } from "foo";'], { ignoreTrimWhitespace: false, maxComputationTimeMs: 0, computeMoves: false });
add('sub words', ['const fooBarBaz = getValue(item);'], ['const fooQuxBaz = getValues(item);'], { ignoreTrimWhitespace: false, maxComputationTimeMs: 0, computeMoves: false, extendToSubwords: true });
const block = ['function moved(a, b) {', '    const sum = a + b;', '    const product = a * b;', '    return sum + product;', '}'];
const others = Array.from({ length: 12 }, (_, i) => `const other${i} = compute(${i}, "text ${i}");`);
add('moved block', [...block, ...others], [...others, ...block], { ignoreTrimWhitespace: true, maxComputationTimeMs: 0, computeMoves: true });
add('moved and changed block', [...block, ...others], [...others.slice(0, 6), ...block.map(l => l.replace('sum', 'total')), ...others.slice(6)], { ignoreTrimWhitespace: true, maxComputationTimeMs: 0, computeMoves: true });
add('crlf characters', ['a\rb', 'line'], ['a\r\nb', 'line'], { ignoreTrimWhitespace: false, maxComputationTimeMs: 0, computeMoves: false });
add('surrogates', ['😀 smile', 'x'], ['😃 smile', 'x'], { ignoreTrimWhitespace: false, maxComputationTimeMs: 0, computeMoves: false });

// random
const r = rng(20261124);
for (let i = 0; i < 300; i++) {
  const n = 1 + Math.floor(r() * (i < 280 ? 40 : 400));
  const original = Array.from({ length: n }, () => randomLine(r));
  const modified = mutate(original, r, 1 + Math.floor(r() * 8));
  const options: Opt = { ignoreTrimWhitespace: r() < 0.5, maxComputationTimeMs: 0, computeMoves: r() < 0.6, extendToSubwords: r() < 0.2 };
  add('random ' + i, original, modified, options);
}
for (let i = 0; i < 2; i++) {
  const original = Array.from({ length: 900 }, (_, k) => k % 7 === 0 ? '' : words[Math.floor(r() * words.length)] + ' ' + words[Math.floor(r() * words.length)]);
  const modified = mutate(original, r, 12);
  add('large (Myers) ' + i, original, modified, { ignoreTrimWhitespace: false, maxComputationTimeMs: 0, computeMoves: true });
}
writeFileSync(process.argv[2], JSON.stringify(cases));
console.log(cases.length); process.exit(0);
