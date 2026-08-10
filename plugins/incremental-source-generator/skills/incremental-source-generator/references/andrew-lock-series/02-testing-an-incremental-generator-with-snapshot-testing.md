Title: Testing an incremental generator with snapshot testing: Creating a source generator - Part 2

URL Source: http://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/

Published Time: 2021-12-14T10:00:00.0000000

Markdown Content:
December 14, 2021 ~12 min read

[Creating a source generator - Part 2](https://andrewlock.net/series/creating-a-source-generator/)

This is the second post in the series: [Creating a source generator](https://andrewlock.net/series/creating-a-source-generator/).

1.   [Part 1 - Creating an incremental generator](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/)
2.   Part 2 - Testing an incremental generator with snapshot testing (this post) 
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

In my previous post, I showed in detail how to create a source generator, but I missed out one very important step: testing. In this post I describe one of the ways I like to test my source generators, by running the source generator manually against a known string and evaluating the output. _Snapshot testing_ provides a great way to ensure your generator keeps working, and in this post I use the excellent `Verify` library.

## [Recap: the EnumExtensions generator](http://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/#recap-the-enumextensions-generator)

As a quick recap, [in the previous post](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/) I discussed the problem with calling `ToString()` on an `enum` (it's slow), and described how we could use a source generator to create an extension method that provides the same functionality, but 100s of times faster.

So for a simple `enum` like the following:

```
public enum Colour
{
    Red = 0,
    Blue = 1,
}
```

we generate an extension method that looks like the following:

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

As you can see, this implementation consists of a simple `switch` expression, and makes use of the `nameof` keyword, so that doing `Colour.Red.ToStringFast()` returns `"Red"` as expected.

I'm not going to go over the implementation of the generator in this post, refer back to [the previous post](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/) if that's what you're after.

Instead, in this post, we're going to look at a way of testing our source generator generates the right code. My preferred approach for this is to use "snapshot testing".

## [Snapshot testing for source generators](http://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/#snapshot-testing-for-source-generators)

I haven't written about snapshot testing before, and this post is long enough without going into detail here but the concept is quite simple: instead of asserting against one or two properties, snapshot testing asserts a whole object (or other file) is identical to the expected result. There's a lot more to it than that, but it'll have to do for now!

> Luckily, [Dan Clarke](https://twitter.com/dracan) recently [wrote an excellent introduction to snapshot testing](https://www.danclarke.com/snapshot-testing-with-verify) as his contribution to [the .NET Advent Calendar](https://dotnet.christmas/2021/11)!

As it turns out, source generators are a great fit for snapshot testing. Source generators are all about generating a deterministic output for a given input (the source code), and we always want that output to be exactly the same. By creating a "snapshot" of our required output and comparing the real output against it, we can be sure our source generator is working correctly

Now, you could write the code to do all this manually, but there's no need, as there's an excellent library to do that for you called [Verify](https://github.com/VerifyTests/Verify) written by [Simon Cropp](https://github.com/SimonCropp). This library takes care of the serialization and comparison for you, handling file naming, and even integrating with diff-tools to make it simple to compare visualize the differences between your objects when a test fails.

Verify also has a whole host of extensions for snapshot testing almost anything: in-memory objects, EF Core queries, images, Blazor components, HTML, XAML, WinForms UIs, [the list is seemingly endless](https://github.com/orgs/VerifyTests/repositories)! The extension we're interested in though is [Verify.SourceGenerators](https://github.com/VerifyTests/Verify.SourceGenerators).

> I didn't realise that Verify had built-in support for testing generators until recently. Previously I had been "manually" using Verify, but when I heard [Simon](https://twitter.com/SimonCropp) talking to [Dan Clarke](https://twitter.com/dracan) on the [Unhandled Exception Podcast](https://unhandledexceptionpodcast.com/posts/0029-snapshottesting/), I had to give it a try!

The extensions and helpers provided by _Verify.SourceGenerators_ work with both "original" source generators (`ISourceGenerator`) and incremental source generators `IIncrementalGenerator`, and have two main benefits over the "manual" approach I was using previously:

*   They automatically handle multiple generated files being added to the compilation
*   They gracefully handle any diagnostics added to the compilation

For those reasons I'll be going through and updating any source generators I have to use his library!

That covers the basic of snapshot testing for now, so it's time to add a test project and start testing our incremental source generator!

## [1. Create a test project](http://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/#1-create-a-test-project)

I'm going to carry on from where I left off last time, in which we have a single project called `NetEscapades.EnumGenerators` in a solution. This project contains our source generator.

In the following script I do the following:

*   Create an xunit test project
*   Add it to the solution
*   Add a reference to the src project from the test project
*   Add some packages that we need to the test project: 
    *   _Microsoft.CodeAnalysis.CSharp_ and _Microsoft.CodeAnalysis.Analyzers_ contain methods for running a source generator in memory and examining the output.
    *   _Verify.XUnit_ contains the _Verify_ snapshot testing integration for xunit. There are equivalent adapters for other testing frameworks
    *   _Verify.SourceGenerators_ contains the extensions to _Verify_ specifically for working with source generators. This isn't required, but makes things a lot easier!

```
dotnet new xunit -o ./tests/NetEscapades.EnumGenerators.Tests
dotnet sln add ./tests/NetEscapades.EnumGenerators.Tests
dotnet add ./tests/NetEscapades.EnumGenerators.Tests reference ./src/NetEscapades.EnumGenerators
# Add some helper packages to the test project
dotnet add ./tests/NetEscapades.EnumGenerators.Tests package Microsoft.CodeAnalysis.CSharp
dotnet add ./tests/NetEscapades.EnumGenerators.Tests package Microsoft.CodeAnalysis.Analyzers
dotnet add ./tests/NetEscapades.EnumGenerators.Tests package Verify.SourceGenerators
dotnet add ./tests/NetEscapades.EnumGenerators.Tests package Verify.XUnit
```

After running the above script, your test project's _.csproj_ file should look something like the following

```
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <ImplicitUsings>true</ImplicitUsings>
  </PropertyGroup>

  <!-- Add these 👇 to the base template  -->
  <ItemGroup>
    <PackageReference Include="Verify.XUnit" Version="14.7.0" />
    <PackageReference Include="Verify.SourceGenerators" Version="1.2.0" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.2" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.0.1" PrivateAssets="all" />
  </ItemGroup>
  
  <!-- Add  👇 a reference to the generator project  -->
  <ItemGroup>
    <ProjectReference Include="..\..\src\NetEscapades.EnumGenerators\NetEscapades.EnumGenerators.csproj" />
  </ItemGroup>

  <!-- 👇 These are all part of the base template  -->
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="16.11.0" />
    <PackageReference Include="xunit" Version="2.4.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.3">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="3.1.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

</Project>
```

Now we have all the dependencies installed lets write a test!

## [2. Create a simple snapshot test](http://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/#2-create-a-simple-snapshot-test)

Testing a source generator takes a little bit of set-up, so we're going to create a helper class that creates a `Compilation` from a `string`, runs our source generator on it, and then uses snapshot testing to test the output.

Before we get to that, let's see what our test is going to look like:

```
using VerifyXunit;
using Xunit;

namespace NetEscapades.EnumGenerators.Tests;

[UsesVerify] // 👈 Adds hooks for Verify into XUnit
public class EnumGeneratorSnapshotTests
{
    [Fact]
    public Task GeneratesEnumExtensionsCorrectly()
    {
        // The source code to test
        var source = @"
using NetEscapades.EnumGenerators;

[EnumExtensions]
public enum Colour
{
    Red = 0,
    Blue = 1,
}";

        // Pass the source code to our helper and snapshot test the output
        return TestHelper.Verify(source);
    }
}
```

Out `TestHelper` is doing all the work here, so rather than bury the lede, the following shows the initial implementation, annotated to describe what's going on

```
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;

namespace NetEscapades.EnumGenerators.Tests;

public static class TestHelper
{
    public static Task Verify(string source)
    {
        // Parse the provided string into a C# syntax tree
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Create a Roslyn compilation for the syntax tree.
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: new[] { syntaxTree });

        // Create an instance of our EnumGenerator incremental source generator
        var generator = new EnumGenerator();

        // The GeneratorDriver is used to run our generator against a compilation
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // Run the source generator!
        driver = driver.RunGenerators(compilation);

        // Use verify to snapshot test the source generator output!
        return Verifier.Verify(driver);
    }
}
```

If you run your snapshot test, Verify will attempt to compare a snapshot of the `GeneratorDriver` output with an existing snapshot. As this is the first time you're running the test, the test will fail, so Verify will automatically pop open your default diff tool, in my case VS Code. However, the diff probably doesn't show what you're expecting!

![Image 1: Diff tool showing {} in the left pane, and an empty right pane](https://andrewlock.net/content/images/2021/snapshot_source_gen_01.png)

The right-hand pane is empty, as we don't have an existing snapshot. But rather than showing our source generator output on the left, we only see `{}`. It looks like something went wrong.

Well, it turns out, that's because I didn't read the docs. The [Verify.SourceGenerators](https://github.com/VerifyTests/Verify.SourceGenerators#initialize) readme very clearly states that you need to initialize the converters for handling source generator outputs, by calling `VerifySourceGenerators.Enable();` once for the assembly.

The correct way to do this in modern c# is [to use a `[ModuleInitializer]` attribute](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.moduleinitializerattribute?view=net-6.0). As described [in the spec](https://github.com/dotnet/runtime/blob/main/docs/design/specs/Ecma-335-Augments.md#module-initializer), this code will run once, before any other code in your assembly.

You can create a module initializer by decorating any `static void` method in your project with the `[ModuleInitializer]` attribute. In our case, we would do the following:

```
using System.Runtime.CompilerServices;
using VerifyTests;

namespace NetEscapades.EnumGenerators.Tests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifySourceGenerators.Enable();
    }
}
```

> Note that module initializers are a C#9 feature, which means you can use them even if you're targeting older versions of .NET. However the `[ModuleInitializer]` attribute is only available in .NET 5+. If you're targeting older versions of .NET, create your own implementation of the attribute, similar to [the approach I describe in this post](https://andrewlock.net/exploring-dotnet-6-part-11-callerargumentexpression-and-throw-helpers/#argumentnullexception-throw-) for the `[DoesNotReturn]` attribute.

After adding the initializer, if we run our test again, we get something that looks a bit better: it's our custom `[EnumExtensions]` attribute we added to the compilation as part of our source generator:

![Image 2: Diff tool showing the EnumExtensionsAttribute in the left pane, and an empty right pane](https://andrewlock.net/content/images/2021/snapshot_source_gen_02.png)

This attribute looks like what we're expecting, but there's still something wrong; there is no other generated source code. Our source generator has added the attribute, but it should also be generating an `EnumExtensions` class. 🤔

## [3. Debugging a failure: missing references](http://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/#3-debugging-a-failure-missing-references)

The good thing about testing source generators like this is that they're _super_ easy to debug. No need to start up separate instances of your IDE or anything like that. You're literally running the source generator in the context of the unit test, so you can just hit "Debug" on the test in your IDE (I'm using JetBrains Rider) and step through the code!

Given that the test isn't throwing an exception, it's just not generating the correct output, I suspected that my logic must be wrong somewhere in the source generator. I placed a breakpoint in `GetSemanticTargetForGeneration()`[the first "transform" methods in our incremental generator pipeline](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/#5-building-the-incremental-generator-pipeline). I then started debugging, and checked to see that we hit the breakpoint.

![Image 3: JetBrains Rider debugging the source generator, we have hit a breakpoint](https://andrewlock.net/content/images/2021/snapshot_source_gen_03.png)

As you can see above, we've hit the breakpoint in `GetSemanticTargetForGeneration()` and the `enumDeclarationSyntax` variable contains the `Colour` enum from our test code, so everything is looking good so far. I stepped through the method, in which we loop over the attributes on the `enum` declaration, trying to find our `[EnumExtensions]` attribute. However, strangely, an attempt to use the `SemanticModel` to access the `Symbol` of the `[EnumExtensions]` syntax returned `null` so we bailed out! This explains _how_ our source generator was failing. The next question is, why?

![Image 4: context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol was null](https://andrewlock.net/content/images/2021/snapshot_source_gen_04.png)

Before I stopped debugging I checked the values of `context.SemanticModel.GetSymbolInfo(attributeSyntax).CandidateSymbols` using the immediate window. This returned a single value, so the failure wasn't due to ambiguity or some similar problem. Checking `context.SemanticModel.GetSymbolInfo(attributeSyntax).CandidateReason` returned `NotAnAttributeType`.

eh? `NotAnAttributeType`?

After a bit of digging I realised that the problem was that the compilation doesn't have any references by default. That meant that it couldn't find `System.Attribute`, so it couldn't create the `[EnumExtensions]` attribute correctly. The solution was to update my `TestHelper` to add a reference to the right dll. I created a reference to the assembly containing `object` (`System.Private.CoreLib` here), and added that to the compilation. The full `TestHelper` class becomes:

```
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;

namespace NetEscapades.EnumGenerators.Tests;

public static class TestHelper
{
    public static Task Verify(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        // Create references for assemblies we require
        // We could add multiple references if required
        IEnumerable<PortableExecutableReference> references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
        };

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: new[] { syntaxTree },
            references: references); // 👈 pass the references to the compilation

        EnumGenerator generator = new EnumGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGenerators(compilation);

        return Verifier
            .Verify(driver)
            .UseDirectory("Snapshots");
    }
}
```

After making this change and running the test, Verify pops open our diff-tool again, and this time it contains two diffs—the `[EnumExtensions]` attribute as before, but also the generated `EnumExtensions` class:

![Image 5: The generated source code](https://andrewlock.net/content/images/2021/snapshot_source_gen_05.png)

At this point we can accept the verified file diffs, which will save them to disk. You can either manually copy the diffs across from one side to the other, or you can run the command that Verify puts into the clipboard at a terminal, e.g.

```
cmd /c del "C:\repo\sourcegen\tests\NetEscapades.EnumGenerators.Tests\EnumGeneratorSnapshotTests.GeneratesEnumExtensionsCorrectly.00.verified.txt"
cmd /c del "C:\repo\sourcegen\tests\NetEscapades.EnumGenerators.Tests\EnumGeneratorSnapshotTests.GeneratesEnumExtensionsCorrectly.02.verified.cs"
cmd /c del "C:\repo\sourcegen\tests\NetEscapades.EnumGenerators.Tests\EnumGeneratorSnapshotTests.GeneratesEnumExtensionsCorrectly.verified.cs"
cmd /c del "C:\repo\sourcegen\tests\NetEscapades.EnumGenerators.Tests\EnumGeneratorSnapshotTests.GeneratesEnumExtensionsCorrectly.verified.txt"
cmd /c move /Y "C:\repo\sourcegen\tests\NetEscapades.EnumGenerators.Tests\EnumGeneratorSnapshotTests.GeneratesEnumExtensionsCorrectly.00.received.cs" "C:\repo\sourcegen\tests\NetEscapades.EnumGenerators.Tests\EnumGeneratorSnapshotTests.GeneratesEnumExtensionsCorrectly.00.verified.cs"
cmd /c move /Y "C:\repo\sourcegen\tests\NetEscapades.EnumGenerators.Tests\EnumGeneratorSnapshotTests.GeneratesEnumExtensionsCorrectly.01.received.cs" "C:\repo\sourcegen\tests\NetEscapades.EnumGenerators.Tests\EnumGeneratorSnapshotTests.GeneratesEnumExtensionsCorrectly.01.verified.cs"
```

Now that we've updated our snapshots, if we run the tests again, the snapshot tests will pass! 🎉

## [4. Moar tests](http://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/#4-moar-tests)

Now that we've written a single snapshot test for our source generator, it's trivial to add some more. I decided to test the following cases:

*   `enum` without the attribute—doesn't generate the extension method
*   `enum` missing the correct namespace import—doesn't generate the extension method
*   Two `enum`s in the file —generates extensions for both `enum`s
*   Two `enum`s, one without an attribute—only generates an extension for the attributed `enum`

You can find [the source code for these examples on GitHub](https://github.com/andrewlock/blog-examples/tree/master/NetEscapades.EnumGenerators2), but they look virtually identical to our existing test. The only thing that changes are the test source code and the snapshots.

## [5. Testing diagnostics](http://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/#5-testing-diagnostics)

One aspect of source generators we haven't looked at yet is diagnostics. Source generators also act as analyzers, so they can report problems in user's source code. This is useful if you need to tell a user that they're using your generator incorrectly in some way, for example.

We don't have any diagnostics in our source generator, but just to demonstrate that they work well with snapshot testing, we'll add a dummy one in!

First we'll create a helper method to generate a diagnostic for an `enum` in the source generator:

```
static Diagnostic CreateDiagnostic(EnumDeclarationSyntax syntax)
{
    var descriptor = new DiagnosticDescriptor(
        id: "TEST01",
        title: "A test diagnostic",
        messageFormat: "A description about the problem",
        category: "tests",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    return Diagnostic.Create(descriptor, syntax.GetLocation());
}
```

Next, we'll call that method to create a diagnostic in the `Execute()` method of our source generator, and register it with the output using the `SourceProductionContext` provided to the method:

```
static void Execute(Compilation compilation, ImmutableArray<EnumDeclarationSyntax> enums, SourceProductionContext context)
{
    if (enums.IsDefaultOrEmpty)
    {
        return;
    }

    // Add a dummy diagnostic
    context.ReportDiagnostic(CreateDiagnostic(enums[0]));

    // ...
}
```

> Remember, this is just to demonstrate snapshot testing, we don't really want random diagnostics appearing!

If we re-run our tests, we'll now get failures. Verify extracts both the additional source code added to the compilation _and_ the diagnostics. The Diagnostic is a C# object, so it's serialized to a JSON-ish document, something like the following:

```
{
  Diagnostics: [
    {
      Id: TEST01,
      Title: A test diagnostic,
      Severity: Warning,
      WarningLevel: 1,
      Location: : (3,0)-(8,1),
      MessageFormat: A description about the problem,
      Message: A description about the problem,
      Category: tests
    }
  ]
}
```

Verify fires up the diff tool one more time, and shows that there's now an additional file for the tests, the diagnostic:

![Image 6: Diff tool showing the serialized diagnostic in the left pane, and an empty right pane](https://andrewlock.net/content/images/2021/snapshot_source_gen_06.png)

Source generators seem like an almost perfect use-case for snapshot testing, given that there's normally a very specific, deterministic output that you want for a given input. Obviously you can architect your source generators for more granular unit testing, but for the most part I find snapshot testing with a bit of debugging where necessary gives me everything I need!

## [Summary](http://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/#summary)

In this post I showed how to use snapshot testing to test the source generator I created [in my previous post](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/). I gave a brief introduction to snapshot testing, and then showed how you can use Verify.SourceGenerators to test your generator output. We debugged through a couple of issues, and finally demonstrated that Verify handles both diagnostics and syntax trees that your source generator creates.

[![Image 7: Creating an incremental generator](https://andrewlock.net/content/images/2021/source_gen_banner.jpg)Previous Creating an incremental generator: Creating a source generator - Part 1](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/)[![Image 8: Integration testing and NuGet packaging](https://andrewlock.net/content/images/2021/nuget_tests_banner.png)Next Integration testing and NuGet packaging: Creating a source generator - Part 3](https://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/)
