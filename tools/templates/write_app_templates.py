#!/usr/bin/env python3
"""Writes the .template.config of the app templates: tkcode-arc, tkcode-arc-xplat and the scenario templates.

Both offer the same layouts and options and take most of their files from content/_shared/, so their template.json
files are written from this one description: the symbols, the sources (each conditional source twice in tkcode-arc,
once at the root and once under <name>/ for --tests), the outputs and the restore step. The text symbols (one per UI
string) come from sync_strings.py, which reads strings.json.

    python3 tools/templates/write_app_templates.py          # write
    python3 tools/templates/write_app_templates.py --check  # fail when a template.json is out of date

Edit this file, not the template.json files.
"""
import argparse
import json
import os
import sys
from collections import OrderedDict as O
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import sync_strings  # noqa: E402

def choice(desc, display, default, choices):
    return O([("type","parameter"),("datatype","choice"),("description",desc),("displayName",display),("defaultValue",default),("choices",choices)])
def flag(desc, display, default):
    return O([("type","parameter"),("datatype","bool"),("description",desc),("displayName",display),("defaultValue",default)])
def computed(v): return {"type":"computed","value":v}
def size_switch(token, values):
    cases=[{"condition":f'(layout == "{k}")',"value":v} for k,v in values.items() if k!="default"]
    cases.append({"condition":"","value":values["default"]})
    return {"type":"generated","generator":"switch","replaces":token,"parameters":{"evaluator":"C++","datatype":"string","cases":cases}}

