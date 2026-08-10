# Part 2 — Testing an incremental generator with snapshot testing

> Source: [Creating a source generator - Part 2: Testing an incremental generator with snapshot testing](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/)
> Andrew Lock, .NET Escapades
> Original article (c) Andrew Lock, all rights reserved. This file is an original summary, not a copy.

## Why read this

Generators produce text, and text is awkward to assert on line by line. This post shows how to run a generator inside a plain xUnit test — build a `Compilation` in memory, drive the generator over it, and lock the emitted output into a checked-in snapshot file. Send the user here when they have an `IIncrementalGenerator` with no test harness at all, when hand-written string assertions have become unmaintainable, or when a generator "works in the IDE" and they need a reproducible failing case.

## Key points

**Packages.** The test project pulls in `Microsoft.CodeAnalysis.CSharp` (Roslyn compilation APIs), `Microsoft.CodeAnalysis.Analyzers`, `Verify.XUnit`, and `Verify.SourceGenerators`. The last one is the piece that teaches Verify how to serialize Roslyn generator results instead of dumping an opaque object graph. The sample test project targets `net6.0` with `Nullable` and `ImplicitUsings` enabled.

**The test harness is four steps.** A single static helper (in the post, `TestHelper.Verify(string source)`) does the whole job:

1. `CSharpSyntaxTree.ParseText(source)` — turn the test's input C# into a syntax tree.
2. Build metadata references with `MetadataReference.CreateFromFile(...)` over a known assembly's `Location` (e.g. `typeof(object).Assembly.Location`) — a `CSharpCompilation` starts with **no** references at all.
3. `CSharpCompilation.Create(assemblyName, syntaxTrees, references)` — assemble the compilation the generator will see.
4. `CSharpGeneratorDriver.Create(generator)` to get a `GeneratorDriver`, then `driver = driver.RunGenerators(compilation)`.

That last reassignment matters: `GeneratorDriver` is immutable, so `RunGenerators` returns a *new* driver carrying the run results. Discarding the return value leaves you verifying a driver that never ran.

**Minimal shape of the helper:**

```csharp
GeneratorDriver driver = CSharpGeneratorDriver.Create(new MyGenerator());
driver = driver.RunGenerators(compilation);
return Verifier.Verify(driver).UseDirectory("Snapshots");
```

**Verify is handed the driver, not a string.** `Verifier.Verify(driver)` combined with `Verify.SourceGenerators` walks the run result and writes one snapshot file per generated source file, plus a serialized form of any diagnostics the generator reported through `SourceProductionContext.ReportDiagnostic` (id, severity, location, message). Multiple emitted files land as separately numbered `.verified` files rather than one giant blob, so a diff points at the file that actually changed.

**`Verify.SourceGenerators` must be switched on once per assembly** via a `[ModuleInitializer]` method that calls into `VerifySourceGenerators` (`Enable()` in current versions — check the exact name against the package version you installed). Without it, Verify has no converter for Roslyn types and the snapshot degenerates into an essentially empty object.

**`.UseDirectory("Snapshots")`** keeps `.verified` / `.received` files in a subfolder instead of scattering them beside the test class.

**Snapshot workflow.** The first run has no `.verified` file, so the test fails and Verify opens your diff tool with the `.received` content. You approve by promoting `.received` to `.verified` (the diff tool does it, or you move the file). The `.verified` files are the artifact you commit; every later run diffs against them.

**Adjacent APIs the post doesn't need but you will eventually:** `driver.GetRunResult()` for programmatic access to `GeneratedSources` and `Diagnostics`, and `RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics)` when you want to compile the generated code and assert it produces no errors.

## Pitfalls

- **Empty compilation references silently break the generator.** With no `MetadataReference` in the compilation, the semantic model can't bind attribute types, so `GetSymbolInfo` yields nothing, the generator's predicate/transform never matches, and the test snapshots an empty result. The symptom looks like a generator bug; the cause is the test's compilation.
- **Forgetting the module initializer** produces a snapshot with no useful content, which is easy to misread as "the generator emitted nothing".
- **Dropping the result of `RunGenerators`.** The driver is immutable; `driver.RunGenerators(compilation);` without assignment is a no-op from the verifier's point of view.
- **Approving a snapshot you didn't read.** Promoting `.received` to `.verified` is one keystroke in a diff tool, which makes it just as easy to bless a regression. Read the diff.
- **Reference set drives coverage.** Adding only the core BCL assembly is enough for a minimal case; test inputs that touch other libraries need those references added too, or the generator will see unresolved symbols.
- **Snapshots are exact-text comparisons.** Whitespace, trailing newlines, and any version or timestamp you emit into generated headers will churn the snapshot. Keep generated output deterministic, or scrub the volatile parts.

## In his words

> "Source generators seem like an almost perfect use-case for snapshot testing, given that there's normally a very specific, deterministic output that you want for a given input."

— Andrew Lock, [Creating a source generator - Part 2](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/)

## Read the full article

Full walkthrough, with the complete test project and the debugging story behind the empty first snapshot:
<https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/>
