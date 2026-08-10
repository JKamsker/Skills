# Andrew Lock — "Creating a Source Generator" (Series Index)

> Canonical: [https://andrewlock.net/series/creating-a-source-generator/](https://andrewlock.net/series/creating-a-source-generator/)
> Andrew Lock, .NET Escapades
> Original article (c) Andrew Lock, all rights reserved. This file is an original summary, not a copy.

## Why read this

This is the hub page for the most complete public walkthrough of building a production
`IIncrementalGenerator`: the pipeline API, snapshot and integration testing, NuGet packaging,
marker-attribute distribution, cacheability, MSBuild configuration, and multi-SDK support.
Send a reader here when they don't yet know *which* problem they have — it routes them to the one
part that answers their question instead of making them read fifteen posts. If the question is
already sharp ("why does my pipeline re-run on every keystroke?"), skip the index and link the
specific part below.

## Key points

The series builds one generator incrementally across fifteen parts, so the parts are cumulative
but individually readable. Concrete routing:

| If you need to… | Go to | Link |
|---|---|---|
| Write your first `IIncrementalGenerator` | Part 1 — Creating an incremental generator | [link](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/) |
| Snapshot-test generated output | Part 2 — Testing with snapshot testing | [link](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/) |
| Ship it as a NuGet package / test it end-to-end | Part 3 — Integration testing and packaging | [link](https://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/) |
| Let users configure generation via an attribute | Part 4 — Marker attributes | [link](https://andrewlock.net/creating-a-source-generator-part-4-customising-generated-code-with-marker-attributes/) |
| Emit into the right namespace / nested type chain | Part 5 — Namespace and type hierarchy | [link](https://andrewlock.net/creating-a-source-generator-part-5-finding-a-type-declarations-namespace-and-type-hierarchy/) |
| Commit generated `.g.cs` files to source control | Part 6 — Saving output in source control | [link](https://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/) |
| Understand why shipping the marker attribute is hard | Part 7 — Marker attribute problem (1) | [link](https://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/) |
| Ship generator + attributes in one package | Part 8 — Marker attribute problem (2) | [link](https://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/) |
| Fix a generator that kills IDE performance | Part 9 — Performance pitfalls | [link](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/) |
| Prove your pipeline actually caches | Part 10 — Testing cacheable outputs | [link](https://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/) |
| Replace a call site instead of adding code | Part 11 — Interceptors | [link](https://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/) |
| Branch on `LanguageVersion` / nullable settings | Part 12 — Compilation options and C# version | [link](https://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/) |
| Read an MSBuild property from the generator | Part 13 — MSBuild properties and user config | [link](https://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/) |
| Support several Roslyn/SDK versions at once | Part 14 — Multiple SDK versions | [link](https://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/) |
| Use the .NET 10 fix for marker attributes | Part 15 — `[Embedded]` attributes in .NET 10 | [link](https://andrewlock.net/exploring-dotnet-10-preview-features-4-solving-the-source-generator-marker-attribute-problem-in-dotnet-10/) |

Shared API surface the series keeps returning to, worth knowing before you pick a part:

- **Pipeline entry points** on `IncrementalGeneratorInitializationContext`:
  `SyntaxProvider.ForAttributeWithMetadataName(...)` (preferred for attribute-driven generators;
  needs `Microsoft.CodeAnalysis.CSharp` 4.4.0+), `SyntaxProvider.CreateSyntaxProvider(predicate, transform)`,
  `CompilationProvider`, `ParseOptionsProvider`, `AdditionalTextsProvider`, `AnalyzerConfigOptionsProvider`.
- **Pipeline operators**: `Select`, `SelectMany`, `Where`, `Collect`, `Combine`, `WithComparer`, `WithTrackingName`.
- **Outputs**: `RegisterPostInitializationOutput` (marker attributes, embedded sources),
  `RegisterSourceOutput`, `RegisterImplementationSourceOutput`.
- **MSBuild bridge** (Part 13): declare `<CompilerVisibleProperty Include="MyProp" />` or
  `<CompilerVisibleItemMetadata …/>` in a `build/*.props` shipped in the package; read it via
  `AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.MyProp", out var v)`
  (item metadata uses the `build_metadata.` prefix). Values land in the generated
  `*.GeneratedMSBuildEditorConfig.editorconfig` under `obj/`.
- **Packaging** (Parts 3, 8, 14): generator DLL goes to `analyzers/dotnet/cs`, with
  `<IncludeBuildOutput>false</IncludeBuildOutput>`; the attributes assembly is packed to both
  `analyzers/dotnet/cs` and `lib/netstandard2.0`. Reference the generator project with
  `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`. For multi-Roslyn, use versioned
  paths such as `analyzers/dotnet/roslyn4.4/cs` and `analyzers/dotnet/roslyn4.11/cs`, built from
  separate projects pinned to `Microsoft.CodeAnalysis.CSharp` 4.4.0 and 4.11.0, gated with
  `#if` symbols for version-only APIs (interceptors need 4.11).
- **Seeing the output** (Part 6): `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>`
  plus `<CompilerGeneratedFilesOutputPath>`; exclude the emitted tree from compilation with
  `<Compile Remove="$(CompilerGeneratedFilesOutputPath)/**/*.cs" />`.
- **Cacheability testing** (Part 10): `WithTrackingName` on each stage, construct the driver with
  `GeneratorDriverOptions(trackIncrementalGeneratorSteps: true)`, run twice against a cloned
  compilation, then inspect `GeneratorDriverRunResult.Results[..].TrackedSteps` and assert every
  `IncrementalStepRunReason` is `Cached` or `Unchanged`.
- **.NET 10 marker attributes** (Part 15): `context.RegisterPostInitializationOutput(ctx => ctx.AddEmbeddedAttributeDefinition())`
  and apply `[global::Microsoft.CodeAnalysis.EmbeddedAttribute]` to the generated marker type.
  Requires Roslyn 4.14+ and SDK 9.0.300+; the attribute stays invisible outside its own compilation,
  so it cannot be part of a public API surface other projects must reference.

## Pitfalls

- Never let `ISymbol`, `SyntaxNode`, or `Compilation` survive into a pipeline stage's output —
  they get new instances on every edit, so nothing caches. Project them into a small data model first.
- Data models must have value equality: `record`, `record struct`, or hand-written `IEquatable<T>`.
  `ImmutableArray<T>` does *not* compare structurally, so wrap it (an `EquatableArray<T>` type).
- `Combine`-ing directly with `CompilationProvider` destroys incrementality; `Select` the one value
  you actually need (assembly name, language version) before combining.
- `Diagnostic` objects capture `Location`/`ISymbol`; carry cacheable `DiagnosticInfo`/`LocationInfo`
  records through the pipeline and build the real `Diagnostic` only at emit time.
- Reflection inside a generator reflects the *compiler host's* runtime, not the target project's.
- Emitting the marker attribute as post-init source breaks down once consumers use
  `[InternalsVisibleTo]` — you get duplicate/ambiguous type errors (CS0436). Parts 7, 8, and 15
  exist because of this; don't design around the naive version.
- `RegisterImplementationSourceOutput` only fits code that cannot change semantics; using it for
  ordinary generated types will produce IDE-visible breakage.

## In his words

> "In this series I show how to create an incremental source generator, using the APIs introduced in .NET 6."

— Andrew Lock, [.NET Escapades](https://andrewlock.net/series/creating-a-source-generator/)

On switching from `CreateSyntaxProvider` to `ForAttributeWithMetadataName`, reporting a figure he
attributes to others rather than to his own measurements:

> "I've seen various quotes that this can remove 99% of the number of nodes your code ends up evaluating."

— Andrew Lock, [.NET Escapades](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/)

## Read the full article

[Creating a source generator — series index, andrewlock.net](https://andrewlock.net/series/creating-a-source-generator/)
