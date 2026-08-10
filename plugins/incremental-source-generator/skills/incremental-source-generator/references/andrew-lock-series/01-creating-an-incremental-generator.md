Title: Creating an incremental generator: Creating a source generator - Part 1

URL Source: http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/

Published Time: 2021-12-09T10:00:00.0000000

Markdown Content:
December 09, 2021 ~15 min read

[Creating a source generator - Part 1](https://andrewlock.net/series/creating-a-source-generator/)

This is the first post in the series: [Creating a source generator](https://andrewlock.net/series/creating-a-source-generator/).

1.   Part 1 - Creating an incremental generator (this post) 
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
13.   [Part 13 - Accessing MSBuild properties and user configuration from source generators](https://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/)
14.   [Part 14 - Supporting multiple .NET SDK versions in a source generator](https://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/)
15.   [Part 15 - Solving the source generator 'marker attribute' problem in .NET 10](https://andrewlock.net/exploring-dotnet-10-preview-features-4-solving-the-source-generator-marker-attribute-problem-in-dotnet-10/)

> This post is my contribution to [the .NET Advent calendar](https://dotnet.christmas/) be sure to check there for other great posts!

In this post I describe how to create an incremental source generator. As a case study, I describe a source generator for generating an extension method for `enum`s called `ToStringFast()`. This method is _much_ faster than the built-in `ToString()` equivalent, and using a source generator means it's just as easy to use!

> This is based on a source generator I created recently called NetEscapades.EnumGenerators. You can find it [on GitHub](https://github.com/andrewlock/NetEscapades.EnumGenerators/) or [on NuGet](https://www.nuget.org/packages/NetEscapades.EnumGenerators/).

I start by providing a small amount of background on source generators, and the problem with calling `ToString()` on an `enum`. For the remainder of the post, I walk step by step through creating an incremental generator. The final result is a working source generator, though with limitations, as I describe at the end of the post.

1.   [Creating the Source generator project](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#1-creating-the-source-generator-project)
2.   [Collecting details about enums](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#2-collecting-details-about-enums)
3.   [Adding a marker attribute](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#3-adding-a-marker-attribute)
4.   [Creating the incremental source generator](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#4-creating-the-incremental-source-generator)
5.   [Building the incremental generator pipeline](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#5-building-the-incremental-generator-pipeline)
6.   [Implementing the pipeline stages](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#6-implementing-the-pipeline-stages)
7.   [Parsing the EnumDeclarationSyntax to create an EnumToGenerate](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#7-parsing-the-enumdeclarationsyntax-to-create-an-enumtogenerate)
8.   [Generating the source code](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#8-generating-the-source-code)
9.   [Limitations](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#limitations)

## [Background: source generators](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#background-source-generators)

Source generators were added as a built-in feature in .NET 5. They perform code generation at compile time, providing the ability to add source code to your project automatically. This opens up a [vast realm of possibilities](https://github.com/amis92/csharp-source-generators), but the ability to use source generators to replace things that would otherwise need to be done using reflection is a firm favourite.

I've written many posts about source generators already, for example:

*   [Using source generators to find all routable components in a Blazor WebAssembly app](https://andrewlock.net/using-source-generators-to-find-all-routable-components-in-a-webassembly-app/)
*   [Improving logging performance with source generators](https://andrewlock.net/exploring-dotnet-6-part-8-improving-logging-performance-with-source-generators/)
*   [Source generator updates: incremental generators](https://andrewlock.net/exploring-dotnet-6-part-9-source-generator-updates-incremental-generators/)

If you're completely new to source generators, [I recommend this intro to source generators talk by Jason Bock](https://www.youtube.com/watch?v=4DVV7FXukC8&list=PLdo4fOcmZ0oVFtp9MDEBNbA2sSqYvXSXO&index=77&t=71s) given at .NET Conf. It's only half an hour (he has a longer version of the talk too) and it will get you up and running quickly.

In .NET 6, a new API was introduced for creating "incremental generators". These have broadly the same functionality as the source generators in .NET 5, but they are designed to take advantage of caching to significantly improve performance, so that your IDE doesn't slow down! The main downside to incremental generators is that they are only supported in the .NET 6 SDK (and so only in VS 2022).

## [The domain: enums and `ToString()`](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#the-domain-enums-and-tostring-)

The simple `enum` in c# is a handy little think for representing a choice of options. Under the hood, it's represented by a numeric value (typically an `int`), but instead of having to remember in your code that `0` represents "Red" and `1` represents "Blue", you can use an enum that holds that information for you:

```
public enum Colour // Yes, I'm British
{
    Red = 0,
    Blue = 1,
}
```

In your code, you pass instances of the enum `Colour` around, but behind the scenes the runtime really just uses an `int`. The trouble is, sometimes you want to get the name of the colour. The built-in way to do that is to call `ToString()`

```
public void PrintColour(Colour colour)
{
    Console.Writeline("You chose "+ colour.ToString()); // You chose Red
}
```

That probably is all well known to anyone reading this post. But it's maybe less common knowledge that this is _sloooow_. We'll look at how slow shortly, but first we'll look at a fast implementation, using modern C#:

```
public static class EnumExtensions
{
    public string ToStringFast(this Colour colour)
        => colour switch
        {
            Colour.Red => nameof(Colour.Red),
            Colour.Blue => nameof(Colour.Blue),
            _ => colour.ToString(),
        }
    }
}
```

This simple switch statement checks for each of the known values of `Colour` and uses `nameof` to return the textual representation of the `enum`. If it's an unknown value, then the underlying value is returned as a string.

> You always have to be careful about these unknown values: for example this is valid C# `PrintColour((Colour)123)`

If we compare this simple switch statement to the default `ToString()` implementation using [BenchmarkDotNet](https://benchmarkdotnet.org/) for a known colour, you can see how much faster our implementation is:

```
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19042.1348 (20H2/October2020Update)
Intel Core i7-7500U CPU 2.70GHz (Kaby Lake), 1 CPU, 4 logical and 2 physical cores
  DefaultJob : .NET Framework 4.8 (4.8.4420.0), X64 RyuJIT
.NET SDK=6.0.100
  DefaultJob : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT
```

| Method | FX | Mean | Error | StdDev | Ratio | Gen 0 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- |
| EnumToString | `net48` | 578.276 ns | 3.3109 ns | 3.0970 ns | 1.000 | 0.0458 | 96 B |
| ToStringFast | `net48` | 3.091 ns | 0.0567 ns | 0.0443 ns | 0.005 | - | - |
| EnumToString | `net6.0` | 17.9850 ns | 0.1230 ns | 0.1151 ns | 1.000 | 0.0115 | 24 B |
| ToStringFast | `net6.0` | 0.1212 ns | 0.0225 ns | 0.0199 ns | 0.007 | - | - |

First off, it's worth pointing out that `ToString()` in .NET 6 is over 30× faster and allocates only a quarter of the bytes than the method in .NET Framework! Compare that to the "fast" version though, and it's still super slow!

As fast as it is, creating the `ToStringFast()` method is a bit of a pain, as you have to make sure to keep it up to date as your enum changes. Luckily, that's a perfect usecase for a source generator!

> I'm aware of a couple of enum generators from the community, namely [this one](https://github.com/Spinnernicholas/EnumFastToStringDotNet) and [this one](https://github.com/meziantou/Meziantou.Framework/blob/main/src/Meziantou.Framework.FastEnumToStringGenerator/EnumToStringSourceGenerator.cs), but neither of them did quite I wanted, [so I made my own](https://github.com/andrewlock/NetEscapades.EnumGenerators)!

In this post, we'll walk through creating a source generator to generate the `ToStringFast()` method, using the new incremental source generators supported in the .NET 6 SDK.

## [1. Creating the Source generator project](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#1-creating-the-source-generator-project)

To get started we need to create a C# project. Source generators must target `netstandard2.0`, and you'll need to add some standard packages to get access to the source generator types.

Start by creating a class library. The following uses the sdk to create a solution and a project in the current folder:

```
dotnet new sln -n NetEscapades.EnumGenerators
dotnet new classlib -o ./src/NetEscapades.EnumGenerators
dotnet sln add ./src/NetEscapades.EnumGenerators
```

Replace the contents of _NetEscapades.EnumGenerators.csproj_ with the following. I've described what each of the properties do in comments:

```
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <!-- 👇 Source generators must target netstandard 2.0 -->
    <TargetFramework>netstandard2.0</TargetFramework> 
    <!-- 👇 We don't want to reference the source generator dll directly in consuming projects -->
    <IncludeBuildOutput>false</IncludeBuildOutput> 
    <!-- 👇 New project, why not! -->
    <Nullable>enable</Nullable>
    <ImplicitUsings>true</ImplicitUsings>
    <LangVersion>Latest</LangVersion>
  </PropertyGroup>

  <!-- The following libraries include the source generator interfaces and types we need -->
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.2" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.0.1" PrivateAssets="all" />
  </ItemGroup>

  <!-- This ensures the library will be packaged as a source generator when we use `dotnet pack` -->
  <ItemGroup>
    <None Include="$(OutputPath)\$(AssemblyName).dll" Pack="true" 
        PackagePath="analyzers/dotnet/cs" Visible="false" />
  </ItemGroup>
</Project>
```

This is pretty much just all boilerplate for now, so lets get onto the code.

## [2. Collecting details about enums](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#2-collecting-details-about-enums)

Before we build the generator itself, lets consider the extension method we're trying to create. At a minimum, we need to know:

*   The full `Type` name of the `enum`
*   The name of all the values

And that's pretty much it. There's lots of more information we could collect for a better user experience, but for now, we'll stick with that, to get something working. Given that, we can create a simple type to hold the details about the enums we discover:

```
public readonly record struct EnumToGenerate
{
    public readonly string Name;
    public readonly EquatableArray<string> Values;

    public EnumToGenerate(string name, List<string> values)
    {
        Name = name;
        Values = new(values.ToArray());
    }
}
```

This type uses a `record` so that it implements `IEquatable<EnumToGenerate>` automatically, which is important for performance reasons, as discussed in post 9 in this series. It also uses [the custom `EquatableArray<T>` type](https://github.com/andrewlock/blog-examples/blob/master/NetEscapades.EnumGenerators/src/NetEscapades.EnumGenerators/EquatableArray.cs) which wraps an array and implements structural equality based on its contents.

## [3. Adding a marker attribute](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#3-adding-a-marker-attribute)

We also need to think about how we are going to choose which enums to generate the extension methods for. We could do it for every enum in the project, but that seems a bit overkill. Instead, we could use a "marker attribute". A marker attribute is a simple attribute that doesn't have any functionality, and only exists so that something else (in this case, our source generator) can locate the type. Users would decorate their enum with the attribute, so we know to generate the extension method for it:

```
[EnumExtensions] // Our marker attribute
public enum Colour
{
    Red = 0,
    Blue = 1,
}
```

We'll create a simple marker attribute as shown below, but we're not going to define this attribute in code directly. Rather, we're creating a string _containing_ c# code for the `[EnumExtensions]` marker attribute. We'll make the source generator automatically add this to the compilation of consuming projects at runtime so the attribute is available.

```
public static class SourceGenerationHelper
{
    public const string Attribute = @"
namespace NetEscapades.EnumGenerators
{
    [System.AttributeUsage(System.AttributeTargets.Enum)]
    public class EnumExtensionsAttribute : System.Attribute
    {
    }
}";
}
```

We'll be adding more to this `SourceGenerationHelper` class later, but for now it's time to create the actual generator itself.

## [4. Creating the incremental source generator](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#4-creating-the-incremental-source-generator)

To create an incremental source generator, you need to do 3 things:

1.   Include the _Microsoft.CodeAnalysis.CSharp_ package in your project. Note that incremental generators were introduced in version 4.0.0, and are only supported in .NET 6+/VS 2022.
2.   Create a class that implements `IIncrementalGenerator`
3.   Decorate the class with the `[Generator]` attribute

We've already done the first step, so let's create our `EnumGenerator` implementation:

```
namespace NetEscapades.EnumGenerators;

[Generator]
public class EnumGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Add the marker attribute to the compilation
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
            "EnumExtensionsAttribute.g.cs", 
            SourceText.From(SourceGenerationHelper.Attribute, Encoding.UTF8)));

        // TODO: implement the remainder of the source generator
    }
}
```

`IIncrementalGenerator` only requires you implement a single method, `Initialize()`. In this method you can register your "static" source code (like the marker attributes), as well as build a pipeline for identifying syntax of interest, and transforming that syntax into source code.

In the implementation above, I've already added the code that registers our marker attribute to the compilation. In the next section we'll build up the code to identify enums that have been decorated with the marker attribute.

## [5. Building the incremental generator pipeline](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#5-building-the-incremental-generator-pipeline)

One of the key things to remember when building source generators, is that there are a _lot_ of changes happening when you're writing source code. Every change the user makes could trigger the source generator to run again, so you _have_ to be efficient, otherwise you're going to kill the user's IDE experience

> This isn't just anecdotal, the preview versions of the `[LoggerMessage]` generator ran into [exactly this problem](https://github.com/dotnet/runtime/issues/56702).

The [design of incremental generators](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md) is to create a "pipeline" of transforms and filters, memoizing the results at each layer to avoid re-doing work if there are no changes. It's important that the stage of the pipeline is very efficient, as this will be called a _lot_, ostensibly for every source code change. Later layers need to remain efficient, but there's more leeway there. If you've designed your pipeline well, later layers will only be called when users are editing code that matters to you.

> I wrote about this design [in a recent blog post](https://andrewlock.net/exploring-dotnet-6-part-9-source-generator-updates-incremental-generators/).

With that in mind (and taking inspiration from the `[LoggerMessage]` generator) we'll create a simple generator pipeline that does the following:

*   **Filter syntax to only `enum`s which have one or more attributes**. This should be very fast, and will contain all the enums we're interested in.
*   **Extra data model for only `enum`s which have the**`[EnumExtensions]`**attribute**. This is slightly more costly than the first stage, as it uses the semantic model (not just syntax), but it returns the `EnumToGenerate` data model, which is highly cacheable.
*   **Use the data model to generate the extension code for each enum**. This is where we generate the source code based on an `EnumToGenerate` instance, and register it as a source generator output.

> ⚠ In the .NET 7 SDK there is a new, more efficient, API for doing the first two steps, `ForAttributeWithMetadataName()`. I discuss this API in part 9 of this series, or you can see the usage in the example source code (and below)

In code, the pipeline is shown below. The three steps above correspond to the `IsSyntaxTargetForGeneration()`, `GetSemanticTargetForGeneration()` and `Execute()` methods respectively, which we'll show in the next section.

```
namespace NetEscapades.EnumGenerators;

[Generator]
public class EnumGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Add the marker attribute
        context.RegisterPostInitializationOutput(static ctx => ctx.AddSource(
            "EnumExtensionsAttribute.g.cs", SourceText.From(SourceGenerationHelper.Attribute, Encoding.UTF8)));

        // Do a simple filter for enums
        IncrementalValuesProvider<EnumToGenerate?> enumsToGenerate = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s), // select enums with attributes
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx)) // select enums with the [EnumExtensions] attribute and extract details
            .Where(static m => m is not null); // Filter out errors that we don't care about

        // If you're targeting the .NET 7 SDK, use this version instead:
        // IncrementalValuesProvider<EnumToGenerate?> enumsToGenerate = context.SyntaxProvider
        //     .ForAttributeWithMetadataName(
        //         "NetEscapades.EnumGenerators.EnumExtensionsAttribute",
        //         predicate: static (s, _) => true,
        //         transform: static (ctx, _) => GetEnumToGenerate(ctx.SemanticModel, ctx.TargetNode))
        //     .Where(static m => m is not null);

        // Generate source code for each enum found
        context.RegisterSourceOutput(enumsToGenerate,
            static (spc, source) => Execute(source, spc));
    }
}
```

The first stage of the pipeline uses `CreateSyntaxProvider()` to filter the incoming list of syntax tokens. The predicate, `IsSyntaxTargetForGeneration()`, provides a first layer of filtering. We use the transform, `GetSemanticTargetForGeneration()`, to provide additional filtering after the predicate and to transform into our `EnumToGenerate` data model. The subsequent `Where()` clause looks like LINQ, but it's actually a method on `IncrementalValuesProvider` which does that second layer of filtering for us.

> Remember, in the .NET 7 SDK, there is a new, more efficient API for doing the first two steps, `ForAttributeWithMetadataName()`. If you can target the .NET 7+ SDK, then you should always use this API. I discuss it further in part 9 of this series.

In the final step of the pipeline we run the `Execute()` method for each `enum` instance we discover to generate the `EnumExtensions` class.

Now let's take a look at each of those methods.

### [6. Implementing the pipeline stages](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#6-implementing-the-pipeline-stages)

The first stage of the pipeline needs to be very fast, so we operate solely on the `SyntaxNode` passed in, filtering down to select only `EnumDeclarationSyntax` nodes, which have at least one attribute:

```
static bool IsSyntaxTargetForGeneration(SyntaxNode node)
    => node is EnumDeclarationSyntax m && m.AttributeLists.Count > 0;
```

As you can see, this is a very efficient predicate. It's using a simple pattern match to check the type of the node, and checking the properties.

> In C# 10 you could also write that as `node is EnumDeclarationSyntax { AttributeLists.Count: > 0 }`, but personally I prefer the former.

After this efficient filtering has run, we can be a bit more critical. We don't want _any_ attribute, we only want our specific marker attribute. In `GetSemanticTargetForGeneration()` we loop through each of the nodes that passed the previous test, and look for our marker attribute. If the node has the attribute, we use it to create a `EnumToGenerate`, our data-model type. If the enum doesn't have the marker attribute, we return `null`, and filter it out in the next stage.

```
static EnumToGenerate? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
{
    // we know the node is a EnumDeclarationSyntax thanks to IsSyntaxTargetForGeneration
    var enumDeclarationSyntax = (EnumDeclarationSyntax)context.Node;

    // loop through all the attributes on the method
    foreach (AttributeListSyntax attributeListSyntax in enumDeclarationSyntax.AttributeLists)
    {
        foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
        {
            if (context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol is not IMethodSymbol attributeSymbol)
            {
                // weird, we couldn't get the symbol, ignore it
                continue;
            }

            INamedTypeSymbol attributeContainingTypeSymbol = attributeSymbol.ContainingType;
            string fullName = attributeContainingTypeSymbol.ToDisplayString();

            // Is the attribute the [EnumExtensions] attribute?
            if (fullName == "NetEscapades.EnumGenerators.EnumExtensionsAttribute")
            {
                // return the enum. Implementation shown in section 7.
                return GetEnumToGenerate(context.SemanticModel, enumDeclarationSyntax);
            }
        }
    }

    // we didn't find the attribute we were looking for
    return null;
}
```

> Note that we're still trying to be efficient where we can, so we're using `foreach` loops, rather than LINQ. I haven't shown the `GetEnumToGenerate()` method which converts the syntax node into our `EnumToGenerate` data model yet, we'll look at that in the next section.

After we've run this stage of the pipeline, we will have an `EnumToGenerate` provider, which returns a new `EnumToGenerate` instance for each `enum` decorated with our attribute. In the `Execute` method, we pass that to our `SourceGenerationHelper` class to generate the source code, and add it to the compilation output.

```
static void Execute(EnumToGenerate? enumToGenerate, SourceProductionContext context)
{
    if (enumToGenerate is { } value)
    {
        // generate the source code and add it to the output
        string result = SourceGenerationHelper.GenerateExtensionClass(value);
        // Create a separate partial class file for each enum
        context.AddSource($"EnumExtensions.{value.Name}.g.cs", SourceText.From(result, Encoding.UTF8));
    }
}
```

We're getting close now, we just have two more methods to fill in:`GetEnumToGenerate()`, and `SourceGenerationHelper.GenerateExtensionClass()`.

## [7. Parsing the EnumDeclarationSyntax to create an EnumToGenerate](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#7-parsing-the-enumdeclarationsyntax-to-create-an-enumtogenerate)

The `GetEnumToGenerate()` method is where most of the typical work associated with working with Roslyn happens. We need to use the combination of the syntax tree and the `SemanticModel` to get the details we need, namely:

*   The full type name of the `enum`
*   The name of all the values in the `enum`

```
static EnumToGenerate? GetEnumToGenerate(SemanticModel semanticModel, SyntaxNode enumDeclarationSyntax)
{
    // Get the semantic representation of the enum syntax
    if (semanticModel.GetDeclaredSymbol(enumDeclarationSyntax) is not INamedTypeSymbol enumSymbol)
    {
        // something went wrong
        return null;
    }

    // Get the full type name of the enum e.g. Colour, 
    // or OuterClass<T>.Colour if it was nested in a generic type (for example)
    string enumName = enumSymbol.ToString();

    // Get all the members in the enum
    ImmutableArray<ISymbol> enumMembers = enumSymbol.GetMembers();
    var members = new List<string>(enumMembers.Length);

    // Get all the fields from the enum, and add their name to the list
    foreach (ISymbol member in enumMembers)
    {
        if (member is IFieldSymbol field && field.ConstantValue is not null)
        {
            members.Add(member.Name);
        }
    }

    return new EnumToGenerate(enumName, members);
}
```

The only thing remaining is to actually generate the source code from our `EnumToGenerate`!

## [8. Generating the source code](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#8-generating-the-source-code)

The final method `SourceGenerationHelper.GenerateExtensionClass()` shows how we take our list of `EnumToGenerate`, and generate the `EnumExtensions` class. This one is relatively simple conceptually (though a little hard to visualise!), as it's just building up a string:

```
public static string GenerateExtensionClass(EnumToGenerate enumToGenerate)
{
    var sb = new StringBuilder();
    sb.Append(@"
namespace NetEscapades.EnumGenerators
{
    public static partial class EnumExtensions
    {");
    sb.Append(@"
            public static string ToStringFast(this ").Append(enumToGenerate.Name).Append(@" value)
                => value switch
                {");
    foreach (var member in enumToGenerate.Values)
    {
        sb.Append(@"
            ").Append(enumToGenerate.Name).Append('.').Append(member)
            .Append(" => nameof(")
            .Append(enumToGenerate.Name).Append('.').Append(member).Append("),");
    }

    sb.Append(@"
                _ => value.ToString(),
            };
");
    
    sb.Append(@"
    }
}");

    return sb.ToString();
}
```

And we're done! We now have a fully functioning source generator. Adding the source generator to a project containing the `Colour` enum from the start of the post will create an extension method like the following:

```
public static class EnumExtensions
{
    public string ToStringFast(this Colour colour)
        => colour switch
        {
            Colour.Red => nameof(Colour.Red),
            Colour.Blue => nameof(Colour.Blue),
            _ => colour.ToString(),
        }
    }
}
```

## [Limitations](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#limitations)

With your source generator complete, you can package it up by running `dotnet pack -c Release`, and upload to NuGet!

Hold on, don't actually do that.

There's a whole load of limitations to this code, not least the fact we haven't actually tested it yet. Off the top of my head:

*   The `EnumExtensions` class is always called the same thing, and is always in the same namespace. It would be nice for the user to be able to control that
*   We haven't considered the visibility of the `enum`. If the `enum` is `internal`, the generated code won't compile, as it's a `public` extension method
*   We should mark the code as autogenerated and with `#nullable enable` as the code formatting might not match the project conventions
*   We haven't tested it, so we don't know if it actually works!
*   Adding marker attributes directly to the compilation can sometimes be an issue, more about this one in a later post.

That said, this has hopefully still been useful. I will address many of the above issues in future posts, but the code in this post should provide a good framework if you're looking to create your own incremental generator.

## [Summary](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#summary)

In this post I described all the steps required to create an incremental generator. I showed how to create the project file, how to add a marker attribute to the compilation, how to implement `IIncrementalGenerator`, and how to keep performance in mind to ensure consumers of your generator don't experience lag in their IDE. The resulting implementation has many limitations, but it shows the basic process. In future posts in this series, we'll address many of these limitations.

You can find my [NetEscapades.EnumGenerators project on GitHub](https://github.com/andrewlock/NetEscapades.EnumGenerators), and the source code for the basic stripped down version of it used in this post [in my blog samples](https://github.com/andrewlock/blog-examples/tree/master/NetEscapades.EnumGenerators).

[![Image 1: Upgrading a .NET 5 "Startup-based" app to .NET 6](https://andrewlock.net/content/images/2021/net5_vs_net6.png)Previous Upgrading a .NET 5 "Startup-based" app to .NET 6: Exploring .NET 6 - Part 12](https://andrewlock.net/exploring-dotnet-6-part-12-upgrading-a-dotnet-5-startup-based-app-to-dotnet-6/)[![Image 2: Testing an incremental generator with snapshot testing](https://andrewlock.net/content/images/2021/snapshot_source_gen_banner.png)Next Testing an incremental generator with snapshot testing: Creating a source generator - Part 2](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/)
