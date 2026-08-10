# Part 9 — Avoiding Performance Pitfalls in Incremental Generators

> Source: [Creating a source generator, part 9: Avoiding performance pitfalls in incremental generators](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/)
> Andrew Lock, .NET Escapades
> Original article (c) Andrew Lock, all rights reserved. This file is an original summary, not a copy.

## Why read this

This is the single most important post in the series for anyone whose generator "works" but makes the IDE sluggish. "Performance" here means the cost your generator imposes on Visual Studio / Rider while someone types — not the speed of the code you emit. If a review or bug report involves re-running transforms on every keystroke, cache misses, non-equatable models, or a generator that regenerates identical output constantly, send the reader here.

## Key points

**Know which stage runs how often.** The pipeline is layered by cost. The syntax-provider predicate runs against effectively every syntax node on every edit. The transform runs for every node the predicate accepts, also on every edit. Everything downstream (`Select`, `Combine`, `Collect`, and the `RegisterSourceOutput` action) only re-runs when the value flowing into it *compares unequal* to the previously cached value. So the whole incrementality story reduces to one question: does `Equals` on my model return `true` when nothing meaningful changed?

**Prefer `ForAttributeWithMetadataName` over `CreateSyntaxProvider`.** For attribute-driven generators (the common case), `context.SyntaxProvider.ForAttributeWithMetadataName(fullyQualifiedMetadataName, predicate, transform)` lets Roslyn use its internal attribute index instead of walking every node. Andrew relays reported figures of around a 99% reduction in nodes evaluated, without claiming the measurement as his own. Requirements: `Microsoft.CodeAnalysis.CSharp` 4.4.0 or later, and building with the .NET 7+ SDK (it will not work on the .NET 6 SDK). The transform receives a `GeneratorAttributeSyntaxContext` rather than a `GeneratorSyntaxContext`.

**Keep the predicate syntax-only.** Do not touch the semantic model in the predicate; a cheap type test such as `node is EnumDeclarationSyntax` is the right shape. Selectivity and speed are in tension — the predicate must be both, because anything it lets through pays for a transform on every keypress.

**Never let Roslyn types past the transform.** `SyntaxNode`, `ISymbol` and friends must not appear in the data model that flows down the pipeline. `ISymbol` implementations do not give you structural equality, and syntax nodes are recreated per compilation so fresh instances compare unequal even when the source text is logically identical. Andrew notes syntax nodes are *sometimes* equatable, but treating them as unusable in the pipeline is the safe rule. The transform's job is to project everything you need into a plain, self-contained model and drop the Roslyn objects on the floor.

**Make the model value-equatable.** Use `readonly record struct` / `record` (or a hand-written type implementing `IEquatable<T>`), and make sure every nested member is value-equatable too. Records generate `Equals`/`GetHashCode` for you, so two models carrying identical field values compare equal and the pipeline correctly short-circuits. `IncrementalValuesProvider<T>.WithComparer()` can supply an `IEqualityComparer<T>` as an escape hatch, but building equality into the type is the more robust choice.

**Collections are the classic cache killer.** `T[]`, `List<T>` and — surprisingly — `ImmutableArray<T>` all fail structural equality, so a record that holds one will report "changed" on every run even when the contents match. The fix is a small `EquatableArray<T>` wrapper struct that implements `IEquatable<EquatableArray<T>>` with element-wise comparison and a content-based `GetHashCode`. Most published versions derive from the .NET Community Toolkit implementation. Because generators must target `netstandard2.0`, `System.HashCode` is unavailable, so these implementations usually ship a trimmed-down `HashCode` polyfill alongside.

Illustrative shape only (write your own):

```csharp
public readonly record struct EnumToGenerate(string Name, EquatableArray<string> Values);
```

**`CompilationProvider` poisons whatever it touches.** A new `Compilation` is produced on every keystroke and it is not equatable, so `Combine`-ing your carefully cached provider with `context.CompilationProvider` re-runs everything downstream every time — undoing all the work you did on the model. Instead `Select` the few facts you need out of the compilation (assembly name, a language-version flag, whether some type is available) into a small equatable value first, then combine with *that*. The same reasoning applies to `AnalyzerConfigOptionsProvider` and MSBuild-property inputs: project them to primitives before combining.

