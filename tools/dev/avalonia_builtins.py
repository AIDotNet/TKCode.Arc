#!/usr/bin/env python3
"""Lists the Avalonia built-in controls Arc uses, as input for the base theme that replaces AtomUI's.

Usage:
  python3 -I tools/dev/avalonia_builtins.py [--root <repo-root>] [--dir src] [--json] [--files N]

Scans <repo-root>/<dir> (default src/; pass --dir several times, e.g. --dir src --dir gallery):

  * *.axaml: elements in the Avalonia XML namespace whose name is an Avalonia control (ScrollViewer, TextBox, ...).
    A usage counts as "explicit" when the element sets Theme="...", or when a Style in the same file whose selector
    targets it (by #Name or by type as the last selector step) sets the Theme property.
  * *.cs: `new X(` / `new X {` of Avalonia controls (resolved like the compiler would: a type Arc declares in the
    file's namespace or a parent namespace shadows the Avalonia one; the Avalonia namespace must be imported or
    spelled out). "explicit" means the object initializer, or a later `<variable>.Theme =`, sets a theme.
  * *.cs classes whose direct base class is an Avalonia templated control, with whether Arc declares a ControlTheme
    for the class itself and what its StyleKeyOverride points at.
  * hosts and template parts that need a theme whenever an app runs (Window, PopupRoot, OverlayPopupHost, ...).

Writes a Markdown report to stdout (or JSON with --json). The Avalonia type catalog below is a snapshot of
Avalonia 12.1.4 (Avalonia.Controls + Avalonia.Base exported types deriving from Control); "templated" means it
derives from TemplatedControl and therefore renders nothing without a ControlTheme that sets Template.
"""
import argparse
import collections
import json
import os
import re
import sys
import xml.parsers.expat

AVALONIA_XMLNS = "https://github.com/avaloniaui"

# name -> (namespace, templated). Snapshot of Avalonia 12.1.4; abstract types are left out (never instantiated).
_CATALOG = """
Avalonia.Controls: T AutoCompleteBox Button ButtonSpinner Calendar CalendarDatePicker Carousel CarouselPage CheckBox
  ComboBox ComboBoxItem CommandBar CommandBarButton CommandBarSeparator CommandBarToggleButton ContentControl ContentPage
  ContextMenu DataValidationErrors DatePicker DatePickerPresenter DrawerPage DropDownButton Expander FlyoutPresenter
  GridSplitter GroupBox HyperlinkButton ItemsControl Label ListBox ListBoxItem MaskedTextBox Menu MenuFlyoutPresenter
  MenuItem NativeMenuBar NavigationPage NumericUpDown PageNavigationHost PathIcon PipsPager ProgressBar RadioButton
  RefreshContainer RefreshVisualizer RepeatButton ScrollViewer Separator Slider SplitButton SplitView TabControl
  TabItem TabbedPage TableView TableViewCell TableViewColumnHeader TableViewRow TextBox TimePicker TimePickerPresenter
  ToggleSplitButton ToggleSwitch ToolTip TransitioningContentControl TreeView TreeViewItem UserControl Window WindowBase
Avalonia.Controls: C Border Canvas Control Decorator DockPanel ExperimentalAcrylicBorder Grid Image
  LayoutTransformControl NativeControlHost Panel RelativePanel ReversibleStackPanel SelectableTextBlock StackPanel
  TextBlock ThemeVariantScope TickBar Viewbox VirtualizingCarouselPanel VirtualizingStackPanel WrapPanel
Avalonia.Controls.Embedding: T EmbeddableControlRoot
Avalonia.Controls.Notifications: T NotificationCard WindowNotificationManager
Avalonia.Controls.Presenters: C ContentPresenter ItemsPresenter ScrollContentPresenter TableViewCellsPresenter
  TableViewColumnHeadersPresenter TextPresenter
Avalonia.Controls.Primitives: T CalendarButton CalendarDayButton CalendarItem HeaderedContentControl
  HeaderedItemsControl HeaderedSelectingItemsControl OverlayPopupHost PopupRoot ScrollBar SelectingItemsControl
  TabStrip TabStripItem TemplatedControl TextSelectionHandle Thumb ToggleButton
Avalonia.Controls.Primitives: C AccessText AdornerLayer DateTimePickerPanel OverlayLayer Popup TextSelectorLayer
  Track UniformGrid VisualLayerManager
Avalonia.Controls.Shapes: C Arc Ellipse Line Path Polygon Polyline Rectangle Sector
"""