def symbols(xplat):
    s=O()
    s["layout"]=choice("The window layout.","Layout","blank",[
        {"choice":"blank","displayName":"Blank","description":"A top bar with the app name and a theme switch over one page."},
        {"choice":"sidebar","displayName":"Sidebar","description":"A collapsible sidebar with icons (TKCode.Arc.Blocks), a top bar and page switching."},
        {"choice":"header","displayName":"Header","description":"A top bar whose page switch (SegmentedControl) changes the page below it."},
        {"choice":"dock","displayName":"Dock","description":"An IDE-style dock layout (TKCode.Arc.Dock): tool panes, document tabs, drag to dock or float, the layout saved."}])
    s["mvvm"]=choice("How the views get their data and actions.","MVVM","none",[
        {"choice":"none","displayName":"None","description":"Code-behind: each view handles its own events."},
        {"choice":"toolkit","displayName":"CommunityToolkit.Mvvm","description":"View models with source-generated properties and commands, bound with compiled bindings."}])
    s["di"]=flag("Creates the view models and services with Microsoft.Extensions.DependencyInjection (Services/AppServices.cs). Implies --mvvm toolkit.","Dependency injection","false")
    s["settings"]=flag("Saves the appearance, the accent, the window's size and position and the layout's state (folded sidebar, dock panes, language) between runs.","Save preferences","true")
    s["i18n"]=flag("Keeps the UI text in Localization/*.xlf (English, 简体中文, 繁體中文) and adds a language switch.","Localization","false")
    s["uiLanguage"]=choice("The language of the UI text; with --i18n, the language the app starts in.","UI language","en-US",[
        {"choice":"en-US","displayName":"English"},{"choice":"zh-CN","displayName":"简体中文"},{"choice":"zh-TW","displayName":"繁體中文"}])
    if not xplat:
        s["tests"]=flag("Adds a headless test project (xunit.v3, Avalonia.Headless.XUnit) and a solution: the app goes to <name>/, the tests to <name>.Tests/.","Tests","false")
    else:
        s["tests"]=flag("Adds a headless test project (xunit.v3, Avalonia.Headless.XUnit), <name>.Tests/.","Tests","false")
    s["customTitlebar"]=flag("Draws Arc's title bar (ArcWindow) instead of the system's, with room for your own controls in it.","Arc title bar","false")
    s["framework"]=O([("type","parameter"),("datatype","choice"),("description","The target framework."),("displayName","Framework"),("defaultValue","net10.0"),("replaces","net10.0"),
        ("choices",[{"choice":"net10.0","description":".NET 10 (LTS)"},{"choice":"net8.0","description":".NET 8 (LTS)"},{"choice":"net11.0","description":".NET 11 (needs the .NET 11 SDK)"}])])
    if xplat:
        # The browser host has no net8.0 (Avalonia.Browser targets net8.0-browser, but Arc's browser support is tested on net10.0).
        s["framework"]["choices"]=[{"choice":"net10.0","description":".NET 10 (LTS)"}]
    s["accent"]=O([("type","parameter"),("datatype","choice"),("description","The accent color the app starts with (ArcAccentPreset)."),("displayName","Accent"),("defaultValue","Neutral"),("replaces","Neutral"),
        ("onlyIf",[{"after":"ArcAccentPreset."}]),("choices",[{"choice":c} for c in ["Neutral","Violet","Blue","Green","Amber","Orange","Coral","Rose"]])])
    s["skipRestore"]=flag("Skips the automatic restore of the project on create.","Skip restore","false")
    s["DisplayName"]=O([("type","derived"),("valueSource","name"),("valueTransform","identity"),("replaces","ArcDisplayName"),("description","The project name as typed, for the window title and the app name in the UI.")])
    s["AssemblyIdentifier"]=O([("type","derived"),("valueSource","name"),("valueTransform","identifierOnly"),("replaces","ArcStarterAppIdentifier"),
        ("description","The project name without the characters an identifier cannot hold: the namespace of the Arc assets generated for the app.")])
    s["StartLanguage"]=O([("type","derived"),("valueSource","uiLanguage"),("valueTransform","identity"),("replaces","ArcStartLanguage")])
    s["WindowWidth"]=size_switch("ArcWindowWidth",{"blank":"960","dock":"1280","default":"1120"})
    s["WindowHeight"]=size_switch("ArcWindowHeight",{"blank":"640","dock":"800","default":"720"})
    s["WindowMinWidth"]=size_switch("ArcWindowMinWidth",{"blank":"480","dock":"800","default":"640"})
    s["WindowMinHeight"]=size_switch("ArcWindowMinHeight",{"blank":"400","dock":"520","default":"440"})
    s["UsesMvvm"]=computed('(mvvm == "toolkit" || di)')
    s["UsesDi"]=computed("(di)")
    s["UsesSettings"]=computed("(settings)")
    s["UsesI18n"]=computed("(i18n)")
    s["UsesBlocks"]=computed('(layout == "sidebar")')
    s["UsesSidebar"]=computed('(layout == "sidebar")')
    s["UsesDock"]=computed('(layout == "dock")')
    s["HasNavigation"]=computed('(layout == "sidebar" || layout == "header")')
    s["UsesTests"]=computed("(tests)")
    s["UsesArcWindow"]=computed("(customTitlebar)")
    s["NeedsCjkFont"]=computed('(i18n || uiLanguage != "en-US")')
    s["IsXplat"]=computed("(true)" if xplat else "(false)")
    s["HasSolution"]=computed("(true)" if xplat else "(tests)")
    s["IsScenario"]=computed("(false)")
    s["IsAiChat"]=computed("(false)")
    s["IsWorkflow"]=computed("(false)")
    s["UsesWorkflow"]=computed("(false)")
    return s

def app_sources(target, cond, own_source, own_exclude):
    def c(x): return f"({cond} && {x})" if cond else f"({x})"
    shared="../_shared/App/"
    mv={"condition":"(!UsesMvvm)","exclude":["ViewModels/**"]}
    out=[]
    out.append(O([("condition",f"({cond})") if cond else ("condition","(true)"),("source",own_source),("target",target),("exclude",own_exclude)]))
    out.append(O([("condition",f"({cond})" if cond else "(true)"),("source",shared),("target",target),("exclude",["Layouts/**","Features/**","Common/**"])]))
    out.append(O([("condition",f"({cond})" if cond else "(true)"),("source",shared+"Common/"),("target",target)]))
    out.append(O([("condition",c("UsesSettings")),("source",shared+"Features/Settings/"),("target",target)]))
    out.append(O([("condition",c("UsesI18n")),("source",shared+"Features/I18n/"),("target",target)]))
    out.append(O([("condition",c("UsesDi")),("source",shared+"Features/Di/"),("target",target),("modifiers",[{"condition":"(!HasNavigation)","exclude":["Services/ProjectStore.cs"]}])]))
    out.append(O([("condition",c("HasNavigation")),("source",shared+"Layouts/Navigation/"),("target",target),("modifiers",[mv])]))
    for lay,name in (("blank","Blank"),("sidebar","Sidebar"),("header","Header"),("dock","Dock")):
        out.append(O([("condition",c(f'layout == "{lay}"')),("source",shared+f"Layouts/{name}/"),("target",target),("modifiers",[mv])]))
    for o in out:
        if o["condition"]=="(true)": del o["condition"]
    return out

