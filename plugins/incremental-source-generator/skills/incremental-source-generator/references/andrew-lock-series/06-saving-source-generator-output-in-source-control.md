# Part 6 — Saving source generator output in source control

> Source: [Creating a source generator, part 6: Saving source generator output in source control](https://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/)
> Andrew Lock, .NET Escapades
> Original article (c) Andrew Lock, all rights reserved. This file is an original summary, not a copy.

## Why read this

Generated code is invisible by default: it lives only in the compiler's memory, so a pull request that changes a generator shows the generator diff but never the resulting `.g.cs` diff. This article shows how to make the compiler write generated files to disk, relocate them somewhere reviewable, commit them, and — critically — keep the build working afterwards. Send a reader here when they want generator output visible in code review, when they ask "where do the `.g.cs` files actually go?", or when they turned on emission and their build started failing with duplicate-member errors that appear out of nowhere on a *later* build.

## Key points

### The symptom: the second build fails, not the first

Once emitted files are written into the project directory, the compiler reports the generated members as duplicates:

```
error CS0111: Type 'OrderStatusExtensions' already defines a member called 'IsDefined' with the same parameter types
```

The confusing part is the timing. The SDK evaluates its implicit `**/*.cs` glob before compilation, so on a clean tree the emitted files do not exist yet, the build succeeds, and the files are written as a side effect. Every build after that sees them on disk and fails. The same trap fires immediately on CI once the files are committed, because a fresh clone already has them. "It built fine yesterday / it builds on my colleague's clean checkout" is the signature of this bug, not evidence against it.

### The cause: the files are compiled twice over

The emitted files are ordinary `.cs` files sitting under the project directory, so the SDK's default globbing hands them to the compiler as normal sources. Meanwhile the generator still runs and still produces the same members in memory — emission does not replace generation, it only records it. Every generated type therefore reaches the compilation twice.

### The two properties involved

- **`EmitCompilerGeneratedFiles`** (bool) is the switch. Set it in a `PropertyGroup` or pass it per build as `dotnet build /p:EmitCompilerGeneratedFiles=true`. Off by default; with it on, every generator's output for that project is written to disk during compilation.
- **`CompilerGeneratedFilesOutputPath`** (path, relative to the project directory) relocates that output. This is what makes the files reviewable — the default location is inside `obj`, which is gitignored in essentially every repo, so emission alone buys you nothing in a PR.

### Where the files actually land

By default the output goes under `$(IntermediateOutputPath)/generated`, i.e. the **configuration- and TFM-qualified** intermediate folder — `obj/Debug/net6.0/generated/…`, not bare `obj/`. (`$(BaseIntermediateOutputPath)` is just `obj\`; it is `$(IntermediateOutputPath)` that carries the `Debug/net6.0` part. The source article is loose on this point.)

Within that root, the layout is:

```
{generator assembly name}/{generator fully-qualified type name}/{hint name}
```

So the first folder is the assembly that *contains* the generator (not the project being compiled), and the second is the generator type's full name. That keeps two *different* generators from colliding on disk — but note it does not separate two *versions* of the same generator, which share an assembly name and type name and therefore write to the same path. Setting `CompilerGeneratedFilesOutputPath` replaces only the root; the per-generator subfolders remain.

### The fix: remove the output folder from the `Compile` glob

`Compile Remove` is not an optimisation, it is the thing that makes the whole arrangement legal:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>GeneratedSources</CompilerGeneratedFilesOutputPath>
</PropertyGroup>

<ItemGroup>
  <Compile Remove="$(CompilerGeneratedFilesOutputPath)/**/*.cs" />
</ItemGroup>
```

Now the files land in `GeneratedSources/…` inside the project, git sees them as normal tracked files, and the compilation ignores them entirely.

### Multi-targeting needs the TFM in the path

If a project builds several `TargetFramework`s and the generator emits different code per framework (conditional compilation, framework-specific APIs), a single shared folder means each inner build overwrites the previous one and the committed files silently represent whichever TFM finished last.

Two requirements pull in opposite directions, and that is the whole trick:

1. Each inner build must **write** somewhere unique, so append `$(TargetFramework)` to the output path.
2. Each inner build must **exclude** every TFM's folder, not just its own — the `Compile` glob it inherits covers the entire project directory, so a `net8.0` build still sees the `net9.0` files.

Satisfy both by writing to the TFM-qualified path while excluding one level higher. The exclusion below is deliberately broader than the output path:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>GeneratedSources\$(TargetFramework)</CompilerGeneratedFilesOutputPath>
</PropertyGroup>

<ItemGroup>
  <Compile Remove="GeneratedSources\**\*.cs" />
</ItemGroup>
```

If you would rather not repeat the folder name, hoist it into a property of your own and reference it in both places — the shape is what matters, not the name.

### Why bother

Committed output turns generator behaviour into something reviewable — a change in emitted code shows up as a diff instead of a silent behavioural shift. Andrew's motivating case is the Datadog .NET tracer, where generated members are called from the native side of the profiler, so an unnoticed change in generated code is a genuine risk. Note that all of this is consumer-side MSBuild configuration and is orthogonal to how a generator is written: you can apply it to any project referencing any analyzer or generator, including third-party ones you do not control.

## Pitfalls

- **Assuming a passing build means you are done.** Enable emission, build once, see green, commit — and the failure lands on the next person (or the next build). Always build twice before believing the configuration works.
- **Adding `CompilerGeneratedFilesOutputPath` without `Compile Remove`.** These two are a pair. Pointing the output anywhere inside the project directory without the exclusion guarantees duplicate-definition errors — CS0111 when the generator emits members into a partial type, CS0101 when it emits a whole new type.
- **Leaving the default `obj` path and expecting to commit it.** Emission alone gives you files you cannot review; you also need them out of the intermediate directory.
- **Sharing one output folder across target frameworks.** Whichever inner build finishes last wins, and the committed files misrepresent every other TFM.
- **Excluding the TFM-qualified path when multi-targeting.** If `Compile Remove` uses `$(CompilerGeneratedFilesOutputPath)` in the multi-targeting setup, sibling TFM folders are still globbed in and you are back to duplicate-member errors.
- **Stale files are never cleaned up.** Nothing deletes output from a generator you removed, a hint name you renamed, or a TFM you dropped — those files linger in the committed folder and quietly rot. Prune them by hand when the generator surface changes.
- **Treating the committed files as source.** They are build artifacts kept for auditability. Hand-editing them changes nothing: they are excluded from compilation and overwritten on the next build.
- **Diff noise.** Every generator change rewrites the committed output, so expect large mechanical diffs. That visibility is the point, but it is a real cost to weigh per repo.

## In his words

> "That's because the compiler is including the emitted files _in addition_ to the in-memory source generator output."
— Andrew Lock, [.NET Escapades](https://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/)

> "…the source generator output is emitted to disk, it is included in source control so can be reviewed in PRs etc, and it doesn't impact the compilation itself."
— Andrew Lock, [.NET Escapades](https://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/)

## Read the full article

<https://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/>
