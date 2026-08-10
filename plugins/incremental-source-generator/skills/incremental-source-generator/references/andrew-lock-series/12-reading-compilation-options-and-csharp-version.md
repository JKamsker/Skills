# Part 12 — Reading compilation options and the C# language version

> [Creating a source generator - Part 12: Reading compilation options and the C# version in source generators](https://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/)
> Andrew Lock, .NET Escapades
> Original article (c) Andrew Lock, all rights reserved. This file is an original summary, not a copy.

## Why read this

A generator that emits file-scoped namespaces, `required` members, or raw string literals will break any consumer project pinned to an older `LangVersion`. This post shows how a generator inspects the consuming compilation at build time — language version, platform, optimization level, assembly name — so it can emit syntax the consumer can actually compile. Send the reader here when a generator must degrade gracefully across C# versions, or when someone is about to hardcode modern syntax into emitted output.

## Key points

- The entry point is `IncrementalGeneratorInitializationContext.CompilationProvider`, an `IncrementalValueProvider<Compilation>` supplied by Roslyn during `Initialize`.
- `Compilation` itself is language-agnostic. To reach C#-specific data, cast to `CSharpCompilation` and read `CSharpCompilation.LanguageVersion`, which returns the `LanguageVersion` enum from `Microsoft.CodeAnalysis.CSharp`.
- Other useful facts hanging off the same value: `Compilation.AssemblyName`, and `Compilation.Options` (`CompilationOptions`) exposing `Platform` (AnyCpu / x86 / x64 / …) and `OptimizationLevel` (`Debug` or `Release`).
- Project the pieces you need out of the `Compilation` with `Select` into a small value (tuple or record) rather than pushing the whole `Compilation` down the pipeline. `Compilation` is not usefully equatable, so carrying it forward destroys downstream caching.
- Minimal shape of the pipeline stage (original illustration, not the article's code):

  ```csharp
  var langVersion = context.CompilationProvider.Select(static (compilation, _) =>
      compilation is CSharpCompilation csharp ? csharp.LanguageVersion : LanguageVersion.Default);
  ```

- `LanguageVersion` members run `CSharp1` through the newest version the referenced Roslyn knows about (`CSharp11` at the time of writing), plus the aliases `Default`, `Latest`, `LatestMajor`, and `Preview`.
- The enum's backing integers follow a predictable major/minor scheme — C# 11 is 1100, C# 13 is 1300 — so you can test for a version your Roslyn reference has no member for by casting: `version >= (LanguageVersion)1300`.
- Consumers control this with the `<LangVersion>` MSBuild property in their `.csproj`. The alias forms (`default`, `latest`, `latestMajor`, `preview`) resolve to a concrete version by the time your generator observes it, so you compare against real versions, not aliases.
- Generation then becomes a version switch: below a threshold, emit the conservative syntax; at or above it, emit the modern form. Same semantics, two spellings.
- The `Microsoft.CodeAnalysis.CSharp` package version you compile against is what defines the API and enum surface available to you, and it sets the floor for which SDKs and IDE versions can load your generator. Reference the oldest version you can tolerate rather than the newest available.

### Adjacent APIs the post does not cover

The article stays on `CompilationProvider` and the C# version. Two neighbours worth knowing when you need them:

- `IncrementalGeneratorInitializationContext.ParseOptionsProvider` yields `ParseOptions`; cast to `CSharpParseOptions` and read `LanguageVersion` there. It carries parse-level settings (language version, preprocessor symbols, document kind) without dragging in the full compilation.
- Nullable context lives on the compilation options: cast `Compilation.Options` to `CSharpCompilationOptions` and read `NullableContextOptions` (`Disable`, `Warnings`, `Annotations`, `Enable`), which reflects the consumer's `<Nullable>` MSBuild property. In practice, emitting an explicit `#nullable enable` (or `#nullable disable`) directive at the top of every generated file is more robust than branching on the consumer's setting, because the directive wins regardless of project configuration.
- `Microsoft.CodeAnalysis.CSharp.LanguageVersionFacts` provides helpers such as `MapSpecifiedToEffectiveVersion` and `ToDisplayString` if you need to resolve or render a version yourself.

## Pitfalls

- **Doing real work inside the `CompilationProvider` stage.** It produces a new value on every keystroke in the IDE. Symbol lookups, syntax walking, or anything expensive inside that `Select` runs constantly. Extract only the small facts you need.
- **Letting `Compilation` leak downstream.** Combining the raw `Compilation` into later stages means the cache comparison always fails and the whole tail of your pipeline re-executes on every edit.
- **Assuming the language version from the target framework.** A project can set `<LangVersion>` independently, up or down, so a `net8.0` project is not guaranteed to be on the matching C# version.
- **Comparing against `Latest`, `Preview`, or `Default`.** Compare against concrete versions; the aliases are inputs, not the values you branch on.
- **Requiring a `LanguageVersion` member that does not exist in your Roslyn reference.** Bumping `Microsoft.CodeAnalysis.CSharp` to get a newer enum member silently raises the minimum SDK your generator supports and can make it fail to load for existing users. Cast the integer instead.
- **Emitting modern syntax unconditionally.** File-scoped namespaces, `required`, raw string literals, and generic attributes each have a version floor; without a check they turn into compile errors in the consumer's project, not yours.

## In his words

> "returns a new value for every keypress in the IDE"

— Andrew Lock, [.NET Escapades](https://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/), on `CompilationProvider`

## Read the full article

[Creating a source generator - Part 12: Reading compilation options and the C# version in source generators](https://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/) — Andrew Lock, .NET Escapades.