common_exclude=[".template.config/**","**/bin/**","**/obj/**","**/.DS_Store"]
def base(identity, name, short, desc, xplat, sources, outputs):
    d=O()
    d["$schema"]="http://json.schemastore.org/template"
    d["author"]="TKCode.Arc contributors"
    d["classifications"]=["Desktop","Avalonia","Arc","Windows","macOS","Linux"]+(["Web","WebAssembly"] if xplat else [])
    d["identity"]=identity
    d["groupIdentity"]=identity
    d["name"]=name
    d["description"]=desc
    d["shortName"]=short
    d["defaultName"]="ArcApp"
    d["sourceName"]="ArcStarterApp"
    d["preferNameDirectory"]=True
    d["tags"]={"language":"C#","type":"solution" if xplat else "project"}
    d["symbols"]=symbols(xplat)
    d["forms"]={"identifierOnly":{"identifier":"replace","pattern":"[^A-Za-z0-9_]","replacement":""}}
    d["sources"]=sources
    d["primaryOutputs"]=outputs
    d["postActions"]=[O([("id","restore"),("condition","(!skipRestore)"),("description","Restore NuGet packages required by this project."),
        ("manualInstructions",[{"text":"Run 'dotnet restore'"}]),("actionId","210D431B-A78B-4D2F-B762-4ED3E3EA9025"),("continueOnError",True)])]
    return d

tests_src=O([("condition","(UsesTests)"),("source","../_shared/Tests/"),("target","./")])
sln_src=O([("condition","(HasSolution)"),("source","../_shared/Solution/"),("target","./")])

# tkcode-arc
src=app_sources("./","!tests","./",common_exclude)+app_sources("./ArcStarterApp/","tests","./",common_exclude)+[tests_src,sln_src]
outs=[{"condition":"(!tests)","path":"ArcStarterApp.csproj"},{"condition":"(tests)","path":"ArcStarterApp/ArcStarterApp.csproj"},
      {"condition":"(tests)","path":"ArcStarterApp.Tests/ArcStarterApp.Tests.csproj"}]
app=base("TKCode.Arc.Templates.App","TKCode.Arc App","tkcode-arc",
  "A desktop application built with TKCode.Arc (Avalonia): Arc's theme, fonts and Lucide icons, light/dark switching and saved preferences, in a blank, sidebar, header or dock layout, with optional MVVM, dependency injection, localization and tests.",
  False,src,outs)

# tkcode-arc-xplat: the shared app is a library in ArcStarterApp/, hosts beside it.
xsrc=app_sources("./ArcStarterApp/","","./ArcStarterApp/",[])
xsrc[0]["exclude"]=[]
xsrc+=[O([("source","./ArcStarterApp.Desktop/"),("target","./ArcStarterApp.Desktop/")]),
       O([("source","./ArcStarterApp.Browser/"),("target","./ArcStarterApp.Browser/"),("copyOnly",["Assets/Fonts/**"]),("modifiers",[{"condition":"(!NeedsCjkFont)","exclude":["Assets/Fonts/**","CjkFont.cs"]},{"condition":"(!UsesSettings)","exclude":["BrowserSettingsStore.cs"]}])]),
       tests_src,sln_src]
xouts=[{"path":"ArcStarterApp/ArcStarterApp.csproj"},{"path":"ArcStarterApp.Desktop/ArcStarterApp.Desktop.csproj"},
       {"path":"ArcStarterApp.Browser/ArcStarterApp.Browser.csproj"},{"condition":"(tests)","path":"ArcStarterApp.Tests/ArcStarterApp.Tests.csproj"}]
xplat=base("TKCode.Arc.Templates.CrossPlatform","TKCode.Arc Cross-platform App","tkcode-arc-xplat",
  "A TKCode.Arc application for the desktop and the browser (WebAssembly): one shared project with the views and two hosts, with the same layouts and options as tkcode-arc.",
  True,xsrc,xouts)

