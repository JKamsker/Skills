# Part 8 — Solving the source generator 'marker attribute' problem, part 2

> Source: [Creating a source generator, part 8: Solving the source generator 'marker attribute' problem, part 2](https://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/)
> Andrew Lock, .NET Escapades
> Original article (c) Andrew Lock, all rights reserved. This file is an original summary, not a copy.

## Why read this

Part 7 diagnosed the marker-attribute dilemma; this post picks the winner. If your generator embeds its `[MyMarker]` attribute via `RegisterPostInitializationOutput`, every project in a solution gets its own copy — and the moment two of those projects are linked by `[InternalsVisibleTo]`, the compiler sees the same attribute type twice and the build breaks. Send a reader here when they are deciding how to *ship* a marker attribute, when consumers report duplicate-type errors under `InternalsVisibleTo`, or when they are laying out the NuGet package for a generator and don't know which folder each DLL belongs in.

## Key points

- **The chosen answer: put the marker attributes in their own assembly, and ship that assembly inside the same NuGet package as the generator.** The attributes are a real, referenceable DLL — not generated source — so there is exactly one definition per compilation graph and `[InternalsVisibleTo]` becomes a non-issue.
- **Two projects, not one.** A `*.Attributes` project (targeting `netstandard2.0`) holds the attribute types. The generator project references it with:
  ```xml
  <ProjectReference Include="..\MyGen.Attributes\MyGen.Attributes.csproj" PrivateAssets="All" />
  ```
  `PrivateAssets="All"` stops the attributes project leaking out as a NuGet dependency of the generator package.
- **Package layout is the crux.** The attributes DLL must be packed to **two** locations:
  ```text
  analyzers/dotnet/cs/   MyGen.dll            (the generator)
  analyzers/dotnet/cs/   MyGen.Attributes.dll (so the generator can load/see it)
  lib/netstandard2.0/    MyGen.Attributes.dll (so user code can reference it)
  ```
  The `analyzers/dotnet/cs` copy is required because a generator assembly cannot resolve references from the consuming project — only assemblies loaded alongside it in the analyzer directory. The `lib` copy is what makes `[MyMarker]` visible to the user's own source.
- **Packing mechanics.** The generator project sets `<IncludeBuildOutput>false</IncludeBuildOutput>` (so the SDK does not drop the generator into `lib/`) and adds explicit `None` items instead, e.g.
  ```xml
  <None Include="$(OutputPath)\$(AssemblyName).dll" Pack="true" PackagePath="analyzers/dotnet/cs" />
  ```
  with sibling entries pointing the attributes DLL at both `analyzers/dotnet/cs` and `lib/netstandard2.0`.
- **Consumer experience is a single `PackageReference`** with no extra metadata required. Optionally a consumer can add `ExcludeAssets="runtime"` to keep the attributes DLL out of their output folder.
- **`ExcludeAssets="runtime"` is only safe because the marker attributes are decorated with `[System.Diagnostics.Conditional]`.** With `Conditional`, usages are not emitted into the consumer's IL, so there is no runtime dependency on the attributes assembly — only a compile-time one.
- **Local (same-solution) development needs different wiring than the package.** The consuming/test project references the two projects differently:
  ```xml
  <ProjectReference Include="..\MyGen\MyGen.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  <ProjectReference Include="..\MyGen.Attributes\MyGen.Attributes.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="true" />
  ```
  The generator is analyzer-only (`ReferenceOutputAssembly="false"`); the attributes project is *both* an analyzer input and a normal compile reference (`ReferenceOutputAssembly="true"`), mirroring the dual placement in the package.
- **Embedding is kept as an opt-in escape hatch.** Two conditional-compilation constants gate it: `STRONGLY_TYPED_ID_EMBED_ATTRIBUTES` (generate the attributes into the compilation) and `STRONGLY_TYPED_ID_EXCLUDE_ATTRIBUTES` (suppress them). A consumer who wants the embedded flavour defines the constant and excludes the compile assets of the shipped DLL.
- **Precedent:** the same shape is what Microsoft uses for the `[LoggerMessage]` generator, whose attribute lives in `Microsoft.Extensions.Logging.Abstractions`.

## Options he rejected (and why)

1. **Just ship the generator DLL normally** (drop `IncludeBuildOutput=false`) so its attributes are referenceable. Rejected: the generator's own transitive dependencies — `System.Collections.Immutable`, `Microsoft.CodeAnalysis.*` — become the consumer's dependencies and cause version conflicts.
2. **Two separate NuGet packages** (generator + attributes). Rejected: users install one and not the other, and the naming does not make the relationship obvious.
3. **Embed by default, offer an optional attributes package** for the `[InternalsVisibleTo]` case, switched by an MSBuild constant. Rejected as too much machinery for the size of the problem.

## Pitfalls

- Forgetting the `analyzers/dotnet/cs` copy of the attributes DLL: the package looks right, user code compiles against the attribute, but the generator fails to load or cannot resolve the attribute type at analysis time.
- Forgetting `PrivateAssets="All"` on the generator→attributes project reference: the attributes project surfaces as a package dependency and pulls in unwanted graph entries.
- Leaving `IncludeBuildOutput` at its default: the generator assembly ends up in `lib/`, where consumers reference it directly and inherit Roslyn/immutable-collections version conflicts.
- Adding `ExcludeAssets="runtime"` without `[Conditional]` on the attributes: the consumer's assembly then references a DLL that is not in the output folder.
- Copying the packaged wiring into same-solution project references: `ReferenceOutputAssembly` must be `false` for the generator and `true` for the attributes project, and both need `OutputItemType="Analyzer"`.
- Note that the fix here is a *packaging* decision, not a compiler trick. `RegisterPostInitializationOutput` embedding still works and is retained as an opt-in path; it simply is not the default recommendation once `[InternalsVisibleTo]` is in play.

## In his words

> "Option 1. is the standard approach, but it doesn't work when users are using `[InternalsVisibleTo]`, as you can end up defining the same type multiple times."

— Andrew Lock, [Creating a source generator, part 8](https://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/)

> "The final option seemed like the best approach, and gives the smoothest experience for users."

— Andrew Lock, [Creating a source generator, part 8](https://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/)

## Read the full article

[https://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/](https://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/) — the post walks through each rejected option with the full `.csproj` for the generator, the attributes project, and the packaged result.
