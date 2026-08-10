Title: Reading compilation options and the C# version in source generators: Creating a source generator - Part 12

URL Source: http://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/

Published Time: 2025-01-14T09:00:00.0000000

Markdown Content:
January 14, 2025 ~8 min read

[Creating a source generator - Part 12](https://andrewlock.net/series/creating-a-source-generator/)

This is the twelfth post in the series: [Creating a source generator](https://andrewlock.net/series/creating-a-source-generator/).

1.   [Part 1 - Creating an incremental generator](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/)
2.   [Part 2 - Testing an incremental generator with snapshot testing](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/)
3.   [Part 3 - Integration testing and NuGet packaging](https://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/)
4.   [Part 4 - Customising generated code with marker attributes](https://andrewlock.net/creating-a-source-generator-part-4-customising-generated-code-with-marker-attributes/)
5.   [Part 5 - Finding a type declaration's namespace and type hierarchy](https://andrewlock.net/creating-a-source-generator-part-5-finding-a-type-declarations-namespace-and-type-hierarchy/)
6.   [Part 6 - Saving source generator output in source control](https://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/)
7.   [Part 7 - Solving the source generator 'marker attribute' problem - Part 1](https://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/)
8.   [Part 8 - Solving the source generator 'marker attribute' problem - Part 2](https://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/)
9.   [Part 9 - Avoiding performance pitfalls in incremental generators](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/)
10.   [Part 10 - Testing your incremental generator pipeline outputs are cacheable](https://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/)
11.   [Part 11 - Implementing an interceptor with a source generator](https://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/)
12.   Part 12 - Reading compilation options and the C# version in source generators (this post) 
13.   [Part 13 - Accessing MSBuild properties and user configuration from source generators](https://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/)
14.   [Part 14 - Supporting multiple .NET SDK versions in a source generator](https://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/)
15.   [Part 15 - Solving the source generator 'marker attribute' problem in .NET 10](https://andrewlock.net/exploring-dotnet-10-preview-features-4-solving-the-source-generator-marker-attribute-problem-in-dotnet-10/)

In this post I show how to access information about the project a source generator is running in, such as the C# version, the name of the assembly, or the current configuration (`Debug` or `Release`).

## [Why might you need details about the compilation?](http://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/#why-might-you-need-details-about-the-compilation-)

If you're writing a source generator, you're generating C# source code that is included in the target project's compilation. But you have to be careful—which C# features are you using in your generated code? Do you know which features you _can_ safely use?

For example, if you're building a source generator that could be used with the .NET 6 or 7 SDK, then you _can't_ just assume that you can use [collection expressions](https://andrewlock.net/series/behind-the-scenes-of-collection-expressions/). Collection expressions were introduced in C# 12 (along with .NET 8), and _can_ be used when you're targeting earlier framework versions of .NET, as long as you're using a new enough version of the .NET SDK and are using C# 12 or higher.

Depending on the code you're generating, that means you should _potentially_ be generating different code based on the consuming project's C# version. Collection expressions are a great example that necessitate this, as in general, you _should_ use them where you can, because [they can improve runtime performance](https://andrewlock.net/behind-the-scenes-of-collection-expressions-part-2-exploring-the-generated-code-list-and-fallback-cases/#optimizing-listt).

Yes, that's kind of a pain, and it seems _generally_ quite rare to bother with the added complexity, outside of the generators that ship with the runtime. And even in those cases, they [tend to just bail out of generation entirely](https://github.com/dotnet/runtime/blob/dae890906431049d32e24d498a1d707a441a64a8/src/libraries/System.Text.RegularExpressions/gen/RegexGenerator.cs#L296-L302) rather than support multiple SDK versions.

> Interestingly, [there was a proposal](https://github.com/dotnet/roslyn/issues/61094) a few years ago to let the version of C# used in the generator be _higher_ than that used in the rest of the project. This may have solved the issue in some cases, but it didn't go anywhere due to the additional complexity it would have added to the compiler and/or IDEs.

In the following sections I'll show how to access some basic compilation settings, and then we'll move onto detecting and working with the C# version.

## [Accessing details about the compilation from a source generator](http://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/#accessing-details-about-the-compilation-from-a-source-generator)

We'll start by retrieving some static compilation settings like the platform being compiled for (e.g. `x64`, `x86`, or, more likely, `AnyCPU`) and the configuration setting (`Debug` or `Release`). I'll also show how to grab the name of the assembly of the target project.

These details are all available from within a source generator from the `Compilation` type. You can access the `Compilation` using `IncrementalGeneratorInitializationContext.CompilationProvider`. For example, the following very simple generator shows how to grab several details, and output a source generated file that includes the values as comments. It's more likely that you would want to expose these as constants in a generated class, but this is just for demo purposes!

```
[Generator]
public sealed class IncrementalBuildInformationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Create an IncrementalValueProvider from CompilationProvider
        IncrementalValueProvider<(Platform Platform, OptimizationLevel OptimizationLevel, string? AssemblyName)> settings = context
          .CompilationProvider
          .Select((Compilation c, CancellationToken _) =>
          {
              // Grab the values from Compilation and CompilationOptions
              return (c.Options.Platform, c.Options.OptimizationLevel, c.AssemblyName);
          });

        // Generate the source from the captured values
        context.RegisterSourceOutput(settings, static (spc, opts) =>
        {
            var  source = 
                $$"""
                // Platform: {{opts.Platform}}
                // Configuration: {{opts.OptimizationLevel}}
                // AssemblyName: {{opts.AssemblyName}}
                """;

            spc.AddSource("Example.g.cs", source);
        });
    }
}
```

When you run this generator, the generated _Example.g.cs_ file contains something like this following:

```
// Platform: AnyCPU
// AssemblyName: MyExampleProject
// Configuration: Release
```

There are _many_ more details you can find on the `Compilation` and `CompilationOptions` object if you need them. However, you should be a little careful about doing to much work in the CompilationProvider, and should be wary about accessing and using symbols and some syntax. The `CompilationProvider` returns a new value for every keypress in the IDE, so you should [bear that in mind](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/#5-be-careful-using-compilationprovider).

## [Finding the C# Language version](http://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/#finding-the-c-language-version)

The `CompilationOptions` object contains a lot of details about the compilation, but it _doesn't_ expose anything about which version of C# is being used. That's because those details are exposed by the `CSharpCompilation` object, which is a _subclass_ of `Compilation`.

To access the C# language version, we can extend the above example, checking that the provided compilation object is a `CSharpCompilation`, and extracting the `LanguageVersion`. _Theoretically_ you could be in a VB project, so can't guarantee that you'll have a `CSharpCompilation`, though I'm really not sure if the generator even runs in that case, so this is mostly just a case of keeping the C# compiler happy.

```
[Generator]
public sealed class IncrementalBuildInformationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var settings = context.CompilationProvider
          .Select((c, _)  => 
          {
            // Assuming this is a C# project, this should be true!
            LanguageVersion? csharpVersion = c is CSharpCompilation comp
              ? comp.LanguageVersion
              : null;

            return (
                c.Options.Platform,
                c.Options.OptimizationLevel,
                c.AssemblyName,
                LanguageVersion: csharpVersion);
          });

        context.RegisterSourceOutput(settings, static (spc, opts) =>
        {
           var  source = 
              $$"""
                // Platform: {{opts.Platform}}
                // Configuration: {{opts.OptimizationLevel}}
                // AssemblyName: {{opts.AssemblyName}}
                // C# version: {{opts.LanguageVersion}}
              """;

            spc.AddSource("Example.g.cs", source);
        });
    }
}
```

With this addition, the generated file looks something like this instead:

```
// Platform: AnyCPU
// AssemblyName: MyExampleProject
// Configuration: Release
// C# version: CSharp13
```

The `LanguageVersion` property is an `enum`, so we're just printing the `ToString` value here.

## [Understanding the possible values of C# version](http://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/#understanding-the-possible-values-of-c-version)

One interesting point is that the possible values of `LanguageVersion` at _compile_ time are controlled by the version of the _Microsoft.CodeAnalysis.CSharp_ package you reference in your source generator. For example, for the `4.4.0` version of the package, which corresponds to the .NET 7 (i.e. the C# 11) SDK release, `LanguageVersion` contains the following values:

```
public enum LanguageVersion
{
    CSharp1 = 1,
    CSharp2 = 2,
    CSharp3 = 3,
    CSharp4 = 4,
    CSharp5 = 5,
    CSharp6 = 6,
    CSharp7 = 7,
    CSharp7_1 = 701,
    CSharp7_2 = 702,
    CSharp7_3 = 703,
    CSharp8 = 800,
    CSharp9 = 900,
    CSharp10 = 1000,
    CSharp11 = 1100,
    LatestMajor = int.MaxValue - 2,
    Preview = int.MaxValue - 1,
    Latest = int.MaxValue,
    Default = 0,
}
```

However, if you run the above generator in a project that's built with the .NET 9 SDK (which supports C# 13), you'll see the following output:

```
// Platform: AnyCPU
// AssemblyName: MyExampleProject
// Configuration: Release
// C# version: CSharp13
```

The above shows that we're building with C# 13, and that the `LanguageVersion` property returned the value `CSharp13`, _even though our generator didn't know that value existed at at compile time_!

> This demonstrates the relationship with the _Microsoft.CodeAnalysis.CSharp_ package you reference at compile time for your source generator versus at runtime. The NuGet package is providing an API surface to code against, but the .NET SDK is free to provide a _different_ implementation, as long as the API is binary compatible. In this case, adding an additional enum value is perfectly legal.

So remember: the `LanguageVersion`_may_ contain a value that you can't reference at compile time!

## [Changing the C# LangVersion in a project](http://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/#changing-the-c-langversion-in-a-project)

By default, the version of C# used by a project is tied to the version of the framework it's targeting. So if you're targeting .NET 8, the default C# version is `12`, while for .NET 9 it's `13`.

You can override the C# version for a project using the `<LangVersion>` property in your _.csproj_:

```
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <!-- Using C#10 instead of the default C# 13 -->
    <LangVersion>10</LangVersion>
  </PropertyGroup>
</Project>
```

The source generator above detects this change, and the `LanguageVersion` updates as expected:

```
// Platform: AnyCPU
// AssemblyName: MyExampleProject
// Configuration: Release
// C# version: CSharp10
```

Note that it's _also_ possible to [specify some "special" versions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/configure-language-version#c-language-version-reference) in the `<LangVersion>` element:

*   `default` or `latestMajor`: The compiler accepts syntax from the latest released major version of the compiler.
*   `latest`: The compiler accepts syntax from the latest released version of the compiler (including minor version).
*   `preview`: The compiler accepts all valid language syntax from the latest preview version.

Note that if users specifies `default`, `latest`, or `latestMajor` in their _.csproj_ it's automatically mapped to the corresponding specific `CSharpX` version when you retrieve it in the `LanguageVersion` property. For example, setting this in your _.csproj_

```
<LangVersion>latest</LangVersion>
```

results in the following generated C# version (when running with the .NET 9 SDK)

```
// C# version: CSharp13
```

The one exception to this is `Preview`, which _isn't_ re-mapped as far as I can tell (presumably because the "next" C# version doesn't have an entry in the enum)! So if the target project has this:

```
<LangVersion>preview</LangVersion>
```

then your generated code looks like this:

```
// C# version: Preview
```

Once you have extracted a `LanguageVersion` in your source generator, the question becomes what to _do_ with it.

## [Generating different code based on `LanguageVersion`](http://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/#generating-different-code-based-on-languageversion)

In the extreme case, you might decide that you simply can't support generating code unless a specific C# version is supported. If that's the case, you can simply add a version check wherever it's required in your pipeline to bail out.

Alternatively, you might decide that you just want to generate _different_ code. If so, then you'll probably want to add a condition in your generation code something like the following:

```
if(currentVersion <= LanguageVersion.CSharp11)
{
    // can't use collection expressions
    return """
           return new List<string>
           {
               "value1",
               "value2",
           };
           """
}
else
{
    // C# 12+ gives us collection expressions
    return """
           return
           [
               "value1",
               "value2",
           ];
           """
}
```

Obviously it's a toy example, but it's pretty much all you need to do! The only real complexity is when you're referencing a version of _Microsoft.CodeAnalysis.CSharp_ that doesn't contain the `LanguageVersion` property you need.

> The version of the _Microsoft.CodeAnalysis.CSharp_ package ties directly to a specific version of the Rosyln API and .NET SDK, so you often can't "simply" update the package, as that reduces the range of SDKs your source generator works on. I'll talk more about this in a subsequent post.

For example, in the above code I worked around the fact that `CSharp12` wasn't an option in the `4.4.0` version of _Microsoft.CodeAnalysis.CSharp_. But what if I needed to specialise for `CSharp13`? I couldn't use any cute `<=` tricks for that…

I think the best option in that scenario is to use the fact that the values for future C# versions use known `int` values, and to "cast" to the correct value, for example:

```
// test for C#13 or higher
if(currentVersion >= (LanguageVersion)1300)
{
    // can use params collections
    return """
           public void Loop(params ReadOnlySpan<T> items)
           {
               foreach (var item in items)
                  Console.WriteLine(item)
           };
           """
}
else
{
    // No params collections, have to use array instead
    return """
           public void Loop(params T[] items)
           {
               foreach (var item in items)
                  Console.WriteLine(item)
           };
           """
}
```

It's not particularly pretty, but it works!

I think that covers pretty much everything to do with fetching compilation options and language versions. I haven't covered reading MSBuild properties yet, as that's a bit more involved. I'll cover that in the next post.

## [Summary](http://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/#summary)

In this post I showed how you can retrieve compilation options such as the current configuration and platform from a `Compilation` object in a source generator. I then showed how you can determine the C# version used by the target project. This is important information for a source generator, as you must make sure not to generate code that contains features from an unsupported C# version.

In the next post I'll show how to read MSBuild properties from the target project, how you can use those to configure your source generator, and how to make it easier for users by packing extra files in a NuGet package.

[![Image 1: Implementing an interceptor with a source generator](https://andrewlock.net/content/images/2024/enumgenerators_banner.webp)Previous Implementing an interceptor with a source generator: Creating a source generator - Part 11](https://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/)[![Image 2: Accessing MSBuild properties and user configuration from source generators](https://andrewlock.net/content/images/2025/generator_props_banner.png)Next Accessing MSBuild properties and user configuration from source generators: Creating a source generator - Part 13](https://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/)

Andrew Lock | .Net Escapades

![Image 3](https://andrewlock.net/assets/img/icons/apple/apple-touch-icon-180x180.png)Want an email when

there's new posts?