def _parse_catalog():
    catalog = {}
    namespace = None
    templated = False
    for line in _CATALOG.strip().splitlines():
        if not line.startswith(" "):
            head, rest = line.split(":", 1)
            namespace = head.strip()
            kind, *names = rest.split()
            templated = kind == "T"
        else:
            names = line.split()
        for name in names:
            catalog[name] = (namespace, templated)
    return catalog


CATALOG = _parse_catalog()

# Hosts and template parts every app needs, whether or not Arc's own templates mention them.
HOSTS = [
    ("Window", "桌面宿主窗口（Gallery.Desktop、用户应用）；去掉 AtomUI 的 Window 之后，内置 Window 也需要模板", None),
    ("EmbeddableControlRoot", "Browser 单视图宿主（Gallery.Browser）", None),
    ("PopupRoot", "原生弹出窗口：ShouldUseOverlayLayer=false 的 Popup、ContextMenu/ToolTip 在桌面上的默认宿主", None),
    ("OverlayPopupHost", "覆盖层弹出：ArcPopup 默认走覆盖层；Browser 与 Headless 下所有 Popup", None),
    ("ScrollBar", "ScrollViewer 模板的滚动条部件（Arc 的 ScrollViewer 用法若不自带模板，就靠它）", None),
    ("Thumb", "ScrollBar / GridSplitter 模板部件", None),
    ("RepeatButton", "ScrollBar 两端按钮（Fluent 风格模板中）", None),
    ("ToolTip", "`ToolTip.Tip` 附加属性创建的提示控件", r"\bToolTip\.(Tip|SetTip)\b"),
    ("DataValidationErrors", "绑定校验失败时包裹输入控件的错误装饰（模板中常见）", r"\bDataValidationErrors\b"),
    ("TextSelectionHandle", "TextBox 触摸选择手柄（Mobile、Browser 触屏）", None),
    ("ContextMenu", "TextBox 默认右键菜单（剪切/复制/粘贴），以及显式 ContextMenu", r"\bContextMenu\b"),
    ("MenuFlyoutPresenter", "MenuFlyout / TextBox 默认 ContextFlyout 的呈现器", r"\bMenuFlyout\b"),
    ("MenuItem", "ContextMenu / MenuFlyout 的菜单项", None),
    ("FlyoutPresenter", "`Flyout` 的呈现器", r"\bnew\s+Flyout\b|<Flyout\b|\bFlyout\.ShowAttachedFlyout\b"),
]

DECL_RE = re.compile(r"\b(?:class|record|struct|interface|enum)\s+(\w+)")
NAMESPACE_RE = re.compile(r"^\s*namespace\s+([\w.]+)", re.M)
USING_RE = re.compile(r"^\s*using\s+(?:static\s+)?([\w.]+)\s*;", re.M)
ALIAS_RE = re.compile(r"^\s*using\s+(\w+)\s*=\s*(?:global::)?([\w.]+)\s*;", re.M)
NEW_RE = re.compile(r"\bnew\s+((?:global::)?(?:[A-Z]\w*\.)+)?(\w+)\s*(?=[({])")
CLASS_RE = re.compile(
    r"((?:\b(?:public|internal|private|protected|sealed|abstract|partial|static)\s+)*)class\s+(\w+)(?:<[^>{]*>)?"
    r"(?:\s*\([^)]*\))?\s*:\s*((?:global::)?[\w.]+)", re.M)