# The scenario templates (tkcode-arc-admin, -ai-chat, -devtool): a finished sample app each. MVVM, dependency injection
# and saved preferences are always on; the UI is English (Arc's blocks have English text set through their Labels, so a
# translated scenario would mix languages). Each takes the project file and Program.cs from ArcStarterApp, the app's
# frame (App.cs, AppSetup, MainWindow, settings) and the test project from _shared, and its own views from ./App/.
def scenario(folder, identity, name, short, desc, size, flags, navigation, extra=()):
    s=O()
    s["tests"]=flag("Adds a headless test project (xunit.v3, Avalonia.Headless.XUnit) and a solution: the app goes to <name>/, the tests to <name>.Tests/.","Tests","false")
    s["customTitlebar"]=flag("Draws Arc's title bar (ArcWindow) instead of the system's.","Arc title bar","false")
    base_symbols=symbols(False)
    for key in ("framework","accent","skipRestore","DisplayName","AssemblyIdentifier"):
        s[key]=base_symbols[key]
    for token,value in zip(("ArcWindowWidth","ArcWindowHeight","ArcWindowMinWidth","ArcWindowMinHeight"),size):
        s[token[3:]]={"type":"generated","generator":"constant","replaces":token,"parameters":{"value":value}}
    fixed={"UsesMvvm":True,"UsesDi":True,"UsesSettings":True,"UsesI18n":False,"UsesBlocks":True,"UsesDock":False,"UsesSidebar":False,
           "HasNavigation":navigation,"NeedsCjkFont":False,"IsXplat":False,"IsScenario":True,"IsAiChat":False,"IsWorkflow":False,"UsesWorkflow":False}
    fixed.update(flags)
    for key,value in fixed.items():
        s[key]=computed("(true)" if value else "(false)")
    s["UsesTests"]=computed("(tests)")
    s["UsesArcWindow"]=computed("(customTitlebar)")
    s["HasSolution"]=computed("(tests)")
    shared="../_shared/App/"
    def app(target, cond):
        out=[O([("condition",cond),("source","../ArcStarterApp/"),("target",target),("exclude",common_exclude)]),
             O([("condition",cond),("source",shared),("target",target),("exclude",["Layouts/**","Features/**","Common/**"])]),
             O([("condition",cond),("source",shared+"Common/"),("target",target)]),
             O([("condition",cond),("source",shared+"Features/Settings/"),("target",target)])]
        if navigation:
            out.append(O([("condition",cond),("source",shared+"Layouts/Navigation/Navigation/"),("target",target+"Navigation/"),("exclude",["AppPages.cs"])]))
        for source,sub,exclude in extra:
            out.append(O([("condition",cond),("source",shared+source),("target",target+sub),("exclude",exclude)]))
        out.append(O([("condition",cond),("source","./App/"),("target",target)]))
        return out
    src=app("./","(!tests)")+app("./ArcStarterApp/","(tests)")+[tests_src,O([("condition","(UsesTests)"),("source","./Tests/"),("target","./")]),sln_src]
    d=base(identity,name,short,desc,False,src,outs)
    d["classifications"]=["Desktop","Avalonia","Arc","Starter","Windows","macOS","Linux"]
    d["symbols"]=s
    return folder,d

