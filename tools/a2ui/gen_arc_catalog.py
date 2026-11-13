"""Writes src/TKCode.Arc.A2UI/Protocol/Schemas/arc_catalog.json: the Arc catalog (18 components drawn with Arc controls).

Run: python3 tools/a2ui/gen_arc_catalog.py. The descriptions and instructions are written for the model that produces A2UI.
"""
import json, pathlib

ID = "https://github.com/AIDotNet/TKCode.Arc/a2ui/catalogs/arc/v1"
CT = "common_types.json#/$defs/"
def ref(name, description=None):
    d = {"$ref": CT + name}
    if description: d["description"] = description
    return d
S, N, B, V, L = "DynamicString", "DynamicNumber", "DynamicBoolean", "DynamicValue", "DynamicStringList"
W = {"type": "number", "description": "Flex weight inside a Row or Column."}
TONE = "info, success, warning or danger."

def comp(name, description, props, required, checkable=False):
    body = {"type": "object", "description": description,
            "properties": {"component": {"const": name}, **props, "weight": W},
            "required": ["component", *required]}
    if checkable:
        inner = dict(body); inner.pop("description")
        return {"type": "object", "description": description, "allOf": [ref("Checkable"), inner]}
    return body

C = {}
C["BarChart"] = comp("BarChart", "Bars comparing a few values (up to 48).", {
    "title": ref(S, "Chart title."), "labels": ref(L, "One label per bar."),
    "values": ref(V, "One number per bar (a list, or a binding to a list)."),
    "unit": {"type": "string", "description": "Unit shown with values, for example 'users'."},
    "height": {"type": "number", "description": "Plot height in px, 120-600 (default 180)."}}, ["labels", "values"])
C["LineChart"] = comp("LineChart", "Trends of one or more series over the same labels.", {
    "title": ref(S), "labels": ref(L, "X axis labels."),
    "series": ref(V, "[{\"name\": string, \"values\": number[]}] with one value per label, or a binding to such a list."),
    "unit": {"type": "string"},
    "height": {"type": "number", "description": "Plot height in px, 120-600 (default 200); match a taller card beside it."}}, ["labels", "series"])
C["DonutChart"] = comp("DonutChart", "Parts of a whole, with the total in the middle.", {
    "title": ref(S), "labels": ref(L), "values": ref(V, "One number per label."),
    "totalLabel": {"type": "string", "description": "Text under the total in the middle."}}, ["labels", "values"])
C["Sparkline"] = comp("Sparkline", "A tiny trend beside a value.", {
    "label": ref(S), "values": ref(V, "Numbers, oldest first."), "value": ref(S), "change": ref(S)}, ["values"])
C["Metric"] = comp("Metric", "One headline number.", {
    "label": ref(S), "value": ref(N, "The number."),
    "prefix": {"type": "string", "description": "For example '$'."},
    "suffix": {"type": "string", "description": "For example '%' or ' users'."},
    "decimals": {"type": "integer", "minimum": 0, "maximum": 4},
    "change": ref(S, "For example '+12%' (a leading '-' shows a decrease)."),
    "context": ref(S, "For example 'vs last week'.")}, ["label", "value"])
C["Table"] = comp("Table", "Records in rows; columns pick keys of each row object.", {
    "caption": ref(S),
    "columns": {"type": "array", "items": {"type": "object", "properties": {
        "key": {"type": "string"}, "title": {"type": "string"}, "numeric": {"type": "boolean"},
        "format": {"type": "string", "description": "currency:<ISO code>, percent or number"}}, "required": ["key"]}},
    "rows": ref(V, "A list of objects keyed by column key, usually a binding.")}, ["columns", "rows"])
C["Timeline"] = comp("Timeline", "Dated events, newest first.", {
    "label": {"type": "string"},
    "events": ref(V, "[{\"id\", \"at\": ISO 8601, \"title\", \"actor\"?, \"meta\"?}]")}, ["events"])
