// Shared recording state: every top-level call a test makes into a remend
// source module is captured as { fn, args, result }.
const refs = new Map();
let depth = 0;

export const state = {
  file: "",
  test: "",
  cap: Number.POSITIVE_INFINITY,
  records: [],
  perTest: new Map(),
  skipped: 0,
};

const isPlain = (value) =>
  value === null ||
  ["string", "number", "boolean"].includes(typeof value);

// Converts an argument to JSON, or returns undefined when it cannot be
// represented (functions, undefined, non-string remend input).
const serialize = (value) => {
  if (refs.has(value)) return { $ref: refs.get(value) };
  if (isPlain(value)) return value;
  if (Array.isArray(value)) {
    const items = value.map(serialize);
    return items.includes(undefined) ? undefined : items;
  }
  if (typeof value === "object") {
    const out = {};
    for (const [k, v] of Object.entries(value)) {
      const s = serialize(v);
      if (s === undefined) return undefined;
      out[k] = s;
    }
    return out;
  }
  return undefined;
};

const serializeResult = (value) => {
  if (Array.isArray(value)) return Array.from(value, (v) => (v === undefined ? null : v));
  return value === undefined ? null : value;
};

export const wrap = (name, value) => {
  if (typeof value !== "function") {
    if (value !== null && typeof value === "object") refs.set(value, name);
    return value;
  }
  return function recorded(...args) {
    if (depth > 0) return value.apply(this, args);
    depth += 1;
    let result;
    try {
      result = value.apply(this, args);
    } finally {
      depth -= 1;
    }
    const key = `${state.file}\u0000${state.test}`;
    const count = state.perTest.get(key) ?? 0;
    const serialArgs = serialize(args);
    const validInput = !name.endsWith(".remend") || typeof args[0] === "string";
    if (serialArgs === undefined || !validInput) {
      state.skipped += 1;
    } else if (count < state.cap || state.uncapped?.(state.test)) {
      state.perTest.set(key, count + 1);
      state.records.push({
        test: state.test,
        fn: name,
        args: serialArgs,
        expected: serializeResult(result),
      });
    }
    return result;
  };
};
