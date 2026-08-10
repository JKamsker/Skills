# Part 13 — Accessing MSBuild properties and user configuration from source generators

> Source: [Creating a source generator, Part 13](https://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/)
> Andrew Lock, .NET Escapades
> Original article (c) Andrew Lock, all rights reserved. This file is an original summary, not a copy.

## Why read this

A generator often needs a knob: opt into an experimental emit mode, change the namespace it writes into, toggle interception. MSBuild properties are the natural place for that knob, but the compiler does not hand generators the project file — properties must be explicitly opted in before a generator can see them. Send the reader here when they ask "how do users configure my generator?", "how do I read a `<PropertyGroup>` value from `IIncrementalGenerator`?", or when they want the setting to ship with their NuGet package so consumers only set one property.

## Key points

- **Entry point is `context.AnalyzerConfigOptionsProvider`**, an `IncrementalValueProvider<AnalyzerConfigOptionsProvider>` exposed on `IncrementalGeneratorInitializationContext`. From it, `GlobalOptions` gives an `AnalyzerConfigOptions` with `TryGetValue(string key, out string? value)`. Everything is a `string` — you parse and validate yourself.
- **Keys are prefixed, not bare.** An MSBuild property `Foo` is read as `build_property.Foo`. Key lookup is case-insensitive (`AnalyzerConfigOptions.KeyComparer`).
- **Why the prefix exists:** the SDK does not pipe MSBuild into Roslyn directly. During build it writes `<ProjectName>.GeneratedMSBuildEditorConfig.editorconfig` into `obj/`, translating selected properties into editorconfig entries such as `build_property.RootNamespace = MyApp`. The generator reads that editorconfig view. Opening the file in `obj/` is the fastest way to check whether a property actually made it through.
- **Only opted-in properties appear.** The SDK exposes a small default set (`RootNamespace`, `ProjectDir`, and similar). To surface your own, the consuming project needs an item:
  ```xml
  <ItemGroup>
    <CompilerVisibleProperty Include="EnableMyGenerator" />
  </ItemGroup>
  ```
  with the value set normally in a `<PropertyGroup>`. The generator then reads `build_property.EnableMyGenerator`.
- **Ship the opt-in from your package** so consumers don't have to write `CompilerVisibleProperty` themselves. Put a `<YourPackageId>.props` containing the `CompilerVisibleProperty` items into the package, packed with `Pack="true" PackagePath="build"` on a `None` item. NuGet auto-imports `build/<PackageId>.props` and `build/<PackageId>.targets` for consumers. (Beyond the article: `buildTransitive/` is the variant that also flows to transitive consumers.)
- **Use `.targets`, not `.props`, when you must react to the property's value.** `.props` files are imported before the project body, so a property the user sets in their `.csproj` is not yet assigned. The article's pattern is a `.targets` file with `<Project InitialTargets="...">` and a `<Target>` that reads the final value and derives other MSBuild state.
- **Worked example — interceptors.** A single `EnableEnumGeneratorInterceptor` property drives everything: the `.props` file makes it compiler-visible so the generator can branch on it, and the `.targets` file appends the generator's namespace to `InterceptorsNamespaces` (`InterceptorsPreviewNamespaces` on older SDKs) so the compiler accepts the emitted `[InterceptsLocation]` code. The consumer sets one property instead of three.
- **Per-file configuration (beyond this article, same API family).** `AnalyzerConfigOptionsProvider` also has `GetOptions(SyntaxTree)` and `GetOptions(AdditionalText)` for scope-specific values. Item metadata is opted in with `<CompilerVisibleItemMetadata Include="AdditionalFiles" MetadataName="MyKind" />` and read as `build_metadata.AdditionalFiles.MyKind` from `GetOptions(additionalText)` — the item type and metadata name are both part of the key.
- **Pipeline shape.** Project the value out immediately so the cached node is a small comparable value, not the provider object:
  ```csharp
  IncrementalValueProvider<bool> enabled = context.AnalyzerConfigOptionsProvider
      .Select((p, _) => p.GlobalOptions
          .TryGetValue("build_property.EnableMyGenerator", out var v)
          && bool.TryParse(v, out var b) && b);
  ```
  Then `Combine` that with your collected model right before `RegisterSourceOutput`.

## Pitfalls

- **Forgetting `CompilerVisibleProperty`.** The property is set, the build succeeds, `TryGetValue` silently returns `false`. Check the generated editorconfig in `obj/` before debugging the generator.
- **Dropping the `build_property.` prefix**, or expecting the MSBuild property name to work verbatim.
- **Assuming a value is final in a `.props` file.** Consumers set properties in the project body, in `Directory.Build.props`, on the CLI, or from another package — the author's point is that you cannot know when the value settles, which is exactly why value-dependent logic belongs in `.targets`.
- **Treating values as typed.** Everything arrives as a string; `"true"` casing, empty strings, and unset keys all need explicit handling, and a bad value should degrade gracefully rather than throw inside the generator.
- **Combining the raw provider into a per-item stage.** `AnalyzerConfigOptionsProvider` has no value equality, so a node holding the provider itself invalidates on unrelated editorconfig or build churn and drags every combined item with it. `Select` the scalar first, combine late.

## In his words

> "One such artifact created is <ProjectName>.GeneratedMSBuildEditorConfig.editorconfig. Go ahead, take a look, you'll find this file for all your .NET projects."

— Andrew Lock, [.NET Escapades](https://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/)

> "However, customers can set the property in many different ways. That means you don't know exactly _when_ the value of the property be the \"final\" value."

— Andrew Lock, [.NET Escapades](https://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/)

## Read the full article

[Creating a source generator, Part 13: Providing and accessing MSBuild settings in source generators](https://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/) — Andrew Lock, .NET Escapades.
