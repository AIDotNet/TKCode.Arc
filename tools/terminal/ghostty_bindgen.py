#!/usr/bin/env python3
"""从 libghostty-vt 生成 C# 互操作代码（docs/terminal/02-core-ghostty.md §4）。

与 02 §4.1 原方案（ClangSharp 解析头文件）的差异：结构体、联合体、枚举、别名全部取自库自带的
类型清单 ghostty_type_json()，它给出每个字段的偏移、大小、枚举值和 packed 位布局，是官方要求绑定
使用的权威来源（types.h）；结构体用显式布局（FieldOffset），布局与库完全一致。函数签名用一个小的
解析器从头文件的 GHOSTTY_API 声明中读取（头文件格式统一：一条声明一个函数，参数 "类型 名字"）。
不依赖 libclang。

用法：
  tools/terminal/ghostty_bindgen.py generate --lib <libghostty-vt> --include <include 目录> --out <输出目录>
  tools/terminal/ghostty_bindgen.py manifest --lib <libghostty-vt>        # 打印类型清单
输出：GhosttyTypes.g.cs、GhosttyNative.g.cs（生成文件入库，升级时看 diff，02 §2.2 第 4 步）。
"""
import argparse
import ctypes
import glob
import json
import os
import re
import sys

NAMESPACE = "TKCode.Terminal.Core.Interop"
LIB = "ghostty-vt"

PRIM = {
    "u8": "byte", "i8": "sbyte", "u16": "ushort", "i16": "short", "u32": "uint", "i32": "int",
    "u64": "ulong", "i64": "long", "f32": "float", "f64": "double", "bool": "bool",
}
C_PRIM = {
    "uint8_t": "byte", "int8_t": "sbyte", "uint16_t": "ushort", "int16_t": "short", "uint32_t": "uint",
    "int32_t": "int", "uint64_t": "ulong", "int64_t": "long", "size_t": "nuint", "float": "float",
    "double": "double", "bool": "bool", "char": "byte", "void": "void", "int": "int",
}
# 对外公开的枚举副本（门面 API 使用，值与原生枚举相同）：原生名 → 公共名
PUBLIC_ENUMS = {"GhosttyKey": ("TerminalKey", "物理按键（W3C UI Events 的 KeyboardEvent.code），值与 GhosttyKey 相同。")}
# 不是导出符号（wasm 专用）
SKIP_HEADERS = {"wasm.h"}
# C# 关键字
KEYWORDS = {"base", "checked", "event", "fixed", "in", "is", "lock", "object", "operator", "out", "params", "ref",
            "string", "unchecked", "value", "default", "internal", "char", "bool", "int", "long", "short", "byte"}


def load_manifest(lib_path):
    lib = ctypes.CDLL(os.path.abspath(lib_path))
    fn = lib.ghostty_type_json
    fn.restype = ctypes.c_char_p
    return json.loads(fn().decode("utf-8"))


def pascal(name):
    parts = re.split(r"[_\s]+", name)
    out = "".join(p[:1].upper() + p[1:].lower() if p.isupper() or p.islower() else p[:1].upper() + p[1:] for p in parts if p)
    if out and out[0].isdigit():
        out = "_" + out
    return out


def ident(name):
    return "@" + name if name in KEYWORDS else name