**Order the pipeline cheapest-first.** Root at `ForAttributeWithMetadataName`, project to an equatable model in the transform/`Select`, do expensive derivation after that projection, `Collect`/`Combine` only once the values are cacheable, and hand the final model to `RegisterSourceOutput`. Static marker attributes belong in `RegisterPostInitializationOutput`, which runs once early and never participates in the cached pipeline.

Illustrative ordering only (write your own):

```csharp
var models = context.SyntaxProvider
    .ForAttributeWithMetadataName(
        "MyLib.MyMarkerAttribute",
        predicate: static (node, _) => node is EnumDeclarationSyntax,
        transform: static (ctx, _) => ToEquatableModel(ctx)) // no ISymbol/SyntaxNode escapes here
    .Where(static m => m is not null);

var options = context.CompilationProvider
    .Select(static (c, _) => c.AssemblyName ?? string.Empty); // project, don't combine raw

context.RegisterSourceOutput(models.Combine(options), static (spc, pair) => Emit(spc, pair));
```

**Diagnostics need the same treatment.** A `Diagnostic` typically closes over `Location` and often `ISymbol`, neither of which is equatable, so emitting diagnostics naively drags uncacheable state through the pipeline. Andrew's pattern is to carry a `Result<T>`-style model pairing the value with an `EquatableArray<DiagnosticInfo>`, where `DiagnosticInfo` stores an equatable `LocationInfo` (file path, `TextSpan`, `LinePositionSpan`) that is rehydrated into a real `Location` only at output time. `DiagnosticDescriptor` itself is fine — it implements `IEquatable`.

**`RegisterImplementationSourceOutput` as an optional lever.** If your generated code is a pure implementation detail — nothing in user code binds against it semantically — registering via `RegisterImplementationSourceOutput` tells the host it may defer that output until a real build rather than running it for IDE analysis. Andrew flags this as promising rather than proven; the benefit depends on the host actually exploiting the distinction.

**No reflection.** Reflection inside a generator reflects over the compiler/IDE process, not the compilation being analysed, so it can find types that do not exist in the target application (and miss ones that do). Use the semantic model instead.

## Pitfalls

- Returning `ImmutableArray<T>` (or `List<T>`) from a transform and assuming the record's generated `Equals` handles it — it does not compare contents, and caching silently dies.
- Calling `Combine(context.CompilationProvider)` "just to get the assembly name", which re-runs the whole tail of the pipeline on every keystroke.
- Storing `INamedTypeSymbol`, `Location`, or a `SyntaxNode` on the model "temporarily" to use later during emit.
- Doing semantic-model work in the predicate instead of the transform.
- Reaching for `CreateSyntaxProvider` out of habit on an attribute-driven generator that could use `ForAttributeWithMetadataName`.
- Assuming `WithComparer()` rescues an otherwise reference-equality model everywhere — it only applies to the one provider you attach it to.
- Using reflection APIs, which inspect the wrong runtime entirely.

## In his words

> "The predicate must also be highly selective, as any node that passes will run the transform (on every keypress)."

— Andrew Lock, [.NET Escapades](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/)

> "only checks if each instance wraps the same underlying array, not whether the contents of the two different arrays are equal."

— Andrew Lock, on `ImmutableArray<T>` equality, [.NET Escapades](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/)

> "The reason this is almost certainly not what you want to do, is that using reflection APIs is doing reflection over the compiler/IDE host's runtime, not over the target application's runtime."

— Andrew Lock, [.NET Escapades](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/)

## Related

Part 10 of the same series covers testing that your pipeline outputs are actually cacheable — the natural follow-up once you have applied the rules above.

## Read the full article

[Creating a source generator, part 9: Avoiding performance pitfalls in incremental generators](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/) — Andrew Lock, .NET Escapades.
