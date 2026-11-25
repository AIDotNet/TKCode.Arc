// BenchmarkDotNet over the code editor engine (design/architecture/code-editor.md section 8, task 1.9).
//
// Run every benchmark:   dotnet run -c Release --project tools/editor/Benchmarks -- --filter '*'
// A quick pass:          add --job short
// One class:             --filter '*EditBenchmarks*'

using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
