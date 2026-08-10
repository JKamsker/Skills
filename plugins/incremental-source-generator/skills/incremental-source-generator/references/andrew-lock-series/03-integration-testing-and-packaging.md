# Andrew Lock, Part 3 — Integration Testing and NuGet Packaging

> Source: [Creating a source generator - Part 3: Integration testing and NuGet packaging](https://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/)
> Author: Andrew Lock, .NET Escapades
> Original article (c) Andrew Lock, all rights reserved. This file is an original summary, not a copy.

## Why read this

Snapshot/unit tests over `CSharpGeneratorDriver` prove the generator emits the right *string*; they prove nothing about whether a consumer can actually reference it and compile. This part closes that gap: it wires the generator into a real consuming project so the generated code is compiled and executed by tests, then packs the generator into a NuGet package with the layout the Roslyn compiler actually looks for. Send the user here when a generator "works in tests" but produces nothing in a consuming project, when the packed `.nupkg` silently does nothing, or when they are about to ship a generator to nuget.org for the first time.

## Key points

**Referencing a generator from a consumer project.** A plain `<ProjectReference>` is wrong — that just adds the generator assembly as a normal library dependency, and the compiler never loads it as an analyzer. Two metadata attributes flip it:

```xml
<ProjectReference Include="..\MyGen\MyGen.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

- `OutputItemType="Analyzer"` promotes the built DLL into the `@(Analyzer)` item group, which is what becomes `csc /analyzer:...`. Without it the generator never runs.
- `ReferenceOutputAssembly="false"` keeps the generator assembly out of the consumer's compile references, so consumers don't accidentally bind against Roslyn types or the generator's internals.

**What the integration test project actually does.** It is an ordinary xUnit project that references the generator with the attributes above, declares real types decorated with the generator's marker attribute (in the article's running example, enums with `[EnumExtensions]`), and then calls the generated API (`ToStringFast()`) from tests. Two things get verified at once: compilation succeeding proves the member was generated with the expected name/signature, and the assertions prove the generated body behaves correctly. The tests cover the interesting shapes — an ordinary declared member, a value cast from an undefined integer, and a combined `[Flags]` value — comparing generated output against the built-in `ToString()`.

**Packing the generator.** The compiler discovers generators inside a package by convention, at `analyzers/dotnet/cs`. Two csproj changes get it there:

```xml
<PropertyGroup>
  <IncludeBuildOutput>false</IncludeBuildOutput>
</PropertyGroup>

<ItemGroup>
  <None Include="$(OutputPath)\$(AssemblyName).dll"
        Pack="true"
        PackagePath="analyzers/dotnet/cs"
        Visible="false" />
</ItemGroup>
```

`IncludeBuildOutput=false` suppresses the default placement into `lib/<tfm>/`; the `None` item with `Pack="true"` and an explicit `PackagePath` puts the same DLL in the analyzer path instead. Pack with an explicit version into a local output directory, e.g. `dotnet pack -c Release -o ./artifacts -p:Version=0.1.0-beta`.

**Testing the package, not just the project.** A second test project consumes the generator as a real `<PackageReference Include="..." Version="0.1.0-beta" />` rather than a project reference, which is the only way to catch a broken package layout. To avoid duplicating test code, it links the same `.cs` files from the project-reference test project:

```xml
<Compile Include="..\MyGen.IntegrationTests\*.cs" Link="%(Filename)%(Extension)" />
```

**Isolating the local feed.** Restore is pointed at a dedicated config (e.g. `nuget.integration-tests.config`) that does `<clear />` on `packageSources` and then adds nuget.org plus the local `./artifacts` folder as a source. Restore, build and test are then run as three separate steps so the custom config and isolated package folder are honoured: `dotnet restore --configfile ...`, then `dotnet build --no-restore`, then `dotnet test --no-build --no-restore`.

## Pitfalls

- **Forgetting either ProjectReference attribute.** Omit `OutputItemType="Analyzer"` and the generator silently never runs — you get "member does not exist" errors with no diagnostic explaining why. Omit `ReferenceOutputAssembly="false"` and the consumer picks up an unwanted assembly reference.
- **Shipping the DLL in `lib/`.** If you pack normally, the package restores fine and adds a reference, but the compiler never loads a generator from `lib/`. Nothing is generated and there is no error — the failure mode is pure silence. The `analyzers/dotnet/cs` path is not optional.
- **Polluting the global NuGet cache.** Restoring a locally-built test package with a fixed version caches that exact version globally. Rebuild the package with the same version number and restore keeps handing you the stale cached copy, producing confusing "my fix didn't take" behaviour. Hence the `<clear />`-ed config and a package directory scoped to the test run.
- **Running `dotnet test` in one shot.** Because the package test needs a non-default NuGet config, restore has to happen explicitly first; collapsing restore/build/test into a single command loses the custom source configuration.
- **Assuming snapshot tests cover you.** They validate emitted text against a fixed compilation, not the reference plumbing, the package layout, or whether the generated code compiles in a real project. Both layers are needed.

## Notes beyond this article

The article's packaging section covers the single-assembly case. Generators with their own NuGet dependencies need extra work (the dependency assemblies must be packed alongside into `analyzers/dotnet/cs`, since analyzer loading ignores the package's dependency graph) — later parts of the series and separate posts cover that, along with properties such as `IsRoslynComponent` (enables the Roslyn debugger launch profile) and `EnforceExtendedAnalyzerRules`. Do not assume those appear here.

## Read the full article

[Creating a source generator - Part 3: Integration testing and NuGet packaging](https://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/) — Andrew Lock, .NET Escapades.