scenarios=[
    scenario("ArcAdminApp","TKCode.Arc.Templates.Admin","TKCode.Arc Admin App","tkcode-arc-admin",
        "A SaaS admin app built with TKCode.Arc: sign-in, a dashboard with metrics and a chart, a customer table with search, filters, paging and a detail drawer, team members and roles, billing and settings, a command palette and notifications. Sample data behind service interfaces; MVVM and dependency injection.",
        ("1280","800","1100","600"),{"UsesSidebar":True},True,[("Layouts/Navigation/Models/","Models/",["Project.cs"])]),
    scenario("ArcAiChatApp","TKCode.Arc.Templates.AiChat","TKCode.Arc AI Chat App","tkcode-arc-ai-chat",
        "An AI chat client built with TKCode.Arc: a conversation list, a streaming Markdown chat with reasoning, attachments and a model picker, conversations saved between runs. A local sample responder streams the replies; plug in any model client (no vendor SDK included). MVVM and dependency injection.",
        ("1200","780","800","520"),{"UsesSidebar":True,"IsAiChat":True},False),
    scenario("ArcDevtoolApp","TKCode.Arc.Templates.Devtool","TKCode.Arc Developer Tool","tkcode-arc-devtool",
        "An IDE-style developer tool built with TKCode.Arc: a dock layout with a file explorer, editor tabs, a property grid, build output and problems, a command palette and a saved layout that reopens the files. MVVM and dependency injection.",
        ("1280","800","900","560"),{"UsesDock":True},False),
    scenario("ArcWorkflowApp","TKCode.Arc.Templates.Workflow","TKCode.Arc Workflow Editor","tkcode-arc-workflow",
        "A workflow editor built with TKCode.Arc: a step palette, an editable flow canvas with connections, undo, automatic layout, a minimap and zoom, a step inspector, a simulated run and JSON import and export, the workflow saved between runs. MVVM and dependency injection.",
        ("1280","800","900","560"),{"UsesBlocks":False,"UsesWorkflow":True,"IsWorkflow":True},False),
    scenario("ArcProjectsApp","TKCode.Arc.Templates.Projects","TKCode.Arc Project Planner","tkcode-arc-projects",
        "A project planning app built with TKCode.Arc: a sidebar with a Kanban board, a Gantt timeline, a week calendar and a release checklist, from sample data behind a service interface. MVVM and dependency injection.",
        ("1280","820","960","600"),{"UsesSidebar":True},True),
    scenario("ArcAnalyticsApp","TKCode.Arc.Templates.Analytics","TKCode.Arc Analytics Dashboard","tkcode-arc-analytics",
        "An analytics dashboard built with TKCode.Arc: KPIs with drill-down, revenue by month, cohort retention, a usage forecast with a budget and a service status page, from sample data behind a service interface. MVVM and dependency injection.",
        ("1280","860","960","600"),{},True),
]

def scenario_host_config():
    return O([("$schema","http://json.schemastore.org/dotnetcli.host"),("symbolInfo",O([
        ("tests",{"longName":"tests","shortName":""}),("customTitlebar",{"longName":"custom-titlebar","shortName":""}),
        ("framework",{"longName":"framework","shortName":"f"}),("accent",{"longName":"accent","shortName":""}),("skipRestore",{"longName":"no-restore","shortName":""})]))])


def host_config():
    return O([("$schema","http://json.schemastore.org/dotnetcli.host"),("symbolInfo",O([
        ("layout",{"longName":"layout","shortName":""}),("mvvm",{"longName":"mvvm","shortName":""}),("di",{"longName":"di","shortName":""}),
        ("settings",{"longName":"settings","shortName":""}),("i18n",{"longName":"i18n","shortName":""}),("uiLanguage",{"longName":"ui-language","shortName":""}),
        ("tests",{"longName":"tests","shortName":""}),("customTitlebar",{"longName":"custom-titlebar","shortName":""}),
        ("framework",{"longName":"framework","shortName":"f"}),("accent",{"longName":"accent","shortName":""}),("skipRestore",{"longName":"no-restore","shortName":""})]))])


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--check", action="store_true", help="only report what is out of date")
    options = parser.parse_args()
    table = json.loads(sync_strings.TABLE.read_text(), object_pairs_hook=O)
    texts = sync_strings.symbols(table)
    content = sync_strings.PACKAGE / "content"
    outputs = {}
    for folder, config in (("ArcStarterApp", app), ("ArcXplatApp", xplat)):
        config["symbols"].update(texts)
        outputs[content / folder / ".template.config" / "template.json"] = json.dumps(config, indent=2, ensure_ascii=False) + "\n"
        outputs[content / folder / ".template.config" / "dotnetcli.host.json"] = json.dumps(host_config(), indent=2) + "\n"
    for folder, config in scenarios:
        outputs[content / folder / ".template.config" / "template.json"] = json.dumps(config, indent=2, ensure_ascii=False) + "\n"
        outputs[content / folder / ".template.config" / "dotnetcli.host.json"] = json.dumps(scenario_host_config(), indent=2) + "\n"
    stale = [path for path, text in outputs.items() if not path.exists() or path.read_text() != text]
    if options.check:
        for path in stale:
            print(f"out of date: {path.relative_to(sync_strings.REPO)} (run tools/templates/write_app_templates.py)")
        return 1 if stale else 0
    for path in stale:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(outputs[path])
        print(f"wrote {path.relative_to(sync_strings.REPO)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
