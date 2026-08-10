---
name: incremental-source-generator
description: Design, review, debug, and fix Roslyn incremental source generators. Use when creating or modifying `IIncrementalGenerator` implementations, diagnosing incorrect or missing generated code, improving incrementality or IDE performance, adding marker attributes or analyzer-config/MSBuild inputs, inspecting emitted `.g.cs` output, or adding tests and packaging for source generators and analyzers.
license: MIT
compatibility: Requires the .NET SDK for the build and test commands
---

# Incremental Source Generator

## Overview

Use this skill to build or repair Roslyn incremental generators with an emphasis on correctness, cacheability, and IDE performance. Prefer the Roslyn design docs for API rules, then use the Andrew Lock series for concrete patterns, tests, packaging, and failure analysis.

## Workflow

1. Identify the active problem before editing.
   Separate design work, correctness bugs, performance regressions, packaging issues, and test gaps. Locate the generator entry points, post-initialization output, supporting models, emitted hint names, and the tests or consuming projects that prove the behavior.

2. Gather evidence from the current implementation.
   Read the generator and nearby data models before changing code. If output is wrong or missing, rebuild with generated-source emission enabled and inspect the emitted `.g.cs` files. If behavior depends on project configuration, inspect additional files, analyzer config, compiler-visible MSBuild properties, and any marker attributes added during post-initialization.

3. Read only the references that match the task.
   Start with the bundled Roslyn design docs:
   - `references/roslyn/incremental-generators.md`
   - `references/roslyn/incremental-generators.cookbook.md`

   For worked implementation guidance, use `references/andrew-lock-series.md`. It is an annotated
   index of Andrew Lock's "Creating a source generator" series: pick the row matching the task and
   fetch that URL. The articles are linked rather than bundled, so fetch only what the task needs:
   - Fundamentals and first implementation: parts 1, 4, 5, 6
   - Testing, packaging, and cacheability: parts 2, 3, 9, 10
   - Advanced marker, configuration, and versioning topics: parts 7, 8, 11, 12, 13, 14, 15

   If network access is unavailable, work from the bundled Roslyn docs and this skill's checklists;
   they cover the pipeline, equality, and packaging rules that cause most generator bugs.

4. Design or repair the pipeline with incrementality in mind.
   - Prefer `SyntaxProvider.ForAttributeWithMetadataName(...)` when an attribute can drive discovery.
   - Keep predicates purely syntactic and cheap. Push semantic work into later transforms.
   - Extract compact, equatable models early. Prefer `record` or `record struct`, strings, enums, and wrapper collections with value equality.
   - Remove `ISymbol`, `SyntaxNode`, and `Location` from long-lived models as soon as possible.
   - Combine the smallest derived values that solve the problem. Do not push full `Compilation` objects or other high-churn inputs farther downstream than necessary.
   - Pass cancellation tokens through Roslyn and file APIs, and check them inside expensive loops.
   - Generate source text with `StringBuilder` or another text writer. Do not build large `SyntaxNode` trees just to call `NormalizeWhitespace`.
   - Keep output additive. Use post-initialization output for marker attributes and related helper stubs.
   - Keep hint names stable and unique.
   - Report diagnostics when invalid user inputs need feedback instead of silently skipping the case.

5. Check common failure modes explicitly.
   - Discovery is too broad or uses `CreateSyntaxProvider` where an attribute-driven approach is available.
   - Models are not value-equatable, so cache hits are lost. Most common: ``ImmutableArray<T>``, ``List<T>`` or similar are **not** value-equatable and therefore do not cache. Use ``EquatableArray<T>`` or similar instead.
   - Arrays, lists, syntax nodes, locations, or symbols flow too far through the pipeline.
   - Marker attributes have visibility, duplication, or `EmbeddedAttribute` problems.
   - Generated code has namespace, containing-type, accessibility, `partial`, or `#nullable enable` issues.
   - Analyzer-config or MSBuild settings are not exposed with `CompilerVisibleProperty` or `CompilerVisibleItemMetadata`, or are read from the wrong scope.
   - Packaging omits analyzer assets or runtime dependencies from `analyzers/dotnet/cs`.

6. Verify the fix at the right level.
   - Add focused tests for semantics and generated output shape.
   - Use snapshot or golden-file tests when exact generated text matters.
   - Add integration or packaging tests when analyzer layout or consumer behavior matters.
   - Rebuild with generated-source emission after changes and inspect the output.
   - When incrementality is suspect, test that equivalent inputs produce equivalent models and cached outputs.

## Useful Commands

Use these commands when the repository does not already provide more specific wrappers:

PowerShell:

```powershell
dotnet build <project-or-solution> -t:Rebuild `
  /p:EmitCompilerGeneratedFiles=true `
  /p:CompilerGeneratedFilesOutputPath=artifacts/generated-src

dotnet test <project-or-solution>
```

bash / zsh:

```bash
dotnet build <project-or-solution> -t:Rebuild \
  /p:EmitCompilerGeneratedFiles=true \
  /p:CompilerGeneratedFilesOutputPath=artifacts/generated-src

dotnet test <project-or-solution>
```

Emitted files land under `artifacts/generated-src/<assembly>/<generator>/`. Use forward slashes in
the MSBuild property so the same command works on every platform.

## Notes

- Prefer primary Roslyn docs over blog guidance when they disagree.
- Treat Andrew Lock's series (linked from `references/andrew-lock-series.md`) as implementation guidance and worked examples, especially for testing, packaging, marker attributes, and performance pitfalls.
- Keep edits narrow. Generator bugs often come from a small number of pipeline, equality, or symbol-shape mistakes.
