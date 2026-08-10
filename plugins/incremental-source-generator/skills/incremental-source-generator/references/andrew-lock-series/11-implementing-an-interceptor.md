Title: Implementing an interceptor with a source generator: Creating a source generator - Part 11

URL Source: http://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/

Published Time: 2025-01-07T09:00:00.0000000

Markdown Content:
January 07, 2025 ~11 min read

[Creating a source generator - Part 11](https://andrewlock.net/series/creating-a-source-generator/)

This is the eleventh post in the series: [Creating a source generator](https://andrewlock.net/series/creating-a-source-generator/).

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
11.   Part 11 - Implementing an interceptor with a source generator (this post) 
12.   [Part 12 - Reading compilation options and the C# version in source generators](https://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/)
13.   [Part 13 - Accessing MSBuild properties and user configuration from source generators](https://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/)
14.   [Part 14 - Supporting multiple .NET SDK versions in a source generator](https://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/)
15.   [Part 15 - Solving the source generator 'marker attribute' problem in .NET 10](https://andrewlock.net/exploring-dotnet-10-preview-features-4-solving-the-source-generator-marker-attribute-problem-in-dotnet-10/)

In this post I describe how you can use the interceptor support available in the .NET 8 and .NET 9 SDK to replace method calls at build time. The APIs have evolved a little since they were first introduced in .NET 8, so in this post I show how to use the new APIs with a source generator.

> This post is a follow on [from a recent post](https://andrewlock.net/recent-updates-for-netescapades-enumgenerators-interceptors/) in which I described the preview interceptor support I added to my [NetEscapades.EnumGenerators NuGet package](https://www.nuget.org/packages/NetEscapades.EnumGenerators).

I start by explaining what interceptors are, why they can be useful, and scenarios where they're already being used. I then show how the new interceptor APIs work, and how you can use them in your own source generators.

## [What are interceptors, and why do we need them?](http://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/#what-are-interceptors-and-why-do-we-need-them-)

Interceptors are an interesting new feature, first introduced in experimental form in C#12, that allow you to replace (or "intercept") a method call in your application with an alternative method. When your app is compiled, the compiler automatically "swaps out" the call to the original method with your substitute.

An obvious question here is _why_ would you want to do that? Why not just call the substitute method directly? In general, that is the simplest solution, but in reality that may not be possible. There's a lot of existing code written, and asking everyone to update their code to use new APIs isn't necessarily feasible.

But why would it be so urgent to use new APIs?

The main reason is ahead-of-time compilation (AOT) which [I've discussed several times in a previous series](https://andrewlock.net/exploring-the-dotnet-8-preview-the-minimal-api-aot-template/). Interceptors aren't _specifically_ for AOT, but it's one of the clearest use cases. By using interceptors you could take code which previously wasn't AOT friendly, and replace it with an AOT-friendly source-generated version.

Customer's don't need to change their code, the source generator just automatically "upgrades" the method calls to use source-generated versions of the APIs that are compatible with AOT instead. If that sounds familiar, it's because that's exactly what the [configuration source generator](https://andrewlock.net/exploring-the-dotnet-8-preview-using-the-new-configuration-binder-source-generator/) and [existing minimal API source generator](https://andrewlock.net/exploring-the-dotnet-8-preview-exploring-the-new-minimal-api-source-generator/) do in .NET 8 and later.

Interceptors are particularly interesting because they're the one case where source generators can be used to _change_ existing code. Normally source generators can only _add_ additional code.

> For a video discussion of interceptors, [there was a great discussion a year ago on the .NET community standup a year or so ago](https://www.youtube.com/watch?v=X1_QeH1yAto&list=PLdo4fOcmZ0oX-DBuRG4u58ZTAJgBAeQ-t&index=12). It's a little out of date now, but I still recommend it.

That's all quite abstract, so in the next section I'll show a very simple example of an interceptor, so that you can understand the mechanics of how they work.

## [An interceptor in practice](http://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/#an-interceptor-in-practice)

To demonstrate how interceptors work, I'll show a small example using my [NetEscapades.EnumGenerators NuGet package](https://www.nuget.org/packages/NetEscapades.EnumGenerators). This package provides an `[EnumExtensions]` attribute that you can apply to an enum. [When enabled, it can also generate interceptors for calls to `ToString()`](https://andrewlock.net/recent-updates-for-netescapades-enumgenerators-interceptors/).

### [The original example without interceptors](http://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/#the-original-example-without-interceptors)

Lets say you have the following simple program. It defines an enum, `MyEnum`, which is decorated with the `[EnumExtensions]` attribute. The program itself simply creates an instance of the enum, and calls `ToString()` on it:

```
using NetEscapades.EnumGenerators;

var value = MyEnum.First;
Console.WriteLine(value.ToString());

[EnumExtensions]
internal enum MyEnum
{
    First,
    Second,
    Third,
}
```

Without an interceptor, this code calls the `ToString()` method defined on the base `Enum` type. This method is historically surprisingly slow (more on that later), so _NetEscapades.EnumGenerators_ provides an alternative, faster, implementation:

```
internal static partial class MyEnumExtensions
{
    public static string ToStringFast(this MyEnum value)
        => value switch
        {
            MyEnum.First => nameof(MyEnum.First),
            MyEnum.Second => nameof(MyEnum.Second),
            _ => value.ToString(),
        };
}
```

This implementation is significantly faster, and allocates less than the runtime's `ToString()` implementation:

```
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19042.1348 (20H2/October2020Update)
Intel Core i7-7500U CPU 2.70GHz (Kaby Lake), 1 CPU, 4 logical and 2 physical cores
  DefaultJob : .NET Framework 4.8 (4.8.4420.0), X64 RyuJIT
.NET SDK=6.0.100
  DefaultJob : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT
```

| Method | FX | Mean | Error | StdDev | Ratio | Gen 0 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- |
| ToString | `net48` | 578.276ns | 3.3109ns | 3.0970ns | 1.000 | 0.0458 | 96 B |
| ToStringFast | `net48` | 3.091ns | 0.0567ns | 0.0443ns | 0.005 | - | - |
| ToString | `net6.0` | 17.9850ns | 0.1230ns | 0.1151ns | 1.000 | 0.0115 | 24 B |
| ToStringFast | `net6.0` | 0.1212ns | 0.0225ns | 0.0199ns | 0.007 | - | - |

The obvious downside is that you have to remember to _use_ the `ToStringFast()` method, with interceptors we can automate that instead!

### [Enabling interceptor support](http://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/#enabling-interceptor-support)

If you [enable the interceptor support](https://andrewlock.net/recent-updates-for-netescapades-enumgenerators-interceptors/), the _NetEscapades.EnumGenerators_ library doesn't just emit extension methods, it also emits interceptor code.

> Note that to use the interceptor support described in this post, you must be building with the 8.0.4xx SDK (or 9.0.x SDK) at a minimum. Earlier versions of the SDK include now-deprecated APIs, but only the later SDKs include the APIs used in this post.

The _NetEscapades.EnumGenerators_ library generates interceptor code similar to the following:

```
static file class EnumInterceptors
{
    [InterceptsLocation(version: 1, data: "yxKJBEhzkHdnMhHGENjk8qgBAABQcm9ncmFtLmNz")] // Program.cs(4,24)
    public static string MyEnumExtensionsToString(this Enum value)
        => MyEnumExtensions.ToStringFast((MyEnum)value);
}
```

The `[InterceptsLocation]` attribute is what makes the `MyEnumExtensionsToString()` method an _interceptor_. At compile time, the compiler replaces the call to `ToString()` with the call to `MyEnumExtensionsToString()`, something like the following:

```
Console.WriteLine(EnumInterceptors.MyEnumExtensionsToString(value));
```

The library automatically detects all the calls to `MyEnum.ToString()` and adds an `[InterceptsLocation]` attribute for each instance. That means you don't have to remember to call `ToStringFast()` yourself, you can just let the interceptor handle it itself!

### [Emitting the `InterceptsLocationAttribute` definition](http://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/#emitting-the-interceptslocationattribute-definition)

The compiler looks for the `[InterceptsLocation]` attribute to identify interceptors, but the library itself _also_ emits the `[InterceptsLocation]` attribute, rather than relying on the attribute to be defined in the runtime. This is a similar pattern as used for many other compiler features, as it does not tie the feature to a specific runtime version.

When generating the enum interceptors, the _NetEscapades.EnumGenerators_ library also generates the `[InterceptsLocation]` attribute definition itself, as shown below:

```
using System;
using System.Diagnostics;

#nullable enable
namespace System.Runtime.CompilerServices
{
    // this type is needed by the compiler to implement interceptors,
    // it doesn't need to come from the runtime itself

    [Conditional("DEBUG")] // not needed post-build, so can evaporate it
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    sealed file class InterceptsLocationAttribute : Attribute
    {
        public InterceptsLocationAttribute(int version, string data)
        {
            _ = version;
            _ = data;
        }
    }
}
```

The attribute is generated above as a `file` scoped attribute ([as suggested in the docs](https://github.com/dotnet/roslyn/blob/98ea496177bc8e607dbf454abd6b5a5e4678aed2/docs/features/interceptors.md#interceptslocationattribute)), so that multiple generators can generate it separately if necessary.

The arguments to the `[InterceptsLocation]` attribute are:

*   `version`: A version number defining the encoding. Only version `1` is currently defined, but the compiler may introduce new encodings in the future
*   `data`: An encoding defining the location to intercept, that's not intended to be human-readable. The version `1` encoding is a base64-encoding of the following: 
    *   16 byte xxHash128 content checksum of the file containing the intercepted call.
    *   `int32` in little-endian format for the position (i.e. `SyntaxNode.Position`) of the call in syntax.
    *   utf-8 string data containing a file name, used for error reporting.

> Note that these argument is a big change from the _previous_ experimental interceptor `[InterceptsLocation]` attribute, which used the human-readable values `[InterceptsLocation(path, line, column)]`. This path-based API has been deprecated, and will be removed in the future. This also means interceptors are _only_ really feasible for use with source generators now.

If it looks difficult to generate the `data` argument correctly, then don't worry, you don't have to! Instead, there's a Roslyn API you can use to generate the required `data` value from a source generator.

## [Implementing an interceptor in a source generator](http://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/#implementing-an-interceptor-in-a-source-generator)

Now we've covered all the background, I'll describe how I implemented the interceptor support in the `NetEscapades.EnumGenerators` source generator. I'm going to assume you're familiar with source generators in general ([see the earlier posts in this series if not](https://andrewlock.net/series/creating-a-source-generator/)!), and will just focus on the interceptor part.

Broadly speaking, there are three things we need to do:

*   Find the locations we want to intercept
*   Extract an `InterceptableLocation` instance for each location
*   Use the `InterceptableLocation` (and other information) to generate the interceptor target and `[InterceptsLocation]` attribute.

For the first part we use the `SyntaxValueProvider.CreateSyntaxProvider()` method and pass in "predicate" and "transform" functions:

```
var locations = context.SyntaxProvider
    .CreateSyntaxProvider(
        predicate: static (node, _) => InterceptorPredicate(node),
        transform: static (context, ct) => InterceptorTransform(context, ct))
    .Where(candidate => candidate is not null);
```

The predicate runs for every change in syntax, so we do a lightweight check using pattern matching to see if we're in a `ToString()` method call:

```
private static bool InterceptorPredicate(SyntaxNode node) =>
        node is InvocationExpressionSyntax { 
            Expression: MemberAccessExpressionSyntax {
                Name.Identifier.ValueText: "ToString"
            }
        };
```

This includes more instances than we actually want, as it includes _all_ calls to `ToString()`, but the important thing is to significantly reduce the number of nodes we investigate in the next stage. In the `InterceptorTransform` method we do the full check, to make sure we are only looking at `Enum.ToString()` cases.

The code below uses a bunch of pattern matching and examining of the _semantic_ model to check that the invocation is an `Enum.ToString()` call, and if so calls `GetInterceptableLocation()` to get the details we need. I've added comments to explain what each of the checks are doing:

```
private static CandidateInvocation? InterceptorTransform(GeneratorSyntaxContext ctx, CancellationToken ct)
{
    // Is this an instance method invocation? (we know it must be due to the predicate check, but play it safe)
    if (ctx.Node is InvocationExpressionSyntax {Expression: MemberAccessExpressionSyntax {Name: { } nameSyntax}} invocation
        // Get the semantic definition of the method invocation
        && ctx.SemanticModel.GetOperation(ctx.Node, ct) is IInvocationOperation targetOperation
        // This is the main check - is the method a ToString invocation on System.Enum.ToString()?
        && targetOperation.TargetMethod is {Name : "ToString", ContainingType: {Name: "Enum", ContainingNamespace: {Name: "System", ContainingNamespace.IsGlobalNamespace: true}}}
        // Grab the Type of the enum on which this is being invoked 
        && targetOperation.Instance?.Type is { } type
    {
        // If we get to here, we know we want to generate an interceptor,
        // so use the experimental GetInterceptableLocation() API to get the data
        // we need. This returns null if the location is not interceptable, but
        // should never be non-null for this example.
#pragma warning disable RSEXPERIMENTAL002 // / Experimental interceptable location API
        if (ctx.SemanticModel.GetInterceptableLocation(invocation) is { } location)
        {
            // Return the location details and the full type details
            return new CandidateInvocation(location, type.ToString());
        }
#pragma warning restore RSEXPERIMENTAL002
    }

    // Not an interceptor location we're interested in 
    return null;
}

// Record for holding the interception details
#pragma warning disable RSEXPERIMENTAL002 // / Experimental interceptable location API
public record CandidateInvocation(InterceptableLocation Location, string EnumName);
#pragma warning restore RSEXPERIMENTAL002
```

The `GetInterceptableLocation()` API is currently (as of the .NET 9.0.101 SDK and 4.12.0 of the [Microsoft.CodeAnalysis.CSharp](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp)) marked as experimental, so you need to explicitly acknowledge that using `#pragma` as in the code above.

> This will no longer be required soon, as [the interceptors feature has recently been marked stable](https://github.com/dotnet/roslyn/pull/76312), and the `[Experimental]` attribute has been removed! 🎉

The `GetInterceptableLocation()` method returns an instance of the `InterceptableLocation` class, which encodes the important `version` and `data` values we need to use to render the `[InterceptsLocation]` attribute.

Going back to our source generator, now all that remains is to generate our final code. Whether you need to combine the locations with other data extracted from the source generator depends on exactly what you're trying to generate. For simplicity in this post, I've hardcoded most of the details that are unrelated to the interceptor but you can find [the full details on GitHub](https://github.com/andrewlock/NetEscapades.EnumGenerators/blob/main/src/NetEscapades.EnumGenerators/EnumGenerator.cs) if you prefer.

```
// Reproduced from above
var locations = context.SyntaxProvider
    .CreateSyntaxProvider(
        predicate: static (node, _) => InterceptorPredicate(node),
        transform: static (context, ct) => InterceptorTransform(context, ct))
    .Where(candidate => candidate is not null);

// Output the interceptor code
context.RegisterSourceOutput(enumInterceptions, ExecuteInterceptors);

private static void ExecuteInterceptors(
    SourceProductionContext context,
    ImmutableArray<CandidateInvocation> toIntercept)
{
    var sb = new StringBuilder();

    // group by the target enum.
    var grouped = toIntercept.GroupBy(x => x.EnumName);
    foreach (var grouping in grouped)
    {
        string enumType = grouping.Key;

        // Add the InterceptsLocationAttribute to the generated file,
        // along with the start of the interceptor
        sb.Append("""
                    #nullable enable
                    namespace System.Runtime.CompilerServices
                    {
                        [global::System.Diagnostics.Conditional("DEBUG")]
                        [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
                        sealed file class InterceptsLocationAttribute : global::System.Attribute
                        {
                            public InterceptsLocationAttribute(int version, string data)
                            {
                                _ = version;
                                _ = data;
                            }
                        }
                    }
                    
                    namespace NetEscapades.EnumGenerators
                    {
                        static file class EnumInterceptors
                        {
                    """);

        // Generate the [InterceptsLocation] attributes, using the InterceptableLocation
        foreach (var invocation in grouping)
        {
            var location = invocation.Location;
            int version = location.Version; // 1
            string data = location.Data; // e.g. yxKJBEhzkHdnMhHGENjk8qgBAABQcm9ncmFtLmNz
            string displayLocation = location.GetDisplayLocation(); // e.g. Program.cs(19,32)
            sb.AppendLine(
                $"""        [global::System.Runtime.CompilerServices.InterceptsLocation({version}, "{data}")] // {displayLocation}""");
        }

        // Add the interceptor implementation
        sb.AppendLine($$"""
                            public static string MyEnumExtensionsToString(this global::System.Enum value)
                                => global::MyTestNameSpace.MyEnumExtensions.ToStringFast(({{enumType}})value);
                        }
                    }
                    """);

        // Add the source to the compilation
        string contents = sb.ToString();
        string filename = $"{enumType}_Interception.g.cs";
        context.AddSource(filename, SourceText.From(contents, Encoding.UTF8));
        sb.Clear();
    }
}
```

And that's all there is to the mechanics of interceptors. I've glossed over some complexities such as actually _enabling_ interceptors and how to gracefully handle an insufficient SDK version, but I'll dig into those in later posts.

Before I close this post, I'll quickly address the "experimental" part of interceptors.

## [What is the status of the interceptors feature?](http://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/#what-is-the-status-of-the-interceptors-feature-)

When I recently [announced](https://bsky.app/profile/andrewlock.bsky.social/post/3la7xn745a72y) preview support for [interceptors in NetEscapades.EnumGenerators](https://andrewlock.net/recent-updates-for-netescapades-enumgenerators-interceptors/), [Jason Bock](https://bsky.app/profile/jasonbock.net)[pointed out that,](https://bsky.app/profile/jasonbock.net/post/3lalxlvnmjs2r) at that point, interceptors were still described as experimental, and not listed as an official feature.

However, [a recent PR](https://github.com/dotnet/roslyn/pull/76312) explicitly updated the docs. Specifically, that PR:

*   [Updates the docs](https://github.com/dotnet/roslyn/blob/98ea496177bc8e607dbf454abd6b5a5e4678aed2/docs/features/interceptors.md) to describe the feature as stable in .NET 9.0.2xx SDK and later.
*   Removed the `[Experimental]` attribute from the APIs.
*   Marked the old `[InterceptsLocation(path, line, column)]` API as officially deprecated, with the intention of removing them later.

On that basis, I think it's now safe to look into interceptors more broadly. Interceptors have been used both [by ASP.NET Core](https://github.com/dotnet/aspnetcore/issues/55152) and by [the configuration binder source generator](https://github.com/dotnet/runtime/issues/101079) to provide support for AOT, so they've been tested in real production scenarios too.

That said, the waters are still a _little_ murky. The [C# features list](https://github.com/dotnet/csharplang/blob/410cfaed5528d1b7e92a25cc15993017912b6afd/Language-Version-History.md) still doesn't include interceptors as a feature yet, though I suspect that's just an oversight. You also still need to "opt-in" to individual interceptors by adding the namespace of the interceptor in the `<InterceptorsNamespaces>` MSBuild property. There doesn't seem to be any intention to change that behaviour, as it provides an easy layer of control over any interceptors added to your project.

Taking all that into account, interceptors are now considered stable, so I think it's worth looking into and understanding them. There may not be as many use cases for them as there are for source generators in general, but they provide interesting opportunities for some scenarios.

## [Summary](http://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/#summary)

In this post I described the interceptor support introduced in the .NET 8 and 9 SDKs. Interceptors can replace a method call with another at compile time, which is particularly useful for some scenarios like Ahead Of Time compilation. I showed how the APIs have changed recently, and how you can use these new APIs to create an interceptor using a source generator. In the .NET 9.0.2xx SDK interceptors are considered a stable language feature, so it's worth looking into to see if they can solve any use cases that can't be handled another way.

[![Image 1: Building LaTeX projects on Windows easily with Docker](https://andrewlock.net/content/images/2024/latex_windows.webp)Previous Building LaTeX projects on Windows easily with Docker](https://andrewlock.net/building-latex-projects-on-windows-easily-with-docker/)[![Image 2: Reading compilation options and the C# version in source generators](https://andrewlock.net/content/images/2025/lang_version_banner.webp)Next Reading compilation options and the C# version in source generators: Creating a source generator - Part 12](https://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/)

Andrew Lock | .Net Escapades

![Image 3](https://andrewlock.net/assets/img/icons/apple/apple-touch-icon-180x180.png)Want an email when

there's new posts?
