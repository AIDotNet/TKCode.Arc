// Converts monaco-editor's built-in languages (src/languages/definitions, MIT) into C# for TKCode.Arc.Editor:
// one Languages/BuiltIn/<Name>Language.g.cs per language (registration data, language configuration, Monarch
// definition) and the languages' tokenization test cases as JSON for TKCode.Arc.Editor.Tests.
//
//   node --import ./register-hooks.mjs convert.mjs <monaco-editor>/src/languages/definitions <repo root> <commit>
//
// The definitions are loaded as modules (hooks.mjs shims the editor API, registerLanguage and the test runner), so what
// is converted is exactly what monaco-editor registers. Regular expressions keep their JavaScript source; the engine
// translates them (JsRegex.TranslateMonarch / FromLiteral).

import { readdirSync, existsSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const [definitionsDir, repoRoot, commit = 'unknown'] = process.argv.slice(2);
if (!definitionsDir || !repoRoot) {
	console.error('usage: node --import ./register-hooks.mjs convert.mjs <definitions dir> <repo root> [monaco-editor commit]');
	process.exit(2);
}

const outDir = path.join(repoRoot, 'src/TKCode.Arc.Editor/Languages/BuiltIn');
const testDir = path.join(repoRoot, 'tests/TKCode.Arc.Editor.Tests/Languages/BuiltIn/TestData');

const contribution = await import('./shims/contribution.mjs');
const testRunner = await import('./shims/testRunner.mjs');

const dirs = readdirSync(definitionsDir, { withFileTypes: true })
	.filter((d) => d.isDirectory() && d.name !== 'test')
	.map((d) => d.name)
	.sort();

// 1. registrations
for (const dir of dirs) {
	const register = path.join(definitionsDir, dir, 'register.ts');
	if (existsSync(register)) {
		await import(pathToFileURL(register).href);
	}
}

// 2. definitions (through each registration's loader, as the editor does)
const languages = [];
for (const def of contribution.registered) {
	const mod = await def.loader();
	languages.push({ def, conf: mod.conf, language: mod.language });
}
languages.sort((a, b) => a.def.id.localeCompare(b.def.id));

// 3. test cases
for (const dir of dirs) {
	for (const file of readdirSync(path.join(definitionsDir, dir))) {
		if (file.endsWith('.test.ts')) {
			await import(pathToFileURL(path.join(definitionsDir, dir, file)).href);
		}
	}
}

// ---- C# emission

const cs = (s) =>
	JSON.stringify(s).replace(/[\u0085\u2028\u2029]/g, (c) => '\\u' + c.charCodeAt(0).toString(16).padStart(4, '0'));

const isRegExp = (v) => v instanceof RegExp;

function pascal(id) {
	return id
		.split(/[^A-Za-z0-9]+/)
		.filter(Boolean)
		.map((p) => p[0].toUpperCase() + p.slice(1))
		.join('');
}

function regexLiteral(v) {
	if (isRegExp(v)) {
		return v.flags.replace(/[gy]/g, '') ? `JsRegex.FromLiteral(${cs(v.source)}, ${cs(v.flags)})` : `JsRegex.FromLiteral(${cs(v.source)})`;
	}
	return `JsRegex.FromLiteral(${cs(v)})`;
}

function regexSource(v) {
	return isRegExp(v) ? v.source : String(v);
}

function stringList(arr, indent) {
	if (arr.length === 0) {
		return '[]';
	}
	const items = arr.map(cs);
	const oneLine = '[' + items.join(', ') + ']';
	if (oneLine.length < 100) {
		return oneLine;
	}
	const lines = [];
	let line = '';
	for (const item of items) {
		if (line.length + item.length > 100) {
			lines.push(line.trimEnd());
			line = '';
		}
		line += item + ', ';
	}
	if (line) {
		lines.push(line.trimEnd());
	}
	return '[\n' + lines.map((l) => indent + '    ' + l).join('\n') + '\n' + indent + ']';
}

const ACTION_KEYS = ['next', 'switchTo', 'goBack', 'bracket', 'nextEmbedded', 'log'];

function action(a, indent) {
	if (a === undefined || a === null) {
		return 'null';
	}
	if (typeof a === 'string') {
		return cs(a);
	}
	if (Array.isArray(a)) {
		return 'Groups(' + a.map((x) => action(x, indent)).join(', ') + ')';
	}
	if (typeof a.token === 'string') {
		const args = [cs(a.token)];
		for (const key of ACTION_KEYS) {
			if (a[key] !== undefined) {
				args.push(`${key}: ${typeof a[key] === 'number' ? a[key] : cs(a[key])}`);
			}
		}
		return 'Token(' + args.join(', ') + ')';
	}
	if (a.cases) {
		const inner = indent + '    ';
		const cases = Object.keys(a.cases).map((k) => `${inner}(${cs(k)}, ${action(a.cases[k], inner)})`);
		return 'Cases(\n' + cases.join(',\n') + ')';
	}
	// not a valid action: monarchCompile rejects it, so does the engine
	const props = ACTION_KEYS.filter((k) => a[k] !== undefined).map((k) => `${k[0].toUpperCase() + k.slice(1)} = ${typeof a[k] === 'number' ? a[k] : cs(a[k])}`);
	return 'new MonarchAction { ' + props.join(', ') + ' }';
}

function rule(r, indent) {
	if (Array.isArray(r)) {
		const args = [cs(regexSource(r[0])), action(r[1], indent)];
		if (r.length >= 3) {
			args.push(cs(r[2]));
		}
		return 'R(' + args.join(', ') + ')';
	}
	if (r.include !== undefined) {
		return `Include(${cs(r.include)})`;
	}
	const args = [r.regex === undefined ? 'null' : cs(regexSource(r.regex)), action(r.action, indent)];
	if (typeof r.name === 'string') {
		args.push(cs(r.name));
	}
	return 'Expanded(' + args.join(', ') + ')';
}

function attributeValue(v, indent) {
	if (typeof v === 'string') {
		return `MonarchValue.String(${cs(v)})`;
	}
	if (isRegExp(v)) {
		return `RegExp(${cs(v.source)})`;
	}
	if (Array.isArray(v) && v.every((x) => typeof x === 'string')) {
		return `MonarchValue.StringArray(${stringList(v, indent)})`;
	}
	return `MonarchValue.Other(${cs(JSON.stringify(v) ?? String(v))})`;
}

const LANGUAGE_KEYS = new Set(['tokenizer', 'ignoreCase', 'unicode', 'defaultToken', 'brackets', 'start', 'tokenPostfix', 'includeLF']);

function monarchLanguage(lang) {
	const i1 = '        ';
	const i2 = i1 + '    ';
	const i3 = i2 + '    ';
	const out = [];
	out.push('new()');
	out.push(i1 + '{');
	if (lang.ignoreCase !== undefined) {
		out.push(`${i2}IgnoreCase = ${lang.ignoreCase ? 'true' : 'false'},`);
	}
	if (lang.unicode !== undefined) {
		out.push(`${i2}Unicode = ${lang.unicode ? 'true' : 'false'},`);
	}
	if (lang.includeLF !== undefined) {
		out.push(`${i2}IncludeLF = ${lang.includeLF ? 'true' : 'false'},`);
	}
	for (const key of ['defaultToken', 'start', 'tokenPostfix']) {
		if (typeof lang[key] === 'string') {
			out.push(`${i2}${key[0].toUpperCase() + key.slice(1)} = ${cs(lang[key])},`);
		}
	}
	if (Array.isArray(lang.brackets)) {
		out.push(`${i2}Brackets =`);
		out.push(`${i2}[`);
		for (const b of lang.brackets) {
			const [open, close, token] = Array.isArray(b) ? b : [b.open, b.close, b.token];
			out.push(`${i3}new(${cs(open)}, ${cs(close)}, ${cs(token)}),`);
		}
		out.push(`${i2}],`);
	}
	const attrs = Object.keys(lang).filter((k) => !LANGUAGE_KEYS.has(k));
	if (attrs.length > 0) {
		out.push(`${i2}Attributes = new MonarchAttributes`);
		out.push(`${i2}{`);
		for (const k of attrs) {
			out.push(`${i3}[${cs(k)}] = ${attributeValue(lang[k], i3)},`);
		}
		out.push(`${i2}},`);
	}
	out.push(`${i2}Tokenizer = new MonarchStates`);
	out.push(`${i2}{`);
	for (const state of Object.keys(lang.tokenizer)) {
		out.push(`${i3}[${cs(state)}] =`);
		out.push(`${i3}[`);
		for (const r of lang.tokenizer[state]) {
			out.push(`${i3}    ${rule(r, i3 + '    ')},`);
		}
		out.push(`${i3}],`);
	}
	out.push(`${i2}},`);
	out.push(i1 + '}');
	return out.join('\n');
}

const INDENT_ACTIONS = ['None', 'Indent', 'IndentOutdent', 'Outdent'];

function pair(p) {
	return Array.isArray(p) ? `(${cs(p[0])}, ${cs(p[1])})` : `(${cs(p.open)}, ${cs(p.close)})`;
}

function configuration(conf) {
	if (!conf) {
		return 'new()';
	}
	const i2 = '            ';
	const out = ['new()', '        {'];
	if (conf.comments) {
		const parts = [];
		if (conf.comments.lineComment) {
			const lc = conf.comments.lineComment;
			parts.push(`LineComment = ${typeof lc === 'string' ? cs(lc) : `new LineCommentConfig(${cs(lc.comment)})${lc.noIndent !== undefined ? ` { NoIndent = ${lc.noIndent} }` : ''}`}`);
		}
		if (conf.comments.blockComment) {
			parts.push(`BlockComment = ${pair(conf.comments.blockComment)}`);
		}
		out.push(`${i2}Comments = new CommentRule { ${parts.join(', ')} },`);
	}
	if (conf.brackets) {
		out.push(`${i2}Brackets = [${conf.brackets.map(pair).join(', ')}],`);
	}
	if (conf.wordPattern) {
		out.push(`${i2}WordPattern = ${regexLiteral(conf.wordPattern)},`);
	}
	if (conf.indentationRules) {
		const r = conf.indentationRules;
		const extra = [];
		if (r.indentNextLinePattern) {
			extra.push(`IndentNextLinePattern = ${regexLiteral(r.indentNextLinePattern)}`);
		}
		if (r.unIndentedLinePattern) {
			extra.push(`UnIndentedLinePattern = ${regexLiteral(r.unIndentedLinePattern)}`);
		}
		out.push(`${i2}IndentationRules = new IndentationRule(${regexLiteral(r.decreaseIndentPattern)}, ${regexLiteral(r.increaseIndentPattern)})${extra.length ? ' { ' + extra.join(', ') + ' }' : ''},`);
	}
	if (conf.onEnterRules) {
		out.push(`${i2}OnEnterRules =`);
		out.push(`${i2}[`);
		for (const r of conf.onEnterRules) {
			const a = r.action;
			const actionExtra = [];
			if (a.appendText !== undefined) {
				actionExtra.push(`AppendText = ${cs(a.appendText)}`);
			}
			if (a.removeText !== undefined) {
				actionExtra.push(`RemoveText = ${a.removeText}`);
			}
			const enter = `new EnterAction(IndentAction.${INDENT_ACTIONS[a.indentAction]})${actionExtra.length ? ' { ' + actionExtra.join(', ') + ' }' : ''}`;
			const ruleExtra = [];
			if (r.afterText) {
				ruleExtra.push(`AfterText = ${regexLiteral(r.afterText)}`);
			}
			if (r.previousLineText) {
				ruleExtra.push(`PreviousLineText = ${regexLiteral(r.previousLineText)}`);
			}
			out.push(`${i2}    new OnEnterRule(${regexLiteral(r.beforeText)}, ${enter})${ruleExtra.length ? ' { ' + ruleExtra.join(', ') + ' }' : ''},`);
		}
		out.push(`${i2}],`);
	}
	if (conf.autoClosingPairs) {
		out.push(`${i2}AutoClosingPairs =`);
		out.push(`${i2}[`);
		for (const p of conf.autoClosingPairs) {
			const [open, close] = Array.isArray(p) ? p : [p.open, p.close];
			const notIn = !Array.isArray(p) && p.notIn ? ` { NotIn = ${stringList(p.notIn, '')} }` : '';
			out.push(`${i2}    new AutoClosingPairConditional(${cs(open)}, ${cs(close)})${notIn},`);
		}
		out.push(`${i2}],`);
	}
	if (conf.surroundingPairs) {
		out.push(`${i2}SurroundingPairs =`);
		out.push(`${i2}[`);
		for (const p of conf.surroundingPairs) {
			const [open, close] = Array.isArray(p) ? p : [p.open, p.close];
			out.push(`${i2}    new AutoClosingPair(${cs(open)}, ${cs(close)}),`);
		}
		out.push(`${i2}],`);
	}
	if (conf.colorizedBracketPairs) {
		out.push(`${i2}ColorizedBracketPairs = [${conf.colorizedBracketPairs.map(pair).join(', ')}],`);
	}
	if (typeof conf.autoCloseBefore === 'string') {
		out.push(`${i2}AutoCloseBefore = ${cs(conf.autoCloseBefore)},`);
	}
	if (conf.folding) {
		const parts = [];
		if (conf.folding.offSide !== undefined) {
			parts.push(`OffSide = ${conf.folding.offSide}`);
		}
		if (conf.folding.markers) {
			parts.push(`Markers = new FoldingMarkers(${regexLiteral(conf.folding.markers.start)}, ${regexLiteral(conf.folding.markers.end)})`);
		}
		out.push(`${i2}Folding = new FoldingRules { ${parts.join(', ')} },`);
	}
	out.push('        }');
	return out.join('\n');
}

function extensionPoint(def) {
	const parts = [];
	for (const key of ['extensions', 'filenames', 'aliases', 'mimetypes']) {
		if (Array.isArray(def[key])) {
			parts.push(`${key[0].toUpperCase() + key.slice(1)} = ${stringList(def[key], '            ')}`);
		}
	}
	if (typeof def.firstLine === 'string') {
		parts.push(`FirstLine = ${cs(def.firstLine)}`);
	}
	return `new LanguageExtensionPoint(${cs(def.id)})\n        {\n${parts.map((p) => '            ' + p + ',').join('\n')}\n        }`;
}

const header = (file) => `// <auto-generated/>
// Generated by tools/editor/monarch-convert from monaco-editor (src/languages/definitions/${file}, commit ${commit}),
// MIT license. Do not edit: change the converter and run it again (tools/editor/README.md).
`;

rmSync(outDir, { recursive: true, force: true });
mkdirSync(outDir, { recursive: true });
const names = [];
for (const { def, conf, language } of languages) {
	const name = pascal(def.id) + 'Language';
	names.push({ id: def.id, name });
	const source = `${header(def.id)}
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Standalone.Monarch;
using static TKCode.Arc.Editor.Standalone.Monarch.MonarchBuilder;

namespace TKCode.Arc.Editor.Languages.BuiltIn;

/// <summary>The built-in language <c>${def.id}</c>.</summary>
internal static class ${name}
{
    /// <summary>Its registration, language configuration and Monarch definition.</summary>
    public static BuiltInLanguage Definition { get; } = new(
        ${extensionPoint(def)},
        CreateConfiguration,
        CreateMonarchLanguage);

    private static LanguageConfiguration CreateConfiguration() =>
        ${configuration(conf)};

    private static MonarchLanguage CreateMonarchLanguage() =>
        ${monarchLanguage(language)};
}
`;
	writeFileSync(path.join(outDir, name + '.g.cs'), source.replace(/\n{3,}/g, '\n\n'));
}

writeFileSync(path.join(outDir, 'BuiltInLanguages.g.cs'), `${header('*')}
namespace TKCode.Arc.Editor.Languages.BuiltIn;

/// <summary>Every built-in language, in id order. Referencing one language's class keeps only that one in a trimmed app.</summary>
internal static class BuiltInLanguages
{
    public static IReadOnlyList<BuiltInLanguage> All { get; } =
    [
${names.map((n) => `        ${n.name}.Definition,`).join('\n')}
    ];
}
`);

// ---- test data

rmSync(testDir, { recursive: true, force: true });
mkdirSync(testDir, { recursive: true });
const byLanguage = new Map();
for (const rec of testRunner.recorded) {
	const key = rec.languages[0];
	if (!byLanguage.has(key)) {
		byLanguage.set(key, []);
	}
	byLanguage.get(key).push({ languages: rec.languages, tests: rec.tests });
}
for (const [id, recs] of byLanguage) {
	writeFileSync(path.join(testDir, id + '.json'), JSON.stringify(recs));
}

console.log(`${languages.length} languages, test data for ${byLanguage.size}`);
