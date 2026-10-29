#!/usr/bin/env python3
"""F3 switch-over: the mechanical part of moving TKCode.Arc from AtomUI to TKCode.Arc.Foundation.

Usage:
  python3 -I tools/dev/migrate_atomui.py [--root <repo-root>] [--dry-run] [--verbose]

Idempotent: a second run changes nothing. With --dry-run nothing is written; the summary says what would change.
Plan: design/roadmap/atomui-replacement.md (F3) and design/architecture/foundation.md section 11.

What it does (only what is safely mechanical):

  1. C# (src/, gallery/, tests/, tools/): deletes `using AtomUI.Theme.DesignTokens;` (the token declarations keep their
     names; the attribute and base class now come from Foundation in namespace TKCode.Arc).
  2. Every `**/Themes/*Theme.axaml` under src/ gets `x:Class` (Foundation ARCF010): `<RootNamespace>.<FileName>`;
     when two theme files of one project share a file name, the class gets the folder names as prefix. Files that
     already have x:Class are left alone. Files without `x:ClassModifier` get `internal` (the generator makes them
     public otherwise).
  3. Project files: removes `AtomUIRegistrationPackageId`, `AtomUIThemeControlCatalog` and `AtomUIThemeDefinition`
     items (and the comment line right above them); replaces the `AtomUI.Desktop.Controls` package reference with a
     Foundation project reference in TKCode.Arc.Core and drops it elsewhere (Foundation flows through Core); projects
     that declare tokens, theme files or language catalogs (Localization/*.xlf) get `<ArcFoundationGenerator>true</ArcFoundationGenerator>`, for which
     build/Arc.Repository.targets adds the generator (as analyzer) and imports Foundation's targets file.
  4. Deletes `<project>/GeneratedFiles/AtomUI.Generator/` (AtomUI's generator output; git-ignored).
  5. Directory.Packages.props: removes the AtomUI package versions and their comment.

Everything else that still names AtomUI is listed at the end (file, line, category) and left for hand work:
entry points, theme switching, localization, test hosts.
"""
import argparse
import collections
import os
import re
import shutil
import sys

SKIP_DIRS = {"bin", "obj", ".git", ".work", ".claude", "artifacts", ".artifacts", "node_modules", "GeneratedFiles",
             "TestResults"}
# Foundation is the replacement itself; FoundationAotSample is a standalone spike.
SKIP_PREFIXES = ("src/TKCode.Arc.Foundation", "tests/TKCode.Arc.Foundation.Tests", "tools/dev/FoundationAotSample")
CODE_ROOTS = ("src", "gallery", "tests", "tools")

USING_DESIGN_TOKENS = re.compile(r"^[ \t]*using[ \t]+AtomUI\.Theme\.DesignTokens[ \t]*;[ \t]*\r?\n", re.M)
ATOMUI_MENTION = re.compile(r"AtomUI")

CSPROJ_ITEMS = re.compile(
    r"(?P<comment>^[ \t]*<!--(?:(?!-->).)*-->[ \t]*\r?\n)?"
    r"^[ \t]*<(?P<tag>AtomUIRegistrationPackageId|AtomUIThemeControlCatalog)>[^<]*</(?P=tag)>[ \t]*\r?\n"
    r"|^[ \t]*<AtomUIThemeDefinition\b[^>]*/>[ \t]*\r?\n",
    re.M)
ATOMUI_PACKAGE_REF = re.compile(r"^(?P<indent>[ \t]*)<PackageReference Include=\"AtomUI\.Desktop\.Controls\"[ \t]*/>[ \t]*\r?\n", re.M)
FOUNDATION_REF = '<ProjectReference Include="../TKCode.Arc.Foundation/TKCode.Arc.Foundation.csproj" />'
GENERATOR_PROPERTY = "<ArcFoundationGenerator>true</ArcFoundationGenerator>"
PROPERTY_GROUP_END = re.compile(r"^(?P<indent>[ \t]*)</PropertyGroup>", re.M)

