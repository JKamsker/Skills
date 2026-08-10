# Andrew Lock — "Creating a source generator" series (external reading)

Andrew Lock's series is the best practical walkthrough of incremental generators. The articles are
**not bundled** with this skill: they are copyrighted, all-rights-reserved content on the author's
site. Fetch the URL for the topic you need when you need it.

Series index: <https://andrewlock.net/series/creating-a-source-generator/>

Use this table to pick the right article instead of fetching the whole series.

| # | Topic | When to read it | URL |
| :- | :---- | :-------------- | :-- |
| 1 | Creating an incremental generator | First implementation; pipeline shape, `IIncrementalGenerator` basics | <https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/> |
| 2 | Snapshot testing a generator | Setting up tests where exact generated text matters | <https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/> |
| 3 | Integration testing and NuGet packaging | Analyzer asset layout, `analyzers/dotnet/cs`, consumer-facing packaging | <https://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/> |
| 4 | Marker attributes | Driving generation from an attribute; post-initialization output | <https://andrewlock.net/creating-a-source-generator-part-4-customising-generated-code-with-marker-attributes/> |
| 5 | Namespace and type hierarchy | Emitting correct namespaces, nested/containing types, `partial` shape | <https://andrewlock.net/creating-a-source-generator-part-5-finding-a-type-declarations-namespace-and-type-hierarchy/> |
| 6 | Committing generated output | `EmitCompilerGeneratedFiles`, reviewing `.g.cs` in source control | <https://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/> |
| 7 | Marker attribute problem, part 1 | Attribute visibility/duplication across referencing assemblies | <https://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/> |
| 8 | Marker attribute problem, part 2 | `EmbeddedAttribute` and the internal-attribute approach | <https://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/> |
| 9 | Avoiding performance pitfalls | **Read this for any incrementality bug.** Equatable models, what breaks caching | <https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/> |
| 10 | Testing pipeline outputs are cacheable | Proving equivalent inputs produce cached outputs | <https://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/> |
| 11 | Implementing an interceptor | Interceptors via a generator | <https://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/> |
| 12 | Compilation options and C# version | Reading `LanguageVersion` and compilation options | <https://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/> |
| 13 | MSBuild properties and user config | `CompilerVisibleProperty`, `CompilerVisibleItemMetadata`, analyzer config | <https://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/> |
| 14 | Supporting multiple SDK versions | Shipping one analyzer across differing Roslyn versions | <https://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/> |
| 15 | Marker attribute problem in .NET 10 | Current guidance if targeting .NET 10 | <https://andrewlock.net/exploring-dotnet-10-preview-features-4-solving-the-source-generator-marker-attribute-problem-in-dotnet-10/> |

Articles © Andrew Lock, .NET Escapades — all rights reserved. Linked, not redistributed.
