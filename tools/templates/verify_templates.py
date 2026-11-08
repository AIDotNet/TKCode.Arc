#!/usr/bin/env python3
"""Creates, builds and starts every dotnet new template of TKCode.Arc.Templates.

For each case (an app template with a set of options, plus item templates run inside it) the script:

1. creates the project from the packed template, the way a user would (dotnet new, restore from a local feed);
2. compares the list of created files with tests/Baselines/templates/<case>.txt (--update rewrites the baselines);
3. builds it (the solution, when there is one) with warnings as errors, and runs its tests (--tests);
4. starts the desktop app with Avalonia's headless platform: shows the main window, walks every page, switches the
   theme and the language, opens what the item templates made, and fails on any warning or error Avalonia logs. With
   saved preferences on it runs twice and checks that the theme, the accent, the language and the layout's state came
   back.

The smoke code is not part of the templates: a Directory.Build.props above the cases adds it (and Avalonia.Headless) to
the desktop project of every case, and makes it the entry point. Everything runs in an isolated work folder with its own
NuGet package folder and dotnet new settings, so neither the machine's template list nor its NuGet cache is touched, and
the apps keep their settings in the case folder (AppSettings.Store), not in the user's.

    python3 tools/templates/verify_templates.py                  # pack what the templates need, then check every case
    python3 tools/templates/verify_templates.py --feed artifacts/nupkg   # use packages packed already (CI)
    python3 tools/templates/verify_templates.py --case blank --case items
    python3 tools/templates/verify_templates.py --update         # accept new file lists
    python3 tools/templates/verify_templates.py --templates-only --case admin   # after a template edit: repack only the templates

The cross-platform cases build a WebAssembly host, which needs the wasm-tools workload (wasm-tools-net10 with the
.NET 11 SDK); --skip-browser leaves those cases out. design/engineering/project-templates.md describes the cases.
"""

from __future__ import annotations

import argparse
import dataclasses
import json
import os
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
BASELINES = REPO / "tests" / "Baselines" / "templates"
STRINGS = json.loads((REPO / "templates" / "TKCode.Arc.Templates" / "strings.json").read_text())

# The packages an app made from the templates references, and what they depend on.
PACK_PROJECTS = [
    "src/TKCode.Arc.Foundation/TKCode.Arc.Foundation.csproj",
    "src/TKCode.Arc.Core/TKCode.Arc.Core.csproj",
    "src/TKCode.Arc/TKCode.Arc.csproj",
    "src/TKCode.Arc.Fonts/TKCode.Arc.Fonts.csproj",
    "src/TKCode.Arc.Icons.Lucide/TKCode.Arc.Icons.Lucide.csproj",
    "src/TKCode.Arc.Audio/TKCode.Arc.Audio.csproj",
    "src/TKCode.Arc.Blocks/TKCode.Arc.Blocks.csproj",
    "src/TKCode.Arc.Dock/TKCode.Arc.Dock.csproj",
    "src/TKCode.Arc.Workflow/TKCode.Arc.Workflow.csproj",
    "templates/TKCode.Arc.Templates/TKCode.Arc.Templates.csproj",
]


@dataclasses.dataclass
class Item:
    template: str
    name: str
    args: list[str] = dataclasses.field(default_factory=list)


@dataclasses.dataclass
class Case:
    key: str
    project: str
    args: list[str]
    items: list[Item] = dataclasses.field(default_factory=list)
    template: str = "tkcode-arc"

    def option(self, name: str, default: str) -> str:
        if name in self.args:
            return self.args[self.args.index(name) + 1]
        return default

    def flag(self, name: str, default: bool = False) -> bool:
        if name not in self.args:
            return default
        index = self.args.index(name)
        return not (index + 1 < len(self.args) and self.args[index + 1] == "false")

    @property
    def xplat(self) -> bool:
        return self.template == "tkcode-arc-xplat"

    @property
    def library(self) -> bool:
        return self.template == "tkcode-arc-lib"

    @property
    def scenario(self) -> str | None:
        """admin, ai-chat or devtool for a scenario template; they have MVVM, DI and settings always on."""
        prefix = "tkcode-arc-"
        name = self.template[len(prefix):]
        return name if name in SCENARIOS else None

    @property
    def layout(self) -> str:
        return self.option("--layout", "blank")

    @property
    def navigation(self) -> bool:
        return self.layout in ("sidebar", "header")

    @property
    def di(self) -> bool:
        return self.flag("--di") or self.scenario is not None

    @property
    def mvvm(self) -> bool:
        return self.option("--mvvm", "none") == "toolkit" or self.di

    @property
    def settings(self) -> bool:
        if self.library:
            return False
        return self.scenario is not None or self.flag("--settings", True)

    @property
    def i18n(self) -> bool:
        return self.flag("--i18n")

    @property
    def language(self) -> str:
        return self.option("--ui-language", "en-US")

    @property
    def tests(self) -> bool:
        return self.flag("--tests", self.library)

    @property
    def arc_window(self) -> bool:
        return self.flag("--custom-titlebar")

    @property
    def framework(self) -> str:
        return self.option("-f", "net10.0")

    @property
    def accent(self) -> str:
        return self.option("--accent", "Neutral")

    @property
    def solution(self) -> bool:
        return self.xplat or self.tests or self.library

    @property
    def desktop_project(self) -> str:
        if self.library:
            return f"{self.project}.Sample"
        return f"{self.project}.Desktop" if self.xplat else self.project

    def app_dir(self, root: Path) -> Path:
        if self.library:
            return root / "src" / self.project
        return root / self.project if self.solution else root

    def desktop_dir(self, root: Path) -> Path:
        if self.library:
            return root / "samples" / self.desktop_project
        return root / self.desktop_project if self.xplat else self.app_dir(root)


