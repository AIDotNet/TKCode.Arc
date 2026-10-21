using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class CodeBlockLanguagesDemo : UserControl
{
    private const string Csharp = """
        using Avalonia.Controls;

        namespace Shop;

        public sealed class CartView : UserControl
        {
            // Totals update when an item changes.
            public decimal Total { get; private set; }

            public void Add(decimal price, int quantity = 1)
            {
                Total += price * quantity;
                Status.Text = $"Total: {Total:C}";
            }
        }
        """;

    private const string Tsx = """
        import { useState } from "react";

        /** Says hello, then counts the clicks. */
        export function Greeting({ name }: { name: string }) {
          const [count, setCount] = useState(0);
          return <button onClick={() => setCount(count + 1)}>Hello {name}, {count} clicks</button>;
        }
        """;

    private const string Python = """"
        from dataclasses import dataclass

        @dataclass
        class Stats:
            count: int = 0

            def add(self, value: float) -> "Stats":
                """Counts one more sample."""
                self.count += 1  # no averaging yet
                return self
        """";

    private const string Rust = """
        use std::collections::HashMap;

        /// Counts words.
        fn count(text: &str) -> HashMap<&str, usize> {
            let mut words = HashMap::new();
            for word in text.split_whitespace() {
                *words.entry(word).or_insert(0) += 1;
            }
            words
        }
        """;

    private const string Go = """
        package main

        import "fmt"

        // Greet prints a greeting n times.
        func Greet(name string, n int) {
        	for i := 0; i < n; i++ {
        		fmt.Printf("Hello, %s!\n", name)
        	}
        }
        """;

    private const string Bash = """
        #!/usr/bin/env bash
        set -euo pipefail
        # Build, then publish the gallery.
        for rid in osx-arm64 win-x64; do
          dotnet publish -c Release -r "$rid" -o "out/$rid"
        done
        """;

    private const string Yaml = """
        name: build
        on:
          push:
            branches: [main]
        jobs:
          test:
            runs-on: ubuntu-latest
            steps:
              - run: dotnet test # all projects
        """;

    private const string Sql = """
        -- Customers with more than five orders
        SELECT c.name, COUNT(o.id) AS orders
        FROM customers c
        JOIN orders o ON o.customer_id = c.id
        GROUP BY c.name
        HAVING COUNT(o.id) > 5;
        """;

    private const string Json = """
        {
          "name": "arc-demo",
          "version": "1.4.0",
          "private": true,
          "scripts": { "dev": "next dev", "build": "next build" },
          "dependencies": { "motion": "^12.0.0", "lucide-react": "^0.460.0" }
        }
        """;

    private const string Axaml = """
        <arc:CodeBlock FileName="greeting.tsx"
                       Language="tsx"
                       IsCollapsible="True"
                       MaxLines="6"
                       Code="{Binding Source}" />
        """;

    private static readonly Dictionary<string, (string FileName, string Code)> s_samples = new()
    {
        ["cs"] = ("CartView.cs", Csharp),
        ["tsx"] = ("greeting.tsx", Tsx),
        ["py"] = ("stats.py", Python),
        ["rust"] = ("main.rs", Rust),
        ["go"] = ("greet.go", Go),
        ["bash"] = ("deploy.sh", Bash),
        ["yaml"] = ("ci.yml", Yaml),
        ["sql"] = ("orders.sql", Sql),
        ["json"] = ("package.json", Json),
        ["axaml"] = ("Usage.axaml", Axaml),
        ["text"] = ("notes.txt", "Plain text: languages without a grammar are not highlighted.")
    };

    public CodeBlockLanguagesDemo()
    {
        InitializeComponent();
        Viewer.Code = Csharp;
    }

    private void OnLanguageClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Content: string language } && s_samples.TryGetValue(language, out var sample))
        {
            Viewer.FileName = sample.FileName;
            Viewer.Language = language;
            Viewer.Code = sample.Code;
        }
    }
}
