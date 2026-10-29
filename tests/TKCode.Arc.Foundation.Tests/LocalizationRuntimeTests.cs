using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TKCode.Arc.Foundation.Samples;
using TKCode.Arc.Generated.TKCodeArcFoundationTests;
using TKCode.Arc.Localization;

namespace TKCode.Arc.Foundation.Tests;

/// <summary>The language runtime: lookup, live switching, fallback and the coverage check at startup.</summary>
public class LocalizationRuntimeTests
{
    [AvaloniaFact]
    public void The_application_starts_in_its_default_language_with_the_samples_catalog()
    {
        var languages = ArcLanguageManager.Current!;
        languages.Language.ShouldBe(LanguageTags.EnUS);
        languages.SupportedLanguages.ShouldBe([LanguageTags.EnUS, LanguageTags.ZhCN, LanguageTags.ZhTW]);
        languages.Catalogs.ShouldContain(SampleLangResources.Catalog);
        ArcGeneratedAssets.Languages.ShouldBe([SampleLangResources.Catalog]);
        ArcRuntime.Current!.Languages.ShouldBeSameAs(languages);
    }

    [AvaloniaFact]
    public void CSharp_lookup_and_format_follow_the_language()
    {
        SampleLangResources.Get(SampleLangResourceKind.Save).ShouldBe("Save changes");
        SampleLangResources.Format(SampleLangResourceKind.RowCount, 1200.ToString("N0", ArcLanguage.Culture)).ShouldBe("1,200 rows");
        ArcLanguage.Set(LanguageTags.ZhCN);
        SampleLangResources.Get(SampleLangResourceKind.Save).ShouldBe("保存更改");
        SampleLangResources.Format(SampleLangResourceKind.RowCount, 3).ShouldBe("3 行");
        ArcLanguage.Culture.Name.ShouldBe("zh-CN");
        ArcLanguage.Set("zh-tw");
        SampleLangResources.Get(SampleLangResourceKind.Save).ShouldBe("儲存變更");
        // A value the enum does not declare.
        SampleLangResources.Get((SampleLangResourceKind)99).ShouldBe("99");
    }

    [AvaloniaFact]
    public void Text_is_published_under_the_boxed_members()
    {
        TestHelpers.AppResource(SampleLangResourceKind.Cancel).ShouldBe("Cancel");
        SampleLangResources.Key(SampleLangResourceKind.Cancel).ShouldBe(SampleLangResourceKind.Cancel);
        ArcLanguage.Set(LanguageTags.ZhCN);
        TestHelpers.AppResource(SampleLangResourceKind.Cancel).ShouldBe("取消");
    }

    [AvaloniaFact]
    public void Markup_extension_text_follows_the_language()
    {
        var view = new SampleLocalizedView();
        var window = TestHelpers.Show(view);
        var save = TestHelpers.Part<TextBlock>(view, "SaveText");
        var cancel = TestHelpers.Part<TextBlock>(view, "CancelText");
        save.Text.ShouldBe("Save changes");
        cancel.Text.ShouldBe("Cancel");

        ArcLanguage.Set(LanguageTags.ZhCN);
        Dispatcher.UIThread.RunJobs();
        save.Text.ShouldBe("保存更改");
        cancel.Text.ShouldBe("取消");

        ArcLanguage.Set(LanguageTags.ZhTW);
        Dispatcher.UIThread.RunJobs();
        save.Text.ShouldBe("儲存變更");
        window.Close();
    }

