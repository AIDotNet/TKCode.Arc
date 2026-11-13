"""Convert A2UI conformance YAML suites to JSON (YAML 1.2 via ruamel.yaml).

Usage: uv run --with ruamel.yaml python tools/a2ui/yaml_to_json.py <src-dir> <dst-dir>
Every *.yaml under <src-dir> becomes <dst-dir>/<same relative path>.json with sorted keys.
"""
import json
import pathlib
import sys

from ruamel.yaml import YAML


def plain(value):
    if isinstance(value, dict):
        return {str(k): plain(v) for k, v in value.items()}
    if isinstance(value, list):
        return [plain(v) for v in value]
    return value


def main() -> None:
    src = pathlib.Path(sys.argv[1])
    dst = pathlib.Path(sys.argv[2])
    yaml = YAML(typ="safe", pure=True)
    yaml.version = (1, 2)
    count = 0
    for path in sorted(src.rglob("*.yaml")):
        data = plain(yaml.load(path.read_text(encoding="utf-8")))
        out = dst / path.relative_to(src).with_suffix(".json")
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(json.dumps(data, ensure_ascii=False, indent=1, sort_keys=True) + "\n", encoding="utf-8")
        count += 1
    print(f"converted {count} files")


if __name__ == "__main__":
    main()