C["Stepper"] = comp("Stepper", "Where a multi-step flow is.", {
    "steps": ref(V, "[{\"label\", \"description\"?}]"),
    "current": ref(N, "0-based index of the current step; the step count means all done."),
    "orientation": {"type": "string", "enum": ["horizontal", "vertical"]}}, ["steps"])
C["Progress"] = comp("Progress", "Completion of a task.", {
    "label": ref(S), "value": ref(N), "max": {"type": "number"}}, ["value"])
C["Alert"] = comp("Alert", "A status message with a tone.", {
    "tone": ref(S, TONE), "title": ref(S), "text": ref(S)}, ["title"])
C["Badge"] = comp("Badge", "A short status label.", {
    "text": ref(S), "tone": ref(S, "neutral, " + TONE)}, ["text"])
C["Avatar"] = comp("Avatar", "A person's avatar (initials when there is no picture).", {
    "name": ref(S), "imageUrl": ref(S)}, ["name"])
C["Markdown"] = comp("Markdown", "Rich text: headings, lists, tables, code, links. HTML is not rendered.", {
    "text": ref(S, "Markdown source.")}, ["text"])
C["CodeBlock"] = comp("CodeBlock", "Source code with syntax highlighting and a copy button.", {
    "code": ref(S), "language": {"type": "string", "description": "For example csharp, json, python, bash."},
    "fileName": ref(S)}, ["code"])
C["Accordion"] = comp("Accordion", "Collapsible sections; each shows one child component.", {
    "items": {"type": "array", "minItems": 1, "items": {"type": "object", "properties": {
        "title": ref(S, "Section title."), "child": ref("Child", "The ID of the section's component.")},
        "required": ["title", "child"], "additionalProperties": False}},
    "multiple": {"type": "boolean", "description": "Several sections may be open at once."},
    "expanded": {"type": "integer", "description": "Index of the section open at first; -1 for none."}}, ["items"])
C["Switch"] = comp("Switch", "An on/off setting (two-way bound).", {
    "label": ref(S), "value": ref(B)}, ["label", "value"], checkable=True)
C["SegmentedControl"] = comp("SegmentedControl", "One choice from 2-5 short options shown side by side (two-way bound).", {
    "options": {"type": "array", "minItems": 1, "items": {"type": "object", "properties": {
        "label": ref(S), "value": {"type": "string"}}, "required": ["label", "value"]}},
    "value": ref(S, "The chosen option's value.")}, ["options", "value"], checkable=True)
C["Rating"] = comp("Rating", "Stars (two-way bound unless readOnly).", {
    "label": ref(S), "value": ref(N), "max": {"type": "integer", "minimum": 1, "maximum": 10},
    "allowHalf": {"type": "boolean"}, "readOnly": {"type": "boolean"}}, ["value"], checkable=True)

doc = {
    "$schema": "https://json-schema.org/draft/2020-12/schema",
    "$id": ID, "catalogId": ID, "protocolVersion": "1.0",
    "title": "Arc catalog",
    "description": "Charts, data and input components drawn with TKCode.Arc controls. Use next to the basic catalog: set \"@catalogId\" on the component.",
    "instructions": ("Rich components for dashboards, documents and workflows. BarChart compares a few values; LineChart shows trends "
        "of one or more series; DonutChart shows parts of a whole; Sparkline is a tiny trend beside a value; Metric is one headline "
        "number. Table lists records (bind rows to a list in the data model; columns pick keys). Timeline shows dated events; "
        "Stepper shows where a multi-step flow is; Progress shows completion; Alert, Badge and Avatar carry status and people. "
        "Markdown is rich text and CodeBlock is source code; Accordion folds sections, each one child component. Switch, "
        "SegmentedControl and Rating are inputs bound two-way to the data model like the basic inputs. Bind data-heavy "
        "properties to the data model and change them with updateDataModel."),
    "components": C,
}
out = pathlib.Path(__file__).resolve().parents[2] / "src/TKCode.Arc.A2UI/Protocol/Schemas/arc_catalog.json"
out.write_text(json.dumps(doc, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print("wrote", out, len(C), "components")