    [AvaloniaFact]
    public void Bound_resource_observables_follow_the_language_and_only_changed_text_is_rewritten()
    {
        var text = new TextBlock();
        var brand = new TextBlock();
        var window = TestHelpers.Show(new StackPanel { Children = { text, brand } });
        using var binding = text.Bind(TextBlock.TextProperty, text.GetResourceObservable(SampleLangResourceKind.Save));
        var changes = 0;
        using var brandBinding = brand.Bind(TextBlock.TextProperty, brand.GetResourceObservable(SampleLangResourceKind.Brand));
        Dispatcher.UIThread.RunJobs();
        brand.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty)
            {
                changes++;
            }
        };
        var instance = TestHelpers.AppResource(SampleLangResourceKind.Brand);

        ArcLanguage.Set(LanguageTags.ZhCN);
        Dispatcher.UIThread.RunJobs();

        text.Text.ShouldBe("保存更改");
        // "Arc" is the same in both languages: its entry keeps its instance and its bindings do not change.
        changes.ShouldBe(0);
        TestHelpers.AppResource(SampleLangResourceKind.Brand).ShouldBeSameAs(instance);
        window.Close();
    }

    [AvaloniaFact]
    public void Changed_reports_old_and_new_language_once_per_switch()
    {
        var manager = ArcLanguageManager.Current!;
        var events = new List<(LanguageTag Old, LanguageTag New)>();
        var global = 0;
        EventHandler<LanguageChangedEventArgs> onGlobal = (_, _) => global++;
        manager.Changed += (_, e) => events.Add((e.OldLanguage, e.NewLanguage));
        ArcLanguage.Changed += onGlobal;
        try
        {
            ArcLanguage.Set(LanguageTags.ZhCN);
            ArcLanguage.Set(LanguageTags.ZhCN);
            ArcLanguage.Set(LanguageTags.EnUS);
        }
        finally
        {
            ArcLanguage.Changed -= onGlobal;
        }
        events.ShouldBe([(LanguageTags.EnUS, LanguageTags.ZhCN), (LanguageTags.ZhCN, LanguageTags.EnUS)]);
        global.ShouldBe(2);
    }

    [AvaloniaFact]
    public void A_language_the_application_does_not_offer_is_refused()
    {
        var error = Should.Throw<ArgumentException>(() => ArcLanguage.Set("pt-BR"));
        error.Message.ShouldContain("'pt-BR' is not one of the application's languages (en-US, zh-CN, zh-TW)");
        ArcLanguage.Current.ShouldBe(LanguageTags.EnUS);
    }

    [Fact]
    public void Missing_text_falls_back_to_parents_then_the_default_language_then_the_source()
    {
        var catalog = Catalog(
            ("en-US", ["One", "Two", "Three", "Four"]),
            ("zh-CN", ["一", "二", "三", null]),
            ("zh-Hant", ["壹", null, null, null]),
            ("zh-Hant-TW", [null, "貳", null, null]));
        var manager = new ArcLanguageManager(LanguageTags.ZhCN,
            [LanguageTags.ZhCN, LanguageTag.Parse("zh-Hant-TW"), LanguageTags.EnUS], [catalog]);

        manager.Set(LanguageTag.Parse("zh-hant-tw"));
        manager.Get(catalog, 0).ShouldBe("壹");   // parent zh-Hant
        manager.Get(catalog, 1).ShouldBe("貳");   // own table
        manager.Get(catalog, 2).ShouldBe("三");   // default language zh-CN
        manager.Get(catalog, 3).ShouldBe("Four"); // source language
        manager.Resources[TestKind.B].ShouldBe("貳");
    }

    [Fact]
    public void Without_an_application_text_comes_from_the_source_language()
    {
        // Plain facts may still see the headless application, whose language is en-US; the catalog answers alike without one.
        var catalog = SampleLangResources.Catalog;
        catalog.SourceText(SampleLangResources.Index(SampleLangResourceKind.Save)).ShouldBe("Save changes");
        catalog.SourceLanguage.ShouldBe(LanguageTags.EnUS);
        SampleLangResources.Get(SampleLangResourceKind.Save).ShouldBe("Save changes");
        SampleLangResources.Format(SampleLangResourceKind.RowCount, 2).ShouldBe("2 rows");
    }

    [AvaloniaFact]
    public void Startup_fails_when_an_enabled_package_does_not_cover_a_language()
    {
        var application = new Application();
        var builder = new ArcBuilder()
            .UseFoundationSamples()
            .UsePackage("Gappy", p => p.AddLanguages([Catalog(("en-US", ["One", "Two", "Three", "Four"]), ("zh-CN", ["一", null, "三", null]))]))
            .UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN, LanguageTag.Parse("pt-BR")]);

        var error = Should.Throw<InvalidOperationException>(() => ArcRuntime.Install(application, builder));

        error.Message.ShouldBe("""
            The application's languages are not covered by every enabled package:
            - Package 'TKCode.Arc.Foundation.Tests' has no 'pt-BR' text for 'TKCode.Arc.Foundation.Samples.SampleLangResourceKind' (add Localization/pt-BR.xlf).
            - Package 'Gappy' is missing 2 'zh-CN' entries of 'Tests.TestKind': B, D.
            - Package 'Gappy' has no 'pt-BR' text for 'Tests.TestKind' (add Localization/pt-BR.xlf).
            """.ReplaceLineEndings("\n"));
        ArcRuntime.Find(application).ShouldBeNull();
        application.Resources.MergedDictionaries.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Two_packages_cannot_register_the_same_catalog()
    {
        var builder = new ArcBuilder()
            .UsePackage("First", p => p.AddLanguages([SampleLangResources.Catalog]))
            .UsePackage("Second", p => p.AddLanguages([SampleLangResources.Catalog]));
        Should.Throw<InvalidOperationException>(() => ArcRuntime.Install(new Application(), builder))
              .Message.ShouldBe("Language catalog 'TKCode.Arc.Foundation.Samples.SampleLangResourceKind' is registered by both 'First' and 'Second'.");
    }

    [Fact]
    public void The_default_language_must_be_offered()
    {
        Should.Throw<ArgumentException>(() => new ArcBuilder().UseLanguages(LanguageTags.ZhCN, [LanguageTags.EnUS]))
              .Message.ShouldContain("'zh-CN' is not one of the supported languages");
    }

    [Theory]
    [InlineData("en-us", "en-US")]
    [InlineData("ZH_hant_tw", "zh-Hant-TW")]
    [InlineData("es-419", "es-419")]
    [InlineData("zh", "zh")]
    public void Language_tags_are_canonical(string text, string canonical)
    {
        LanguageTag.Parse(text).Value.ShouldBe(canonical);
        LanguageTag.Parse(text).ShouldBe(LanguageTag.Parse(canonical));
    }

    [Theory]
    [InlineData("")]
    [InlineData("e")]
    [InlineData("en--US")]
    [InlineData("1x-US")]
    [InlineData("en-toolongsubtag")]
    public void Invalid_language_tags_are_rejected(string text)
    {
        LanguageTag.TryParse(text, out _).ShouldBeFalse();
    }

    [Fact]
    public void Language_tags_know_their_parents()
    {
        var tag = LanguageTag.Parse("zh-Hant-TW");
        tag.Parent!.Value.ShouldBe("zh-Hant");
        tag.Parent!.Parent!.Value.ShouldBe("zh");
        tag.Parent!.Parent!.Parent.ShouldBeNull();
        LanguageTag.Parse("zh").Includes(tag).ShouldBeTrue();
        LanguageTag.Parse("zh-Hans").Includes(tag).ShouldBeFalse();
        LanguageTags.ZhTW.ToCulture().Name.ShouldBe("zh-TW");
    }

    private enum TestKind
    {
        A,
        B,
        C,
        D
    }

    private static ArcLanguageCatalog Catalog(params (string Language, string?[] Texts)[] tables)
        => new("Tests.TestKind", LanguageTags.EnUS, [TestKind.A, TestKind.B, TestKind.C, TestKind.D], ["A", "B", "C", "D"],
            tables.Select(t => new ArcLanguageTable(LanguageTag.Parse(t.Language), t.Texts)).ToArray());
}
