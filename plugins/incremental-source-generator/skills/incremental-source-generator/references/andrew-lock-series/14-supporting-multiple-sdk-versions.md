# Part 14 — Supporting multiple .NET SDK versions in a source generator

> Source: [Creating a source generator - Part 14: Supporting multiple .NET SDK versions in a source generator](https://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/)
> Andrew Lock, .NET Escapades
> Original article (c) Andrew Lock, all rights reserved. This file is an original summary, not a copy.

## Why read this

A generator assembly is bound to the `Microsoft.CodeAnalysis.CSharp` version it compiles against, and that version sets a hard floor on the SDK, MSBuild, and Visual Studio versions your consumers must have. If you take a dependency on a newer Roslyn API, every consumer has to upgrade — even the ones who never use the new feature. Send the user here when they want one NuGet package to light up newer APIs on newer SDKs while still loading on older ones, and when they need the concrete project/packaging layout to do it.

## Key points

- **The mechanism is a package path convention, not runtime probing.** Ship several copies of the analyzer under `analyzers/dotnet/roslyn<version>/cs/` (for example `analyzers/dotnet/roslyn4.4/cs/` and `analyzers/dotnet/roslyn4.11/cs/`) instead of the single unversioned `analyzers/dotnet/cs/`. The SDK inspects the compiler's own Roslyn API version and loads the highest versioned folder that compiler supports, ignoring the rest.
- **One generator project per Roslyn baseline.** The article restructures a single generator into per-version projects — named along the lines of `<Project>.Roslyn4_04` and `<Project>.Roslyn4_11` — each pinning a different `Microsoft.CodeAnalysis.CSharp` version, plus a dedicated `<Project>.Pack` project whose only job is to produce the NuGet package.
- **Factor the shared build config into a `.targets` file.** Each per-version `.csproj` sets a single property (e.g. `RoslynApiVersion`) and imports a shared `*.Build.targets`. The targets file uses `Version="$(RoslynApiVersion)"` on the `Microsoft.CodeAnalysis.CSharp` `PackageReference` so the per-project delta is one line, not a duplicated file.
- **Gate newer APIs with `DefineConstants`.** The higher-version project appends a symbol (the article uses `INTERCEPTORS` via `<DefineConstants>$(DefineConstants);INTERCEPTORS</DefineConstants>`) and the shared source uses `#if` around code that only compiles against the newer Roslyn API.
- **Packing.** The `.Pack` project sets `IncludeBuildOutput=false` (it has no output of its own worth shipping) and picks up each generator's built DLL via `None Include` items with `Pack="true"` and a `PackagePath` pointing at the matching `analyzers/dotnet/roslyn<version>/cs` folder. `ProjectReference`s to the generator projects use `ReferenceOutputAssembly="false"` so the pack project doesn't take a compile dependency on them.
- **Consumer-side reference metadata is unchanged:** `PrivateAssets="all"` so the analyzer reference doesn't flow transitively, and `OutputItemType="Analyzer"` when referencing a generator by project.
- **Roslyn API availability is the thing you are actually multi-targeting for.** `IIncrementalGenerator` requires Roslyn 4.0; `SyntaxValueProvider.ForAttributeWithMetadataName` — the fast, recommended entry point — requires 4.3.1; the interceptors API the article targets requires 4.11.0. Roslyn 4.4 corresponds to the .NET 7 SDK; Roslyn 4.11 corresponds to .NET SDK 8.0.400 and later.
- **Tests multiply too.** Unit/snapshot test projects are split per Roslyn baseline (`Tests.Roslyn4_04`, `Tests.Roslyn4_11`), each project-referencing its own generator. Shared test source uses the same `#if INTERCEPTORS` gating.
- **Integration tests pick a generator from the live SDK version.** Condition on `NETCoreSdkVersion` with the MSBuild property function comparison, e.g.
  ```xml
  <PropertyGroup Condition="$([MSBuild]::VersionGreaterThanOrEquals('$(NETCoreSdkVersion)', '8.0.400'))">
    <UsingModernDotNetSdk>true</UsingModernDotNetSdk>
  </PropertyGroup>
  ```
  then branch the `ProjectReference` on `UsingModernDotNetSdk`.
- **CI has to actually exercise both paths.** The article's GitHub Actions setup installs multiple SDKs, builds and tests on the newer one, then pins the older SDK by writing a `global.json` (`dotnet new globaljson`) and re-runs the packaged-NuGet tests to prove the older folder loads.
- `EnforceExtendedAnalyzerRules` is set on the analyzer projects (the standard analyzer-authoring rule set toggle).

## Pitfalls

- Don't multi-target reflexively. The article's own verdict is that the complexity rarely pays for itself unless you genuinely need an optional newer-SDK-only feature. Bumping the single Roslyn floor is usually the better trade.
- Duplication is the real cost: two generator projects, two test projects, extra CI legs, and `#if` blocks scattered through shared source. Every future change is applied N times.
- Folder naming must match the convention exactly. `roslyn4.4`, not `roslyn4.04` or `Roslyn4.4` — the SDK matches on the recognised path shape, and a folder it doesn't understand simply doesn't get loaded, so your generator silently produces nothing.
- Testing only against the newest installed SDK proves nothing about the older path. Without a `global.json`-pinned run against the packaged `.nupkg`, a broken older folder ships undetected.
- The `.Pack` project must not accidentally reference the generators as normal compile references; without `ReferenceOutputAssembly="false"` / `IncludeBuildOutput=false` you get the generator assembly landing in `lib/` as a library, which is not how analyzers are consumed.

## In his words

> "If you don't _have_ to do this, then don't. It's a _lot_ of work and added complexity to a project, without a big pay off."

— Andrew Lock, [Creating a source generator - Part 14](https://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/)

## Read the full article

[andrewlock.net — Creating a source generator, Part 14: Supporting multiple .NET SDK versions in a source generator](https://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/)