STYLEKEY_RE = re.compile(r"StyleKeyOverride\s*=>\s*typeof\(\s*([\w.]+)\s*\)")
ASSIGN_RE = re.compile(r"\b(?:var|[A-Z]\w*)\s+(\w+)\s*=\s*$|(\b[\w.]+)\s*=\s*$")


def rel(path, root):
    return os.path.relpath(path, root)


def walk(root, dirs, ext):
    for d in dirs:
        base = os.path.join(root, d)
        for dirpath, dirnames, filenames in os.walk(base):
            dirnames[:] = [n for n in dirnames if n not in ("bin", "obj", "GeneratedFiles", "node_modules")]
            for name in sorted(filenames):
                if name.endswith(ext):
                    yield os.path.join(dirpath, name)


# ---------------------------------------------------------------- axaml

class AxamlScan:
    def __init__(self, path):
        self.path = path
        self.elements = []        # (type, name, line, has_theme, in_template)
        self.theme_selectors = []  # selectors of Styles that set Theme
        self.control_themes = []  # (key, target_type, line)
        self._stack = []
        self._style_selector = []

    def run(self):
        parser = xml.parsers.expat.ParserCreate(namespace_separator=" ")
        parser.StartElementHandler = lambda n, a: self._start(parser, n, a)
        parser.EndElementHandler = self._end
        with open(self.path, "rb") as f:
            parser.ParseFile(f)
        return self

    def _start(self, parser, qname, attrs):
        ns, _, local = qname.rpartition(" ")
        self._stack.append(local)
        attrs = {k.rpartition(" ")[2]: v for k, v in attrs.items()}
        if local == "Style":
            self._style_selector.append(attrs.get("Selector", ""))
        if local == "Setter" and attrs.get("Property") == "Theme" and self._style_selector:
            self.theme_selectors.append(self._style_selector[-1])
        if local == "ControlTheme":
            self.control_themes.append((attrs.get("Key"), attrs.get("TargetType", ""), parser.CurrentLineNumber))
        if ns == AVALONIA_XMLNS and "." not in local and local in CATALOG:
            in_template = "ControlTemplate" in self._stack[:-1]
            name = attrs.get("Name")
            explicit = "Theme" in attrs or "Template" in attrs
            self.elements.append((local, name, parser.CurrentLineNumber, explicit, in_template))

    def _end(self, qname):
        local = qname.rpartition(" ")[2]
        self._stack.pop()
        if local == "Style":
            self._style_selector.pop()

    def styled_theme(self, type_name, name):
        for selector in self.theme_selectors:
            for part in selector.split(","):
                last = re.split(r"\s+|>|/template/", part.strip())[-1]
                if name and re.search(r"#" + re.escape(name) + r"\b", last):
                    return True
                if re.match(re.escape(type_name) + r"(?![\w])", last) and "#" not in last:
                    return True
        return False


def target_name(target):
    """`arc:Button`, `{x:Type arc:Button}`, `Button` -> ('arc', 'Button') / ('', 'Button')."""
    t = target.strip()
    m = re.match(r"\{x:Type\s+([^}]+)\}", t)
    if m:
        t = m.group(1).strip()
    prefix, _, local = t.rpartition(":")
    return prefix, local


# ---------------------------------------------------------------- C#

