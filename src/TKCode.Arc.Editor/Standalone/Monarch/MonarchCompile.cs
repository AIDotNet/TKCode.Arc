// Ported from vscode (src/vs/editor/standalone/common/monarch/monarchCompile.ts, commit b57d5c9e), MIT license.
//
// Differences:
// - The definition is a typed MonarchLanguage (MonarchTypes.cs), so most of upstream's type checks of the JSON are
//   done by the types; the checks that remain (an undefined state or attribute, a bad bracket) throw as upstream.
// - compileRegExp translates the JavaScript source with JsRegex.TranslateMonarch and builds a .NET Regex with
//   RegexOptions.ECMAScript (\d, \w, \b ASCII-only, as in JavaScript without u) and IgnoreCase for the i flag. The
//   compiled expressions of a definition are cached by source and flags in the lexer, so a pattern used by many rules
//   is built once.
// - The lexer matches a rule's ^(?:...) at the position in the line with Regex.Match(line, position, length), whose
//   input is that slice only: ^ anchors at the position and what is before it is invisible to \b and lookbehinds, as
//   upstream's match on line.substr(position), without copying the rest of the line.
// - Unmatched capture groups are empty strings in the matches array (upstream: undefined, which the lexer treats as
//   empty where it reads them).

using System.Text.RegularExpressions;

namespace TKCode.Arc.Editor.Standalone.Monarch;

