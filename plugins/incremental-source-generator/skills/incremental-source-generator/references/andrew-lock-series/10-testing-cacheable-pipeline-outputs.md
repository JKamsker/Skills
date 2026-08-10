Title: Testing your incremental generator pipeline outputs are cacheable: Creating a source generator - Part 10

URL Source: http://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/

Published Time: 2024-01-23T10:00:00.0000000

Markdown Content:
January 23, 2024 ~13 min read

[Creating a source generator - Part 10](https://andrewlock.net/series/creating-a-source-generator/)

This is the tenth post in the series: [Creating a source generator](https://andrewlock.net/series/creating-a-source-generator/).

1.   [Part 1 - Creating an incremental generator](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/)
2.   [Part 2 - Testing an incremental generator with snapshot testing](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/)
3.   [Part 3 - Integration testing and NuGet packaging](https://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/)
4.   [Part 4 - Customising generated code with marker attributes](https://andrewlock.net/creating-a-source-generator-part-4-customising-generated-code-with-marker-attributes/)
5.   [Part 5 - Finding a type declaration's namespace and type hierarchy](https://andrewlock.net/creating-a-source-generator-part-5-finding-a-type-declarations-namespace-and-type-hierarchy/)
6.   [Part 6 - Saving source generator output in source control](https://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/)
7.   [Part 7 - Solving the source generator 'marker attribute' problem - Part 1](https://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/)
8.   [Part 8 - Solving the source generator 'marker attribute' problem - Part 2](https://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/)
9.   [Part 9 - Avoiding performance pitfalls in incremental generators](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/)
10.   Part 10 - Testing your incremental generator pipeline outputs are cacheable (this post) 
11.   [Part 11 - Implementing an interceptor with a source generator](https://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/)
12.   [Part 12 - Reading compilation options and the C# version in source generators](https://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/)
13.   [Part 13 - Accessing MSBuild properties and user configuration from source generators](https://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/)
14.   [Part 14 - Supporting multiple .NET SDK versions in a source generator](https://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/)
15.   [Part 15 - Solving the source generator 'marker attribute' problem in .NET 10](https://andrewlock.net/exploring-dotnet-10-preview-features-4-solving-the-source-generator-marker-attribute-problem-in-dotnet-10/)

In [my previous post](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/) I described some of the patterns you should use when building an incremental generator to ensure you don't slow your IDE's performance. In this post I show an approach to _test_ that you're following that advice using unit tests.

> This post builds directly on [the previous post](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/) where I discuss potential performance issues with incremental generators. It also assumes that you have unit tests for your generators, using the `CSharpGeneratorDriver`, [as described in an earlier post](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/).

## [Cacheability is the most important factor](http://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/#cacheability-is-the-most-important-factor)

As a reminder, when we're talking about the performance of an incremental generator, we're _not_ talking about the performance of the code that the source generator outputs. Rather, we're talking about how the performance of the generator _itself_ when it runs inside the IDE. As discussed in [the previous post](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/), you can easily end up in a situation where you're performing huge amounts of work with every keypress in the IDE!

One of the key ways to reduce the work of the generator is to take advantage of the "incremental pipeline". Incremental generators are designed to heavily cache the values you output, and to use [memoisation](https://en.wikipedia.org/wiki/Memoization) to significantly reduce the work your IDE has to do.

As an example, lets consider the `EnumExtensions` generator I've used as an example throughput this series. The generator pipeline might look something like the following example. I've added rough "names" to describe what each stage in the pipeline does.

```
[Generator]
public class EnumGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 👇 Inital extraction - the predicate and transform run very frequently
        IncrementalValuesProvider<EnumDetails?> enumDetails = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "NetEscapades.EnumGenerators.EnumExtensionsAttribute", 
                predicate: static (node, _) => node is EnumDeclarationSyntax,
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx));

        // 👇 Transform - this (somewhat contrived) stage transforms EnumDetails into EnumToGenerate
        IncrementalValuesProvider<EnumToGenerate> valuesToGenerate = enumDetails
            .Select(static (value, _) => ConvertValue(value));

        // Output - Generates the source code
        context.RegisterSourceOutput(enumsToGenerate,
            static (spc, source) => Execute(source, spc));
    }
}
```

The design of this simple generator follows the rules from my last post:

*   It uses `ForAttributeWithMetadataName`
*   Uses a value-type based data model based on `record` instances

The primary reason for the `record`-based data model is to ensure that the outputs of each stage of the pipeline can be compared. If the output of a stage has been seen before, then the subsequent steps in the pipeline don't need to execute for that stage, as shown below

![Image 1: Image of caching and memoisation in incremental generators](https://andrewlock.net/content/images/2024/generator_caching.svg)

As shown above, if any of your stages _aren't_ correctly cacheable (i.e. don't have value-equality) then you'll be doing a lot more work with every IDE key press, as all the stages will _always_ execute!

Ensuring you have a layered, highly cacheable, incremental generator is one of the best ways to improve the overall performance of your generator in the IDE. The trouble is, it's not always obvious when you've accidentally _not_ created a cacheable generator!

## [Testing for problematic generator outputs](http://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/#testing-for-problematic-generator-outputs)

The incremental generator APIs don't _prevent_ you from doing all sorts of things that could create terrible IDE performance issues. And while [helpers exist to test that your generator produces the right _output_](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/), it's much harder to test that your generators are caching correctly.

In this section I show some helper methods I've been playing with that attempt to do just that: they'll warn you if you've accidentally made an output uncacheable, or if you've used any "problematic" types like `Compilation`.

> This is prototype stuff based on [some suggestions](https://github.com/zompinc/sync-method-generator/issues/20#issuecomment-1890832210) from [Cyrus Najmabadi](https://github.com/CyrusNajmabadi) and things I've found in [the .NET repos](https://github.com/dotnet/runtime/blob/b4488eca7f87cde6f2a19d388b79a723d9ec71e8/src/libraries/System.Text.Json/tests/System.Text.Json.SourceGeneration.Unit.Tests/JsonSourceGeneratorIncrementalTests.cs), but as it's just for use in unit tests, it's pretty low risk to give it a try. I subsequently also found [this similar post](https://www.meziantou.net/testing-roslyn-incremental-source-generators.htm) from Gérald Barré.

These helpers are intended to be used with [your "normal" generator unit tests](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/) that use `CSharpGeneratorDriver`. The basic idea is:

*   Add "tracking names" to each of the output steps you're interested in
*   Run the generator with a given `Compilation`
*   Clone the `Compilation`, and re-run the generator
*   Make some assertions about the results 
    *   All of the generated outputs should be cached
    *   All of the intermediate stages should be cached/unchanged
    *   The outputs of equivalent stages should be equal across runs
    *   None of the tracked outputs include problematic outputs like `Compilation`

This list of assertions is probably a bit excessive, as the first two probably _imply_ the latter two, so you may not want to go as deep as I do in this post!

> You can see [an example of a similar approach](https://github.com/dotnet/runtime/blob/b4488eca7f87cde6f2a19d388b79a723d9ec71e8/src/libraries/System.Text.Json/tests/System.Text.Json.SourceGeneration.Unit.Tests/JsonSourceGeneratorIncrementalTests.cs#L190) in the .NET runtime repository, testing the caching behaviour of the System.Text.Json source generator.

The helper methods rely on adding a "tracking name" to the output stages of your generator. This ensures you only compare the stages that you control, instead of the "internal" stages of the incremental generator pipeline, some of which _won't_ be cached.

### [Adding tracking names to your stages](http://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/#adding-tracking-names-to-your-stages)

You can give a name to any of the output stages in your incremental generator pipeline by calling `WithTrackingName(name)` and passing in a `string`. To make them easier to work with later, I put all the tracking names in a class as `const`s:

```
internal class TrackingNames
{
    public const string InitialExtraction = nameof(InitialExtraction);
    public const string Transform = nameof(Transform);
}
```

The exact names you use here don't matter, the important thing is just that they're unique. You can then attach them to each of the intermediate stages of your pipeline. The example pipeline below only has two initial stages, so we only have two tracking names:

```
[Generator]
public class EnumGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<EnumDetails?> enumDetails = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "NetEscapades.EnumGenerators.EnumExtensionsAttribute", 
                predicate: static (node, _) => node is EnumDeclarationSyntax,
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .WithTrackingName(TrackingNames.InitialExtraction); // 👈 Add tracking name

        IncrementalValuesProvider<EnumToGenerate> valuesToGenerate = enumDetails
            .Select(static (value, _) => ConvertValue(value))
            .WithTrackingName(TrackingNames.Transform); // 👈 Add tracking name

        context.RegisterSourceOutput(enumsToGenerate,
            static (spc, source) => Execute(source, spc));
    }
}
```

The incremental generator pipeline will uses these names when storing the intermediate values of the pipeline, so we can retrieve them later! Next we'll look at the helper method that runs the generator and tests the outputs are all looking good.

### [Creating a helper for comparing generator outputs](http://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/#creating-a-helper-for-comparing-generator-outputs)

The following class is a generic helper class, [very similar to the version I showed earlier in this series](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/). This is all "standard" code for setting up a source generator test run, it's the `RunGeneratorAndAssertOutput` method that's the interesting part we'll come to shortly.

```
public class TestHelpers
{
    // You call this method passing in C# sources, and the list of stages you expect
    // It runs the generator, asserts the outputs are ok, 
    public static (ImmutableArray<Diagnostic> Diagnostics, string[] Output) GetGeneratedTrees<T>(
        string[] sources, // C# source code 
        string[] stages,  // The tracking stages we expect
        bool assertOutputs = true) // You can disable cacheability checking during dev
        where T : IIncrementalGenerator, new() // T is your generator
    {
        // Convert the source files to SyntaxTrees
        IEnumerable<SyntaxTree> syntaxTrees = sources.Select(static x => CSharpSyntaxTree.ParseText(x));

        // Configure the assembly references you need
        // This will vary depending on your generator and requirements
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(_ => !_.IsDynamic && !string.IsNullOrWhiteSpace(_.Location))
            .Select(_ => MetadataReference.CreateFromFile(_.Location))
            .Concat(new[] { MetadataReference.CreateFromFile(typeof(T).Assembly.Location) });

        // Create a Compilation object
        // You may want to specify other results here
        CSharpCompilation compilation = CSharpCompilation.Create(
            "EnumExtensions.Generated",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Run the generator, get the results, and assert cacheability if applicable
        GeneratorDriverRunResult runResult = RunGeneratorAndAssertOutput<T>(
            compilation, stages, assertOutputs);

        // Return the generator diagnostics and generated sources
        return (runResult.Diagnostics, runResult.GeneratedTrees.Select(x => x.ToString()).ToArray());
    }
}
```

This is all relatively standard stuff, although because it's Roslyn, it's still complicated for the average C# developer. 😅 You would use it in a test something like this:

```
public class EnumExtensionsGeneratorTests
{
    // A collection of all the tracking names. I'll show how to simplify this later
    private static string[] AllTrackingNames = [TrackingNames.InitialExtraction, TrackingNames.Transform];

    [Fact]
    public void CanGenerate()
    {
        const string input = """
            [EnumExtensions]
            public enum MyEnum
            {
                First,
                Second,
            }
            """;
        const string expected = """... not shown for brevity...""";

        // run the generator, passing in the inputs and the tracking names
        var (diagnostics, output) 
            = TestHelpers.GetGeneratedOutput<EnumExtensionsGenerator>([input], AllTrackingNames);

        // Assert the output
        using var s = new AssertionScope();
        diagnostics.Should().BeEmpty();
        output.Should().Be(expected);
```

If duplicating the expected list of tracking names in the test class bugs you, you might want to consider adding another overload, something like the code below. This uses reflection to grab all the `const string` fields on the `TrackingName` type automatically, so you don't have to remember to update your tests!

```
public static (ImmutableArray<Diagnostic> Diagnostics, string[] Output) GetGeneratedTrees<T>(params string[] sources)
    where T : IIncrementalGenerator, new()
{
    // get all the const string fields on the TrackingName type
    var trackingNames = typeof(TrackingName)
                        .GetFields()
                        .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string))
                        .Select(x => (string)x.GetRawConstantValue())
                        .Where(x => !string.IsNullOrEmpty(x))
                        .ToArray();

    // Call the other overload, passing in the tracking names
    return GetGeneratedTrees<T>(sources, trackingNames);
}
```

OK, that's the public API for the test helpers, now lets look inside the `RunGeneratorAndAssertOutput<T>()` method.

### [Running the generator and collecting the run results](http://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/#running-the-generator-and-collecting-the-run-results)

The `RunGeneratorAndAssertOutput<T>` method shown below does the following:

*   Creates an instance of the incremental generator and converts it to `ISourceGenerator`
*   Tells the `CSharpGeneratorDriver` to track all the outputs
*   Creates a clone of the `Compilation`
*   Runs the generator and caches the outputs in the `GeneratorDriver` instance
*   Runs the generator again, and compares the tracked outputs from both runs. Throws if there's a problem
*   Verifies that all the final generated source code were cached on the second run
*   Returns the first run result

I'll discuss more about comparing the outputs shortly, but for now, this is the implementation:

```
private static GeneratorDriverRunResult RunGeneratorAndAssertOutput<T>(CSharpCompilation compilation, string[] trackingNames, bool assertOutput = true)
    where T : IIncrementalGenerator, new()
{
    ISourceGenerator generator = new T().AsSourceGenerator();

    // ⚠ Tell the driver to track all the incremental generator outputs
    // without this, you'll have no tracked outputs!
    var opts = new GeneratorDriverOptions(
        disabledOutputs: IncrementalGeneratorOutputKind.None,
        trackIncrementalGeneratorSteps: true);

    GeneratorDriver driver = CSharpGeneratorDriver.Create([generator], driverOptions: opts);
    
    // Create a clone of the compilation that we will use later
    var clone = compilation.Clone();

    // Do the initial run
    // Note that we store the returned driver value, as it contains cached previous outputs
    driver = driver.RunGenerators(compilation);
    GeneratorDriverRunResult runResult = driver.GetRunResult();

    if (assertOutput)
    {
        // Run again, using the same driver, with a clone of the compilation
        GeneratorDriverRunResult runResult2 = driver
                                              .RunGenerators(clone)
                                              .GetRunResult();

        // Compare all the tracked outputs, throw if there's a failure
        AssertRunsEqual(runResult, runResult2, trackingNames);

        // verify the second run only generated cached source outputs
        runResult2.Results[0]
                    .TrackedOutputSteps
                    .SelectMany(x => x.Value) // step executions
                    .SelectMany(x => x.Outputs) // execution results
                    .Should()
                    .OnlyContain(x => x.Reason == IncrementalStepRunReason.Cached);
    }

    return runResult;
}
```

Most of what I've shown so far is just about running the generator with some extra settings.Now we'll take a look at the `AssertRunsEqual()` method which compares the two `GeneratorDriverRunResult` instances to make sure we have the caching we expect!

### [Comparing the run results](http://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/#comparing-the-run-results)

The `AssertRunsEqual()` method shown below is relatively opinionated, in that it assumes you'll always generate at _least_ one tracked output, and it uses [FluentAssertions](https://www.nuget.org/packages/FluentAssertions) for making assertions (primarily so that you get more detailed error messages), so feel free to tweak it so that it works for you.

I've split the method into several layers of helpers, so we'll continue to walk through each layer one at a time. First we have `AssertRunsEqual()`, which does several things:

*   Extracts the list of tracked outputs from each run
*   Filters the tracked outputs to only ones we know about i.e. where we used `WithTrackingName()`
*   Makes some basic assertions that we have the same tracked outputs for both runs
*   Extracts the `ImmutableArray<IncrementalGeneratorRunStep>` collection for each tracked output

```
private static void AssertRunsEqual(
    GeneratorDriverRunResult runResult1,
    GeneratorDriverRunResult runResult2,
    string[] trackingNames)
{
    // We're given all the tracking names, but not all the
    // stages will necessarily execute, so extract all the 
    // output steps, and filter to ones we know about
    var trackedSteps1 = GetTrackedSteps(runResult1, trackingNames);
    var trackedSteps2 = GetTrackedSteps(runResult2, trackingNames);

    // Both runs should have the same tracked steps
    trackedSteps1.Should()
                 .NotBeEmpty()
                 .And.HaveSameCount(trackedSteps2)
                 .And.ContainKeys(trackedSteps2.Keys);

    // Get the IncrementalGeneratorRunStep collection for each run
    foreach (var (trackingName, runSteps1) in trackedSteps1)
    {
        // Assert that both runs produced the same outputs
        var runSteps2 = trackedSteps2[trackingName];
        AssertEqual(runSteps1, runSteps2, trackingName);
    }
    
    return;

    // Local function that extracts the tracked steps
    static Dictionary<string, ImmutableArray<IncrementalGeneratorRunStep>> GetTrackedSteps(
        GeneratorDriverRunResult runResult, string[] trackingNames)
        => runResult
                .Results[0] // We're only running a single generator, so this is safe
                .TrackedSteps // Get the pipeline outputs
                .Where(step => trackingNames.Contains(step.Key)) // filter to known steps
                .ToDictionary(x => x.Key, x => x.Value); // Convert to a dictionary
}
```

Note that for _each_ tracked output, there's a _collection_ of `IncrementalGeneratorRunStep`s. This makes sense if you think about our enum generator: if we have _two_`[EnumExtension]` attributes, then each stage will run at least twice, once for each attribute instance.

### [Comparing run steps](http://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/#comparing-run-steps)

We're nearly at the crucial point where we make sure our outputs are all cacheable. The `AssertEqual` method shown below:

*   Checks that both runs have the same number of executions for the tracking step
*   Asserts that all tracked outputs are considered equal between different runs
*   Asserts that the tracked outputs were correctly cached on the second run

This is where we finally verify that our outputs are correctly defined as `record`s or similar types that can be compared using `.Equals()`

> Note that if you rely on custom comparers (using `.WithComparer()` on your output), then this simple comparison won't work. I haven't bothered working out if or how to use the custom comparer as I don't use them!

```
private static void AssertEqual(
    ImmutableArray<IncrementalGeneratorRunStep> runSteps1,
    ImmutableArray<IncrementalGeneratorRunStep> runSteps2,
    string stepName)
{
    runSteps1.Should().HaveSameCount(runSteps2);

    for (var i = 0; i < runSteps1.Length; i++)
    {
        var runStep1 = runSteps1[i];
        var runStep2 = runSteps2[i];

        // The outputs should be equal between different runs
        IEnumerable<object> outputs1 = runStep1.Outputs.Select(x => x.Value);
        IEnumerable<object> outputs2 = runStep2.Outputs.Select(x => x.Value);

        outputs1.Should()
                .Equal(outputs2, $"because {stepName} should produce cacheable outputs");

        // Therefore, on the second run the results should always be cached or unchanged!
        // - Unchanged is when the _input_ has changed, but the output hasn't
        // - Cached is when the the input has not changed, so the cached output is used 
        runStep2.Outputs.Should()
            .OnlyContain(
                x => x.Reason == IncrementalStepRunReason.Cached || x.Reason == IncrementalStepRunReason.Unchanged,
                $"{stepName} expected to have reason {IncrementalStepRunReason.Cached} or {IncrementalStepRunReason.Unchanged}");

        // Make sure we're not using anything we shouldn't
        AssertObjectGraph(runStep1, stepName);
    }
}
```

At the end of the method, once we've verified the equality between outputs, we call `AssertObjectGraph()` for the outputs. This is another sanity check to make sure you're not accidentally returning anything that is an `ISymbol`, `Compilation`, or a `SyntaxNode`.

### [Making sure we don't use problematic types](http://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/#making-sure-we-don-t-use-problematic-types)

As [I described in my previous post](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/#2-don-t-use-syntax-or-isymbol-instances-in-your-pipeline), you should pretty much never include `ISymbol` or `Compilation` in your incremental generator outputs, as it can cause various GC rooting issues, as well as being problematic for equality purposes.

Similarly, it's generally not a good idea to use `SyntaxNode` instances in your models. There are some cases where you may _have_ to, but those are rare, so I chose to explicitly check and ban them in my helper.

The `AssertObjectGraph()` method shown below is based on [a similar test](https://github.com/dotnet/runtime/blob/b4488eca7f87cde6f2a19d388b79a723d9ec71e8/src/libraries/System.Text.Json/tests/System.Text.Json.SourceGeneration.Unit.Tests/JsonSourceGeneratorIncrementalTests.cs#L148C13-L183C18) used by the System.Text.Json source generator. It walks the object graph, using reflection to read each object's fields, and asserts that you're not using one of the "banned" types.

```
static void AssertObjectGraph(IncrementalGeneratorRunStep runStep, string stepName)
{
    // Including the stepName in error messages to make it easy to isolate issues
    var because = $"{stepName} shouldn't contain banned symbols";
    var visited = new HashSet<object>();

    // Check all of the outputs - probably overkill, but why not
    foreach (var (obj, _) in runStep.Outputs)
    {
        Visit(obj);
    }

    void Visit(object node)
    {
        // If we've already seen this object, or it's null, stop.
        if (node is null || !visited.Add(node))
        {
            return;
        }

        // Make sure it's not a banned type
        node.Should()
            .NotBeOfType<Compilation>(because)
            .And.NotBeOfType<ISymbol>(because)
            .And.NotBeOfType<SyntaxNode>(because);

        // Examine the object
        Type type = node.GetType();
        if (type.IsPrimitive || type.IsEnum || type == typeof(string))
        {
            return;
        }

        // If the object is a collection, check each of the values
        if (node is IEnumerable collection and not string)
        {
            foreach (object element in collection)
            {
                // recursively check each element in the collection
                Visit(element);
            }

            return;
        }

        // Recursively check each field in the object
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            object fieldValue = field.GetValue(node);
            Visit(fieldValue);
        }
    }
}
```

And that's it—some thorough checks on your generator outputs! When I ran this on [my `EnumExtensions` generator](https://github.com/andrewlock/NetEscapades.EnumGenerators), everything passed🎉 Though it made me question whether it was working correctly, so lets make sure!

### [Testing out a problematic model](http://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/#testing-out-a-problematic-model)

To test out the helper, I decided to sabotage my lovely, equatable, data model. I simply added an `object` to each instance. With this addition, two `EnumToGenerate` instances would never be considered equal, which would break caching in the generator:

```
public readonly record struct EnumToGenerate
{
    public readonly object Dummy = new(); // 👈 😱 This won't work!
    public readonly string Name;
    public readonly EquatableArray<string> Values;

    public EnumToGenerate(string name, List<string> values)
    {
        Name = name;
        Values = new(values.ToArray());
    }
}
```

Sure enough, running my unit tests again, and everything blows up!

```
Expected outputs1 to be equal to 
{
    {
        Item1 = NetEscapades.EnumGenerators.EnumToGenerate
        {
            Dummy = System.Object (HashCode=66735713), 
            Name = "MyEnum", 
            Values = [ "First", "Second" ]
        }, 
        Item2 = IncrementalStepRunReason.New {value: 0}
    }
} because InitialExtraction should produce cacheable outputs, but 
{
    {
        Item1 = NetEscapades.EnumGenerators.EnumToGenerate
        {
            Dummy = System.Object (HashCode=10863195), 
            Name = "MyEnum", 
            Values = [ "First", "Second" ]
        }, 
        Item2 = IncrementalStepRunReason.New {value: 0}
    }
} differs at index 0
```

It works! 🎉 Hopefully this will help some other generator authors make sure that they're following best-practices to keep everyone's IDEs working optimally🙂

## [Summary](http://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/#summary)

Source generator performance is a tricky thing to understand, as it can be difficult to profile or debug generators in real-world usage. In [my previous post](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/) I described a number of things you should consider when designing your generator to ensure the incremental pipeline can cache as many outputs as possible, and reduce the work your IDE has to do.

In this post I demonstrated some helper methods you can use to verify that you're sticking to the advice in the previous post, and that your generator is correctly incremental. The tests verify that your generator is caching the intermediate output steps, that your data model is equatable between runs, and that you're not using any problematic objects like `Compilation`.

[![Image 2: Avoiding performance pitfalls in incremental generators](https://andrewlock.net/content/images/2024/source_gen_perf.png)Previous Avoiding performance pitfalls in incremental generators: Creating a source generator - Part 9](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/)[![Image 3: Understanding C# 8 default interface methods](https://andrewlock.net/content/images/2024/defaultinterface.png)Next Understanding C# 8 default interface methods](https://andrewlock.net/understanding-default-interface-methods/)
