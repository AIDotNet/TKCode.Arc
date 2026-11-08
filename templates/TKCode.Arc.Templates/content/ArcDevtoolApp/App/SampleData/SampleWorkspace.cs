using ArcStarterApp.Models;
using ArcStarterApp.Services;

namespace ArcStarterApp.SampleData;

/// <summary>An invented project in memory: changes are kept until the app closes.</summary>
public sealed class SampleWorkspace : IWorkspace
{
    private readonly Dictionary<string, string> _files = new()
    {
        ["README.md"] = """
            # Calculator

            A small expression parser. Open a file from the explorer, edit it, save with Cmd+S / Ctrl+S, and press Run
            to build. Open files come back the next time the app starts.
            """,
        ["src/Program.cs"] = """
            using Calculator;

            var parser = new Parser();
            Console.WriteLine(parser.Evaluate(args.Length > 0 ? args[0] : "1 + 2 * 3"));
            """,
        ["src/Parser.cs"] = """
            namespace Calculator;

            public sealed class Parser
            {
                public double Evaluate(string expression)
                {
                    var tokens = Tokenizer.Read(expression);
                    try
                    {
                        return Sum(tokens);
                    }
                    catch (FormatException e)
                    {
                        return double.NaN;
                    }
                }

                private static double Sum(Queue<Token> tokens) => 0; // TODO: parse terms
            }
            """,
        ["src/Tokenizer.cs"] = """
            namespace Calculator;

            public readonly record struct Token(string Text);

            public static class Tokenizer
            {
                public static Queue<Token> Read(string text) =>
                    new(text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => new Token(t)));
            }
            """,
        ["tests/ParserTests.cs"] = """
            namespace Calculator.Tests;

            public class ParserTests
            {
                [Fact]
                public void Adds() => Assert.Equal(3, new Parser().Evaluate("1 + 2"));
            }
            """,
        ["calculator.json"] = """
            {
              "name": "calculator",
              "version": "0.1.0"
            }
            """,
    };

    public SampleWorkspace()
    {
        Root = Tree(_files.Keys.Order(StringComparer.Ordinal).ToList(), "");
    }

    public string Name => "calculator";

    public IReadOnlyList<WorkspaceNode> Root { get; }

    public bool Exists(string path) => _files.ContainsKey(path);

    public string Read(string path) => _files.TryGetValue(path, out var text) ? text : string.Empty;

    public void Write(string path, string text) => _files[path] = text;

    // Folders first, then files, each sorted by name.
    private static IReadOnlyList<WorkspaceNode> Tree(List<string> paths, string prefix)
    {
        var folders = new List<WorkspaceNode>();
        var files = new List<WorkspaceNode>();
        foreach (var group in paths.GroupBy(p => p.Contains('/') ? p[..p.IndexOf('/')] : ""))
        {
            if (group.Key.Length == 0)
            {
                files.AddRange(group.Select(p => new WorkspaceNode(p, prefix + p)));
            }
            else
            {
                var inner = group.Select(p => p[(group.Key.Length + 1)..]).ToList();
                folders.Add(new WorkspaceNode(group.Key, prefix + group.Key, Tree(inner, prefix + group.Key + "/")));
            }
        }
        return [.. folders.OrderBy(n => n.Name, StringComparer.Ordinal), .. files.OrderBy(n => n.Name, StringComparer.Ordinal)];
    }
}
