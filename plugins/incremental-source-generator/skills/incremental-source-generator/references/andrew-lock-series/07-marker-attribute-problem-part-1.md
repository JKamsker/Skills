Title: Solving the source generator 'marker attribute' problem - Part 1: Creating a source generator - Part 7

URL Source: http://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/

Published Time: 2022-01-25T10:00:00.0000000

Markdown Content:
January 25, 2022 ~8 min read

[Creating a source generator - Part 7](https://andrewlock.net/series/creating-a-source-generator/)

This is the seventh post in the series: [Creating a source generator](https://andrewlock.net/series/creating-a-source-generator/).

1.   [Part 1 - Creating an incremental generator](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/)
2.   [Part 2 - Testing an incremental generator with snapshot testing](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/)
3.   [Part 3 - Integration testing and NuGet packaging](https://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/)
4.   [Part 4 - Customising generated code with marker attributes](https://andrewlock.net/creating-a-source-generator-part-4-customising-generated-code-with-marker-attributes/)
5.   [Part 5 - Finding a type declaration's namespace and type hierarchy](https://andrewlock.net/creating-a-source-generator-part-5-finding-a-type-declarations-namespace-and-type-hierarchy/)
6.   [Part 6 - Saving source generator output in source control](https://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/)
7.   Part 7 - Solving the source generator 'marker attribute' problem - Part 1 (this post) 
8.   [Part 8 - Solving the source generator 'marker attribute' problem - Part 2](https://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/)
9.   [Part 9 - Avoiding performance pitfalls in incremental generators](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/)
10.   [Part 10 - Testing your incremental generator pipeline outputs are cacheable](https://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/)
11.   [Part 11 - Implementing an interceptor with a source generator](https://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/)
12.   [Part 12 - Reading compilation options and the C# version in source generators](https://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/)
13.   [Part 13 - Accessing MSBuild properties and user configuration from source generators](https://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/)
14.   [Part 14 - Supporting multiple .NET SDK versions in a source generator](https://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/)
15.   [Part 15 - Solving the source generator 'marker attribute' problem in .NET 10](https://andrewlock.net/exploring-dotnet-10-preview-features-4-solving-the-source-generator-marker-attribute-problem-in-dotnet-10/)

In this post I describe a problem I've been wrestling with around source generators: where to put the 'marker attributes' that drive the source generator. In this post I describe what marker attributes are, why they're useful for source generators, and why deciding where to put them can be problematic. Finally, In the next post I describe the solution I settled on that seems to give the best of all worlds.

## [Marker attributes and source generators](http://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/#marker-attributes-and-source-generators)

I'm quite a fan of source generators in C#, and I've written [several posts](https://andrewlock.net/tag/source-generators/) about using them in your applications. I [recently updated](https://andrewlock.net/rebuilding-stongly-typed-id-as-a-source-generator-1-0-0-beta-release/)[a library](https://github.com/andrewlock/StronglyTypedId) for generating strong-typed IDs, called [_StronglyTypedId_](https://www.nuget.org/packages/StronglyTypedId/1.0.0-beta02) to use .NET's built-in source generator support rather than a custom Roslyn task.

One of the key stages of most source generators is to identify the syntax in your application that needs to take part in code generation. This will depend entirely on the purpose of the source generator, but a very common approach is to use attributes to decorate code that needs to take part in the code generation process.

For example, [the `LoggerMessage` source generator](https://andrewlock.net/exploring-dotnet-6-part-8-improving-logging-performance-with-source-generators/#the-net-6-loggermessage-source-generator) that is part of the _Microsoft.Extensions.Logging_ library in .NET 6 uses a `[LoggerMessage]` attribute to define the code that will be generated:

```
using Microsoft.Extensions.Logging;

public partial class TestController
{
    // Adding the attribute here indicates the LogHelloWorld
    // method needs to have code generated
    [LoggerMessage(0, LogLevel.Information, "Writing hello world response to {Person}")]
    partial void LogHelloWorld(Person person);
}
```

Similarly, in my _StronglyTypedId_ package I use an attribute `[StronglyTypedId]` applied to `struct`s to indicate that you want the type to be a `StronglyTypedId`:

```
using StronglyTypedIds;

[StronglyTypedId]
public partial struct MyCustomId { }
```

In both of these cases, the attribute itself is only a _marker_, used at compile-time, to tell the source generator what to generate. It doesn't need to be in the final compiled output, though generally it won't be a problem if it is.

The question I'm tackling in this post is: where should those marker attributes be defined?

## [Defining the marker attribute](http://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/#defining-the-marker-attribute)

In some cases, there is a trivial answer. If the generator is an _enhancement_ to an existing library that has some functionality the user needs, then the generator can simply be packaged with that library.

For example, the `LoggerMessage` generator is part of the _Microsoft.Extensions.Logging.Abstractions_ library. It is packaged in the same NuGet package that people will install anyway, and the marker attributes are contained in the referenced dll, so they will always be there. This is the "best case" scenario as far as marker attributes are concerned.

![Image 1: The contents of the Microsoft.Extensions.Logging.Abstractions package contains both the dll and the analyzer](https://andrewlock.net/content/images/2021/attributes_01.png)

But what if you have a library that is _only_ a source generator. You still need to reference those attributes, so on the face of it, you have 3 main options.

1.   Use the source generator to automatically add the attributes to your compilation.
2.   Ask users to add the attribute themselves to the compilation.
3.   Include the attributes in an external dll, and ensure the project references that.

Each of these has its advantages and disadvantages, so in this post I'll talk through the pros and cons of each, and which one I think is the best.

### [1. Adding the attributes to a users compilation](http://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/#1-adding-the-attributes-to-a-users-compilation)

Source-generators have the ability to add source code to a consuming project. In general, source generators cannot access code that they have added to the compilation, which avoids a whole swathe of recursion issues. There is one exception: a source generator can register a "post initialization" hook, which allows them to add some fixed sources to the compilation.

For .NET 6's incremental generator API, this hook is called `RegisterPostInitializationOutput()`. You don't have any access to the user's code at this point, so it's only useful for adding _fixed_ code, but the user can reference it, and you can use code that references it in your source generator. For example

```
[Generator]
public class HelloWorldGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the attribute source
        context.RegisterPostInitializationOutput(i =>
        {
            var attributeSource = @"
            namespace HelloWorld
            {
                public class MyExampleAttribute: System.Attribute {} 
            }";
            i.AddSource("MyExampleAttribute.g.cs", attributeSource);
        });

        // ... generator implementaation
    }
}
```

This hook is seemingly _tailor made_ for adding marker attributes to the user's compilation, which you can then use later in the generator. In fact, this scenario [is explicitly called out in the source generator cook book](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md#augment-user-code) as "the way" to work with marker attributes.

And most of the time, this works perfectly.

Where things fall down, is if a user references your source generator in more than one project. The class `MyExampleAttribute` would be added to two projects, in the `HelloWorld` namespace. If one of your projects references the other, you'll get a `CS0436` warning, and a build warning along the lines of:

```
warning CS0436: The type 'MyExampleAttribute' in 'HelloWorldGenerator\MyExampleAttribute.g.cs' conflicts with the imported type 'MyExampleAttribute' in 'MyProject, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null'.
```

The problem is we've defined the same type, in two different projects, and the compiler can't distinguish between them. So how can we solve that?

The obvious solution is to make the attribute `internal` instead of `public`. That way each project will only reference the `MyExampleAttribute` added to that specific project. And that will work 🎉

However, it _won't_ work if someone is using `[InternalsVisibleTo]`. At that point, effectively all `internal` types are `public`, so we're back to square one.

Now, maybe you're thinking, "people don't really use `[InternalsVisibleTo]` do they?". Well, I originally took this approach in my `StronglyTypedId` and I can confirm [yes, yes they are](https://github.com/andrewlock/StronglyTypedId/issues/38#issuecomment-924674974). But I'm not one to judge, [our `AssemblyInfo.cs` file for my day job contains 22 `[InternalsVisibleTo]` attributes!](https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Trace/AssemblyInfo.cs)

The big problem is that there's no workaround for users here. They're just broken in that scenario. So lets look at another option.

### [2. Ask users to create it themselves](http://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/#2-ask-users-to-create-it-themselves)

The next option is to ask users to add the attribute themselves. You might be wondering how or why that helps, but the key is that the users can add it _once_, and use the same attribute throughout their whole solution. Instead of the source generator adding to _every_ project, the user creates `MyExampleAttribute` in their "domain helpers" class (for example).

This approach isn't actually as weird or backwards as it seems on the face of it. In fact, there are a number of C# features which use _exactly_ this approach. I mentioned one such case [in a recent post](https://andrewlock.net/exploring-dotnet-6-part-11-callerargumentexpression-and-throw-helpers/#argumentnullexception-throw-) when I mentioned using the `[DoesNotReturn]` attribute. This attribute is used for nullable flow-analysis among other things, but it's only _defined_ in the BCL for .NET Core 3. That means you can't use it if you're targeting .NET Core 2.x or .NET Standard right?

Well, no! The C# compiler uses the "add it yourself" approach. It doesn't care _where_ the attribute is defined, as long as it's defined _somewhere_. That means you can add it to your own project (making sure to use the correct namespace), and the C# compiler will "magically" treat it the same as the "original".

```
#if !NETCOREAPP3_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Method)]
    public class DoesNotReturnAttribute: Attribute { }
}
#endif
```

We could take exactly the same approach with source generators. However, asking users to do this just feels a bit like hard work. Also, it's fine for a super basic attribute like `[DoesNotReturn]`, but what about a complex attribute like `[StronglyTypedId]`?

```
using System;

namespace StronglyTypedIds
{
    [AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    [System.Diagnostics.Conditional("STRONGLY_TYPED_ID_USAGES")]
    public sealed class StronglyTypedIdAttribute : Attribute
    {
        public StronglyTypedIdAttribute(
            StronglyTypedIdBackingType backingType = StronglyTypedIdBackingType.Default,
            StronglyTypedIdConverter converters = StronglyTypedIdConverter.Default,
            StronglyTypedIdImplementations implementations = StronglyTypedIdImplementations.Default)
        {
            BackingType = backingType;
            Converters = converters;
            Implementations = implementations;
        }

        public StronglyTypedIdBackingType BackingType { get; }
        public StronglyTypedIdConverter Converters { get; }
        public StronglyTypedIdImplementations Implementations { get; }
    }
}
```

Asking a user to add that, getting everything exactly correct so it doesn't break the generator seems like a non-starter to me. On top of that, you lose the ability to evolve your API, as users would have to update this code _every time_ they update your project. That seems like a recipe for support calls…

So that leaves us just one remaining option.

### [3. Reference the marker attributes in an external dll](http://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/#3-reference-the-marker-attributes-in-an-external-dll)

With this approach, the generator doesn't add the marker attributes itself, and the user doesn't add them to their compilation either. Instead, the source generator relies on the attributes being defined in a dll that is referenced by the user's project.

Note that I'm being deliberately cagey about how or where that dll comes from, as there's lots of options. The `[LoggerMessage]` generator, for example, relies on attributes that are present in the _Microsoft.Extensions.Logging.Abstractions_ NuGet package, which _also_ contains the generator. This is particularly convenient as the generator can be sure the attributes are always available for use, and vice versa; if the attribute is available, so is the generator.

If your generator is an "optional extra" to a "main" dll, then this approach makes perfect sense. A similar argument could be made for including the generator in a separate package, which the "main" package then takes a dependency on, similar to the way this is done for analyzers in some projects. Source generators are really like fancy analyzers, so many of the same patterns should apply. For example, the main _xunit_ package takes a dependency on the _xunit.analyzers_ package.

![Image 2: The xunit package depends on the xunit.analyzers package](https://andrewlock.net/content/images/2021/attributes_02.png)

This approach makes sense if your generator is an "added extra" to a main package. By keeping the dependency chain this way, it ensures that if the marker attributes are present (in the _xunit_ package for example), then the generator will always be referenced.

> Although it's possible to install the generator package (e.g. _xunit.analyzers_) _without_ the main _xunit_ package, attempting to use the marker attributes would be a compile error, so the behaviour is expected.

But going back to the original problem, what if you have a "standalone" generator, that is _just_ a source generator? We don't really have to introduce a NuGet package that only contains the attributes, just to work around this do we?

Another possibility is to include the attributes inside the source generator dll itself. By default, the dll containing the source generator isn't included in the user's compilation, but it _could be_. Crazy enough to work?

I tried several different approaches to tackling the issue with [my _StronglyTypedId_ generator project](https://github.com/andrewlock/StronglyTypedId). And rather than jump straight to the solution, In the next post I'm going to make you suffer along with me as I talk through some of the approaches I tried, how I failed, and ultimately the solution I settled on.

## [Summary](http://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/#summary)

In this post I described what "marker attributes" are in the context of source generators, and how they can help drive the code generation. I then discussed the question of how the attributes should be added to the compilation.

Conventional wisdom uses the source generator itself to add them to the compilation, but this can run into problems when users use the `[InternalsVisibleTo]` attribute. As a workaround, we could ask users to add the attribute itself, as the C# compiler does in some cases. Alternatively, we could add the attributes to a dll, and reference that dll somehow. There are lots of different options for how to achieve this. In the next post I'll explore some of these, and describe the solution I settled on.

[![Image 3: Saving source generator output in source control](https://andrewlock.net/content/images/2022/source_gen_emit_banner.png)Previous Saving source generator output in source control: Creating a source generator - Part 6](https://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/)[![Image 4: Solving the source generator 'marker attribute' problem - Part 2](https://andrewlock.net/content/images/2021/attributes_banner_02.png)Next Solving the source generator 'marker attribute' problem - Part 2: Creating a source generator - Part 8](https://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/)

Andrew Lock | .Net Escapades

![Image 5](https://andrewlock.net/assets/img/icons/apple/apple-touch-icon-180x180.png)Want an email when

there's new posts?