PACKAGES_LINES = re.compile(
    r"^[ \t]*<!--[^\n]*AtomUI[^\n]*-->[ \t]*\r?\n"
    r"|^[ \t]*<PackageVersion Include=\"AtomUI\.[^\"]+\"[^>]*/>[ \t]*\r?\n",
    re.M)

THEME_ROOT = re.compile(r"<ResourceDictionary\b(?P<attrs>[^>]*)>", re.S)
TOKEN_ATTRIBUTE = re.compile(r"\[ControlDesignToken\b")

# Categories for the leftover report: pattern → category (first match wins).
LEFTOVER_CATEGORIES = [
    (re.compile(r"using AtomUI\.Generated\.|GeneratedControlPackageRegistration|GeneratedThemePackageRegistration|IAtomUIBuilder|UseAtomUI|UseDesktopControls|ApplicationStartup"), "registration / entry point"),
    (re.compile(r"AtomUI\.Localization|LanguageCatalog|LanguageTag|GetLocalizer|GetLanguageManager|ILanguageManager|LangResource"), "localization"),
    (re.compile(r"ThemeConfig|ThemeAlgorithm|IThemeManager|GetThemeManager|ThemeRequest|AtomUI\.Theme\.Configuration|AtomUI\.Theme\.Algorithms|AtomUI\.Theme\.Schema|AtomUI\.Theme;|ControlTokenAccessor|AtomUI\.Theme\.Resources"), "theme API"),
    (re.compile(r"using AtomUI;|using AtomUI\.Desktop\.Controls;"), "AtomUI root / desktop-controls namespace"),
    (re.compile(r"<PackageReference Include=\"AtomUI|AtomUIVersion|ArcWasm\w+Assembly"), "build"),
    (re.compile(r"//|<!--|///|\*"), "comment / doc text"),
]


def rel(root, path):
    return os.path.relpath(path, root).replace(os.sep, "/")


def walk(root, top, suffixes):
    base = os.path.join(root, top)
    for directory, dirs, files in os.walk(base):
        dirs[:] = sorted(d for d in dirs if d not in SKIP_DIRS)
        r = rel(root, directory)
        if r.startswith(SKIP_PREFIXES):
            dirs[:] = []
            continue
        for name in sorted(files):
            if name.endswith(suffixes):
                yield os.path.join(directory, name)


def read(path):
    with open(path, "r", encoding="utf-8", newline="") as handle:
        return handle.read()