SCENARIOS = ("admin", "ai-chat", "devtool", "workflow", "projects", "analytics")

ITEMS = [
    Item("tkcode-arc-page", "Reports"),
    Item("tkcode-arc-window", "AboutWindow"),  # the suffix is added when missing and kept when given
    Item("tkcode-arc-dialog", "Rename"),
    Item("tkcode-arc-control", "StatusTile"),
]

# Pairwise over layout x mvvm/di x settings x i18n x language x tests x title bar x framework x accent, plus the item
# templates in both MVVM modes and the cross-platform template.
CASES = [
    Case("blank", "Blank.App", []),
    Case("blank-mvvm", "BlankMvvm", ["--mvvm", "toolkit", "--settings", "false", "-f", "net8.0", "--accent", "Violet"]),
    Case("blank-i18n-di", "BlankWorld", ["--i18n", "--di", "--settings", "false"]),
    Case("sidebar", "Northwind.Desk", ["--layout", "sidebar"]),
    Case("sidebar-mvvm", "SidebarMvvm", ["--layout", "sidebar", "--mvvm", "toolkit", "-f", "net8.0", "--accent", "Blue"]),
    Case("sidebar-plain", "SidebarPlain", ["--layout", "sidebar", "--settings", "false", "--accent", "Rose"]),
    Case("header-i18n", "HeaderWorld", ["--layout", "header", "--i18n", "--ui-language", "zh-TW", "--custom-titlebar"]),
    Case("header-di", "Header.Di", ["--layout", "header", "--di", "--tests", "--ui-language", "zh-CN"]),
    Case("dock", "DockApp", ["--layout", "dock", "--custom-titlebar"]),
    Case("dock-mvvm-i18n", "DockWorld", ["--layout", "dock", "--mvvm", "toolkit", "--i18n", "--tests", "-f", "net8.0"]),
    Case("items", "Acme.Studio", ["--layout", "sidebar", "--mvvm", "toolkit"],
         [Item("tkcode-arc-page", "Reports", ["--view-model"])] + ITEMS[1:]),
    Case("items-plain", "ItemsPlain", ["-f", "net11.0"], ITEMS),
    Case("xplat", "Contoso.Everywhere", ["--layout", "sidebar", "--mvvm", "toolkit", "--i18n", "--tests"],
         template="tkcode-arc-xplat"),
    Case("xplat-plain", "XplatPlain", ["--layout", "header", "--settings", "false"], template="tkcode-arc-xplat"),
    # The scenario templates: each with and without tests, across the frameworks and both title bars.
    Case("admin", "Northwind.Admin", ["--tests"], template="tkcode-arc-admin"),
    Case("admin-plain", "AdminPlain", ["--custom-titlebar", "-f", "net8.0", "--accent", "Blue"], template="tkcode-arc-admin"),
    Case("ai-chat", "Contoso.Chat", ["--tests", "--custom-titlebar"], template="tkcode-arc-ai-chat"),
    Case("ai-chat-plain", "ChatPlain", ["-f", "net8.0"], template="tkcode-arc-ai-chat"),
    Case("devtool", "Fabrikam.Studio", ["--tests"], template="tkcode-arc-devtool"),
    Case("devtool-plain", "DevtoolPlain", ["--custom-titlebar", "-f", "net11.0", "--accent", "Green"], template="tkcode-arc-devtool"),
    Case("workflow", "Contoso.Flows", ["--tests"], template="tkcode-arc-workflow"),
    Case("workflow-plain", "FlowPlain", ["--custom-titlebar", "-f", "net8.0", "--accent", "Violet"], template="tkcode-arc-workflow"),
    Case("projects", "Northwind.Planner", ["--tests"], template="tkcode-arc-projects"),
    Case("projects-plain", "PlannerPlain", ["--custom-titlebar", "-f", "net11.0"], template="tkcode-arc-projects"),
    Case("analytics", "Contoso.Insights", ["--tests", "--custom-titlebar"], template="tkcode-arc-analytics"),
    Case("analytics-plain", "InsightsPlain", ["-f", "net8.0", "--accent", "Amber"], template="tkcode-arc-analytics"),
    # The component library: with its tests and sample, and alone on .NET 8 (packed in both).
    Case("lib", "Acme.Controls", [], template="tkcode-arc-lib"),
    Case("lib-plain", "LibPlain", ["--tests", "false", "--sample", "false", "-f", "net8.0"], template="tkcode-arc-lib"),
]


class StepFailed(Exception):
    """A command of a case failed; the case is reported and the next one runs."""


def log(message: str) -> None:
    print(message, flush=True)


