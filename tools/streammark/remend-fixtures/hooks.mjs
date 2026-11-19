// Module hooks for extract.mjs: resolves the TypeScript sources of vercel/streamdown
// packages/remend (extensionless imports), swaps vitest for a recording shim and
// wraps every src module imported by a test file in a call recorder.
import { existsSync, readFileSync } from "node:fs";
import { basename } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

let cfg;

export async function initialize(data) {
  cfg = data;
}

const RECORD_SUFFIX = "?remend-record";

const resolveTs = (specifier, parentURL) => {
  const base = new URL(specifier, parentURL);
  for (const candidate of [`${base.href}.ts`, `${base.href}/index.ts`, base.href]) {
    const path = fileURLToPath(candidate);
    if (existsSync(path) && !path.endsWith("/src") && !path.endsWith("/src/")) {
      return candidate;
    }
  }
  return null;
};

export async function resolve(specifier, context, next) {
  if (specifier === "vitest") {
    return { url: new URL("./vitest-shim.mjs", import.meta.url).href, shortCircuit: true };
  }
  if (specifier === "fast-check" || specifier === "mdast-util-from-markdown") {
    return next(specifier, {
      ...context,
      parentURL: pathToFileURL(`${cfg.depsDir}/package.json`).href,
    });
  }
  const parent = context.parentURL?.replace(RECORD_SUFFIX, "");
  if (specifier.startsWith(".") && parent?.startsWith(cfg.pkgUrl)) {
    const url = resolveTs(specifier, parent);
    if (url) {
      const fromTest = parent.includes("/__tests__/") && !context.parentURL.endsWith(RECORD_SUFFIX);
      const intoSrc = url.includes("/src/");
      return { url: fromTest && intoSrc ? url + RECORD_SUFFIX : url, shortCircuit: true };
    }
  }
  return next(specifier, context);
}

// Collects the runtime export names of a source module.
const exportNames = (source) => {
  const names = new Set();
  for (const m of source.matchAll(/export\s+(?:const|let|function)\s+(\w+)/g)) {
    names.add(m[1]);
  }
  for (const m of source.matchAll(/export\s*\{([^}]*)\}/g)) {
    for (const part of m[1].split(",")) {
      const item = part.trim();
      if (!item || item.startsWith("type ")) continue;
      const alias = item.split(/\s+as\s+/);
      names.add((alias[1] ?? alias[0]).trim());
    }
  }
  if (/export\s+default\s/.test(source)) {
    names.add("default");
  }
  return [...names];
};

export async function load(url, context, next) {
  if (url.endsWith(RECORD_SUFFIX)) {
    const realUrl = url.slice(0, -RECORD_SUFFIX.length);
    const source = readFileSync(fileURLToPath(realUrl), "utf8");
    const moduleName = basename(fileURLToPath(realUrl), ".ts");
    const recorder = new URL("./recorder.mjs", import.meta.url).href;
    const lines = [
      `import * as real from ${JSON.stringify(realUrl)};`,
      `import { wrap } from ${JSON.stringify(recorder)};`,
    ];
    for (const name of exportNames(source)) {
      const qualified = JSON.stringify(`${moduleName}.${name === "default" ? "remend" : name}`);
      if (name === "default") {
        lines.push(`export default wrap(${qualified}, real.default);`);
      } else {
        lines.push(`export const ${name} = wrap(${qualified}, real.${name});`);
      }
    }
    return { format: "module", source: lines.join("\n"), shortCircuit: true };
  }
  return next(url, context);
}
