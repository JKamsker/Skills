Title: Saving source generator output in source control: Creating a source generator - Part 6

URL Source: http://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/

Published Time: 2022-01-18T10:00:00.0000000

Markdown Content:
January 18, 2022 ~5 min read

[Creating a source generator - Part 6](https://andrewlock.net/series/creating-a-source-generator/)

This is the sixth post in the series: [Creating a source generator](https://andrewlock.net/series/creating-a-source-generator/).

1.   [Part 1 - Creating an incremental generator](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/)
2.   [Part 2 - Testing an incremental generator with snapshot testing](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/)
3.   [Part 3 - Integration testing and NuGet packaging](https://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/)
4.   [Part 4 - Customising generated code with marker attributes](https://andrewlock.net/creating-a-source-generator-part-4-customising-generated-code-with-marker-attributes/)
5.   [Part 5 - Finding a type declaration's namespace and type hierarchy](https://andrewlock.net/creating-a-source-generator-part-5-finding-a-type-declarations-namespace-and-type-hierarchy/)
6.   Part 6 - Saving source generator output in source control (this post) 
7.   [Part 7 - Solving the source generator 'marker attribute' problem - Part 1](https://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/)
8.   [Part 8 - Solving the source generator 'marker attribute' problem - Part 2](https://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/)
9.   [Part 9 - Avoiding performance pitfalls in incremental generators](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/)
10.   [Part 10 - Testing your incremental generator pipeline outputs are cacheable](https://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/)
11.   [Part 11 - Implementing an interceptor with a source generator](https://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/)
12.   [Part 12 - Reading compilation options and the C# version in source generators](https://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/)
13.   [Part 13 - Accessing MSBuild properties and user configuration from source generators](https://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/)
14.   [Part 14 - Supporting multiple .NET SDK versions in a source generator](https://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/)
15.   [Part 15 - Solving the source generator 'marker attribute' problem in .NET 10](https://andrewlock.net/exploring-dotnet-10-preview-features-4-solving-the-source-generator-marker-attribute-problem-in-dotnet-10/)

In this post I describe how to persist the output of your source generator to disk so that it can be part of source control and code reviews, how to control where the files are output, and how to handle the case where your source generator produces different output depending on the target framework.

## [Source generators don't produce artifacts by default](http://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/#source-generators-don-t-produce-artifacts-by-default)

One of the big selling points about source-generators is that they run _in_ the compiler. That makes them more convenient than other source generation techniques, such as t4 templates, as you don't need a separate build step.

However, one potential disadvantage _also_ stems from the fact the source generator runs inside the compiler. That can make it hard to see the effect of a source generator when you're not in the context of an IDE.

For example, if you're reviewing a pull request on GitHub that uses source generators, and you make a change that adds code to the project, you may find it useful to have that output visible in the PR. This may be especially important for "critical" code.

For example, in the [Datadog Tracer](https://github.com/DataDog/dd-trace-dotnet) we recently started using source generators to generate methods called by the "native" part of the profiler, that controls which integrations are enabled. This is a crucial part of the tracer so it's important to see any changes. We wanted any changes to be visible in PRs, so we needed to make sure the source generator output was written to files.

## [Emitting compiler generated files](http://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/#emitting-compiler-generated-files)

There's a simple switch to enable persisting source generator files to the file system: `EmitCompilerGeneratedFiles`. You can set this property in your project file:

```
<PropertyGroup>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
</PropertyGroup>
```

Or you can set the MSBuild property in any other way, e.g. at the command line when building

```
dotnet build /p:EmitCompilerGeneratedFiles=true
```

When you set this property alone, the compiler will output the hint files to disk. For example, if we consider [the NetEscapades.EnumGenerators](https://github.com/andrewlock/NetEscapades.EnumGenerators) package, and enable the `EmitCompilerGeneratedFiles` property, we can see that the source generated files are written to the _obj_ folder:

![Image 1: Generated files in the obj folder](https://andrewlock.net/content/images/2022/source_gen_emit.png)

Specifically, the source generator output is written to a folder defined as:

```
{BaseIntermediateOutpath}/generated/{Assembly}/{SourceGeneratorName}/{GeneratedFile}
```

In the example above, we have

*   `BaseIntermediateOutpath`: _obj/Debug/net6.0_
*   Assembly: _NetEscapades.EnumGenerators_
*   SourceGeneratorName: _NetEscapades.EnumGenerators.EnumGenerator_
*   GeneratedFile: _ColoursExtensions\_EnumExtensions.g.cs_, _EnumExtensionsAttribute.g.cs_

Writing files to the _obj_ folder is all well and good, but it doesn't really solve our problem, as the _bin_ and _obj_ folders are typically excluded from source control. We _could_ explicitly include them into source control, but a better option is to emit the files somewhere else.

## [Controlling the output location](http://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/#controlling-the-output-location)

You can control the location of the compiler emitted files by setting the `CompilerGeneratedFilesOutputPath` property. This is a path relative to the project root folder. So for example, if you set the following in your project file:

```
<PropertyGroup>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

This will write the files to the _Generated_ folder in the project folder:

![Image 2: Generated files in the 'Generated' folder](https://andrewlock.net/content/images/2022/source_gen_emit_02.png)

Whatever you place in `CompilerGeneratedFilesOutputPath` replaces the `{BaseIntermediateOutpath}/generated` prefix in the file path, so the files are written to:

```
{CompilerGeneratedFilesOutputPath}/{Assembly}/{SourceGeneratorName}/{GeneratedFile}
```

On the face of it, this seems like it solves all the issues: the source generator contents are emitted to the file system, to a place that's included in source control. Problem solved right?

The difficulty is when you try and build for a second time, _after_ the files have already been written, you'll get a number of errors:

```
ColoursExtensions_EnumExtensions.g.cs(31,28): error CS0111: Type 'ColoursExtensions' already defines a member called 'IsDefined' with the same parameter types ColoursExtensions_EnumExtensions.g.cs(40,28): error CS0111: Type 'ColoursExtensions' already defines a member called 'TryParse' with the same parameter types
```

That's because the compiler is including the emitted files _in addition_ to the in-memory source generator output. This causes duplication of the types and the errors above. The answer is to exclude the files from the compilation.

## [Excluding emitted files from the compilation](http://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/#excluding-emitted-files-from-the-compilation)

The simple solution to this problem is to remove the emitted files from the project compilation, so that only the in-memory source generator output is part of the compilation. You can exclude these individually (e.g. by right-clicking the file in Visual Studio), or more usefully, you can [use a wildcard pattern](https://www.reddit.com/r/dotnet/comments/mrgx3u/how_to_put_source_generator_code_into_source/) to exclude all the .cs files in those folders:

```
<PropertyGroup>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>

<ItemGroup>
    <!-- Exclude the output of source generators from the compilation -->
    <Compile Remove="$(CompilerGeneratedFilesOutputPath)/**/*.cs" />
</ItemGroup>
```

With this change, we now have the best of all worlds—the source generator output is emitted to disk, it is included in source control so can be reviewed in PRs etc, and it doesn't impact the compilation itself.

## [Splitting by target framework](http://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/#splitting-by-target-framework)

The properties above are [what we initially used](https://github.com/DataDog/dd-trace-dotnet/blob/69403b8873b905230faeee2b3f6284f509517ecf/tracer/src/Datadog.Trace/Datadog.Trace.csproj#L15-L27) when adding our first source generator in the Datadog Tracer. However, this subsequently caused us a bit of an issue.

For context, the Datadog Tracer currently supports multiple target frameworks: `net461`, `netstandard2.0`, `netcoreapp3.1`. However some of our integrations are only applicable for specific target frameworks. For example, [the ASP.NET integration only applies to `net461`, so we use `#if NETFRAMEWORK` to exclude it from the .NET Core assembly](https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AspNet/ApiController_ExecuteAsync_Integration.cs).

The difficulty is that the output of our source generator is _different_ for each target framework, yet the output of each target framework compilation is written into the _same_ folder in all cases. Each time the compiler runs for a target framework, it overwrites the existing file output in _Generated/AssemblyName/GeneratorName/FileName.cs_! Three different outputs of the source generator, but only one of those is persisted to disk.

To work around this problem, we added the target framework to the output file path using the `$(TargetFramework)` property.

```
<PropertyGroup>
    <!-- Persist the source generator (and other) files to disk -->
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <!-- 👇 The "base" path for the source generators -->
    <GeneratedFolder>Generated</GeneratedFolder>
    <!-- 👇 Write the output for each target framework to a different sub-folder -->
    <CompilerGeneratedFilesOutputPath>$(GeneratedFolder)\$(TargetFramework)</CompilerGeneratedFilesOutputPath>
</PropertyGroup>

<ItemGroup>
    <!-- 👇 Exclude everything in the base folder -->
    <Compile Remove="$(GeneratedFolder)/**/*.cs" />
</ItemGroup>
```

With this change, the output of the source generator for each framework is written into a separate folder, so we can easily see the difference between the assemblies.

![Image 3: Splitting files by target framework](https://andrewlock.net/content/images/2022/source_gen_emit_03.png)

Obviously this approach isn't necessary unless you're multi-targeting _and_ you produce different source-generator output for different target frameworks, but it's an easy approach if you are.

## [Summary](http://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/#summary)

In this post I described how you can ensure source generators emit their generated outputs to disk. This can be useful if you want to monitor for changes in the source generator output, or want to be able to review that output in a non-IDE scenario, such as in a pull request on GitHub. I then showed how to control _where_ the files are written, and one approach to handle the case where the source generator creates different output for different target framework builds of your project.

[![Image 4: Finding a type declaration's namespace and type hierarchy](https://andrewlock.net/content/images/2021/extracting_hierarchy.png)Previous Finding a type declaration's namespace and type hierarchy: Creating a source generator - Part 5](https://andrewlock.net/creating-a-source-generator-part-5-finding-a-type-declarations-namespace-and-type-hierarchy/)[![Image 5: Solving the source generator 'marker attribute' problem - Part 1](https://andrewlock.net/content/images/2021/attributes_banner.png)Next Solving the source generator 'marker attribute' problem - Part 1: Creating a source generator - Part 7](https://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/)

Andrew Lock | .Net Escapades

![Image 6](https://andrewlock.net/assets/img/icons/apple/apple-touch-icon-180x180.png)Want an email when

there's new posts?
