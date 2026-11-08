#!/usr/bin/env python3
"""Generates the app templates' text from templates/TKCode.Arc.Templates/strings.json.

The sample app's UI text is written once, in the table, in every language the templates offer. Template files use tokens:

    @@Key@@        in XAML: the text, or {l:AppLangResource Key} with --i18n
    @@cs:Key@@     in C#: a string literal, or AppLangResources.Get(AppLangResourceKind.Key) with --i18n
    @@kind:Key@@   in C#: a string literal, or AppLangResourceKind.Key with --i18n (for text resolved later)

This script writes, from the table:

- a `switch` symbol per token into every app template's template.json (replacing the ones it wrote before), choosing
  the --i18n form or the text in the --ui-language language;
- the catalog the --i18n option adds to the app: Localization/AppLangResourceKind.cs and one .xlf file per language.

    python3 tools/templates/sync_strings.py          # write
    python3 tools/templates/sync_strings.py --check  # fail when something is out of date (verify_templates.py runs it)
"""

from __future__ import annotations

import argparse
import collections
import json
import sys
from pathlib import Path
from xml.sax.saxutils import escape

REPO = Path(__file__).resolve().parents[2]
PACKAGE = REPO / "templates" / "TKCode.Arc.Templates"
TABLE = PACKAGE / "strings.json"
CATALOG = PACKAGE / "content" / "_shared" / "App" / "Features" / "I18n" / "Localization"
TEMPLATES = [
    PACKAGE / "content" / "ArcStarterApp" / ".template.config" / "template.json",
    PACKAGE / "content" / "ArcXplatApp" / ".template.config" / "template.json",
]
SOURCE_LANGUAGE = "en-US"
LANGUAGES = ["en-US", "zh-CN", "zh-TW"]
PREFIXES = ("text:", "cs:", "kind:")


def csharp_literal(text: str) -> str:
    return '"' + text.replace("\\", "\\\\").replace('"', '\\"') + '"'


def xml_attribute(text: str) -> str:
    return escape(text, {'"': "&quot;"})


def switch(token: str, i18n_value: str, texts: dict[str, str], form) -> dict:
    cases = [{"condition": "(i18n)", "value": i18n_value}]
    for language in LANGUAGES:
        if language != SOURCE_LANGUAGE:
            cases.append({"condition": f'(uiLanguage == "{language}")', "value": form(texts[language])})
    cases.append({"condition": "", "value": form(texts[SOURCE_LANGUAGE])})
    return {
        "type": "generated",
        "generator": "switch",
        "replaces": token,
        "parameters": {"evaluator": "C++", "datatype": "string", "cases": cases},
    }


def symbols(table: dict) -> dict:
    result = collections.OrderedDict()
    for key, entry in table.items():
        texts = {language: entry[language] for language in LANGUAGES}
        forms = entry.get("forms", [])
        result[f"text:{key}"] = switch(f"@@{key}@@", f"{{l:AppLangResource {key}}}", texts, xml_attribute)
        if "cs" in forms:
            result[f"cs:{key}"] = switch(f"@@cs:{key}@@", f"AppLangResources.Get(AppLangResourceKind.{key})", texts,
                                         csharp_literal)
        if "kind" in forms:
            result[f"kind:{key}"] = switch(f"@@kind:{key}@@", f"AppLangResourceKind.{key}", texts, csharp_literal)
    return result


def header(language: str) -> str:
    if language == SOURCE_LANGUAGE:
        return ("The app's text in English, the source language: one unit per member of AppLangResourceKind, "
                "with the same id.")
    return (f"The app's text in {language}: each unit's target translates its source; a missing unit fails "
            "the build (ARCF021).")


def catalog_files(table: dict) -> dict[Path, str]:
    files: dict[Path, str] = {}
    members = "\n".join(f"    {key}," for key in table)
    files[CATALOG / "AppLangResourceKind.cs"] = f"""using TKCode.Arc.Localization;

namespace ArcStarterApp.Localization;

/// <summary>
/// The app's text, one member per string. The Foundation generator turns this enum and the .xlf files next to it into
/// AppLangResources (C#: AppLangResources.Get(AppLangResourceKind.Home)) and the {{l:AppLangResource Home}} XAML
/// extension, which follows the language when ArcLanguage.Set changes it. To add a string, add a member here and a unit
/// with the same id to every .xlf file; the build reports a missing one (ARCF021).
/// </summary>
[LanguageCatalog]
public enum AppLangResourceKind
{{
{members}
}}
"""
    for language in LANGUAGES:
        units = []
        for key, entry in table.items():
            source = escape(entry[SOURCE_LANGUAGE])
            if language == SOURCE_LANGUAGE:
                units.append(f'    <unit id="{key}"><segment><source>{source}</source></segment></unit>')
            else:
                target = escape(entry[language])
                units.append(f'    <unit id="{key}"><segment><source>{source}</source>'
                             f'<target state="translated">{target}</target></segment></unit>')
        target_language = "" if language == SOURCE_LANGUAGE else f' trgLang="{language}"'
        files[CATALOG / f"{language}.xlf"] = (
            '<?xml version="1.0" encoding="utf-8"?>\n'
            f"<!-- {header(language)} -->\n"
            f'<xliff version="2.1" srcLang="{SOURCE_LANGUAGE}"{target_language} '
            'xmlns="urn:oasis:names:tc:xliff:document:2.0">\n'
            '  <file id="ArcStarterApp.Localization.AppLangResourceKind">\n'
            + "\n".join(units) + "\n  </file>\n</xliff>\n")
    return files


def updated_template(path: Path, generated: dict) -> str:
    config = json.loads(path.read_text(), object_pairs_hook=collections.OrderedDict)
    kept = collections.OrderedDict((k, v) for k, v in config["symbols"].items() if not k.startswith(PREFIXES))
    kept.update(generated)
    config["symbols"] = kept
    return json.dumps(config, indent=2, ensure_ascii=False) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--check", action="store_true", help="only report what is out of date")
    options = parser.parse_args()

    table = json.loads(TABLE.read_text(), object_pairs_hook=collections.OrderedDict)
    for key, entry in table.items():
        missing = [language for language in LANGUAGES if not entry.get(language)]
        if missing:
            raise SystemExit(f"strings.json: {key} lacks {', '.join(missing)}")

    outputs = catalog_files(table)
    generated = symbols(table)
    for template in TEMPLATES:
        if template.exists():
            outputs[template] = updated_template(template, generated)

    stale = [path for path, text in outputs.items() if not path.exists() or path.read_text() != text]
    if options.check:
        for path in stale:
            print(f"out of date: {path.relative_to(REPO)} (run tools/templates/sync_strings.py)")
        return 1 if stale else 0
    for path in stale:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(outputs[path])
        print(f"wrote {path.relative_to(REPO)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
