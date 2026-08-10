Title: Accessing MSBuild properties and user configuration from source generators: Creating a source generator - Part 13

URL Source: http://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/

Published Time: 2025-01-21T09:00:00.0000000

Markdown Content:
January 21, 2025 ~9 min read

[Creating a source generator - Part 13](https://andrewlock.net/series/creating-a-source-generator/)

This is the thirteenth post in the series: [Creating a source generator](https://andrewlock.net/series/creating-a-source-generator/).

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
12.   [Part 12 - Reading compilation options and the C# version in source generators](https://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/)
13.   Part 13 - Accessing MSBuild properties and user configuration from source generators (this post) 
14.   [Part 14 - Supporting multiple .NET SDK versions in a source generator](https://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/)
15.   [Part 15 - Solving the source generator 'marker attribute' problem in .NET 10](https://andrewlock.net/exploring-dotnet-10-preview-features-4-solving-the-source-generator-marker-attribute-problem-in-dotnet-10/)

In this post I describe how to read MSBuild settings from inside a source generator. I show how you can use this to add "configuration" to your source generators so consumers can control some behaviour of the source generator. Finally I show how you can bundle a _.targets_ file with your generator to make some aspects of this easier to use for consumers, particularly when it comes to interceptors.

## [Why might you want to access MSBuild settings?](http://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/#why-might-you-want-to-access-msbuild-settings-)

In the [previous post](https://andrewlock.net/creating-a-source-generator-part-12-providing-and-accessing-msbuild-settings-in-source-generators/) I described how some features of the compilation can be accessed on the `Compilation` object. However, there's a lot you _can't_ get from there. For example, the default `RootNamespace` does not appear to be accessible from `Compilation`. Instead, you must access the MSBuild property to know what it's set to.

Most of the source generators I've created are controlled solely by attributes applied in code. However there are many cases where you might want to control the behaviour of a source generator in a _global_ way. For example, if you're generating a single type, you may want to be able to control the _name_ of the generated type, or you may want to enable or disable specific features.

As a concrete example, I recently needed to do this while working on [_NetEscapades.EnumGenerators_](https://www.nuget.org/packages/NetEscapades.EnumGenerators). I wanted the interceptor support to be disabled by default, but for consumers to be able to enable it with an MSBuild flag.

In the following sections I'll describe the basics of how to work with both of these aspects.

## [Reading MSBuild settings from a source generator](http://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/#reading-msbuild-settings-from-a-source-generator)

I'll start by showing how you can read the `RootNamespace` for a project. I've started with this setting because it's readable from source generators automatically.

To read an MSBuild setting, you should use the `AnalyzerConfigOptionsProvider`. The following source generator shows how to do this for the `RootNamespace` property. It reads the value from the context and creates a file that just contains a comment with the root namespace:

```
[Generator]
public sealed class RootNamespaceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Create an IncrementalValueProvider from AnalyzerConfigOptionsProvider
        IncrementalValueProvider<string?> rootNamespace = context
            .AnalyzerConfigOptionsProvider
            // Retrieve the RootNamespace property
            .Select((AnalyzerConfigOptionsProvider c, CancellationToken _) =>
                c.GlobalOptions.TryGetValue("build_property.RootNamespace", out var nameSpace)
                    ? nameSpace
                    : null);

        // Generate the source
        context.RegisterSourceOutput(rootNamespace, static (spc, nameSpace) =>
        {
            var source = $$"""
                           // RootNamespace: {{nameSpace}}
                           """;

            spc.AddSource("Example.g.cs", source);
        });
    }
}
```

If you add this generator to a target project, the generated _Example.g.cs_ file contains something like this following:

```
// RootNamespace: MyExampleProject
```

It's important to note that when you retrieve the property from `AnalyzerConfigOptionsProvider`, you must use the prefix `"build_property."`. You might be wondering _why_ you need that prefix; after all, when you're reading MSBuild properties in _.csproj_ files, you don't need that prefix. To explain why, we'll take a short detour.

## [MSBuild, _GeneratedMSBuildEditorConfig.editorconfig_, and source generators](http://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/#msbuild-generatedmsbuildeditorconfig-editorconfig-and-source-generators)

When you build a .NET project, a number of "intermediate" artifacts are created, that are used to create the final build. These files are typically stored inside the `obj` folder of a project. This can include things like where to find the NuGet packages on disk after a restore, source link details, and runtime configuration details.

One such artifact created is `<ProjectName>.GeneratedMSBuildEditorConfig.editorconfig`. Go ahead, take a look, you'll find this file for all your .NET projects. The exact contents will vary depending on the _type_ of the project and on how your project is built, but it'll look something like this:

```
is_global = true
build_property.TargetFramework = net8.0
build_property.TargetPlatformMinVersion = 
build_property.UsingMicrosoftNETSdkWeb = 
build_property.ProjectTypeGuids = 
build_property.InvariantGlobalization = 
build_property.PlatformNeutralAssembly = 
build_property.EnforceExtendedAnalyzerRules = 
build_property._SupportedPlatformList = Linux,macOS,Windows
build_property.RootNamespace = MyExample
build_property.ProjectDir = C:\repos\MyExample\
build_property.EnableComHosting = 
build_property.EnableGeneratedComInterfaceComImportInterop = 
build_property.EffectiveAnalysisLevelStyle = 8.0
build_property.EnableCodeStyleSeverity =
```

EditorConfig is primarily a [specification](https://spec.editorconfig.org/) for maintaining consistent coding style across multiple developers and IDEs when working in the same project. In this case, however, the file is exposing various properties of the MSBuild system, which can then be consumed by your source generator.

As you can see from the above listing, only a few properties are exposed, with `RootNamespace`, `ProjectDir`, and `TargetFramework` being the most interesting. So what if you want to read some _other_ MSBuild property? So that a consumer can specify "settings" for your generator, for example?

## [Enabling generator access for an MSBuild property](http://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/#enabling-generator-access-for-an-msbuild-property)

You've seen how to access a given MSBuild property, but you can _only_ access properties that are exposed in the generated editor config. To make an arbitrary MSBuild property appear in the editor config, you need to mark it as a "compiler aware property".

To achieve this, the target project must add a `<CompilerVisibleProperty>`. For example, if we add the following to a _.csproj_ file, it will set the value of the property `<EnableInterceptor>` and then mark it as compiler aware:

```
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>

    <!-- Set the value of the property -->
    <EnableInterceptor>true</EnableInterceptor>
  </PropertyGroup>

  <ItemGroup>
    <!-- Mark the property as compiler aware, so it's added to the editor config-->
    <CompilerVisibleProperty Include="EnableInterceptor" />
  </ItemGroup>
</Project>
```

If we inspect the generated editor config for this property we can see that the value has indeed been added, on the second line down:

```
is_global = true
build_property.EnableInterceptor = true # 👈 This was added
build_property.TargetFramework = net8.0
build_property.TargetPlatformMinVersion = 
build_property.UsingMicrosoftNETSdkWeb = 
build_property.ProjectTypeGuids = 
build_property.InvariantGlobalization = 
build_property.PlatformNeutralAssembly = 
build_property.EnforceExtendedAnalyzerRules = 
build_property._SupportedPlatformList = Linux,macOS,Windows
build_property.RootNamespace = MyExample
build_property.ProjectDir = C:\repos\MyExample\
build_property.EnableComHosting = 
build_property.EnableGeneratedComInterfaceComImportInterop = 
build_property.EffectiveAnalysisLevelStyle = 8.0
build_property.EnableCodeStyleSeverity =
```

Now that the property is exposed here, we can read it in our generator, and print it as a comment in the generated source, just as we did for RootNamespace:

```
[Generator]
public sealed class EnableInterceptorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var isEnabled = context
            .AnalyzerConfigOptionsProvider
            .Select((config, _) =>
                // Get the value, check if it's set to 'true', otherwise return false
                config.GlobalOptions
                    .TryGetValue($"build_property.EnableInterceptor", out var enableSwitch)
                  && enableSwitch.Equals("true", StringComparison.Ordinal));

        // Generate the source
        context.RegisterSourceOutput(isEnabled, static (spc, enabled) =>
        {
            var source = $$"""
                           // EnableInterceptor: {{enabled}}
                           """;

            spc.AddSource("Example.g.cs", source);
        });
    }
}
```

Sure enough, if we check the _Example.g.cs_ file in our target project, we'll see it looks like the following:

```
// EnableInterceptor: True
```

If we change `<EnableInterceptor>` to be `false`, or if we remove the `<CompilerVisibleProperty>` value, then the output changes to be:

```
// EnableInterceptor: False
```

In the example above I'm not doing anything particularly interesting with the setting value, but you could use it to control the generated source in a more detailed way, or to enable or disable features, for example.

OK, that's all pretty cool, but it kinda sucks that consumers have to specify both the property value `<EnableInterceptor>`_and_ add `<CompilerVisibleProperty>`. Luckily, if you're creating a NuGet package, you can use a NuGet feature to do that automatically.

## [Automatically adding `<CompilerVisibleProperty>` for a setting to a NuGet package](http://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/#automatically-adding-compilervisibleproperty-for-a-setting-to-a-nuget-package)

NuGet packages can contain many different _types_ of files. A non-exhaustive list includes:

*   Dlls that should be referenced by the target application.
*   Analyzers and source generators that are used by the compiler in the target project.
*   Content that is just copied directly to a target project's build output.
*   Build files, that change how the target project is compiled.

We're going to use that last feature to package a `.props` file with our NuGet package, which automatically adds the `<CompilerVisibleProperty>` we need. Overall this is a relatively simple feature: we create a file with the right name, pack it into a NuGet package, and any project consuming our NuGet package automatically gets the `.props` file added to it.

> You can read more about this NuGet feature [in the documentation](https://learn.microsoft.com/en-us/nuget/concepts/msbuild-props-and-targets).

In order to keep things more concrete, I'm going to switch to describing how things work in my [NetEscapades.EnumGenerators](https://github.com/andrewlock/NetEscapades.EnumGenerators) package, instead of the hypothetical generators I've described so far.

First of all, we create a file called `<Package>.props`, i.e. `NetEscapades.EnumGenerators.props`. In here we add the `<CompilerVisibleProperty>` definition we need to access from the generator:

```
<Project>  
  <ItemGroup>
    <CompilerVisibleProperty Include="EnableEnumGeneratorInterceptor" />
  </ItemGroup>
</Project>
```

This ensures the `EnableEnumGeneratorInterceptor` setting is available from the _NetEscapades.EnumGenerators_ source generator.

Next, we need to make sure we pack the _NetEscapades.EnumGenerators.props_ file into the correct place in the NuGet package. The following line in the _NetEscapades.EnumGenerators.csproj_ project file makes sure the file is packed into the `build` folder of the package:

```
<None Include="NetEscapades.EnumGenerators.props" Visible="false"
      Pack="true" PackagePath="build" />
```

And that's all there is too it! When you run `dotnet pack` on the project, the resulting NuGet package has the file in the correct place:

![Image 1: The NuGet package now includes the file](https://andrewlock.net/content/images/2025/generator_props.png)

What's more, any projects consuming the package can _just_ define the property, they don't need to mess around with `<CompilerVisibleProperty>`:

```
<Project>  
  <PropertyGroup>
    <EnableEnumGeneratorInterceptor>true</EnableEnumGeneratorInterceptor>
  </PropertyGroup>
</Project>
```

Much cleaner!

## [Enabling interception using a single property setting](http://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/#enabling-interception-using-a-single-property-setting)

When I was designing the interceptor support for _NetEscapades.EnumGenerators_ I had the goal of consumers only needing to set a single property in their _.csproj_ to enable the support. Unfortunately, the very design of interceptors makes that a little tricky.

Interceptors can be a little confusing, so they require an explicit opt-in from consuming projects. [Users must specify](https://github.com/dotnet/roslyn/blob/main/docs/features/interceptors.md#user-opt-in) the property `<InterceptorsNamespaces>` in their project, which is a list of namespaces that are allowed to contain interceptors. For example:

```
<InterceptorsNamespaces>$(InterceptorsNamespaces);Microsoft.AspNetCore.Http.Generated;</InterceptorsNamespaces>
```

That makes my goal for a "simple one property activation" a little tricky; are users are always going to have to update the main setting _and_`<InterceptorsNamespaces>` to use my interceptor? Well, yes and no.

In my solution to this issue, I took some inspiration from the built-in source generator interceptors that ship with the .NET runtime—the ASP.NET Core [minimal API interceptor](https://andrewlock.net/exploring-the-dotnet-8-preview-changing-method-calls-with-interceptors/#updating-the-minimal-api-generator-to-use-interceptors) and [the configuration binder interceptor](https://github.com/dotnet/runtime/tree/9cf8a80db3354173cfd25dea4660632861dee0eb/src/libraries/Microsoft.Extensions.Configuration.Binder). The configuration binder is particularly interesting, as it behaves exactly as I want: setting `<EnableConfigurationBindingGenerator>` to `true` automatically adds the required `<InterceptorsNamespaces>` values.

The configuration binder interceptor achieves this using some NuGet and MSBuild fun:

*   You can add MSBuild properties and items using a `.props` file in a NuGet file
*   However, customers can set the property in many different ways. That means you don't know exactly _when_ the value of the property be the "final" value.
*   Consequently, reading the value in a `.targets` file is the "safest" way to do this, as those are executed after all the various `.props` files have executed

Based on this, the following approach is what I opted for in my _NetEscapades.EnumGenerators_ package.

Add a `<Package>.targets` file (`NetEscapades.EnumGenerators.targets` in my case) containing a `<Target>` that runs before other defined targets, which sets the `<InterceptorsNamespaces>` property based on the value of the `<EnableEnumGeneratorInterceptor>` property:

```
<!-- An arbitrary name for the target, chosen to avoid collisions -->
<Project InitialTargets="NetEscapades_EnumGenerators_Interceptors">

  <!-- This is the targe that runs -->
  <Target Name="NetEscapades_EnumGenerators_Interceptors">

    <!-- If the user has set EnableEnumGeneratorInterceptor, modify the interceptor namespaces-->
    <PropertyGroup Condition="'$(EnableEnumGeneratorInterceptor)' == 'true'">

      <!-- Modifying both InterceptorsNamespaces and InterceptorsPreviewNamespaces, -->
      <!-- in case they're using an old version of the SDK that doesn't support the former -->
      <InterceptorsNamespaces>$(InterceptorsNamespaces);NetEscapades.EnumGenerators</InterceptorsNamespaces>
      <InterceptorsPreviewNamespaces>$(InterceptorsPreviewNamespaces);NetEscapades.EnumGenerators</InterceptorsPreviewNamespaces>
    </PropertyGroup>
  </Target>
</Project>
```

This file is then packaged into the NuGet in the same way as for the `.props` file:

```
<None Include="NetEscapades.EnumGenerators.targets" Visible="false"
      Pack="true" PackagePath="build" />
```

Which places it in the correct place in the package:

![Image 2: The NuGet package now includes the targets file too](https://andrewlock.net/content/images/2025/generator_props_2.png)

and now customers have a lovely clean behaviour: they need only to set the following in their project, and the `.props` and `.targets` work to set up everything else for them:

```
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <!-- Add this 👇 -->
    <EnableEnumGeneratorInterceptor>true</EnableEnumGeneratorInterceptor>
  </PropertyGroup>
</Project>
```

and voila, interceptors!

I haven't gone into details about the work that needs to happen in the generator to correctly enable and disable the functionality, but ultimately it comes down to reading the `build_property.EnableEnumGeneratorInterceptor` value I showed earlier. We can then bail out of interception if it's not required. If you want to see all the details, you can find the [code on GitHub](https://github.com/andrewlock/NetEscapades.EnumGenerators/blob/ce553416b9aba0a7101c559aac9d18d7b1c1805c/src/NetEscapades.EnumGenerators/EnumGenerator.cs#L53)

## [Summary](http://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/#summary)

In this post I described how you can read MSBuild properties from a source generator, using the `AnalyzerConfigOptionsProvider`. I showed that you need to prefix the properties with `build_property.` and explained why this is necessary, based on the _.editorconfig_ file. Next I showed how you can add arbitrary additional properties to this file using `<CompilerVisibleProperty>` so you can read them from your source generators.

Finally, I showed how you can add `.props` and `.targets` files to your NuGet packages so that consumers of your package don't need to add these properties themselves. I also showed a specific example of using a `.targets` file to add additional values to the `<InterceptorsNamespaces>` property, based on the value of a different property.

[![Image 3: Reading compilation options and the C# version in source generators](https://andrewlock.net/content/images/2025/lang_version_banner.webp)Previous Reading compilation options and the C# version in source generators: Creating a source generator - Part 12](https://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/)[![Image 4: Supporting multiple .NET SDK versions in analyzers and source generators](https://andrewlock.net/content/images/2025/system_text_json_banner.png)Next Supporting multiple .NET SDK versions in analyzers and source generators](https://andrewlock.net/supporting-multiple-sdk-versions-in-analyzers-and-source-generators/)

Andrew Lock | .Net Escapades

![Image 5](https://andrewlock.net/assets/img/icons/apple/apple-touch-icon-180x180.png)Want an email when

there's new posts?
