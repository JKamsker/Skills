# Part 1 — Creating an Incremental Source Generator

> Source: [Creating a source generator - Part 1: Creating an incremental source generator](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/)
> Andrew Lock, .NET Escapades
> Original article (c) Andrew Lock, all rights reserved. This file is an original summary, not a copy.

## Why read this

This is the end-to-end walkthrough of standing up a first `IIncrementalGenerator`: the analyzer project shape, emitting your own marker attribute, the predicate/transform split, and registering output. Send the user here when they are building a generator from scratch, when their generator compiles but emits nothing, or when they need to understand *why* the pipeline is deliberately split into a cheap stage and an expensive stage.

The notes below are organised by **symptom**, not by the article's section order, so this page works as a standalone diagnostic reference. The example used here — a `[GenerateBuilder]` marker that emits a fluent builder for a class — is invented for this file; the article's own worked example is different (see *Read the full article*).

## Key points

### Symptom: the generator never runs, nothing is emitted at all

Almost always a wiring problem, not a code problem. Three things must be true.

**1. The generator assembly targets `netstandard2.0`.** It is loaded into the *compiler process*, not into the consumer's runtime, so a modern TFM will simply fail to load. Only two properties really matter:

```xml
<PropertyGroup>
  <TargetFramework>netstandard2.0</TargetFramework>
  <IncludeBuildOutput>false</IncludeBuildOutput>
</PropertyGroup>
```

`IncludeBuildOutput=false` stops the generator DLL being packed as an ordinary `lib/` dependency. Everything else (`LangVersion`, `Nullable`, `ImplicitUsings`) is taste — set them so you can still write modern C# despite the old TFM.

**2. Roslyn is new enough.** `IIncrementalGenerator` was introduced in Roslyn 4.0.0 and needs .NET 6+ / VS 2022 or later. Reference `Microsoft.CodeAnalysis.CSharp` (and optionally `Microsoft.CodeAnalysis.Analyzers`) with `PrivateAssets="all"` so the compiler dependency does not flow to consumers. Pick the *lowest* Roslyn version you intend to support rather than the newest — the version you compile against is the minimum SDK your consumers need.

**3. The consumer references it as an analyzer, not as a library.** For an in-repo reference:

```xml
<ProjectReference Include="..\Builders.Generator\Builders.Generator.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

For NuGet, the DLL goes in the analyzer path, not `lib/`. The canonical packing snippet (`<None Include="$(OutputPath)\$(AssemblyName).dll" Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />`) is documented in Microsoft's [Roslyn source generators cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md) — use that as the reference, since it also covers packing private dependencies alongside the generator.

**Fast diagnosis:** set `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` and `<CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)\Generated</CompilerGeneratedFilesOutputPath>` in the *consumer* project and rebuild. If no folder appears for your generator, the generator is not loaded — go back to points 1–3. If the folder appears but is empty, the generator loads and your pipeline is filtering everything out.

### Symptom: consumers cannot compile because the marker attribute does not exist

Ship the attribute *from the generator itself* via post-initialization output, so consumers need no extra runtime package:

```csharp
private const string MarkerAttributeSource = """
    using System;

    namespace Contracts.Builders;

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal sealed class GenerateBuilderAttribute : Attribute { }
    """;

public void Initialize(IncrementalGeneratorInitializationContext context)
{
    context.RegisterPostInitializationOutput(
        static ctx => ctx.AddSource("Contracts.Builders.GenerateBuilderAttribute.g.cs", MarkerAttributeSource));
    // ... pipeline registration follows
}
```

`RegisterPostInitializationOutput` runs before the main pipeline, so the attribute is already part of the compilation when user code referencing it is analysed. Its context is deliberately narrow — it has no access to pipeline values, so this registration cannot depend on anything computed elsewhere.

### Symptom: nothing matches, or everything matches

`context.SyntaxProvider.CreateSyntaxProvider(predicate, transform)` takes two delegates with very different budgets. Getting the split wrong is the single most common cause of both "my generator ignores my class" and "my IDE crawls".

```csharp
IncrementalValuesProvider<BuilderTarget> targets = context.SyntaxProvider
    .CreateSyntaxProvider(
        predicate: static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
        transform: static (ctx, ct) => TryDescribeTarget(ctx, ct))
    .Where(static t => t is not null)
    .Select(static (t, _) => t!.Value);
```

- **predicate** — `Func<SyntaxNode, CancellationToken, bool>`. Syntax only; there is no semantic model here by design. A type test plus an attribute-list check is the right shape. It cannot tell you *which* attribute is present (the syntax only carries an identifier, which may be an alias or a short name), so it can only ever be a coarse first filter.
- **transform** — `Func<GeneratorSyntaxContext, CancellationToken, TModel?>`. This is where `ctx.SemanticModel` becomes available and where you decide whether the node really is a target.

Inside the transform, resolve the attribute properly rather than string-matching the syntax. `SemanticModel.GetSymbolInfo(attributeSyntax).Symbol` gives you the attribute *constructor* as an `IMethodSymbol`; compare `symbol.ContainingType.ToDisplayString()` against the **fully qualified** attribute name. Get the declared type with `SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol` and walk its `GetMembers()` for whatever the emitter needs.

The `.Where(...)` above looks like LINQ but is a method on `IncrementalValuesProvider<T>` — it is a real pipeline stage, and it is how non-matching nodes (transform returned `null`) get dropped. The `.Select` afterwards is optional; it just unwraps the nullable so downstream stages have a non-nullable model.

### Symptom: the IDE gets sluggish once your generator is installed

The pipeline reruns on essentially every keystroke, so cost compounds. Three rules:

1. **Keep the predicate trivial.** It is evaluated against enormous numbers of nodes. No semantic model, no allocations, no string formatting.
2. **Be frugal in the transform too.** It runs far less often, but "less often" still means "on every edit to a file containing a candidate". Andrew Lock's specific advice here is to prefer plain `foreach` loops over LINQ when walking symbols in this stage — see *In his words* below. This is guidance about the **transform**, not the predicate; the predicate should be too cheap for the question to arise.
3. **Project out of Roslyn types before the pipeline caches anything.** This is the one that actually decides whether incrementality works.

On point 3: the pipeline caches by comparing the output of each stage with the previous run using the type's equality. So your model must be a small, *structurally* equatable value:

```csharp
internal readonly record struct BuilderTarget(
    string Namespace,
    string TypeName,
    EquatableArray<PropertyInfo> Properties);
