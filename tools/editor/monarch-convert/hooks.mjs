// Module resolution for loading monaco-editor's language definitions in Node: the editor API, the contribution module
// and the test runner become shims, and extension-less relative imports get '.ts' (TypeScript leaves them out).
import { fileURLToPath, pathToFileURL } from 'node:url';
import { existsSync } from 'node:fs';
import path from 'node:path';

const here = path.dirname(fileURLToPath(import.meta.url));
const shims = {
	editor: pathToFileURL(path.join(here, 'shims', 'editor.mjs')).href,
	contribution: pathToFileURL(path.join(here, 'shims', 'contribution.mjs')).href,
	testRunner: pathToFileURL(path.join(here, 'shims', 'testRunner.mjs')).href,
	nodeTest: pathToFileURL(path.join(here, 'shims', 'nodeTest.mjs')).href,
};

export async function resolve(specifier, context, nextResolve) {
	if (specifier === 'node:test') {
		// a test file's own tests drive the real editor; only its testTokenization cases are wanted
		return { url: shims.nodeTest, shortCircuit: true };
	}
	if (/(^|\/)editor$/.test(specifier) && specifier.startsWith('.')) {
		return { url: shims.editor, shortCircuit: true };
	}
	if (specifier.endsWith('/_.contribution')) {
		return { url: shims.contribution, shortCircuit: true };
	}
	if (specifier.endsWith('/test/testRunner')) {
		return { url: shims.testRunner, shortCircuit: true };
	}
	if (specifier.startsWith('.') && !path.extname(specifier) && context.parentURL) {
		const base = path.resolve(path.dirname(fileURLToPath(context.parentURL)), specifier);
		if (existsSync(base + '.ts')) {
			return { url: pathToFileURL(base + '.ts').href, shortCircuit: true };
		}
	}
	return nextResolve(specifier, context);
}

// monaco-editor writes casts such as `<languages.IMonarchLanguage>{ ... }` and `<any[]>[ ... ]`, which type stripping
// cannot erase: drop them.
export async function load(url, context, nextLoad) {
	if (url.endsWith('.ts') && url.startsWith('file:')) {
		const { readFile } = await import('node:fs/promises');
		let source = await readFile(fileURLToPath(url), 'utf8');
		// a cast follows '=', ':', '(', ',' or '[' (not the inside of a regular expression literal such as /<script>[^]/)
		source = source.replace(/(?<=[=:(,[]\s*)<\s*[A-Za-z_][A-Za-z0-9_.]*(?:\[\])?\s*>(?=\s*[{\[(])/g, '');
		return { format: 'module-typescript', source, shortCircuit: true };
	}
	return nextLoad(url, context);
}