class CsIndex:
    def __init__(self, files):
        self.files = files
        self.texts = {}
        self.declared = collections.defaultdict(set)  # name -> namespaces
        for path in files:
            text = open(path, encoding="utf-8-sig").read()
            text = re.sub(r"//[^\n]*", "", text)
            self.texts[path] = text
            ns = self.namespace(text)
            for m in DECL_RE.finditer(text):
                self.declared[m.group(1)].add(ns)

    @staticmethod
    def namespace(text):
        m = NAMESPACE_RE.search(text)
        return m.group(1) if m else ""

    def resolve(self, text, qualifier, name):
        """True when `qualifier name` in this file means the Avalonia control `name`."""
        if name not in CATALOG:
            return False
        av_ns = CATALOG[name][0]
        if qualifier:
            q = qualifier.replace("global::", "").rstrip(".")
            aliases = dict(ALIAS_RE.findall(text))
            q = aliases.get(q, q)
            return q == av_ns
        ns = self.namespace(text)
        parts = ns.split(".") if ns else []
        for i in range(len(parts), 0, -1):
            if ".".join(parts[:i]) in self.declared.get(name, ()):
                return False
        aliases = dict(ALIAS_RE.findall(text))
        if name in aliases:
            return aliases[name] == av_ns + "." + name
        return av_ns in USING_RE.findall(text)


def initializer(text, start):
    """The `{ ... }` object initializer right after position `start` (after optional `(...)`), or ''."""
    i = start
    depth = 0
    while i < len(text) and text[i] in " \t\r\n":
        i += 1
    if i < len(text) and text[i] == "(":
        depth = 0
        while i < len(text):
            if text[i] == "(":
                depth += 1
            elif text[i] == ")":
                depth -= 1
                if depth == 0:
                    i += 1
                    break
            i += 1
        while i < len(text) and text[i] in " \t\r\n":
            i += 1
    if i >= len(text) or text[i] != "{":
        return ""
    depth = 0
    begin = i
    while i < len(text):
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                return text[begin:i + 1]
        i += 1
    return ""


def assigned_variable(text, pos):
    line_start = text.rfind("\n", 0, pos) + 1
    before = text[line_start:pos]
    m = re.search(r"(?:\bvar\s+|\b[A-Z]\w*\??\s+)?([_\w.]+)\s*=\s*$", before)
    return m.group(1) if m else None


def line_of(text, pos):
    return text.count("\n", 0, pos) + 1


# ---------------------------------------------------------------- report