def run(args: list[str], cwd: Path, env: dict[str, str], *, quiet: bool = True) -> str:
    started = time.monotonic()
    result = subprocess.run(args, cwd=cwd, env=env, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    if result.returncode != 0:
        log(result.stdout)
        raise StepFailed(f"failed ({result.returncode}): {' '.join(args)}  [in {cwd}]")
    if not quiet:
        log(result.stdout)
    log(f"    ok  {' '.join(args[:3])} ({time.monotonic() - started:.0f} s)")
    return result.stdout


def read_versions() -> dict[str, str]:
    text = (REPO / "build" / "Versions.props").read_text()
    return {name: re.search(f"<{name}>(.*?)</{name}>", text).group(1) for name in ("ArcVersion", "AvaloniaVersion")}


def identifier(name: str) -> str:
    return re.sub(r"[^A-Za-z0-9_]", "", name)


def with_suffix(name: str, suffix: str) -> str:
    """What the item templates name the class: the suffix is added when the name lacks it."""
    return name if name.endswith(suffix) else name + suffix


def csharp(text: str) -> str:
    return '"' + text.replace("\\", "\\\\").replace('"', '\\"') + '"'


# ----------------------------------------------------------------------------------------------------------------------
# Work folder

ISOLATION_FILES = {
    # Stop MSBuild from importing the repository's own build files when the work folder is inside the repository.
    "Directory.Build.targets": "<Project />\n",
    "Directory.Packages.props": "<Project>\n  <PropertyGroup>\n    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>\n  </PropertyGroup>\n</Project>\n",
}

SMOKE_PROPS = """<Project>
  <!-- Added by tools/templates/verify_templates.py: the smoke entry point and the headless platform, in the project the
       smoke/ folder has a file for (the desktop project of each case). -->
  <PropertyGroup Condition="Exists('$(MSBuildThisFileDirectory)smoke/$(MSBuildProjectName).Smoke.cs')">
    <StartupObject>ArcTemplateSmoke.SmokeProgram</StartupObject>
  </PropertyGroup>
  <ItemGroup Condition="Exists('$(MSBuildThisFileDirectory)smoke/$(MSBuildProjectName).Smoke.cs')">
    <Compile Include="$(MSBuildThisFileDirectory)smoke/$(MSBuildProjectName).Smoke.cs" Link="Smoke.cs" />
    <PackageReference Include="Avalonia.Headless" Version="{avalonia}" />
  </ItemGroup>
</Project>
"""

NUGET_CONFIG = """<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="{feed}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local">
      <package pattern="TKCode.Arc" />
      <package pattern="TKCode.Arc.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"""


def prepare(work: Path, feed: Path, versions: dict[str, str]) -> dict[str, str]:
    cases = work / "cases"
    cases.mkdir(parents=True, exist_ok=True)
    shutil.rmtree(cases / "smoke", ignore_errors=True)
    (cases / "smoke").mkdir()
    for name, text in ISOLATION_FILES.items():
        (cases / name).write_text(text)
    (cases / "Directory.Build.props").write_text(SMOKE_PROPS.format(avalonia=versions["AvaloniaVersion"]))
    (cases / "NuGet.config").write_text(NUGET_CONFIG.format(feed=feed))

    packages = work / "packages"
    packages.mkdir(exist_ok=True)
    # The local packages keep their version between builds: drop the restored copies so this run's are used.
    for old in packages.glob("tkcode.arc*"):
        shutil.rmtree(old)

    env = dict(os.environ)
    env.update({
        "DOTNET_CLI_HOME": str(work / "cli-home"),  # dotnet new's installed templates
        "NUGET_PACKAGES": str(packages),
        "DOTNET_NOLOGO": "1",
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
        # The net8.0 cases run on whatever newer runtime is installed.
        "DOTNET_ROLL_FORWARD": "Major",
    })
    return env


def pack(feed: Path, env: dict[str, str], templates_only: bool = False) -> None:
    if templates_only:
        # Only the template package changed: repack it into the feed packed by an earlier run.
        project = REPO / PACK_PROJECTS[-1]
        log(f"Packing {project.name} into {feed}")
        run(["dotnet", "pack", str(project), "-c", "Release", "-o", str(feed), "-p:ContinuousIntegrationBuild=true"], REPO, env)
        return
    if feed.exists():
        shutil.rmtree(feed)
    feed.mkdir(parents=True)
    log(f"Packing into {feed}")
    # Build first, one project at a time, then pack without building: packing each project with a build rebuilds the
    # shared dependencies (Foundation, Core) for every target framework at once, and those builds trip over each other.
    for project in PACK_PROJECTS:
        run(["dotnet", "build", str(REPO / project), "-c", "Release", "-p:ContinuousIntegrationBuild=true"], REPO, env)
    for project in PACK_PROJECTS:
        run(["dotnet", "pack", str(REPO / project), "-c", "Release", "--no-build", "-o", str(feed),
             "-p:ContinuousIntegrationBuild=true"], REPO, env)


def install_templates(feed: Path, version: str, work: Path, env: dict[str, str]) -> None:
    package = feed / f"TKCode.Arc.Templates.{version}.nupkg"
    if not package.exists():
        raise SystemExit(f"{package} is missing: pack first (run without --feed)")
    shutil.rmtree(work / "cli-home", ignore_errors=True)
    run(["dotnet", "new", "install", str(package)], work, env)


# ----------------------------------------------------------------------------------------------------------------------
# Smoke program

SMOKE_HEADER = """// Generated by tools/templates/verify_templates.py; not part of any template.
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Logging;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc;
using TKCode.Arc.Localization;

namespace ArcTemplateSmoke;

internal static class SmokeProgram
{
    private static readonly List<string> s_failures = new();

    [STAThread]
    public static int Main(string[] args)
    {
        var phase = args.Length > 0 ? args[0] : "first";
__STORE__
        var builder = global::__PROGRAM__.Program.BuildAvaloniaApp();
        Logger.Sink = new FailingSink();
        builder.UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
        try
        {
            Run(phase);
        }
        catch (Exception e)
        {
            s_failures.Add(e.ToString());
        }
        foreach (var failure in s_failures)
        {
            Console.Error.WriteLine("SMOKE FAILURE: " + failure);
        }
        Console.WriteLine(s_failures.Count == 0 ? $"smoke {phase}: ok" : $"smoke {phase}: {s_failures.Count} failure(s)");
        return s_failures.Count == 0 ? 0 : 1;
    }

    private static void Check(bool condition, string what)
    {
        if (!condition)
        {
            s_failures.Add(what);
        }
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();

    // Runs the UI thread until the condition holds (the sample services wait like a server would), at most 15 seconds.
    private static bool WaitFor(Func<bool> condition)
    {
        var stop = DateTime.UtcNow.AddSeconds(15);
        Pump();
        while (!condition())
        {
            if (DateTime.UtcNow > stop)
            {
                return false;
            }
            Thread.Sleep(10);
            Pump();
        }
        return true;
    }

    private static bool HasTemplate(Control control) => control.GetVisualChildren().Any();

    private static T Find<T>(Visual root) where T : Visual => root.GetVisualDescendants().OfType<T>().First();

    private static bool ShowsCjk(Visual root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsEffectivelyVisible && t.Text is { } text && Regex.IsMatch(text, "[\\\\u4e00-\\\\u9fff]"));

    private sealed class FailingSink : ILogSink
    {
        public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning;

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
            => s_failures.Add($"[{level} {area}] {messageTemplate} ({source})");

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
            => s_failures.Add($"[{level} {area}] {messageTemplate} [{string.Join(", ", propertyValues)}] ({source})");
    }
"""


def smoke_library(case: Case) -> str:
    ns = case.project
    body = f"""    private static void Run(string phase)
    {{
        var window = new global::{ns}.Sample.MainWindow();
        window.Show();
        Pump();
        Check(window.IsVisible, "the sample window is not shown");
        var tiles = window.GetVisualDescendants().OfType<global::{ns}.StatTile>().ToList();
        Check(tiles.Count == 3 && tiles.All(HasTemplate), "the library's StatTile has no template (theme not registered?)");
        Check(tiles.All(t => t.Padding == new Thickness(16, 14)), "the StatTile token values were not published");
        ArcTheme.Apply(t => t.Appearance(ArcAppearance.Dark));
        Pump();
        window.Close();
        Pump();
    }}
"""
    header = SMOKE_HEADER.replace("__PROGRAM__", f"{ns}.Sample").replace("__STORE__", "")
    return header + "\n" + body + "}\n"


def smoke_source(case: Case) -> str:
    if case.library:
        return smoke_library(case)
    ns = case.project
    first, second = 'phase == "first"', 'phase == "second"'
    body: list[str] = []
    add = body.append

    # The theme of the first frame.
    add(f"""    private static void Run(string phase)
    {{
        var expectedAccent = ArcPalette.GetSeed(ArcAccentPreset.{case.accent});""")
    if case.settings:
        add(f"""        if ({second})
        {{
            // Saved by the first run: the dark appearance and the Rose accent.
            Check(ArcTheme.State.Appearance == ArcAppearance.Dark, $"saved appearance not restored: {{ArcTheme.State.Appearance}}");
            Check(ArcTheme.State.Accent == ArcPalette.GetSeed(ArcAccentPreset.Rose), $"saved accent not restored: {{ArcTheme.State.Accent}}");
        }}
        else
        {{
            Check(ArcTheme.State.Accent == expectedAccent, $"start-up accent {{ArcTheme.State.Accent}}, expected {{expectedAccent}}");
        }}""")
    else:
        add("""        Check(ArcTheme.State.Accent == expectedAccent, $"start-up accent {ArcTheme.State.Accent}, expected {expectedAccent}");""")

    # The language.
    if case.i18n:
        expected = case.language
        if case.settings:
            add(f"""        var expectedLanguage = {second} ? "zh-CN" : "{expected}";""")
        else:
            add(f"""        var expectedLanguage = "{expected}";""")
        add("""        Check(ArcLanguage.Current.Value == expectedLanguage, $"start-up language {ArcLanguage.Current}, expected {expectedLanguage}");""")

    # The main window.
    if case.di:
        window = (f"new global::{ns}.Views.MainWindow {{ DataContext = Microsoft.Extensions.DependencyInjection."
                  f"ServiceProviderServiceExtensions.GetRequiredService<global::{ns}.ViewModels.MainViewModel>(global::{ns}.App.Services) }}")
    elif case.mvvm:
        window = f"new global::{ns}.Views.MainWindow {{ DataContext = new global::{ns}.ViewModels.MainViewModel() }}"
    else:
        window = f"new global::{ns}.Views.MainWindow()"
    add(f"""        var window = {window};
        window.Show();
        Pump();
        Check(window.IsVisible, "main window not shown");
        Check(HasTemplate(Find<global::{ns}.Views.MainView>(window)), "main view has no content");""")
    if case.arc_window:
        add("""        Check(window is ArcWindow, "main window is not an ArcWindow (--custom-titlebar)");""")

    # Pages.
    if case.navigation:
        add(f"""        var host = Find<global::{ns}.Navigation.PageHost>(window);""")
        if case.layout == "sidebar":
            add("""        var nav = Find<Sidebar>(window);
        Check(HasTemplate(nav), "sidebar has no template");""")
            select = "nav.SelectedKey = page.Key;"
            if case.settings:
                add(f"""        if ({second})
        {{
            Check(nav.IsCollapsed, "saved collapsed sidebar not restored");
            nav.IsCollapsed = false;
        }}""")
        else:
            add("""        var nav = Find<SegmentedControl>(window);""")
            select = "nav.SelectedValue = page.Key;"
        add(f"""        foreach (var page in global::{ns}.Navigation.AppPages.All)
        {{
            {select}
            Pump();
            Check(host.Current?.Key == page.Key, $"page {{page.Key}} not shown (showing {{host.Current?.Key}})");
            Check(host.Child is Control view && HasTemplate(view), $"page {{page.Key}} has no content");
        }}""")

    # Dock: closing a pane is part of the saved layout.
    if case.layout == "dock":
        add("""        var dock = Find<DockLayout>(window);""")
        closed = 'Regex.IsMatch(dock.SaveLayout(), "\\"closed\\":\\\\s*\\\\[[^\\\\]]*\\"" + closedId + "\\"")'
        close = """        // No pane is active before something has focus: take the first one on screen.
        var pane = dock.ActivePane ?? window.GetVisualDescendants().OfType<DockPane>().FirstOrDefault();
        Check(pane is not null && dock.Close(pane), "could not close the active pane");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-closed-pane.txt"), pane?.Id ?? "");"""
        if case.settings:
            add(f"""        if ({second})
        {{
            var closedId = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "smoke-closed-pane.txt"));
            Check({closed}, "saved dock layout not restored (the pane closed last time is open again)");
        }}
        else
        {{
{close}
        }}""")
        else:
            add(close)

    if case.scenario:
        add(smoke_scenario(case))

    # Text in the UI language: Chinese characters exactly when the app shows Chinese.
    chinese = case.language != "en-US"
    if case.i18n and case.settings:
        add(f"""        Check(ShowsCjk(window) == ({second} || {str(chinese).lower()}), "UI text not in the expected language");""")
    else:
        add(f"""        Check(ShowsCjk(window) == {str(chinese).lower()}, "UI text not in the expected language");""")

    # Switching the language at run time, with the picker (on the Settings page in the navigation layouts).
    if case.i18n:
        if case.navigation:
            key = "SelectedKey" if case.layout == "sidebar" else "SelectedValue"
            add(f"""        nav.{key} = "settings";
        Pump();""")
        expected_settings = csharp(STRINGS["Settings"]["zh-CN"])
        add(f"""        var picker = Find<global::{ns}.Views.LanguagePicker>(window);
        Find<Select>(picker).SelectedValue = "zh-CN";
        Pump();
        Check(ArcLanguage.Current.Value == "zh-CN", $"the language picker did not switch the language ({{ArcLanguage.Current}})");
        Check(global::{ns}.Localization.AppLangResources.Get(global::{ns}.Localization.AppLangResourceKind.Settings) == {expected_settings}, "the app's catalog has no zh-CN text");
        Check(ShowsCjk(window), "UI text did not follow the language switch");""")

    # Theme.
    add("""        ArcTheme.Apply(t => t.Appearance(ArcAppearance.Dark).Accent(ArcAccentPreset.Rose));
        Pump();""")
    if case.layout == "sidebar" and case.settings:
        add(f"""        if ({first})
        {{
            nav.IsCollapsed = true;
            Pump();
        }}""")
    add(smoke_items(case))
    add("""        window.Close();
        Pump();""")
    if case.settings:
        add(f"""        Check(global::{ns}.Settings.AppSettings.Store.Read() is {{ Length: > 0 }}, "settings were not saved");""")
    add("    }")

    store = ""
    if case.settings:
        # The settings file of this case: next to the project, not in the user's application data folder.
        store = (f"""        global::{ns}.Settings.AppSettings.Store = new global::{ns}.Settings.FileSettingsStore(
            Path.Combine(AppContext.BaseDirectory, "smoke-settings.json"));
        if (phase == "first")
        {{
            File.Delete(Path.Combine(AppContext.BaseDirectory, "smoke-settings.json"));
        }}""")
    if case.scenario == "workflow":
        store += (f"""
        global::{ns}.Services.WorkflowStore.Store = new global::{ns}.Settings.FileSettingsStore(
            Path.Combine(AppContext.BaseDirectory, "smoke-workflow.json"));
        if (phase == "first")
        {{
            File.Delete(Path.Combine(AppContext.BaseDirectory, "smoke-workflow.json"));
        }}""")
    if case.scenario == "ai-chat":
        store += (f"""
        global::{ns}.Services.ConversationStore.Store = new global::{ns}.Settings.FileSettingsStore(
            Path.Combine(AppContext.BaseDirectory, "smoke-conversations.json"));
        if (phase == "first")
        {{
            File.Delete(Path.Combine(AppContext.BaseDirectory, "smoke-conversations.json"));
        }}""")
    program = f"{ns}.Desktop" if case.xplat else ns
    header = SMOKE_HEADER.replace("__PROGRAM__", program).replace("__STORE__", store)
    return header + "\n" + "\n".join(body) + "\n}\n"


def smoke_scenario(case: Case) -> str:
    ns = case.project
    first, second = 'phase == "first"', 'phase == "second"'
    vm = f"((global::{ns}.ViewModels.MainViewModel)window.DataContext!)"
    if case.scenario == "admin":
        return f"""        // Signed out first: the sign-in screen, then the app.
        var main = {vm};
        Check(Find<LoginSplit>(window).IsEffectivelyVisible, "the sign-in screen is not shown");
        main.SignIn(new global::{ns}.Models.Account("Smoke Test", "smoke@example.com", "Owner"));
        Pump();
        Check(!Find<LoginSplit>(window).IsEffectivelyVisible, "the sign-in screen stays after signing in");
        var nav = Find<Sidebar>(window);
        if ({second})
        {{
            Check(nav.IsCollapsed, "saved collapsed sidebar not restored");
            nav.IsCollapsed = false;
        }}
        var host = Find<global::{ns}.Navigation.PageHost>(window);
        foreach (var page in global::{ns}.Navigation.AppPages.All)
        {{
            main.SelectedKey = page.Key;
            Pump();
            Check(host.Current?.Key == page.Key, $"page {{page.Key}} not shown (showing {{host.Current?.Key}})");
            Check(host.Child is Control view && HasTemplate(view), $"page {{page.Key}} has no content");
        }}
        // Customers: loaded from the sample service, narrowed by the search, a row opens the drawer.
        main.SelectedKey = "customers";
        Pump();
        var customers = (global::{ns}.ViewModels.CustomersPageViewModel)host.Child!.DataContext!;
        Check(WaitFor(() => !customers.IsLoading && customers.Rows.Count > 0), "no customers loaded");
        var shown = customers.Rows.Count;
        customers.Search = "Copperleaf";
        Pump();
        Check(customers.Rows.Count > 0 && customers.Rows.Count < shown && customers.Rows.All(c => c.Company == "Copperleaf"), "the search did not narrow the customers");
        customers.OpenCommand.Execute(customers.Rows[0]);
        Pump();
        Check(window.GetLogicalDescendants().OfType<Drawer>().Any(d => d.IsOpen), "the customer drawer did not open");
        customers.IsDetailOpen = false;
        Pump();
        // The dashboard's figures.
        main.SelectedKey = "dashboard";
        Check(WaitFor(() => window.GetVisualDescendants().OfType<MetricCard>().Count() == 4), "the dashboard shows no metric cards");
        // The command palette's entries run through the view model.
        main.RunCommand("billing");
        Pump();
        Check(host.Current?.Key == "billing", "the command palette's page entry did not navigate");
        if ({first})
        {{
            nav.IsCollapsed = true;
            Pump();
        }}
        main.SignOutCommand.Execute(null);
        Pump();
        Check(Find<LoginSplit>(window).IsEffectivelyVisible, "signing out did not show the sign-in screen");"""
    if case.scenario == "ai-chat":
        return f"""        // A message gets a streamed reply; the conversation is saved and comes back on the second run.
        var chat = Find<AiChat>(window);
        var nav = Find<Sidebar>(window);
        if ({second})
        {{
            Check(nav.IsCollapsed, "saved collapsed sidebar not restored");
            nav.IsCollapsed = false;
            Check(chat.Conversations.Count == 1 && chat.Conversations[0].Title == "Smoke test message", $"saved conversation not restored ({{chat.Conversations.Count}})");
            Check(nav.ItemsSource is System.Collections.IEnumerable rows && rows.Cast<object>().Count() == 1, "the sidebar does not list the saved conversation");
        }}
        else
        {{
            global::{ns}.SampleData.SampleResponder.Pace = TimeSpan.Zero;
            chat.Send("Smoke test message");
            Check(WaitFor(() => !chat.IsBusy), "the reply did not finish");
            var reply = chat.Conversations.Single().Turns[0].SelectedVersion!.Reply;
            Check(reply.Status == AiChatReplyStatus.Done && reply.Text.Contains("SampleResponder"), $"no streamed reply ({{reply.Status}})");
            Check(global::{ns}.Services.ConversationStore.Store.Read() is {{ }} json && json.Contains("Smoke test message"), "the conversation was not saved");
            nav.IsCollapsed = true;
            Pump();
        }}"""
    if case.scenario == "devtool":
        return f"""        // Files open as editor tabs, Run builds into Output and Problems, the open tabs come back with the saved layout.
        var main = {vm};
        var view = Find<global::{ns}.Views.MainView>(window);
        var dock = Find<DockLayout>(window);
        Check(window.GetVisualDescendants().OfType<TKCode.Arc.TreeView>().Any(HasTemplate), "the explorer has no template");
        if ({second})
        {{
            Check(dock.Workspace.FindPane("doc:src/Parser.cs")?.Content is global::{ns}.Views.EditorView, "the editor tab saved with the layout was not reopened");
        }}
        else
        {{
            Check(dock.Workspace.FindPane("doc:README.md") is not null, "README.md is not open on the first start");
            view.OpenFile("src/Parser.cs");
            Pump();
            Check(dock.Workspace.FindPane("doc:src/Parser.cs") is not null, "opening a file added no editor tab");
            global::{ns}.SampleData.SampleBuildService.Pace = TimeSpan.Zero;
            main.RunCommand.Execute(null);
            Check(WaitFor(() => !main.IsBuilding && main.Output.Count > 0), "the build did not finish");
            Check(main.Problems.Count == 1, $"expected one warning, got {{main.Problems.Count}}");
        }}
        Check(main.Selected is not null, "the Properties pane has no file");"""
    if case.scenario == "workflow":
        return f"""        // The saved workflow comes back; a palette step joins the selected one; a run walks the steps.
        var main = {vm};
        var canvas = Find<WorkflowCanvas>(window);
        Check(HasTemplate(canvas) && canvas.Graph == main.Graph, "the canvas does not show the workflow");
        if ({second})
        {{
            Check(main.Graph.Nodes.Count == 7 && main.Graph.FindNode("notify") is {{ }} last && main.Graph.OutgoingEdges(last).Any(),
                $"the saved workflow was not restored ({{main.Graph.Nodes.Count}} steps)");
        }}
        else
        {{
            Check(main.Graph.Nodes.Count == 6, $"the sample workflow has {{main.Graph.Nodes.Count}} steps");
            main.Selected = main.Graph.FindNode("notify");
            main.AddStepCommand.Execute(global::{ns}.Models.StepKinds.Find("wait"));
            Pump();
            Check(main.Graph.Nodes.Count == 7, "the palette did not add a step");
            global::{ns}.SampleData.SampleRunner.Pace = TimeSpan.Zero;
            main.RunCommand.Execute(null);
            Check(WaitFor(() => !main.IsRunning), "the run did not finish");
            Check(main.Graph.Nodes.Count(n => n.State == WorkflowNodeState.Done) >= 5, "the run did not finish the steps");
        }}"""
    if case.scenario in ("projects", "analytics"):
        blocks = ({"board": "KanbanBoard", "timeline": "GanttTimeline", "calendar": "WeekCalendar", "release": "ReleaseReadiness"}
                  if case.scenario == "projects" else
                  {"overview": "KpiDrilldown", "revenue": "RevenueExplorer", "retention": "CohortRetention", "usage": "UsageForecast", "status": "UptimeStatus"})
        checks = "\n".join(
            f"""        main.SelectedKey = "{key}";
        Pump();
        Check(host.Current?.Key == "{key}", "page {key} not shown");
        Check(host.Child is Control {key}View && {key}View.GetVisualDescendants().OfType<{block}>().Any(HasTemplate), "page {key} does not show its {block}");"""
            for key, block in blocks.items())
        collapse = ""
        if case.scenario == "projects":
            collapse = f"""
        var nav = Find<Sidebar>(window);
        if ({second})
        {{
            Check(nav.IsCollapsed, "saved collapsed sidebar not restored");
        }}
        else
        {{
            nav.IsCollapsed = true;
            Pump();
        }}"""
        return f"""        // Every page shows its block with the sample data.
        var main = {vm};
        var host = Find<global::{ns}.Navigation.PageHost>(window);
{checks}{collapse}"""
    raise ValueError(case.scenario)


def smoke_items(case: Case) -> str:
    if not case.items:
        return ""
    ns = case.project
    names = {item.template: item.name for item in case.items}
    page = with_suffix(names["tkcode-arc-page"], "Page")
    view_model = any("--view-model" in item.args for item in case.items)
    page_new = f"new global::{ns}.Pages.{page}()"
    if view_model:
        page_new = f"new global::{ns}.Pages.{page} {{ DataContext = new global::{ns}.ViewModels.{page}ViewModel() }}"
    other = with_suffix(names["tkcode-arc-window"], "Window")
    dialog = with_suffix(names["tkcode-arc-dialog"], "Dialog")
    control = names["tkcode-arc-control"]
    return f"""        // The item templates' output.
        var newPage = {page_new};
        var tile = new global::{ns}.Controls.{control} {{ Title = "Uptime", Value = "99.98%", Caption = "Last 30 days" }};
        var stage = new Window {{ Content = new StackPanel {{ Children = {{ newPage, tile }} }} }};
        stage.Show();
        Pump();
        Check(HasTemplate(newPage), "generated page has no content");
        Check(HasTemplate(tile), "generated control has no template (is its theme registered?)");
        Check(tile.Padding == new Thickness(16, 14), $"generated control's token not applied: Padding {{tile.Padding}}");
        stage.Close();
        var other = new global::{ns}.Views.{other}();
        other.Show(window);
        Pump();
        Check(other.IsVisible, "generated window not shown");
        other.Close();
        Pump();
        var dialog = global::{ns}.Dialogs.{dialog}.ShowAsync(window, "Draft");
        Pump();
        Check(!dialog.IsCompleted, "generated dialog closed by itself");
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump();
        Check(dialog.IsCompletedSuccessfully && dialog.Result is null, "generated dialog did not cancel on Escape");"""


# ----------------------------------------------------------------------------------------------------------------------
# Cases

def file_list(root: Path) -> list[str]:
    files = []
    for path in sorted(root.rglob("*")):
        relative = path.relative_to(root)
        if path.is_file() and not ({"bin", "obj"} & set(relative.parts)):
            files.append(relative.as_posix())
    return files


def text_of(path: Path) -> str:
    """The file's text, or nothing for a binary file (a font)."""
    try:
        return path.read_text()
    except UnicodeDecodeError:
        return ""


def check_case(case: Case, work: Path, env: dict[str, str], update: bool) -> list[str]:
    problems: list[str] = []
    cases = work / "cases"
    root = cases / case.key
    shutil.rmtree(root, ignore_errors=True)
    log(f"\n== {case.key}: dotnet new {case.template} {' '.join(case.args)}")

    run(["dotnet", "new", case.template, "-n", case.project, "-o", str(root), *case.args], cases, env)
    app_dir = case.app_dir(root)
    for item in case.items:
        run(["dotnet", "new", item.template, "-n", item.name, *item.args], app_dir, env)
    if any(item.template == "tkcode-arc-control" for item in case.items) and not case.i18n:
        # What the control template's instructions ask for: uncomment the registration line in AppSetup.cs.
        setup = app_dir / "AppSetup.cs"
        text = setup.read_text()
        line = (f'// .UsePackage("{case.project}", static p => '
                f'TKCode.Arc.Generated.{identifier(case.project)}.ArcGeneratedAssets.AddTo(p))')
        if line not in text:
            problems.append(f"{case.key}: AppSetup.cs lacks the commented registration line {line!r}")
        setup.write_text(text.replace(line, line[3:]))

    # 2. The created files.
    actual = file_list(root)
    baseline = BASELINES / f"{case.key}.txt"
    if update:
        BASELINES.mkdir(parents=True, exist_ok=True)
        baseline.write_text("\n".join(actual) + "\n")
    elif not baseline.exists():
        problems.append(f"{case.key}: no baseline {baseline.relative_to(REPO)} (run with --update)")
    elif baseline.read_text().split() != actual:
        expected = baseline.read_text().split()
        problems.append(f"{case.key}: created files differ from {baseline.relative_to(REPO)}: "
                        f"added {sorted(set(actual) - set(expected))}, missing {sorted(set(expected) - set(actual))}")
    leftovers = [path for path in actual if re.search(r"@@|ArcStarterApp|ArcDisplayName|ArcItem|ArcWindowWidth", text_of(root / path))]
    if leftovers:
        problems.append(f"{case.key}: template tokens left in {leftovers}")

    # 3. Build, warnings as errors; tests.
    (cases / "smoke" / f"{case.desktop_project}.Smoke.cs").write_text(smoke_source(case))
    run(["dotnet", "build", "-c", "Debug", "-warnaserror"], root, env)
    if case.tests:
        output = run(["dotnet", "test", "--no-build", "-c", "Debug"], root, env)
        totals = [int(n) for n in re.findall(r"Total:\s*(\d+)", output)]
        if not totals or min(totals) == 0:
            problems.append(f"{case.key}: dotnet test ran no tests")

    if case.library:
        # The library packs into a NuGet package without warnings.
        run(["dotnet", "pack", str(root / "src" / case.project), "-c", "Release", "-warnaserror", "-o", str(root / "artifacts")], root, env)
        if not (case.flag("--sample", True)):
            return problems

    # 4. Start it headless.
    assembly = case.desktop_dir(root) / "bin" / "Debug" / case.framework / f"{case.desktop_project}.dll"
    for phase in (["first", "second"] if case.settings else ["first"]):
        result = subprocess.run(["dotnet", str(assembly), phase], cwd=root, env=env, text=True,
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=300)
        output = result.stdout.strip()
        log("    " + output.replace("\n", "\n    "))
        if result.returncode != 0:
            problems.append(f"{case.key}: smoke {phase} failed")
    return problems


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--feed", type=Path, help="folder with the packed packages (default: pack into <work>/feed)")
    parser.add_argument("--work", type=Path, default=REPO / "artifacts" / "template-verify", help="work folder")
    parser.add_argument("--case", action="append", choices=[c.key for c in CASES], help="only these cases")
    parser.add_argument("--update", action="store_true", help="rewrite the file-list baselines")
    parser.add_argument("--skip-browser", action="store_true", help="leave out the cross-platform cases (no wasm workload)")
    parser.add_argument("--templates-only", action="store_true",
                        help="repack only the template package into the feed of an earlier run (faster while editing templates)")
    options = parser.parse_args()

    for script in ("sync_strings.py", "write_app_templates.py"):
        if subprocess.run([sys.executable, str(REPO / "tools" / "templates" / script), "--check"]).returncode != 0:
            return 1

    versions = read_versions()
    work = options.work.resolve()
    work.mkdir(parents=True, exist_ok=True)
    feed = (options.feed or work / "feed").resolve()
    env = prepare(work, feed, versions)
    try:
        if options.feed is None:
            pack(feed, env, options.templates_only)
        install_templates(feed, versions["ArcVersion"], work, env)
    except StepFailed as failure:
        log(str(failure))
        return 1

    problems: list[str] = []
    for case in CASES:
        if options.case and case.key not in options.case:
            continue
        if options.skip_browser and case.xplat:
            continue
        try:
            problems += check_case(case, work, env, options.update)
        except StepFailed as failure:
            log(f"    {failure}")
            problems.append(f"{case.key}: {failure}")

    log("")
    if problems:
        for problem in problems:
            log("FAIL " + problem)
        return 1
    log("All template cases passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
