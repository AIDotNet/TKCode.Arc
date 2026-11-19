// Minimal synchronous stand-in for the parts of vitest the remend tests use.
import { state } from "./recorder.mjs";

const stack = [];
export const failures = [];
export let passed = 0;

const fullName = (name) => [...stack, name].join(" > ");

export const describe = (name, body) => {
  stack.push(name);
  try {
    body();
  } finally {
    stack.pop();
  }
};

export const it = (name, body) => {
  state.test = fullName(name);
  try {
    const r = body();
    if (r && typeof r.then === "function") throw new Error("async tests not supported");
    passed += 1;
  } catch (error) {
    failures.push({ file: state.file, test: state.test, message: String(error?.message ?? error) });
  }
};
export const test = it;

const format = (template, row, index) => {
  if (Array.isArray(row)) {
    let i = 0;
    return template.replace(/%[sdifjo#%]/g, (t) => (t === "%#" ? String(index) : t === "%%" ? "%" : String(row[i++])));
  }
  if (row !== null && typeof row === "object") {
    return template.replace(/\$(\w+)/g, (_, k) => String(row[k])).replace(/%#/g, String(index));
  }
  return template.replace(/%[sdifjo]/, String(row)).replace(/%#/g, String(index));
};

it.each = (table) => (template, body) => {
  table.forEach((row, index) => {
    it(format(template, row, index), () => (Array.isArray(row) ? body(...row) : body(row)));
  });
};

const deepEqual = (a, b) => JSON.stringify(a) === JSON.stringify(b);

export const expect = (actual) => {
  const make = (negate) => {
    const check = (ok, what) => {
      if (ok === negate) {
        throw new Error(`expected ${JSON.stringify(actual)} ${negate ? "not " : ""}${what}`);
      }
    };
    return {
      toBe: (e) => check(Object.is(actual, e), `toBe ${JSON.stringify(e)}`),
      toEqual: (e) => check(deepEqual(actual, e), `toEqual ${JSON.stringify(e)}`),
      toContain: (e) => check(actual.includes(e), `toContain ${JSON.stringify(e)}`),
      toMatch: (e) => check(typeof e === "string" ? actual.includes(e) : e.test(actual), `toMatch ${e}`),
    };
  };
  return { ...make(false), not: make(true) };
};