class Generator:
    def __init__(self, manifest):
        self.types = manifest["types"]
        self.abi = manifest["abi"]

    # ---------- 类型名映射 ----------
    def cs_type(self, name):
        """清单中的类型名 → C# 类型名（值语义）。"""
        if name in PRIM:
            return PRIM[name]
        t = self.types.get(name)
        if t is None:
            raise KeyError(name)
        kind = t["kind"]
        if kind == "opaque":
            return "nint"
        if kind == "alias":
            return self.cs_type(t["type"])
        if kind == "packed":
            return PRIM[t["underlying"]]
        return name  # struct / union / enum 同名生成

    def field_type(self, f):
        t = f["type"]
        if t == "pointer":
            elem = f.get("elem")
            if elem in ("function", "opaque") or elem is None:
                return "nint" if elem == "function" else "void*"
            return self.cs_type(elem) + "*"
        if t == "array":
            return None  # 单独处理
        return self.cs_type(t)

    # ---------- 类型 ----------
    def gen_types(self):
        out = [HEADER, "using System.Runtime.CompilerServices;", "using System.Runtime.InteropServices;", "",
               f"namespace {NAMESPACE};", ""]
        for name in sorted(self.types):
            t = self.types[name]
            kind = t["kind"]
            if kind == "enum":
                out += self.gen_enum(name, t)

            elif kind in ("struct", "union"):
                out += self.gen_struct(name, t)
        return "\n".join(out).rstrip() + "\n"

    def gen_public(self):
        out = [HEADER, "", "namespace TKCode.Terminal.Core;", ""]
        for native, (public, doc) in sorted(PUBLIC_ENUMS.items()):
            out += self.gen_enum(public, self.types[native], public=True, doc=doc)
        return "\n".join(out).rstrip() + "\n"

    def gen_enum(self, name, t, public=False, doc=None):
        underlying = PRIM[t["underlying"]]
        summary = doc or f"C: {name}（{t['prefix']}*）。"
        lines = [f"/// <summary>{summary}</summary>", f"{'public' if public else 'internal'} enum {name} : {underlying}", "{"]
        for member, value in t["values"].items():
            if member.endswith("MAX_VALUE") and value == 2147483647:
                continue  # INT_MAX 哨兵（types.h），不生成
            lines.append(f"    {pascal(member)} = {value},")
        lines += ["}", ""]
        return lines

    def gen_struct(self, name, t):
        lines = [f"/// <summary>C: {name}（{t['kind']}，{t['size']} 字节）。</summary>",
                 f"[StructLayout(LayoutKind.Explicit, Size = {t['size']})]",
                 f"internal unsafe struct {name}", "{"]
        has_size = False
        for fname, f in t["fields"].items():
            offset = f["offset"]
            if fname == "size" and offset == 0 and f["type"] == "u64":
                has_size = True
            member = ident(pascal(fname.lstrip("_")) if not fname.startswith("_") else "_" + pascal(fname.lstrip("_")))
            if f["type"] == "array":
                elem = f["elem"]
                count = f["count"]
                if elem in PRIM and elem != "bool":
                    lines.append(f"    [FieldOffset({offset})] public fixed {PRIM[elem]} {member}[{count}];")
                else:
                    lines.append(f"    [FieldOffset({offset})] public fixed byte {member}Raw[{f['size']}]; // {elem}[{count}]")
                continue
            lines.append(f"    [FieldOffset({offset})] public {self.field_type(f)} {member};")
        if has_size:
            lines += ["", f"    /// <summary>sized struct：size 字段填好的新实例（GHOSTTY_INIT_SIZED）。</summary>",
                      f"    public static {name} Create() => new() {{ Size = (ulong)sizeof({name}) }};"]
        lines += ["}", ""]
        return lines

    # ---------- 函数 ----------
    def c_param_type(self, ctype):
        """头文件里的参数类型 → C# 类型。"""
        s = ctype.replace("const", " ").strip()
        stars = s.count("*")
        base = s.replace("*", " ").split()[0]
        if base in C_PRIM:
            cs = C_PRIM[base]
            if base == "void" and stars == 0:
                return "void"
        elif base in self.types:
            cs = self.cs_type(base)
        else:
            raise KeyError(f"unknown C type {ctype}")
        return cs + "*" * stars

    def gen_functions(self, headers):
        decls = []
        for path in headers:
            text = open(path, encoding="utf-8").read()
            for m in re.finditer(r"^GHOSTTY_API\s+([^;]+);", text, re.M):
                decl = " ".join(m.group(1).split())
                mm = re.match(r"(.+?)\b(\w+)\s*\((.*)\)$", decl)
                if not mm:
                    raise ValueError(f"cannot parse: {decl}")
                ret, name, params = mm.groups()
                decls.append((os.path.basename(path), name, ret.strip(), params))
        decls.sort(key=lambda d: d[1])
        out = [HEADER, "using System.Runtime.InteropServices;", "", f"namespace {NAMESPACE};", "",
               "/// <summary>libghostty-vt 的全部导出函数（生成）。只允许门面层调用。</summary>",
               "internal static unsafe partial class GhosttyNative", "{",
               f"    private const string Lib = \"{LIB}\";", ""]
        seen = set()
        for header, name, ret, params in decls:
            if name in seen:
                continue
            seen.add(name)
            cs_ret = self.c_param_type(ret)
            args = []
            if params.strip() not in ("", "void"):
                for p in split_params(params):
                    pm = re.match(r"(.+?)\s*\b(\w+)$", p.strip())
                    if not pm:
                        raise ValueError(f"cannot parse param '{p}' in {name}")
                    ptype, pname = pm.groups()
                    cs = self.c_param_type(ptype)
                    attr = "[MarshalAs(UnmanagedType.U1)] " if cs == "bool" else ""
                    args.append(f"{attr}{cs} {ident(camel(pname))}")
            method = pascal(name.removeprefix("ghostty_").removeprefix("tkcode_ghostty_"))
            if name.startswith("tkcode_ghostty_"):
                method = "Tkcode" + method
            out.append(f"    /// <summary>{name}（{header}）</summary>")
            out.append(f"    [LibraryImport(Lib, EntryPoint = \"{name}\")]")
            if cs_ret == "bool":
                out.append("    [return: MarshalAs(UnmanagedType.U1)]")
            out.append(f"    public static partial {cs_ret} {method}({', '.join(args)});")
            out.append("")
        out[-1:] = ["}"]
        return "\n".join(out) + "\n", len(seen)