def scan(root, dirs):
    axaml_files = list(walk(root, dirs, ".axaml"))
    cs_files = list(walk(root, dirs, ".cs"))

    axaml_usage = collections.defaultdict(list)  # type -> [(file, line, explicit, how, in_template)]
    arc_themes = collections.defaultdict(list)   # arc type -> files with a ControlTheme for it
    keyed_builtin_themes = collections.defaultdict(list)  # avalonia type -> [(file, key)]
    default_builtin_themes = collections.defaultdict(list)
    for path in axaml_files:
        s = AxamlScan(path).run()
        for type_name, name, line, has_theme, in_template in s.elements:
            how = "Theme=" if has_theme else ("Style" if s.styled_theme(type_name, name) else "")
            axaml_usage[type_name].append((rel(path, root), line, bool(how), how, in_template))
        for key, target, line in s.control_themes:
            prefix, local = target_name(target)
            if prefix and prefix != "x":
                arc_themes[local].append(rel(path, root))
            elif local in CATALOG:
                if key and not key.startswith("{x:Type"):
                    keyed_builtin_themes[local].append((rel(path, root), key))
                else:
                    default_builtin_themes[local].append(rel(path, root))

    # Declarations come from src/ as well, so a type Arc declares shadows the Avalonia one when scanning gallery/.
    index = CsIndex(sorted(set(cs_files) | set(walk(root, ["src"], ".cs"))))
    cs_usage = collections.defaultdict(list)
    derived = []
    for path in cs_files:
        text = index.texts[path]
        for m in NEW_RE.finditer(text):
            qualifier, name = m.group(1), m.group(2)
            if not index.resolve(text, qualifier, name):
                continue
            init = initializer(text, m.end())
            explicit = bool(re.search(r"\b(Theme|Template)\s*=", init))
            if not explicit:
                var = assigned_variable(text, m.start())
                if var and re.search(re.escape(var) + r"\.(Theme|Template)\s*=", text):
                    explicit = True
            cs_usage[name].append((rel(path, root), line_of(text, m.start()), explicit, "Theme=" if explicit else "", False))
        class_starts = [c.start() for c in DECL_RE.finditer(text)]
        for m in CLASS_RE.finditer(text):
            modifiers, cls, base = m.group(1), m.group(2), m.group(3)
            base = dict(ALIAS_RE.findall(text)).get(base, base)
            qualifier, _, base_name = base.rpartition(".")
            if not index.resolve(text, qualifier + "." if qualifier else "", base_name):
                continue
            if not CATALOG[base_name][1]:
                continue
            body_end = next((c for c in class_starts if c > m.end()), len(text))
            style_key = STYLEKEY_RE.search(text[m.end():body_end])
            derived.append({
                "abstract": "abstract" in modifiers,
                "sets_template": bool(re.search(r"\b(Template|Theme)\s*=", text[m.end():body_end])),
                "class": cls,
                "base": base_name,
                "file": rel(path, root),
                "line": line_of(text, m.start()),
                "style_key": style_key.group(1).rpartition(".")[2] if style_key else None,
            })
    for d in derived:
        key = d["style_key"] or d["class"]
        d["arc_theme"] = sorted(set(arc_themes.get(key, [])))
        d["builtin_key"] = d["style_key"] in CATALOG and d["style_key"] not in index.declared

    host_refs = {}
    all_text = {p: index.texts[p] for p in cs_files}
    all_text.update({p: open(p, encoding="utf-8-sig").read() for p in axaml_files})
    for name, why, pattern in HOSTS:
        regex = re.compile(pattern) if pattern else re.compile(r"\b" + re.escape(name) + r"\b")
        files = sorted(rel(p, root) for p, t in all_text.items() if regex.search(t))
        host_refs[name] = files

    return {
        "axaml": axaml_usage,
        "cs": cs_usage,
        "derived": derived,
        "arc_themes": arc_themes,
        "keyed_builtin_themes": keyed_builtin_themes,
        "default_builtin_themes": default_builtin_themes,
        "hosts": host_refs,
    }


def summarize(usages, max_files):
    counter = collections.Counter(u[0] for u in usages)
    files = ", ".join(f"`{f}`×{n}" if n > 1 else f"`{f}`" for f, n in counter.most_common(max_files))
    if len(counter) > max_files:
        files += f" …（另 {len(counter) - max_files} 个文件）"
    return files