/// <summary>Compiles a Monarch language definition into a checked lexer.</summary>
internal static class MonarchCompile
{
    private static readonly Regex s_attributeReference = new(@"@(\w+)", RegexOptions.CultureInvariant);
    private static readonly Regex s_stateReference = new(@"\$[sS](\d\d?)", RegexOptions.CultureInvariant);
    private static readonly Regex s_guardKey = new(@"^\$(([sS]?)(\d\d?)|#)(.*)$", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex s_guardOperator = new(@"^(@|!@|~|!~|==|!=)(.*)$", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex s_word = new(@"^\w*$", RegexOptions.CultureInvariant | RegexOptions.ECMAScript);
    private static readonly Regex s_words = new(@"^(\w|\|)*$", RegexOptions.CultureInvariant | RegexOptions.ECMAScript);
    private static readonly Regex s_nextSpecial = new(@"^(@pop|@push|@popall)$", RegexOptions.CultureInvariant);

    private static Func<string, bool> CreateKeywordMatcher(IReadOnlyList<string> arr, bool caseInsensitive)
    {
        if (caseInsensitive)
        {
            var lower = new HashSet<string>(arr.Select(static x => x.ToLowerInvariant()), StringComparer.Ordinal);
            return word => lower.Contains(word.ToLowerInvariant());
        }

        var hash = new HashSet<string>(arr, StringComparer.Ordinal);
        return hash.Contains;
    }

    // Lexer helpers

    /// <summary>
    /// Compiles a regular expression string, adding the 'i' flag if 'ignoreCase' is set, and the 'u' flag if 'unicode' is
    /// set. Also replaces <c>@\w+</c> sequences with the content of the specified attribute; <c>@@</c> stands for a
    /// literal <c>@</c>. With <paramref name="handleSn"/>, a pattern with <c>$Sn</c> becomes a function of the state.
    /// </summary>
    internal static (Regex? Regex, Func<string, Regex>? Dynamic) CompileRegExp(CompiledLexer lexer, string str, bool handleSn)
    {
        // @@ must be interpreted as a literal @, so we replace all occurences of @@ with a placeholder character
        str = str.Replace("@@", "\x01", StringComparison.Ordinal);

        var n = 0;
        bool hadExpansion;
        do
        {
            hadExpansion = false;
            var current = str;
            str = s_attributeReference.Replace(str, m =>
            {
                hadExpansion = true;
                var attr = m.Groups[1].Value;
                string sub;
                var value = lexer.Attributes[attr];
                if (lexer.GetStringAttribute(attr) is { } text)
                {
                    sub = text;
                }
                else if (value is { Kind: MonarchValueKind.RegExp })
                {
                    sub = value.Text!;
                }
                else if (value is null)
                {
                    throw MonarchCommon.CreateError(lexer, "language definition does not contain attribute '" + attr + "', used at: " + current);
                }
                else
                {
                    throw MonarchCommon.CreateError(lexer, "attribute reference '" + attr + "' must be a string, used at: " + current);
                }

                return MonarchCommon.Empty(sub) ? string.Empty : "(?:" + sub + ")";
            });
            n++;
        }
        while (hadExpansion && n < 5);

        // handle escaped @@
        str = str.Replace('\x01', '@');

        // handle $Sn
        if (handleSn && s_stateReference.IsMatch(str))
        {
            string? lastState = null;
            Regex? lastRegEx = null;
            var pattern = str;
            return (null, state =>
            {
                if (lastRegEx is not null && lastState == state)
                {
                    return lastRegEx;
                }

                lastState = state;
                lastRegEx = CreateRegex(lexer, MonarchCommon.SubstituteMatchesRe(lexer, pattern, state), cache: false);
                return lastRegEx;
            });
        }

        return (CreateRegex(lexer, str, cache: true), null);
    }

    private static Regex CreateRegex(CompiledLexer lexer, string source, bool cache)
    {
        if (cache && lexer.RegexCache.TryGetValue(source, out var cached))
        {
            return cached;
        }

        var options = RegexOptions.ECMAScript | RegexOptions.CultureInvariant;
        if (lexer.IgnoreCase)
        {
            options |= RegexOptions.IgnoreCase;
        }

        Regex regex;
        try
        {
            var translated = Model.JsRegex.TranslateMonarch(source, lexer.Unicode);
            regex = new Regex(translated, options, Model.JsRegex.MatchTimeout);
        }
        catch (ArgumentException e)
        {
            throw MonarchCommon.CreateError(lexer, "invalid regular expression /" + source + "/: " + e.Message);
        }

        if (cache)
        {
            lexer.RegexCache[source] = regex;
        }

        return regex;
    }

    /// <summary>Upstream's regex.test for a guard: the whole string matched by an anchored, non-sticky expression.</summary>
    private static bool Test(Regex regex, string s) => regex.IsMatch(s);

    // Compiles guard functions for case matches.

    private static string? SelectScrutinee(string id, string[] matches, string state, int num)
    {
        if (num < 0)
        {
            return id;
        }

        if (num < matches.Length)
        {
            return matches[num];
        }

        if (num >= 100)
        {
            num -= 100;
            var parts = MonarchCommon.SplitState(state);
            if (num < parts.Length)
            {
                return parts[num];
            }
        }

        return null;
    }

    private sealed record Branch(string Name, CompiledAction Value, Func<string, string[], string, bool, bool>? Test);

    private static Branch CreateGuard(CompiledLexer lexer, string ruleName, string tkey, CompiledAction val)
    {
        // get the scrutinee and pattern
        var scrut = -1; // -1: $!, 0-99: $n, 100+n: $Sn
        var oppat = tkey;
        var matches = s_guardKey.Match(tkey);
        if (matches.Success)
        {
            if (matches.Groups[3].Success && matches.Groups[3].Value.Length > 0)
            {
                // if digits
                scrut = int.Parse(matches.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                if (matches.Groups[2].Value.Length > 0)
                {
                    scrut += 100; // if [sS] present
                }
            }

            oppat = matches.Groups[4].Value;
        }

        // get operator
        var op = "~";
        var pat = oppat;
        if (string.IsNullOrEmpty(oppat))
        {
            op = "!=";
            pat = string.Empty;
        }
        else if (s_word.IsMatch(pat))
        {
            // just a word
            op = "==";
        }
        else
        {
            var opMatch = s_guardOperator.Match(oppat);
            if (opMatch.Success)
            {
                op = opMatch.Groups[1].Value;
                pat = opMatch.Groups[2].Value;
            }
        }

        // set the tester function
        Func<string, string, string[], string, bool, bool> tester;

        // special case a regexp that matches just words
        if ((op == "~" || op == "!~") && s_words.IsMatch(pat))
        {
            var inWords = CreateKeywordMatcher(pat.Split('|'), lexer.IgnoreCase);
            tester = (s, _, _, _, _) => op == "~" ? inWords(s) : !inWords(s);
        }
        else if (op == "@" || op == "!@")
        {
            var words = lexer.Attributes[pat];
            if (words is null)
            {
                throw MonarchCommon.CreateError(lexer, "the @ match target '" + pat + "' is not defined, in rule: " + ruleName);
            }

            if (words.Kind != MonarchValueKind.StringArray)
            {
                throw MonarchCommon.CreateError(lexer, "the @ match target '" + pat + "' must be an array of strings, in rule: " + ruleName);
            }

            var inWords = CreateKeywordMatcher(words.Words!, lexer.IgnoreCase);
            tester = (s, _, _, _, _) => op == "@" ? inWords(s) : !inWords(s);
        }
        else if (op == "~" || op == "!~")
        {
            if (pat.IndexOf('$') < 0)
            {
                // precompile regular expression
                var re = CompileRegExp(lexer, "^" + pat + "$", false).Regex!;
                tester = (s, _, _, _, _) => op == "~" ? Test(re, s) : !Test(re, s);
            }
            else
            {
                tester = (s, id, matchesArr, state, _) =>
                {
                    var re = CompileRegExp(lexer, "^" + MonarchCommon.SubstituteMatches(lexer, pat, id, matchesArr, state) + "$", false).Regex!;
                    return Test(re, s);
                };
            }
        }
        else
        {
            // if (op==='==' || op==='!=')
            var patx = MonarchCommon.FixCase(lexer, pat);
            if (pat.IndexOf('$') < 0)
            {
                tester = (s, _, _, _, _) => op == "==" ? s == patx : s != patx;
            }
            else
            {
                tester = (s, id, matchesArr, state, _) =>
                {
                    var patexp = MonarchCommon.SubstituteMatches(lexer, patx, id, matchesArr, state);
                    return op == "==" ? s == patexp : s != patexp;
                };
            }
        }

        // return the branch object
        if (scrut == -1)
        {
            return new Branch(tkey, val, (id, matchesArr, state, eos) => tester(id, id, matchesArr, state, eos));
        }

        return new Branch(tkey, val, (id, matchesArr, state, eos) =>
        {
            var scrutinee = SelectScrutinee(id, matchesArr, state, scrut);
            return tester(string.IsNullOrEmpty(scrutinee) ? string.Empty : scrutinee, id, matchesArr, state, eos);
        });
    }

    /// <summary>Compiles an action: i.e. optimize regular expressions and case matches and do many sanity checks.</summary>
    private static CompiledAction CompileAction(CompiledLexer lexer, string ruleName, MonarchAction? action)
    {
        if (action is null)
        {
            return new CompiledAction { Token = string.Empty };
        }

        if (action.IsString)
        {
            return CompiledAction.FromString(action.Text!); // { token: action }
        }

        if (action.Token is not null)
        {
            // only copy specific typed fields (only happens once during compile Lexer)
            var bracket = MonarchBracket.None;
            if (action.Bracket is { } bracketText)
            {
                bracket = bracketText switch
                {
                    "@open" => MonarchBracket.Open,
                    "@close" => MonarchBracket.Close,
                    _ => throw MonarchCommon.CreateError(lexer, "a 'bracket' attribute must be either '@open' or '@close', in rule: " + ruleName),
                };
            }

            string? next = null;
            if (!string.IsNullOrEmpty(action.Next))
            {
                next = action.Next;
                if (!s_nextSpecial.IsMatch(next))
                {
                    if (next[0] == '@')
                    {
                        next = next[1..]; // peel off starting @ sign
                    }

                    if (next.IndexOf('$') < 0)
                    {
                        // no dollar substitution, we can check if the state exists
                        if (!MonarchCommon.StateExists(lexer, MonarchCommon.SubstituteMatches(lexer, next, string.Empty, [], string.Empty)))
                        {
                            throw MonarchCommon.CreateError(lexer, "the next state '" + action.Next + "' is not defined in rule: " + ruleName);
                        }
                    }
                }
            }

            if (action.NextEmbedded is not null)
            {
                lexer.UsesEmbedded = true;
            }

            return new CompiledAction
            {
                Token = action.Token,
                TokenSubst = action.Token.IndexOf('$') >= 0,
                Bracket = bracket,
                Next = next,
                GoBack = action.GoBack,
                SwitchTo = action.SwitchTo,
                Log = action.Log,
                NextEmbedded = action.NextEmbedded,
            };
        }

        if (action.Array is { } array)
        {
            var results = new CompiledAction[array.Count];
            for (var i = 0; i < array.Count; i++)
            {
                results[i] = CompileAction(lexer, ruleName, array[i]);
            }

            return new CompiledAction { Group = results };
        }

        if (action.Cases is { } actionCases)
        {
            // build an array of test cases
            var cases = new List<Branch>();

            var hasEmbeddedEndInCases = false;
            // for each case, push a test function and result value
            foreach (var (tkey, value) in actionCases)
            {
                var val = CompileAction(lexer, ruleName, value);

                // what kind of case
                if (tkey is "@default" or "@" or "")
                {
                    cases.Add(new Branch(tkey, val, null));
                }
                else if (tkey == "@eos")
                {
                    cases.Add(new Branch(tkey, val, static (_, _, _, eos) => eos));
                }
                else
                {
                    cases.Add(CreateGuard(lexer, ruleName, tkey, val)); // call separate function to avoid local variable capture
                }

                if (!hasEmbeddedEndInCases)
                {
                    hasEmbeddedEndInCases = !val.IsString && (val.HasEmbeddedEndInCases || val.NextEmbedded is "@pop" or "@popall");
                }
            }

            // create a matching function
            var def = CompiledAction.FromString(lexer.DefaultToken);
            return new CompiledAction
            {
                HasEmbeddedEndInCases = hasEmbeddedEndInCases,
                Test = (id, matches, state, eos) =>
                {
                    foreach (var c in cases)
                    {
                        var didmatch = c.Test is null || c.Test(id, matches, state, eos);
                        if (didmatch)
                        {
                            return c.Value;
                        }
                    }

                    return def;
                },
            };
        }

        throw MonarchCommon.CreateError(lexer, "an action must be a string, an object with a 'token' or 'cases' attribute, or an array of actions; in rule: " + ruleName);
    }

    /// <summary>Monarch's Rule.setRegex.</summary>
    private static void SetRegex(CompiledLexer lexer, CompiledRule rule, string sregex)
    {
        rule.MatchOnlyAtLineStart = sregex.Length > 0 && sregex[0] == '^';
        rule.Name = rule.Name + ": " + sregex;
        var body = rule.MatchOnlyAtLineStart ? sregex[1..] : sregex;
        var (regex, dynamic) = CompileRegExp(lexer, "^(?:" + body + ")", true);
        rule.SetSearchRegexFactory(() => CompileRegExp(lexer, body, true));
        if (regex is not null)
        {
            rule.SetRegex(regex);
        }
        else
        {
            rule.SetDynamicRegex(dynamic!);
        }
    }

    /// <summary>
    /// Compiles a language definition: all regular expressions, case matches etc. are compiled and all include rules are
    /// expanded. Also compiles the bracket definitions, supplies defaults, and does many sanity checks.
    /// </summary>
    public static CompiledLexer Compile(string languageId, MonarchLanguage json, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(json);

        // Create our lexer
        var lexer = new CompiledLexer
        {
            LanguageId = languageId,
            IncludeLF = json.IncludeLF ?? false,
            NoThrow = false, // raise exceptions during compilation
            MaxStack = 100,
            Start = json.Start,
            IgnoreCase = json.IgnoreCase ?? false,
            Unicode = json.Unicode ?? false,
            TokenPostfix = json.TokenPostfix ?? "." + languageId,
            DefaultToken = json.DefaultToken ?? "source",
            UsesEmbedded = false, // becomes true if we find a nextEmbedded action
            StateNames = new HashSet<string>(json.Tokenizer.Select(static s => s.Key), StringComparer.Ordinal),
            Attributes = json.Attributes,
            Log = log,
        };

        // Compile an array of rules into newrules where RegExp objects are created.
        void AddRules(string state, List<CompiledRule> newrules, IReadOnlyList<MonarchLanguageRule> rules)
        {
            foreach (var rule in rules)
            {
                if (rule.Include is { } includeText)
                {
                    var include = includeText;
                    if (include.Length > 0 && include[0] == '@')
                    {
                        include = include[1..]; // peel off starting @
                    }

                    if (!json.Tokenizer.TryGetValue(include, out var included))
                    {
                        throw MonarchCommon.CreateError(lexer, "include target '" + include + "' is not defined at: " + state);
                    }

                    AddRules(state + "." + include, newrules, included);
                }
                else
                {
                    var newrule = new CompiledRule(state);

                    // Set up new rule attributes
                    if (!rule.IsExpanded)
                    {
                        SetRegex(lexer, newrule, rule.Regex!);
                        if (rule.IsShortWithNext)
                        {
                            if (rule.Action!.IsString)
                            {
                                newrule.Action = CompileAction(lexer, newrule.Name, new MonarchAction { Token = rule.Action.Text, Next = rule.Next });
                            }
                            else if (rule.Action.Array is null)
                            {
                                newrule.Action = CompileAction(lexer, newrule.Name, rule.Action.WithNext(rule.Next));
                            }
                            else
                            {
                                // an array is an object in JavaScript too: upstream sets `next` as a property of the array,
                                // and compileAction, which compiles the array as a group, never reads it
                                newrule.Action = CompileAction(lexer, newrule.Name, rule.Action);
                            }
                        }
                        else
                        {
                            newrule.Action = CompileAction(lexer, newrule.Name, rule.Action);
                        }
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(rule.Regex))
                        {
                            throw MonarchCommon.CreateError(lexer, "a rule must either be an array, or an object with a 'regex' or 'include' field at: " + state);
                        }

                        if (rule.Name is { } name)
                        {
                            newrule.Name = name;
                        }

                        SetRegex(lexer, newrule, rule.Regex);
                        newrule.Action = CompileAction(lexer, newrule.Name, rule.Action);
                    }

                    newrules.Add(newrule);
                }
            }
        }

        // compile the tokenizer rules
        if (json.Tokenizer.Count == 0)
        {
            throw MonarchCommon.CreateError(lexer, "a language definition must define the 'tokenizer' attribute as an object");
        }

        foreach (var (key, rules) in json.Tokenizer)
        {
            lexer.Start ??= key;
            var newrules = new List<CompiledRule>();
            AddRules("tokenizer." + key, newrules, rules);
            lexer.Tokenizer[key] = newrules;
        }

        // Set simple brackets
        IReadOnlyList<MonarchLanguageBracket> jsonBrackets = json.Brackets ??
        [
            new("{", "}", "delimiter.curly"),
            new("[", "]", "delimiter.square"),
            new("(", ")", "delimiter.parenthesis"),
            new("<", ">", "delimiter.angle"),
        ];

        var brackets = new List<CompiledBracket>();
        foreach (var desc in jsonBrackets)
        {
            if (desc.Open == desc.Close)
            {
                throw MonarchCommon.CreateError(lexer, "open and close brackets in a 'brackets' attribute must be different: " + desc.Open +
                    "\n hint: use the 'bracket' attribute if matching on equal brackets is required.");
            }

            brackets.Add(new CompiledBracket(
                desc.Token + lexer.TokenPostfix,
                MonarchCommon.FixCase(lexer, desc.Open),
                MonarchCommon.FixCase(lexer, desc.Close)));
        }

        lexer.Brackets = brackets;

        // Disable throw so the syntax highlighter goes, no matter what
        lexer.NoThrow = true;
        return lexer;
    }
}
