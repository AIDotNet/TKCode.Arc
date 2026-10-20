"""Ports files from a sandbox made before the gallery and TKCode.Arc renames to the current layout.

Usage: python3 -I tools/dev/port_sandbox.py <sandbox-dir> <old-path>...
Each <old-path> is a file or directory relative to the repository root, in the sandbox's old layout
(src/AtomUI.Arc/..., demo/AtomUI.Arc.Demo/..., tests/AtomUI.Arc.Tests/..., design/...). The converted files are
written into the same sandbox at their new paths, and the new top-level paths are printed (ready for
sandbox.sh merge; pipe them through xargs, since zsh does not split an unquoted variable). Paths already in
the new layout pass through unchanged.

Renames: AtomUI.Arc -> TKCode.Arc (folders, namespaces, generated AtomUI.Generated.TKCodeArc*), the arc xmlns ->
https://github.com/AtomUICommunity/TKCode.Arc, demo/AtomUI.Arc.Demo -> gallery/TKCode.Arc.Gallery,
Demo<Name> -> Gallery<Name>, <X>Demo -> <X>Page, <Category>Demos -> <Category>Pages. Files that use AtomUI's
root namespace (GetThemeManager, IAtomUIBuilder) get `using AtomUI;`, which TKCode.Arc no longer inherits.
"""
import os, re, sys

TEXT_EXT = {".cs", ".axaml", ".md", ".csproj", ".json", ".xlf"}

def convert_text(text, is_demo):
    text = text.replace("https://atomui.net/arc/lucide", "https://github.com/AtomUICommunity/TKCode.Arc/lucide")
    text = text.replace("https://atomui.net/arc", "https://github.com/AtomUICommunity/TKCode.Arc")
    text = text.replace("AtomUI.Arc.Demo", "TKCode.Arc.Gallery")
    text = text.replace("AtomUIArc", "TKCodeArc")
    text = text.replace("AtomUI.Arc", "TKCode.Arc")
    if is_demo:
        text = re.sub(r"\bDemo([A-Z]\w*)\b", r"Gallery\1", text)
        text = re.sub(r"\b(\w+?)Demos\b", r"\1Pages", text)
        text = re.sub(r"\b(\w+?)Demo\b", r"\1Page", text)
    if re.search(r"\bGetThemeManager\b|\bIAtomUIBuilder\b", text) and not re.search(r"^using AtomUI;", text, re.M):
        text = "using AtomUI;\n" + text
    return text

def convert_path(path):
    path = path.replace("demo/AtomUI.Arc.Demo.Desktop/", "gallery/TKCode.Arc.Gallery.Desktop/", 1)
    path = path.replace("demo/AtomUI.Arc.Demo/", "gallery/TKCode.Arc.Gallery/", 1)
    path = re.sub(r"^(src|tests)/AtomUI\.Arc", r"\1/TKCode.Arc", path)
    path = re.sub(r"^gallery/AtomUI\.Arc\.Gallery", "gallery/TKCode.Arc.Gallery", path)
    if path.startswith("gallery/"):
        head, name = os.path.split(path)
        name = re.sub(r"Demos(\.[A-C])?\.cs$", r"Pages\1.cs", name)
        name = re.sub(r"Demo\.cs$", "Page.cs", name)
        path = os.path.join(head, name)
    return path

def port_file(sandbox, old):
    new = convert_path(old)
    src = os.path.join(sandbox, old)
    if os.path.splitext(old)[1] in TEXT_EXT:
        with open(src, encoding="utf-8") as f:
            data = convert_text(f.read(), old.startswith("demo/")).encode("utf-8")
    else:
        with open(src, "rb") as f:
            data = f.read()
    dst = os.path.join(sandbox, new)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    if os.path.abspath(dst) != os.path.abspath(src) or data != open(src, "rb").read():
        with open(dst, "wb") as f:
            f.write(data)
    return new

sandbox, paths = sys.argv[1], sys.argv[2:]
for old in paths:
    old = old.rstrip("/")
    full = os.path.join(sandbox, old)
    if os.path.isdir(full):
        for dirpath, dirnames, filenames in os.walk(full):
            dirnames[:] = [d for d in dirnames if d not in ("bin", "obj", "GeneratedFiles", ".scratch")]
            for name in filenames:
                port_file(sandbox, os.path.relpath(os.path.join(dirpath, name), sandbox))
        print(convert_path(old + "/").rstrip("/"))
    elif os.path.isfile(full):
        print(port_file(sandbox, old))
    else:
        print(f"missing in sandbox: {old}", file=sys.stderr)