class Migration:
    def __init__(self, root, dry_run, verbose):
        self.root = root
        self.dry_run = dry_run
        self.verbose = verbose
        self.counts = collections.Counter()
        self.reports = []          # (category, file, line, text) needing hand work
        self.changed_files = set()

    def write(self, path, text):
        self.changed_files.add(rel(self.root, path))
        if not self.dry_run:
            with open(path, "w", encoding="utf-8", newline="") as handle:
                handle.write(text)

    def note(self, message):
        if self.verbose:
            print("  " + message)

    # 1. C# usings ---------------------------------------------------------------------------------------------------
    def rewrite_usings(self):
        for top in CODE_ROOTS:
            for path in walk(self.root, top, (".cs",)):
                text = read(path)
                new, n = USING_DESIGN_TOKENS.subn("", text)
                if n:
                    self.counts["cs: using AtomUI.Theme.DesignTokens removed"] += n
                    self.write(path, new)
                    self.note(f"using removed: {rel(self.root, path)}")

    # 2. x:Class on theme files --------------------------------------------------------------------------------------
    def add_theme_classes(self):
        for project_dir, csproj in self.projects():
            themes = [p for p in walk(self.root, rel(self.root, project_dir), (".axaml",))
                      if os.path.basename(os.path.dirname(p)) == "Themes" and p.endswith("Theme.axaml")]
            if not themes:
                continue
            root_ns = self.root_namespace(csproj)
            by_name = collections.defaultdict(list)
            for path in themes:
                by_name[os.path.basename(path)[:-len(".axaml")]].append(path)
            for name, paths in by_name.items():
                for path in paths:
                    if len(paths) == 1:
                        class_name = f"{root_ns}.{name}"
                    else:
                        # Same file name twice in one project: prefix the folders between the project and Themes/.
                        folders = os.path.relpath(os.path.dirname(os.path.dirname(path)), project_dir).split(os.sep)
                        prefix = "".join(re.sub(r"\W", "", f) for f in folders if f not in (".", ""))
                        class_name = f"{root_ns}.{prefix}{name}"
                        self.counts["axaml: x:Class with folder prefix (name clash)"] += 1
                    self.add_class(path, class_name)

    def add_class(self, path, class_name):
        text = read(path)
        match = THEME_ROOT.search(text)
        if match is None:
            self.reports.append(("theme file without a ResourceDictionary root", rel(self.root, path), 1, ""))
            return
        attrs = match.group("attrs")
        if re.search(r"\bx:Class\s*=", attrs):
            self.counts["axaml: x:Class already present"] += 1
            return
        if 'xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"' not in attrs:
            self.reports.append(("theme file without the x namespace on its root", rel(self.root, path), 1, ""))
            return
        addition = f'x:Class="{class_name}"'
        if not re.search(r"\bx:ClassModifier\s*=", attrs):
            addition += ' x:ClassModifier="internal"'
            self.counts["axaml: x:ClassModifier=internal added"] += 1
        # Same layout as the other attributes: on its own line, aligned with the previous attribute.
        lines = attrs.split("\n")
        if len(lines) > 1:
            indent = re.match(r"[ \t]*", lines[-1]).group(0)
            new_attrs = attrs.rstrip() + "\n" + indent + addition
            if attrs.rstrip() != attrs:
                new_attrs += attrs[len(attrs.rstrip()):]
        else:
            new_attrs = attrs.rstrip() + " " + addition
        new = text[:match.start("attrs")] + new_attrs + text[match.end("attrs"):]
        self.counts["axaml: x:Class added"] += 1
        self.write(path, new)

    # 3. Project files -----------------------------------------------------------------------------------------------
    def projects(self):
        for top in ("src", "gallery", "tests", "tools"):
            for path in walk(self.root, top, (".csproj",)):
                yield os.path.dirname(path), path

    @staticmethod
    def root_namespace(csproj):
        text = read(csproj)
        match = re.search(r"<RootNamespace>([^<]+)</RootNamespace>", text) or re.search(r"<AssemblyName>([^<]+)</AssemblyName>", text)
        return match.group(1).strip() if match else os.path.splitext(os.path.basename(csproj))[0]

    def has_foundation_inputs(self, project_dir):
        r = rel(self.root, project_dir)
        for path in walk(self.root, r, (".cs", ".axaml", ".xlf")):
            if path.endswith(".axaml"):
                if os.path.basename(os.path.dirname(path)) == "Themes" and path.endswith("Theme.axaml"):
                    return True
            elif path.endswith(".xlf"):
                if os.path.basename(os.path.dirname(path)) == "Localization":
                    return True
            elif TOKEN_ATTRIBUTE.search(read(path)):
                return True
        return False

    def rewrite_projects(self):
        for project_dir, csproj in self.projects():
            text = read(csproj)
            new = text
            name = os.path.splitext(os.path.basename(csproj))[0]

            new, n = CSPROJ_ITEMS.subn("", new)
            if n:
                self.counts["csproj: AtomUI MSBuild items removed"] += n

            def package(match):
                self.counts["csproj: AtomUI.Desktop.Controls reference removed"] += 1
                if name == "TKCode.Arc.Core":
                    self.counts["csproj: Foundation project reference added"] += 1
                    return match.group("indent") + FOUNDATION_REF + "\n"
                return ""
            new = ATOMUI_PACKAGE_REF.sub(package, new)

            if (rel(self.root, project_dir).startswith(("src/", "gallery/")) and GENERATOR_PROPERTY not in new
                    and self.has_foundation_inputs(project_dir)):
                group = PROPERTY_GROUP_END.search(new)
                if group:
                    indent = group.group("indent")
                    new = new[:group.start()] + indent + "    " + GENERATOR_PROPERTY + "\n" + new[group.start():]
                    self.counts["csproj: ArcFoundationGenerator=true added"] += 1

            # An emptied <ItemGroup> or <PropertyGroup> is removed.
            new = re.sub(r"^[ \t]*<ItemGroup>\s*</ItemGroup>[ \t]*\r?\n", "", new, flags=re.M)
            if new != text:
                self.write(csproj, new)

    def rewrite_packages(self):
        path = os.path.join(self.root, "Directory.Packages.props")
        if not os.path.exists(path):
            return
        text = read(path)
        new, n = PACKAGES_LINES.subn("", text)
        if n:
            self.counts["Directory.Packages.props: AtomUI lines removed"] += n
            self.write(path, new)

    # 4. AtomUI generator output -------------------------------------------------------------------------------------
    def delete_generated(self):
        for top in ("src", "gallery", "tests", "tools"):
            base = os.path.join(self.root, top)
            if not os.path.isdir(base):
                continue
            for directory, dirs, _ in os.walk(base):
                dirs[:] = [d for d in dirs if d not in SKIP_DIRS - {"GeneratedFiles"}]
                if os.path.basename(directory) == "GeneratedFiles" and "AtomUI.Generator" in dirs:
                    target = os.path.join(directory, "AtomUI.Generator")
                    self.counts["GeneratedFiles/AtomUI.Generator deleted"] += 1
                    self.note(f"delete {rel(self.root, target)}")
                    if not self.dry_run:
                        shutil.rmtree(target)
                    dirs.remove("AtomUI.Generator")

    # Report ---------------------------------------------------------------------------------------------------------
    def collect_leftovers(self):
        for top in CODE_ROOTS:
            for path in walk(self.root, top, (".cs", ".axaml", ".csproj", ".props", ".targets")):
                if self.dry_run and rel(self.root, path) in self.changed_files and path.endswith(".cs"):
                    text = USING_DESIGN_TOKENS.sub("", read(path))
                else:
                    text = read(path)
                for number, line in enumerate(text.splitlines(), 1):
                    if not ATOMUI_MENTION.search(line):
                        continue
                    category = "other"
                    for pattern, name in LEFTOVER_CATEGORIES:
                        if pattern.search(line):
                            category = name
                            break
                    self.reports.append((category, rel(self.root, path), number, line.strip()))

    def run(self):
        self.rewrite_usings()
        self.add_theme_classes()
        self.rewrite_projects()
        self.rewrite_packages()
        self.delete_generated()
        self.collect_leftovers()

    def summary(self):
        mode = "would change" if self.dry_run else "changed"
        print(f"migrate_atomui: {len(self.changed_files)} files {mode}")
        for key in sorted(self.counts):
            print(f"  {self.counts[key]:6d}  {key}")
        by_category = collections.defaultdict(list)
        for category, path, line, text in self.reports:
            by_category[category].append((path, line, text))
        print(f"left for hand work: {len(self.reports)} lines in {len({r[1] for r in self.reports})} files")
        for category in sorted(by_category, key=lambda c: -len(by_category[c])):
            entries = by_category[category]
            files = sorted({e[0] for e in entries})
            print(f"  {len(entries):6d} lines / {len(files):3d} files  {category}")
            if self.verbose:
                for path, line, text in entries:
                    print(f"           {path}:{line}: {text[:120]}")


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--root", default=os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")))
    parser.add_argument("--dry-run", action="store_true", help="change nothing, print what would change")
    parser.add_argument("--verbose", "-v", action="store_true", help="list every change and every leftover line")
    args = parser.parse_args()
    if not os.path.exists(os.path.join(args.root, "TKCode.Arc.slnx")):
        print(f"not the repository root: {args.root}", file=sys.stderr)
        return 2
    migration = Migration(args.root, args.dry_run, args.verbose)
    migration.run()
    migration.summary()
    return 0


if __name__ == "__main__":
    sys.exit(main())
