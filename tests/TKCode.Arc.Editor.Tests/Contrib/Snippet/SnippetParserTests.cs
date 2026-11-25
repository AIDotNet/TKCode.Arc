// Ported from vscode (src/vs/editor/contrib/snippet/test/browser/snippetParser.test.ts, commit b57d5c9e), MIT license.
// All 54 upstream tests. `instanceof` checks are type checks (Type[] in place of constructors); JavaScript regular
// expression literals are JsRegExp; `undefined` is null.

using Shouldly;
using TKCode.Arc.Editor.Contrib.Snippet;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class SnippetParserTests
{
    [Fact]
    public void Scanner()
    {
        var scanner = new Scanner();
        scanner.Next().Type.ShouldBe(TokenType.EOF);

        AssertTokens(scanner, "abc", TokenType.VariableName, TokenType.EOF);
        AssertTokens(scanner, "{{abc}}", TokenType.CurlyOpen, TokenType.CurlyOpen, TokenType.VariableName, TokenType.CurlyClose, TokenType.CurlyClose, TokenType.EOF);
        AssertTokens(scanner, "abc() ", TokenType.VariableName, TokenType.Format, TokenType.EOF);
        AssertTokens(scanner, "abc 123", TokenType.VariableName, TokenType.Format, TokenType.Int, TokenType.EOF);
        AssertTokens(scanner, "$foo", TokenType.Dollar, TokenType.VariableName, TokenType.EOF);
        AssertTokens(scanner, "$foo_bar", TokenType.Dollar, TokenType.VariableName, TokenType.EOF);
        AssertTokens(scanner, "$foo-bar", TokenType.Dollar, TokenType.VariableName, TokenType.Dash, TokenType.VariableName, TokenType.EOF);
        AssertTokens(scanner, "${foo}", TokenType.Dollar, TokenType.CurlyOpen, TokenType.VariableName, TokenType.CurlyClose, TokenType.EOF);
        AssertTokens(scanner, "${1223:foo}", TokenType.Dollar, TokenType.CurlyOpen, TokenType.Int, TokenType.Colon, TokenType.VariableName, TokenType.CurlyClose, TokenType.EOF);
        AssertTokens(scanner, "\\${}", TokenType.Backslash, TokenType.Dollar, TokenType.CurlyOpen, TokenType.CurlyClose);
        AssertTokens(scanner, "${foo/regex/format/option}",
            TokenType.Dollar, TokenType.CurlyOpen, TokenType.VariableName, TokenType.Forwardslash, TokenType.VariableName,
            TokenType.Forwardslash, TokenType.VariableName, TokenType.Forwardslash, TokenType.VariableName, TokenType.CurlyClose,
            TokenType.EOF);
    }

    private static void AssertTokens(Scanner scanner, string text, params TokenType[] types)
    {
        scanner.Text(text);
        foreach (var type in types)
        {
            scanner.Next().Type.ShouldBe(type);
        }
    }

    private static void AssertText(string value, string expected) => SnippetParser.AsInsertText(value).ShouldBe(expected);

    private static void AssertMarker(TextmateSnippet input, params Type[] ctors) => AssertMarker(input.Children, ctors);

    private static void AssertMarker(string input, params Type[] ctors) => AssertMarker(new SnippetParser().Parse(input).Children, ctors);

    private static void AssertMarker(IReadOnlyList<Marker> input, params Type[] ctors)
    {
        var marker = input.ToList();
        var expected = ctors.ToList();
        while (marker.Count > 0)
        {
            var m = marker[^1];
            marker.RemoveAt(marker.Count - 1);
            expected.Count.ShouldBeGreaterThan(0);
            var ctor = expected[^1];
            expected.RemoveAt(expected.Count - 1);
            ctor.IsInstanceOfType(m).ShouldBeTrue($"{m.GetType().Name} is not {ctor.Name}");
        }

        expected.Count.ShouldBe(0);
    }

    private static void AssertTextAndMarker(string value, string escaped, params Type[] ctors)
    {
        AssertText(value, escaped);
        AssertMarker(value, ctors);
    }

    private static void AssertEscaped(string value, string expected) => SnippetParser.Escape(value).ShouldBe(expected);

    [Fact]
    public void Parser_escaped()
    {
        AssertEscaped("foo$0", "foo\\$0");
        AssertEscaped("foo\\$0", "foo\\\\\\$0");
        AssertEscaped("f$1oo$0", "f\\$1oo\\$0");
        AssertEscaped("${1:foo}$0", "\\${1:foo\\}\\$0");
        AssertEscaped("$", "\\$");
    }

    [Fact]
    public void Parser_text()
    {
        AssertText("$", "$");
        AssertText("\\\\$", "\\$");
        AssertText("{", "{");
        AssertText("\\}", "}");
        AssertText("\\abc", "\\abc");
        AssertText("foo${f:\\}}bar", "foo}bar");
        AssertText("\\{", "\\{");
        AssertText("I need \\\\\\$", "I need \\$");
        AssertText("\\", "\\");
        AssertText("\\{{", "\\{{");
        AssertText("{{", "{{");
        AssertText("{{dd", "{{dd");
        AssertText("}}", "}}");
        AssertText("ff}}", "ff}}");

        AssertText("farboo", "farboo");
        AssertText("far{{}}boo", "far{{}}boo");
        AssertText("far{{123}}boo", "far{{123}}boo");
        AssertText("far\\{{123}}boo", "far\\{{123}}boo");
        AssertText("far{{id:bern}}boo", "far{{id:bern}}boo");
        AssertText("far{{id:bern {{basel}}}}boo", "far{{id:bern {{basel}}}}boo");
        AssertText("far{{id:bern {{id:basel}}}}boo", "far{{id:bern {{id:basel}}}}boo");
        AssertText("far{{id:bern {{id2:basel}}}}boo", "far{{id:bern {{id2:basel}}}}boo");
    }

    [Fact]
    public void Parser_TM_text()
    {
        AssertTextAndMarker("foo${1:bar}}", "foobar}", typeof(Text), typeof(Placeholder), typeof(Text));
        AssertTextAndMarker("foo${1:bar}${2:foo}}", "foobarfoo}", typeof(Text), typeof(Placeholder), typeof(Placeholder), typeof(Text));

        AssertTextAndMarker("foo${1:bar\\}${2:foo}}", "foobar}foo", typeof(Text), typeof(Placeholder));

        var placeholder = (Placeholder)new SnippetParser().Parse("foo${1:bar\\}${2:foo}}").Children[1];
        var children = placeholder.Children;

        placeholder.Index.ShouldBe(1);
        children[0].ShouldBeOfType<Text>();
        children[0].ToString().ShouldBe("bar}");
        children[1].ShouldBeOfType<Placeholder>();
        children[1].ToString().ShouldBe("foo");
    }

    [Fact]
    public void Parser_placeholder()
    {
        AssertTextAndMarker("farboo", "farboo", typeof(Text));
        AssertTextAndMarker("far{{}}boo", "far{{}}boo", typeof(Text));
        AssertTextAndMarker("far{{123}}boo", "far{{123}}boo", typeof(Text));
        AssertTextAndMarker("far\\{{123}}boo", "far\\{{123}}boo", typeof(Text));
    }

    [Fact]
    public void Parser_literal_code()
    {
        AssertTextAndMarker("far`123`boo", "far`123`boo", typeof(Text));
        AssertTextAndMarker("far\\`123\\`boo", "far\\`123\\`boo", typeof(Text));
    }

    [Fact]
    public void Parser_variables_tabstop()
    {
        AssertTextAndMarker("$far-boo", "-boo", typeof(Variable), typeof(Text));
        AssertTextAndMarker("\\$far-boo", "$far-boo", typeof(Text));
        AssertTextAndMarker("far$farboo", "far", typeof(Text), typeof(Variable));
        AssertTextAndMarker("far${farboo}", "far", typeof(Text), typeof(Variable));
        AssertTextAndMarker("$123", "", typeof(Placeholder));
        AssertTextAndMarker("$farboo", "", typeof(Variable));
        AssertTextAndMarker("$far12boo", "", typeof(Variable));
        AssertTextAndMarker("000_${far}_000", "000__000", typeof(Text), typeof(Variable), typeof(Text));
        AssertTextAndMarker("FFF_${TM_SELECTED_TEXT}_FFF$0", "FFF__FFF", typeof(Text), typeof(Variable), typeof(Text), typeof(Placeholder));
    }

    [Fact]
    public void Parser_variables_placeholder_with_defaults()
    {
        AssertTextAndMarker("${name:value}", "value", typeof(Variable));
        AssertTextAndMarker("${1:value}", "value", typeof(Placeholder));
        AssertTextAndMarker("${1:bar${2:foo}bar}", "barfoobar", typeof(Placeholder));

        AssertTextAndMarker("${name:value", "${name:value", typeof(Text));
        AssertTextAndMarker("${1:bar${2:foobar}", "${1:barfoobar", typeof(Text), typeof(Placeholder));
    }

    [Fact]
    public void Parser_variable_transforms()
    {
        AssertTextAndMarker("${foo///}", "", typeof(Variable));
        AssertTextAndMarker("${foo/regex/format/gmi}", "", typeof(Variable));
        AssertTextAndMarker("${foo/([A-Z][a-z])/format/}", "", typeof(Variable));

        // invalid regex
        AssertTextAndMarker("${foo/([A-Z][a-z])/format/GMI}", "${foo/([A-Z][a-z])/format/GMI}", typeof(Text));
        AssertTextAndMarker("${foo/([A-Z][a-z])/format/funky}", "${foo/([A-Z][a-z])/format/funky}", typeof(Text));
        AssertTextAndMarker("${foo/([A-Z][a-z]/format/}", "${foo/([A-Z][a-z]/format/}", typeof(Text));

        // tricky regex
        AssertTextAndMarker("${foo/m\\/atch/$1/i}", "", typeof(Variable));
        AssertMarker("${foo/regex/format/options}", typeof(Text));

        // incomplete
        AssertTextAndMarker("${foo///", "${foo///", typeof(Text));
        AssertTextAndMarker("${foo/regex/format/options", "${foo/regex/format/options", typeof(Text));

        // format string
        AssertMarker("${foo/.*/${0:fooo}/i}", typeof(Variable));
        AssertMarker("${foo/.*/${1}/i}", typeof(Variable));
        AssertMarker("${foo/.*/$1/i}", typeof(Variable));
        AssertMarker("${foo/.*/This-$1-encloses/i}", typeof(Variable));
        AssertMarker("${foo/.*/complex${1:else}/i}", typeof(Variable));
        AssertMarker("${foo/.*/complex${1:-else}/i}", typeof(Variable));
        AssertMarker("${foo/.*/complex${1:+if}/i}", typeof(Variable));
        AssertMarker("${foo/.*/complex${1:?if:else}/i}", typeof(Variable));
        AssertMarker("${foo/.*/complex${1:/upcase}/i}", typeof(Variable));
    }

    [Fact]
    public void Parser_placeholder_transforms()
    {
        AssertTextAndMarker("${1///}", "", typeof(Placeholder));
        AssertTextAndMarker("${1/regex/format/gmi}", "", typeof(Placeholder));
        AssertTextAndMarker("${1/([A-Z][a-z])/format/}", "", typeof(Placeholder));

        // tricky regex
        AssertTextAndMarker("${1/m\\/atch/$1/i}", "", typeof(Placeholder));
        AssertMarker("${1/regex/format/options}", typeof(Text));

        // incomplete
        AssertTextAndMarker("${1///", "${1///", typeof(Text));
        AssertTextAndMarker("${1/regex/format/options", "${1/regex/format/options", typeof(Text));
    }

    [Fact]
    public void No_way_to_escape_forward_slash_in_snippet_regex_36715()
    {
        AssertMarker("${TM_DIRECTORY/src\\//$1/}", typeof(Variable));
    }

    [Fact]
    public void No_way_to_escape_forward_slash_in_snippet_format_section_37562()
    {
        AssertMarker("${TM_SELECTED_TEXT/a/\\/$1/g}", typeof(Variable));
        AssertMarker("${TM_SELECTED_TEXT/a/in\\/$1ner/g}", typeof(Variable));
        AssertMarker("${TM_SELECTED_TEXT/a/end\\//g}", typeof(Variable));
    }

    [Fact]
    public void Parser_placeholder_with_choice()
    {
        AssertTextAndMarker("${1|one,two,three|}", "one", typeof(Placeholder));
        AssertTextAndMarker("${1|one|}", "one", typeof(Placeholder));
        AssertTextAndMarker("${1|one1,two2|}", "one1", typeof(Placeholder));
        AssertTextAndMarker("${1|one1\\,two2|}", "one1,two2", typeof(Placeholder));
        AssertTextAndMarker("${1|one1\\|two2|}", "one1|two2", typeof(Placeholder));
        AssertTextAndMarker("${1|one1\\atwo2|}", "one1\\atwo2", typeof(Placeholder));
        AssertTextAndMarker("${1|one,two,three,|}", "${1|one,two,three,|}", typeof(Text));
        AssertTextAndMarker("${1|one,", "${1|one,", typeof(Text));

        var snippet = new SnippetParser().Parse("${1|one,two,three|}");
        var expected = new Queue<Func<Marker, bool>>(
        [
            m => m is Placeholder,
            m => m is Choice choice && choice.Options.Count == 3 && choice.Options.All(x => x is not null),
        ]);
        snippet.Walk(marker =>
        {
            expected.Dequeue()(marker).ShouldBeTrue();
            return true;
        });
    }

    [Fact]
    public void Snippet_choices_unable_to_escape_comma_and_pipe_31521()
    {
        AssertTextAndMarker("console.log(${1|not\\, not, five, 5, 1   23|});", "console.log(not, not);", typeof(Text), typeof(Placeholder), typeof(Text));
    }

    [Fact]
    public void Marker_toTextmateString()
    {
        static void AssertTextsnippetString(string input, string expected) =>
            new SnippetParser().Parse(input).ToTextmateString().ShouldBe(expected);

        AssertTextsnippetString("$1", "$1");
        AssertTextsnippetString("\\$1", "\\$1");
        AssertTextsnippetString("console.log(${1|not\\, not, five, 5, 1   23|});", "console.log(${1|not\\, not, five, 5, 1   23|});");
        AssertTextsnippetString("console.log(${1|not\\, not, \\| five, 5, 1   23|});", "console.log(${1|not\\, not, \\| five, 5, 1   23|});");
        AssertTextsnippetString("${1|cho\\,ices,wi\\|th,esc\\\\aping,chall\\\\\\,enges|}", "${1|cho\\,ices,wi\\|th,esc\\\\aping,chall\\\\\\,enges|}");
        AssertTextsnippetString("this is text", "this is text");
        AssertTextsnippetString("this ${1:is ${2:nested with $var}}", "this ${1:is ${2:nested with ${var}}}");
        AssertTextsnippetString("this ${1:is ${2:nested with $var}}}", "this ${1:is ${2:nested with ${var}}}\\}");
    }

    [Fact]
    public void Marker_toTextmateString_identity()
    {
        static void CheckCheckChildren(Marker marker1, Marker marker2)
        {
            marker1.GetType().ShouldBe(marker2.GetType());
            marker1.Children.Count.ShouldBe(marker2.Children.Count);
            marker1.ToString().ShouldBe(marker2.ToString());
            for (var i = 0; i < marker1.Children.Count; i++)
            {
                CheckCheckChildren(marker1.Children[i], marker2.Children[i]);
            }
        }

        static void AssertIdent(string input)
        {
            // full loop: (1) parse input, (2) generate textmate string, (3) parse, (4) ensure both trees are equal
            var snippet = new SnippetParser().Parse(input);
            var input2 = snippet.ToTextmateString();
            var snippet2 = new SnippetParser().Parse(input2);
            CheckCheckChildren(snippet, snippet2);
        }

        AssertIdent("$1");
        AssertIdent("\\$1");
        AssertIdent("console.log(${1|not\\, not, five, 5, 1   23|});");
        AssertIdent("console.log(${1|not\\, not, \\| five, 5, 1   23|});");
        AssertIdent("this is text");
        AssertIdent("this ${1:is ${2:nested with $var}}");
        AssertIdent("this ${1:is ${2:nested with $var}}}");
        AssertIdent("this ${1:is ${2:nested with $var}} and repeating $1");
    }

    [Fact]
    public void Parser_choise_marker()
    {
        var placeholders = new SnippetParser().Parse("${1|one,two,three|}").Placeholders;

        placeholders.Count.ShouldBe(1);
        placeholders[0].Choice.ShouldNotBeNull();
        placeholders[0].Children[0].ShouldBeOfType<Choice>();
        ((Choice)placeholders[0].Children[0]).Options.Count.ShouldBe(3);

        AssertText("${1|one,two,three|}", "one");
        AssertText("\\${1|one,two,three|}", "${1|one,two,three|}");
        AssertText("${1\\|one,two,three|}", "${1\\|one,two,three|}");
        AssertText("${1||}", "${1||}");
    }

    [Fact]
    public void Backslash_character_escape_in_choice_tabstop_doesnt_work_58494()
    {
        var placeholders = new SnippetParser().Parse("${1|\\,,},$,\\|,\\\\|}").Placeholders;
        placeholders.Count.ShouldBe(1);
        placeholders[0].Choice.ShouldNotBeNull();
    }

    [Fact]
    public void Parser_only_textmate()
    {
        var p = new SnippetParser();
        AssertMarker(p.Parse("far{{}}boo"), typeof(Text));
        AssertMarker(p.Parse("far{{123}}boo"), typeof(Text));
        AssertMarker(p.Parse("far\\{{123}}boo"), typeof(Text));

        AssertMarker(p.Parse("far$0boo"), typeof(Text), typeof(Placeholder), typeof(Text));
        AssertMarker(p.Parse("far${123}boo"), typeof(Text), typeof(Placeholder), typeof(Text));
        AssertMarker(p.Parse("far\\${123}boo"), typeof(Text));
    }

    [Fact]
    public void Parser_real_world()
    {
        var marker = new SnippetParser().Parse("console.warn(${1: $TM_SELECTED_TEXT })").Children;

        marker[0].ToString().ShouldBe("console.warn(");
        marker[1].ShouldBeOfType<Placeholder>();
        marker[2].ToString().ShouldBe(")");

        var placeholder = (Placeholder)marker[1];
        placeholder.Index.ShouldBe(1);
        placeholder.Children.Count.ShouldBe(3);
        placeholder.Children[0].ShouldBeOfType<Text>();
        placeholder.Children[1].ShouldBeOfType<Variable>();
        placeholder.Children[2].ShouldBeOfType<Text>();
        placeholder.Children[0].ToString().ShouldBe(" ");
        placeholder.Children[1].ToString().ShouldBe("");
        placeholder.Children[2].ToString().ShouldBe(" ");

        var nestedVariable = (Variable)placeholder.Children[1];
        nestedVariable.Name.ShouldBe("TM_SELECTED_TEXT");
        nestedVariable.Children.Count.ShouldBe(0);

        marker = new SnippetParser().Parse("$TM_SELECTED_TEXT").Children;
        marker.Count.ShouldBe(1);
        marker[0].ShouldBeOfType<Variable>();
    }

    [Fact]
    public void Parser_transform_example()
    {
        var children = new SnippetParser().Parse("${1:name} : ${2:type}${3/\\s:=(.*)/${1:+ :=}${1}/};\n$0").Children;

        //${1:name}
        children[0].ShouldBeOfType<Placeholder>();
        children[0].Children.Count.ShouldBe(1);
        children[0].Children[0].ToString().ShouldBe("name");
        ((Placeholder)children[0]).Transform.ShouldBeNull();

        // :
        children[1].ShouldBeOfType<Text>();
        children[1].ToString().ShouldBe(" : ");

        //${2:type}
        children[2].ShouldBeOfType<Placeholder>();
        children[2].Children.Count.ShouldBe(1);
        children[2].Children[0].ToString().ShouldBe("type");

        //${3/\\s:=(.*)/${1:+ :=}${1}/}
        children[3].ShouldBeOfType<Placeholder>();
        children[3].Children.Count.ShouldBe(0);
        var transform = ((Placeholder)children[3]).Transform.ShouldNotBeNull();
        transform.Regexp.Source.ShouldBe("\\s:=(.*)");
        transform.Regexp.Flags.ShouldBe("");
        transform.Children.Count.ShouldBe(2);
        transform.Children[0].ShouldBeOfType<FormatString>();
        ((FormatString)transform.Children[0]).Index.ShouldBe(1);
        ((FormatString)transform.Children[0]).IfValue.ShouldBe(" :=");
        transform.Children[1].ShouldBeOfType<FormatString>();
        ((FormatString)transform.Children[1]).Index.ShouldBe(1);
        children[4].ShouldBeOfType<Text>();
        children[4].ToString().ShouldBe(";\n");
    }

    [Fact]
    public void Parser_default_placeholder_values()
    {
        AssertMarker("errorContext: `${1:err}`, error: $1", typeof(Text), typeof(Placeholder), typeof(Text), typeof(Placeholder));

        var children = new SnippetParser().Parse("errorContext: `${1:err}`, error:$1").Children;
        var p1 = (Placeholder)children[1];
        var p2 = (Placeholder)children[3];

        p1.Index.ShouldBe(1);
        p1.Children.Count.ShouldBe(1);
        p1.Children[0].ToString().ShouldBe("err");

        p2.Index.ShouldBe(1);
        p2.Children.Count.ShouldBe(1);
        p2.Children[0].ToString().ShouldBe("err");
    }

    [Fact]
    public void Parser_default_placeholder_values_and_one_transform()
    {
        AssertMarker("errorContext: `${1:err}`, error: ${1/err/ok/}", typeof(Text), typeof(Placeholder), typeof(Text), typeof(Placeholder));

        var children = new SnippetParser().Parse("errorContext: `${1:err}`, error:${1/err/ok/}").Children;
        var p3 = (Placeholder)children[1];
        var p4 = (Placeholder)children[3];

        p3.Index.ShouldBe(1);
        p3.Children.Count.ShouldBe(1);
        p3.Children[0].ToString().ShouldBe("err");
        p3.Transform.ShouldBeNull();

        p4.Index.ShouldBe(1);
        p4.Children.Count.ShouldBe(1);
        p4.Children[0].ToString().ShouldBe("err");
        p4.Transform.ShouldNotBeNull();
    }

    [Fact]
    public void Repeated_snippet_placeholder_should_always_inherit_31040()
    {
        AssertText("${1:foo}-abc-$1", "foo-abc-foo");
        AssertText("${1:foo}-abc-${1}", "foo-abc-foo");
        AssertText("${1:foo}-abc-${1:bar}", "foo-abc-foo");
        AssertText("${1}-abc-${1:foo}", "foo-abc-foo");
    }

    [Fact]
    public void Backspace_escape_in_TM_only_16212()
    {
        SnippetParser.AsInsertText("Foo \\\\${abc}bar").ShouldBe("Foo \\bar");
    }

    [Fact]
    public void Colon_as_variable_placeholder_value_16717()
    {
        SnippetParser.AsInsertText("${TM_SELECTED_TEXT:foo:bar}").ShouldBe("foo:bar");
        SnippetParser.AsInsertText("${1:foo:bar}").ShouldBe("foo:bar");
    }

    [Fact]
    public void Incomplete_placeholder()
    {
        AssertTextAndMarker("${1:}", "", typeof(Placeholder));
    }

    [Fact]
    public void Marker_len()
    {
        static void AssertLen(string template, params int[] lengths)
        {
            var expected = new Queue<int>(lengths);
            var snippet = new SnippetParser().Parse(template, true);
            snippet.Walk(m =>
            {
                m.Len().ShouldBe(expected.Dequeue());
                return true;
            });
            expected.Count.ShouldBe(0);
        }

        AssertLen("text$0", 4, 0);
        AssertLen("$1text$0", 0, 4, 0);
        AssertLen("te$1xt$0", 2, 0, 2, 0);
        AssertLen("errorContext: `${1:err}`, error: $0", 15, 0, 3, 10, 0);
        AssertLen("errorContext: `${1:err}`, error: $1$0", 15, 0, 3, 10, 0, 3, 0);
        AssertLen("$TM_SELECTED_TEXT$0", 0, 0);
        AssertLen("${TM_SELECTED_TEXT:def}$0", 0, 3, 0);
    }

    [Fact]
    public void Parser_parent_node()
    {
        var snippet = new SnippetParser().Parse("This ${1:is ${2:nested}}$0", true);

        snippet.Placeholders.Count.ShouldBe(3);
        var first = snippet.Placeholders[0];
        var second = snippet.Placeholders[1];
        first.Index.ShouldBe(1);
        second.Index.ShouldBe(2);
        second.Parent.ShouldBeSameAs(first);
        first.Parent.ShouldBeSameAs(snippet);

        snippet = new SnippetParser().Parse("${VAR:default${1:value}}$0", true);
        snippet.Placeholders.Count.ShouldBe(2);
        first = snippet.Placeholders[0];
        first.Index.ShouldBe(1);

        snippet.Children[0].ShouldBeOfType<Variable>();
        first.Parent.ShouldBeSameAs(snippet.Children[0]);
    }

    [Fact]
    public void TextmateSnippet_enclosingPlaceholders()
    {
        var snippet = new SnippetParser().Parse("This ${1:is ${2:nested}}$0", true);
        var first = snippet.Placeholders[0];
        var second = snippet.Placeholders[1];

        snippet.EnclosingPlaceholders(first).ShouldBeEmpty();
        snippet.EnclosingPlaceholders(second).ShouldBe([first]);
    }

    [Fact]
    public void TextmateSnippet_offset()
    {
        var snippet = new SnippetParser().Parse("te$1xt", true);
        snippet.Offset(snippet.Children[0]).ShouldBe(0);
        snippet.Offset(snippet.Children[1]).ShouldBe(2);
        snippet.Offset(snippet.Children[2]).ShouldBe(2);

        snippet = new SnippetParser().Parse("${TM_SELECTED_TEXT:def}", true);
        snippet.Offset(snippet.Children[0]).ShouldBe(0);
        snippet.Offset(snippet.Children[0].Children[0]).ShouldBe(0);

        // forgein marker
        snippet.Offset(new Text("foo")).ShouldBe(-1);
    }

    [Fact]
    public void TextmateSnippet_placeholder()
    {
        new SnippetParser().Parse("te$1xt$0", true).Placeholders.Count.ShouldBe(2);
        new SnippetParser().Parse("te$1xt$1$0", true).Placeholders.Count.ShouldBe(3);
        new SnippetParser().Parse("te$1xt$2$0", true).Placeholders.Count.ShouldBe(3);
        new SnippetParser().Parse("${1:bar${2:foo}bar}$0", true).Placeholders.Count.ShouldBe(3);
    }

    [Fact]
    public void TextmateSnippet_replace_1_of_2()
    {
        var snippet = new SnippetParser().Parse("aaa${1:bbb${2:ccc}}$0", true);

        snippet.Placeholders.Count.ShouldBe(3);
        var second = snippet.Placeholders[1];
        second.Index.ShouldBe(2);

        var enclosing = snippet.EnclosingPlaceholders(second);
        enclosing.Count.ShouldBe(1);
        enclosing[0].Index.ShouldBe(1);

        var nested = new SnippetParser().Parse("ddd$1eee$0", true);
        snippet.Replace(second, nested.Children);

        snippet.ToString().ShouldBe("aaabbbdddeee");
        snippet.Placeholders.Count.ShouldBe(4);
        snippet.Placeholders[0].Index.ShouldBe(1);
        snippet.Placeholders[1].Index.ShouldBe(1);
        snippet.Placeholders[2].Index.ShouldBe(0);
        snippet.Placeholders[3].Index.ShouldBe(0);

        var newEnclosing = snippet.EnclosingPlaceholders(snippet.Placeholders[1]);
        newEnclosing[0].ShouldBeSameAs(snippet.Placeholders[0]);
        newEnclosing.Count.ShouldBe(1);
        newEnclosing[0].Index.ShouldBe(1);
    }

    [Fact]
    public void TextmateSnippet_replace_2_of_2()
    {
        var snippet = new SnippetParser().Parse("aaa${1:bbb${2:ccc}}$0", true);

        snippet.Placeholders.Count.ShouldBe(3);
        var second = snippet.Placeholders[1];
        second.Index.ShouldBe(2);

        var nested = new SnippetParser().Parse("dddeee$0", true);
        snippet.Replace(second, nested.Children);

        snippet.ToString().ShouldBe("aaabbbdddeee");
        snippet.Placeholders.Count.ShouldBe(3);
    }

    [Fact]
    public void Snippet_order_for_placeholders_28185()
    {
        Placeholder.CompareByIndex(new Placeholder(10), new Placeholder(2)).ShouldBe(1);
    }

    [Fact]
    public void Maximum_call_stack_size_exceeded_28983()
    {
        new SnippetParser().Parse("${1:${foo:${1}}}");
    }

    [Fact]
    public void Snippet_can_freeze_the_editor_30407()
    {
        var seen = new HashSet<Marker>(ReferenceEqualityComparer.Instance);

        new SnippetParser().Parse("class ${1:${TM_FILENAME/(?:\\A|_)([A-Za-z0-9]+)(?:\\.rb)?/(?2::\\u$1)/g}} < ${2:Application}Controller\n  $3\nend").Walk(marker =>
        {
            seen.Add(marker).ShouldBeTrue();
            return true;
        });

        seen.Clear();
        new SnippetParser().Parse("${1:${FOO:abc$1def}}").Walk(marker =>
        {
            seen.Add(marker).ShouldBeTrue();
            return true;
        });
    }

    [Fact]
    public void Snippets_make_parser_ignore_0_choice_31599()
    {
        AssertTextAndMarker("${0|foo,bar|}", "${0|foo,bar|}", typeof(Text));
        AssertTextAndMarker("${1|foo,bar|}", "foo", typeof(Placeholder));
    }

    [Fact]
    public void Transform_FormatString_resolve()
    {
        // shorthand functions
        new FormatString(1, "upcase").Resolve("foo").ShouldBe("FOO");
        new FormatString(1, "downcase").Resolve("FOO").ShouldBe("foo");
        new FormatString(1, "capitalize").Resolve("bar").ShouldBe("Bar");
        new FormatString(1, "capitalize").Resolve("bar no repeat").ShouldBe("Bar no repeat");
        new FormatString(1, "pascalcase").Resolve("bar-foo").ShouldBe("BarFoo");
        new FormatString(1, "pascalcase").Resolve("bar-42-foo").ShouldBe("Bar42Foo");
        new FormatString(1, "pascalcase").Resolve("snake_AndPascalCase").ShouldBe("SnakeAndPascalCase");
        new FormatString(1, "pascalcase").Resolve("kebab-AndPascalCase").ShouldBe("KebabAndPascalCase");
        new FormatString(1, "pascalcase").Resolve("_justPascalCase").ShouldBe("JustPascalCase");
        new FormatString(1, "camelcase").Resolve("bar-foo").ShouldBe("barFoo");
        new FormatString(1, "camelcase").Resolve("bar-42-foo").ShouldBe("bar42Foo");
        new FormatString(1, "camelcase").Resolve("snake_AndCamelCase").ShouldBe("snakeAndCamelCase");
        new FormatString(1, "camelcase").Resolve("kebab-AndCamelCase").ShouldBe("kebabAndCamelCase");
        new FormatString(1, "camelcase").Resolve("_JustCamelCase").ShouldBe("justCamelCase");
        new FormatString(1, "kebabcase").Resolve("barFoo").ShouldBe("bar-foo");
        new FormatString(1, "kebabcase").Resolve("BarFoo").ShouldBe("bar-foo");
        new FormatString(1, "kebabcase").Resolve("ABarFoo").ShouldBe("a-bar-foo");
        new FormatString(1, "kebabcase").Resolve("bar42Foo").ShouldBe("bar42-foo");
        new FormatString(1, "kebabcase").Resolve("snake_AndPascalCase").ShouldBe("snake-and-pascal-case");
        new FormatString(1, "kebabcase").Resolve("kebab-AndCamelCase").ShouldBe("kebab-and-camel-case");
        new FormatString(1, "kebabcase").Resolve("_justPascalCase").ShouldBe("just-pascal-case");
        new FormatString(1, "kebabcase").Resolve("__UPCASE__").ShouldBe("upcase");
        new FormatString(1, "kebabcase").Resolve("__BAR_FOO__").ShouldBe("bar-foo");
        new FormatString(1, "snakecase").Resolve("bar-foo").ShouldBe("bar_foo");
        new FormatString(1, "snakecase").Resolve("bar-42-foo").ShouldBe("bar_42_foo");
        new FormatString(1, "snakecase").Resolve("snake_AndPascalCase").ShouldBe("snake_and_pascal_case");
        new FormatString(1, "snakecase").Resolve("kebab-AndPascalCase").ShouldBe("kebab_and_pascal_case");
        new FormatString(1, "snakecase").Resolve("_justPascalCase").ShouldBe("_just_pascal_case");
        new FormatString(1, "notKnown").Resolve("input").ShouldBe("input");

        // if
        new FormatString(1, null, "foo", null).Resolve(null).ShouldBe("");
        new FormatString(1, null, "foo", null).Resolve("").ShouldBe("");
        new FormatString(1, null, "foo", null).Resolve("bar").ShouldBe("foo");

        // else
        new FormatString(1, null, null, "foo").Resolve(null).ShouldBe("foo");
        new FormatString(1, null, null, "foo").Resolve("").ShouldBe("foo");
        new FormatString(1, null, null, "foo").Resolve("bar").ShouldBe("bar");

        // if-else
        new FormatString(1, null, "bar", "foo").Resolve(null).ShouldBe("foo");
        new FormatString(1, null, "bar", "foo").Resolve("").ShouldBe("foo");
        new FormatString(1, null, "bar", "foo").Resolve("baz").ShouldBe("bar");
    }

    private sealed class DictionaryResolver(Dictionary<string, string> values) : IVariableResolver
    {
        public string? Resolve(Variable variable) => values.GetValueOrDefault(variable.Name);
    }

    [Fact]
    public void Unicode_variable_transformations()
    {
        var resolver = new DictionaryResolver(new Dictionary<string, string>
        {
            ["RUSSIAN"] = "одинДва",
            ["GREEK"] = "έναςΔύο",
            ["TURKISH"] = "istanbulLı",
            ["JAPANESE"] = "こんにちは",
        });

        void AssertTransform(string transformName, string varName, string expected)
        {
            var snippet = new SnippetParser().Parse("${" + varName + "/(.*)/${1:/" + transformName + "}/}");
            var variable = (Variable)snippet.Children[0];
            variable.Resolve(resolver);
            variable.ToString().ShouldBe(expected, $"{transformName} failed for {varName}");
        }

        AssertTransform("kebabcase", "RUSSIAN", "один-два");
        AssertTransform("kebabcase", "GREEK", "ένας-δύο");
        AssertTransform("snakecase", "RUSSIAN", "один_два");
        AssertTransform("snakecase", "GREEK", "ένας_δύο");
        AssertTransform("camelcase", "RUSSIAN", "одинДва");
        AssertTransform("camelcase", "GREEK", "έναςΔύο");
        AssertTransform("pascalcase", "RUSSIAN", "ОдинДва");
        AssertTransform("pascalcase", "GREEK", "ΈναςΔύο");
        AssertTransform("upcase", "RUSSIAN", "ОДИНДВА");
        AssertTransform("downcase", "RUSSIAN", "одиндва");
        AssertTransform("kebabcase", "TURKISH", "istanbul-lı");
        AssertTransform("pascalcase", "TURKISH", "IstanbulLı");
        AssertTransform("upcase", "JAPANESE", "こんにちは");
        AssertTransform("kebabcase", "JAPANESE", "こんにちは");
    }

    [Fact]
    public void Snippet_variable_transformation_with_complicated_regex_and_dollar_dollar_55627()
    {
        var snippet = new SnippetParser().Parse("const fileName = \"${TM_FILENAME/(.*)\\..+$/$1/}\"");
        snippet.ToTextmateString().ShouldBe("const fileName = \"${TM_FILENAME/(.*)\\..+$/${1}/}\"");
    }

    [Fact]
    public void HTML_attribute_suggestions_snippet_session_has_end_position_33147()
    {
        var placeholders = new SnippetParser().Parse("src=\"$1\"", true).Placeholders;

        placeholders.Count.ShouldBe(2);
        placeholders[0].Index.ShouldBe(1);
        placeholders[1].Index.ShouldBe(0);
    }

    [Fact]
    public void Snippet_optional_transforms_reusing_the_same_variable_37702()
    {
        var transform = new Transform();
        transform.AppendChild(new FormatString(1, "upcase"));
        transform.AppendChild(new FormatString(2, "upcase"));
        transform.Regexp = new Platform.JsRegExp("^(.)|-(.)", "g");

        transform.Resolve("my-file-name").ShouldBe("MyFileName");

        var clone = (Transform)transform.Clone();
        clone.Resolve("my-file-name").ShouldBe("MyFileName");
    }

    [Fact]
    public void Problem_with_snippets_regex_40570()
    {
        var snippet = new SnippetParser().Parse("${TM_DIRECTORY/.*src[\\/](.*)/$1/}");
        AssertMarker(snippet, typeof(Variable));
    }

    [Fact]
    public void Variable_transformation_with_undefined_variables_in_the_same_snippet_51769()
    {
        var transform = new Transform();
        transform.AppendChild(new Text("bar"));
        transform.Regexp = new Platform.JsRegExp("foo", "gi");
        transform.ToTextmateString().ShouldBe("/foo/bar/ig");
    }

    [Fact]
    public void Transform_serialization_joins_children_without_comma()
    {
        var transformWithFormatString = new Transform();
        transformWithFormatString.AppendChild(new FormatString(1, "upcase"));
        transformWithFormatString.AppendChild(new Text("_"));
        transformWithFormatString.Regexp = new Platform.JsRegExp("foo", "g");
        var serialized = transformWithFormatString.ToTextmateString();
        serialized.ShouldBe("/foo/${1:/upcase}_/g");

        var snippet = new SnippetParser().Parse("${TM_FILENAME" + serialized + "}");
        snippet.ToTextmateString().ShouldBe("${TM_FILENAME" + serialized + "}");
    }

    [Fact]
    public void Snippet_parser_freeze_53144()
    {
        var snippet = new SnippetParser().Parse("${1/(void$)|(.+)/${1:?-\treturn nil;}/}");
        AssertMarker(snippet, typeof(Placeholder));
    }

    [Fact]
    public void Snippets_variable_not_resolved_in_JSON_proposal_52931()
    {
        AssertTextAndMarker("FOO${1:/bin/bash}", "FOO/bin/bash", typeof(Text), typeof(Placeholder));
    }

    [Fact]
    public void Mirroring_sequence_of_nested_placeholders_on_backjumping_58736()
    {
        var snippet = new SnippetParser().Parse("${3:nest1 ${1:nest2 ${2:nest3}}} $3");
        snippet.Children.Count.ShouldBe(3);
        snippet.Children[0].ShouldBeOfType<Placeholder>();
        snippet.Children[1].ShouldBeOfType<Text>();
        snippet.Children[2].ShouldBeOfType<Placeholder>();

        void AssertParent(Marker marker)
        {
            foreach (var child in marker.Children)
            {
                AssertParent(child);
            }

            if (marker is not Placeholder)
            {
                return;
            }

            var found = false;
            Marker? m = marker;
            while (m is not null && !found)
            {
                if (ReferenceEquals(m.Parent, snippet))
                {
                    found = true;
                }

                m = m.Parent;
            }

            found.ShouldBeTrue();
        }

        AssertParent(snippet.Children[2]);
    }

    [Fact]
    public void Backspace_cant_be_escaped_in_snippet_variable_transforms_65412()
    {
        var snippet = new SnippetParser().Parse("namespace ${TM_DIRECTORY/[\\/]/\\\\/g};");
        AssertMarker(snippet, typeof(Text), typeof(Variable), typeof(Text));
    }

    [Fact]
    public void Snippet_cannot_escape_closing_bracket_inside_conditional_insertion_78883()
    {
        var snippet = new SnippetParser().Parse("${TM_DIRECTORY/(.+)/${1:+import { hello \\} from world}/}");
        snippet.Children.Count.ShouldBe(1);
        var variable = snippet.Children[0].ShouldBeOfType<Variable>();
        var transform = variable.Transform.ShouldNotBeNull();
        transform.Children.Count.ShouldBe(1);
        var format = transform.Children[0].ShouldBeOfType<FormatString>();
        format.IfValue.ShouldBe("import { hello } from world");
        format.ElseValue.ShouldBeNull();
    }

    [Fact]
    public void Snippet_escape_backslashes_inside_conditional_insertion_80394()
    {
        var snippet = new SnippetParser().Parse("${CURRENT_YEAR/(.+)/${1:+\\\\}/}");
        snippet.Children.Count.ShouldBe(1);
        var variable = snippet.Children[0].ShouldBeOfType<Variable>();
        var transform = variable.Transform.ShouldNotBeNull();
        transform.Children.Count.ShouldBe(1);
        var format = transform.Children[0].ShouldBeOfType<FormatString>();
        format.IfValue.ShouldBe("\\");
        format.ElseValue.ShouldBeNull();
    }

    [Fact]
    public void Snippet_placeholder_empty_right_after_expansion_152553()
    {
        new SnippetParser().Parse("${1:prog}: ${2:$1.cc} - $2").ToString().ShouldBe("prog: prog.cc - prog.cc");
        new SnippetParser().Parse("${1:prog}: ${3:${2:$1.cc}.33} - $2 $3").ToString().ShouldBe("prog: prog.cc.33 - prog.cc prog.cc.33");

        // cyclic references of placeholders
        new SnippetParser().Parse("${1:$2.one} <> ${2:$1.two}").ToString().ShouldBe(".two.one.two.one <> .one.two.one.two");
    }

    [Fact]
    public void Snippet_choices_are_incorrectly_escaped_applied_180132()
    {
        AssertTextAndMarker("${1|aaa$aaa|}bbb\\$bbb", "aaa$aaabbb$bbb", typeof(Placeholder), typeof(Text));
    }
}