def markdown(result, max_files):
    out = []
    p = out.append
    types = sorted(set(result["axaml"]) | set(result["cs"]),
                   key=lambda t: (not CATALOG[t][1], -(len(result["axaml"].get(t, [])) + len(result["cs"].get(t, []))), t))
    p("## Avalonia 内置控件用法（AXAML 与 C#）")
    p("")
    p("“需要主题”= 派生自 `TemplatedControl`，没有设置 `Template` 的 ControlTheme 就不渲染任何东西。"
      "“显式主题”= 这处用法自己指定了 `Theme`（属性、样式 Setter 或代码），不依赖默认主题。")
    p("")
    p("| 类型 | 需要主题 | AXAML | C# `new` | 显式主题 | 依赖默认主题 | 文件 |")
    p("|---|---|---:|---:|---:|---:|---|")
    for t in types:
        ax = result["axaml"].get(t, [])
        cs = result["cs"].get(t, [])
        all_u = ax + cs
        explicit = sum(1 for u in all_u if u[2])
        needs = "是" if CATALOG[t][1] else "否"
        default = len(all_u) - explicit if CATALOG[t][1] else "—"
        p(f"| `{t}` | {needs} | {len(ax)} | {len(cs)} | {explicit} | {default} | {summarize(all_u, max_files)} |")
    p("")

    p("## 派生自 Avalonia 模板控件的 Arc 类")
    p("")
    concrete = [d for d in result["derived"] if not d["abstract"]]
    themed = [d for d in concrete if d["arc_theme"] and not d["builtin_key"]]
    by_base = collections.Counter(d["base"] for d in themed)
    p(f"直接派生自 Avalonia 模板控件的非抽象 Arc 类共 {len(concrete)} 个，其中 {len(themed)} 个有 Arc 自己的 ControlTheme"
      f"（按基类：{', '.join(f'`{b}` {n}' for b, n in by_base.most_common())}），与 AtomUI 无关。"
      "下表是其余的类：没有 Arc 主题，或 StyleKeyOverride 指向内置类型（借用内置主题）。")
    p("")
    p("| 类 | 基类 | StyleKeyOverride | 代码里设置 Template/Theme | 文件 |")
    p("|---|---|---|---|---|")
    for d in sorted((d for d in concrete if d not in themed), key=lambda d: (d["base"], d["class"])):
        key = f"`{d['style_key']}`" if d["style_key"] else "—"
        sets = "是" if d["sets_template"] else "否"
        p(f"| `{d['class']}` | `{d['base']}` | {key} | {sets} | `{d['file']}:{d['line']}` |")
    p("")

    if result["keyed_builtin_themes"] or result["default_builtin_themes"]:
        p("## Arc 为内置控件定义的 ControlTheme")
        p("")
        p("| 类型 | 带键主题（按需引用） | 默认主题（按类型键） |")
        p("|---|---|---|")
        for t in sorted(set(result["keyed_builtin_themes"]) | set(result["default_builtin_themes"])):
            keyed = result["keyed_builtin_themes"].get(t, [])
            p(f"| `{t}` | {len(keyed)} | {len(result['default_builtin_themes'].get(t, []))} |")
        p("")

    p("## 宿主与模板部件")
    p("")
    p("| 类型 | 为什么需要 | 在扫描范围内被提到的文件数 |")
    p("|---|---|---:|")
    for name, why, _ in HOSTS:
        p(f"| `{name}` | {why} | {len(result['hosts'][name])} |")
    p("")

    need = sorted({t for t in types if CATALOG[t][1] and any(not u[2] for u in result["axaml"].get(t, []) + result["cs"].get(t, []))}
                  | {d["style_key"] for d in result["derived"] if d["builtin_key"]}
                  | {name for name, _, _ in HOSTS})
    p("## 需要基础主题的类型（汇总）")
    p("")
    p(", ".join(f"`{t}`" for t in need))
    p("")
    return "\n".join(out)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--root", default=os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")))
    parser.add_argument("--dir", action="append", dest="dirs")
    parser.add_argument("--json", action="store_true")
    parser.add_argument("--files", type=int, default=6, help="files listed per type (default 6)")
    args = parser.parse_args()
    dirs = args.dirs or ["src"]
    result = scan(args.root, dirs)
    if args.json:
        payload = {
            "usages": {
                t: {
                    "templated": CATALOG[t][1],
                    "namespace": CATALOG[t][0],
                    "axaml": [dict(file=f, line=l, explicit=e, how=h, in_template=i) for f, l, e, h, i in result["axaml"].get(t, [])],
                    "cs": [dict(file=f, line=l, explicit=e) for f, l, e, _, _ in result["cs"].get(t, [])],
                }
                for t in sorted(set(result["axaml"]) | set(result["cs"]))
            },
            "derived": result["derived"],
            "hosts": {name: dict(reason=why, files=result["hosts"][name]) for name, why, _ in HOSTS},
            "keyed_builtin_themes": result["keyed_builtin_themes"],
            "default_builtin_themes": result["default_builtin_themes"],
        }
        json.dump(payload, sys.stdout, ensure_ascii=False, indent=2)
        print()
    else:
        print(markdown(result, args.files))


if __name__ == "__main__":
    main()