def camel(name):
    p = pascal(name)
    return p[:1].lower() + p[1:]


def split_params(params):
    depth, cur, out = 0, "", []
    for ch in params:
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
        if ch == "," and depth == 0:
            out.append(cur)
            cur = ""
        else:
            cur += ch
    if cur.strip():
        out.append(cur)
    return out


HEADER = "// <auto-generated> tools/terminal/ghostty_bindgen.py — 不要手改；升级 libghostty 后重新生成。</auto-generated>\n#nullable disable"


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    g = sub.add_parser("generate")
    g.add_argument("--lib", required=True)
    g.add_argument("--include", required=True, action="append", help="头文件目录，可多次指定")
    g.add_argument("--out", required=True)
    m = sub.add_parser("manifest")
    m.add_argument("--lib", required=True)
    a = ap.parse_args()

    manifest = load_manifest(a.lib)
    if a.cmd == "manifest":
        json.dump(manifest, sys.stdout, indent=1)
        return
    if manifest.get("schema") != 1:
        sys.exit(f"unsupported type manifest schema {manifest.get('schema')}")
    gen = Generator(manifest)
    headers = sorted(h for inc in a.include for h in glob.glob(os.path.join(inc, "**", "*.h"), recursive=True)
                     if os.path.basename(h) not in SKIP_HEADERS)
    os.makedirs(a.out, exist_ok=True)
    with open(os.path.join(a.out, "GhosttyTypes.g.cs"), "w", encoding="utf-8") as f:
        f.write(gen.gen_types())
    with open(os.path.join(a.out, "GhosttyPublic.g.cs"), "w", encoding="utf-8") as f:
        f.write(gen.gen_public())
    native, count = gen.gen_functions(headers)
    with open(os.path.join(a.out, "GhosttyNative.g.cs"), "w", encoding="utf-8") as f:
        f.write(native)
    print(f"generated {sum(1 for t in gen.types.values() if t['kind'] in ('struct', 'union', 'enum'))} types, {count} functions "
          f"(library {manifest.get('library_version')}, commit {manifest.get('commit')})")


if __name__ == "__main__":
    main()