```

A `record` / `record struct` gives you structural equality for free — but only field-by-field using `EqualityComparer<T>.Default`. That means a raw `T[]`, `List<T>` or `ImmutableArray<T>` field compares by *reference*, so every rerun produces a "different" model and every downstream stage re-executes. Wrap collections in an equatable type (the usual solution is a small `EquatableArray<T>` struct implementing `IEquatable<T>` over the elements).

Equally important: never keep `ISymbol`, `SyntaxNode`, `Compilation` or `SemanticModel` in the model. They do not have value equality, and holding them roots entire compilations in memory across runs.

### Symptom: code is generated but lands in the wrong place, or `AddSource` throws

Output is registered as a terminal stage over the model provider:

```csharp
context.RegisterSourceOutput(targets, static (spc, target) =>
    spc.AddSource($"{target.Namespace}.{target.TypeName}.Builder.g.cs", RenderBuilder(target)));
```

- `SourceProductionContext` is also where you report diagnostics (`spc.ReportDiagnostic`) — do that here rather than from the transform.
- **Hint names must be unique within a generator.** Deriving them from the type name alone collides across namespaces; include the namespace (or a hash) as above. The `.g.cs` suffix is convention, not a requirement.
- `AddSource` has both a `string` overload and a `SourceText` overload. If you build output with a `StringBuilder`, use `SourceText.From(text, Encoding.UTF8)` — an explicitly encoded `SourceText` is what the compiler needs to emit correct debug info for the generated file.
- Generated code is *additive*. A generator can only add new compilation units; it cannot rewrite or remove existing user code. If your design requires editing a user's method body, it is not a source generator problem.

### The faster modern API

The .NET 7 SDK (Roslyn 4.4+) added `SyntaxProvider.ForAttributeWithMetadataName(fullyQualifiedMetadataName, predicate, transform)`, which collapses "find candidate nodes" and "check the attribute is really mine" into one heavily optimised step — Roslyn prefilters by attribute name internally instead of running your predicate over every node. Its transform receives a `GeneratorAttributeSyntaxContext` with `TargetNode`, `TargetSymbol` and `Attributes` already resolved, so most of the semantic plumbing above disappears.

It works fine with an attribute you emit yourself from `RegisterPostInitializationOutput`. Prefer it whenever your minimum supported Roslyn version allows; the article explicitly points forward to part 9 of the series for it.

## Pitfalls

- **Treating the predicate as a place to do work.** It runs constantly during typing. Semantic model access, LINQ allocations, or string formatting there is how you make an IDE feel broken.
- **Keeping Roslyn types in the pipeline model.** `ISymbol` / `SyntaxNode` have no value equality and root compilation objects. Project to a plain equatable value type in the transform.
- **A `record` model with an array or `List<T>` field.** Structural equality silently degenerates to reference equality for that field, so caching never hits and every stage re-runs. Use an equatable collection wrapper.
- **Matching the attribute by its short name.** Compare the fully qualified name from `ContainingType.ToDisplayString()`; otherwise a same-named attribute in another namespace matches, and an aliased `using` fails to match.
- **Referencing the generator as an ordinary library.** Without `OutputItemType="Analyzer"` / `ReferenceOutputAssembly="false"` (or the `analyzers/dotnet/cs` pack path), the compiler never loads it and nothing is generated — with no error.
- **A generator TFM other than `netstandard2.0`, or forgetting `IncludeBuildOutput=false`.** The first stops it loading into the compiler; the second leaks the generator as a runtime dependency.
- **Duplicate hint names.** `AddSource` rejects a hint name it has already seen in the same generator. Derive it from something genuinely unique.
- **Emitting code that assumes a shape the input does not guarantee.** Partial declarations, nested types, generic type parameters, the global namespace, and `file`-scoped types all break naive `namespace X { class Y }` emission. Handle them or diagnose them explicitly.

## In his words

> "Every change the user makes could trigger the source generator to run again, so you *have* to be efficient, otherwise you're going to kill the user's IDE experience"

— Andrew Lock, [Creating a source generator - Part 1](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/)

> "If you've designed your pipeline well, later layers will only be called when users are editing code that matters to you."

— Andrew Lock, [Creating a source generator - Part 1](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/)

On the transform stage specifically, where the semantic model is in play:

> "Note that we're still trying to be efficient where we can, so we're using `foreach` loops, rather than LINQ."

— Andrew Lock, [Creating a source generator - Part 1](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/)

## Read the full article

[Creating a source generator - Part 1: Creating an incremental source generator](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/) — Andrew Lock, .NET Escapades.

The article builds a different worked example from the one used above: a marker attribute on an `enum` that generates a fast `ToStringFast()` extension method, which the rest of the series then extends. Worth reading in full for that complete implementation, the benchmark numbers comparing it against `Enum.ToString()`, and the closing section on what source generators cannot do.
