Title: Finding a type declaration's namespace and type hierarchy: Creating a source generator - Part 5

URL Source: http://andrewlock.net/creating-a-source-generator-part-5-finding-a-type-declarations-namespace-and-type-hierarchy/

Published Time: 2022-01-11T10:00:00.0000000

Markdown Content:
January 11, 2022 ~7 min read

[Creating a source generator - Part 5](https://andrewlock.net/series/creating-a-source-generator/)

This is the fifth post in the series: [Creating a source generator](https://andrewlock.net/series/creating-a-source-generator/).

1.   [Part 1 - Creating an incremental generator](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/)
2.   [Part 2 - Testing an incremental generator with snapshot testing](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/)
3.   [Part 3 - Integration testing and NuGet packaging](https://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/)
4.   [Part 4 - Customising generated code with marker attributes](https://andrewlock.net/creating-a-source-generator-part-4-customising-generated-code-with-marker-attributes/)
5.   Part 5 - Finding a type declaration's namespace and type hierarchy (this post) 
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

In this next post on source generators, I show a couple of common patterns that I've needed when building source generators, namely:

*   How to determine the namespace of a type for a given class/struct/enum syntax
*   How to handle nested types when calculating the name of a class/struct/enum

On the face of it, these seem like simple tasks, but there are subtleties that can make it trickier than expected.

## [Finding the namespace for a class syntax](http://andrewlock.net/creating-a-source-generator-part-5-finding-a-type-declarations-namespace-and-type-hierarchy/#finding-the-namespace-for-a-class-syntax)

A common requirement for a source generator is determining the `namespace` of a given `class` or other syntax. For example, so far in this series, the `EnumExtensions` generator I've described generates its extension method in a fixed `namespace`: `NetEscapades.EnumGenerators`. One improvement might be to generate the extension method in the same `namespace` as the original `enum`.

For example, if we have this `enum`:

```
namespace MyApp.Domain
{
    [EnumExtensions]
    public enum Colour
    {
        Red = 0,
        Blue = 1,
    }
}
```

We might want to generate the extension method in the `MyApp.Domain` namespace:

```
namespace MyApp.Domain
{
    public partial static class EnumExtensions
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
}
```

On the face of it, this seems like it should be easy, but unfortunately there's quite a few cases we have to handle:

*   File scoped namespaces—introduced in C# 10, these omit the curly braces, and apply the namespace to the entire file, e.g.:

```
public namespace MyApp.Domain; // file scope namespace

[EnumExtensions]
public enum Colour
{
    Red = 0,
    Blue = 1,
}
```

*   Multiple nested namespaces—somewhat unusual, but you can have multiple nested namespace declarations:

```
namespace MyApp
{
    namespace Domain // nested namespace
    {
        [EnumExtensions]
        public enum Colour
        {
            Red = 0,
            Blue = 1,
        }
    }
}
```

*   Default namespace—if you don't specify a namespace at all, the _default_ namespace is used, which may be `global::`, but may also be overridden in the _csproj_ file using `<RootNamespace>`.

```
[EnumExtensions]
public enum Colour // no namespace specified, so uses the default
{
    Red = 0,
    Blue = 1,
}
```

The following annotated snippet is based on [code used by the `LoggerMessage` generator](https://github.com/dotnet/runtime/blob/25c675ff78e0446fe596cea25c7e3969b0936a33/src/libraries/Microsoft.Extensions.Logging.Abstractions/gen/LoggerMessageGenerator.Parser.cs#L438) to handle all these cases. It can be used when you have some sort of "type" syntax that derives from `BaseTypeDeclarationSyntax` (which includes `EnumDeclarationSyntax`, `ClassDeclarationSyntax`, `StructDeclarationSyntax`, `RecordDeclarationSyntax` etc), so it should handle most cases.

```
// determine the namespace the class/enum/struct is declared in, if any
static string GetNamespace(BaseTypeDeclarationSyntax syntax)
{
    // If we don't have a namespace at all we'll return an empty string
    // This accounts for the "default namespace" case
    string nameSpace = string.Empty;

    // Get the containing syntax node for the type declaration
    // (could be a nested type, for example)
    SyntaxNode? potentialNamespaceParent = syntax.Parent;
    
    // Keep moving "out" of nested classes etc until we get to a namespace
    // or until we run out of parents
    while (potentialNamespaceParent != null &&
            potentialNamespaceParent is not NamespaceDeclarationSyntax
            && potentialNamespaceParent is not FileScopedNamespaceDeclarationSyntax)
    {
        potentialNamespaceParent = potentialNamespaceParent.Parent;
    }

    // Build up the final namespace by looping until we no longer have a namespace declaration
    if (potentialNamespaceParent is BaseNamespaceDeclarationSyntax namespaceParent)
    {
        // We have a namespace. Use that as the type
        nameSpace = namespaceParent.Name.ToString();
        
        // Keep moving "out" of the namespace declarations until we 
        // run out of nested namespace declarations
        while (true)
        {
            if (namespaceParent.Parent is not NamespaceDeclarationSyntax parent)
            {
                break;
            }

            // Add the outer namespace as a prefix to the final namespace
            nameSpace = $"{namespaceParent.Name}.{nameSpace}";
            namespaceParent = parent;
        }
    }

    // return the final namespace
    return nameSpace;
}
```

With this code, we can handle all of the namespace cases defined above. For the default/global namespace, we return `string.Empty`, which indicates to the source generator to _not_ emit a `namespace` declaration. That ensures the generated code will be in the same namespace as the target type, whether it's `global::` or some other value defined in `<RootNamespace>`.

With this code we can now generate our extension method in the same namespace as the original `enum`. This will likely provide a better user experience for consumers of the source generator, as the extension methods for a given `enum` will be more discoverable if they're in the same namespace as the `enum` by default.

## [Finding the full type hierarchy of a type declaration syntax](http://andrewlock.net/creating-a-source-generator-part-5-finding-a-type-declarations-namespace-and-type-hierarchy/#finding-the-full-type-hierarchy-of-a-type-declaration-syntax)

So far in this series, we have implicitly supported _nested_`enum`s for our extension methods, because we've been calling `ToString()` on an `INamedTypeSymbol`, which accounts for nested types. For example, if you have an `enum` defined like this:

```
public record Outer
{
    public class Nested
    {
        [EnumExtensions]
        public enum Colour
        {
            Red = 0,
            Blue = 1,
        }
    }
}
```

Then calling `ToString()` on the `Colour` syntax returns `Outer.Nested.Colour`, which we can happily use in our extension method:

```
public static partial class EnumExtensions
{
    public static string ToStringFast(this Outer.Nested.Colour value)
        => value switch
        {
            Outer.Nested.Colour.Red => nameof(Outer.Nested.Colour.Red),
            Outer.Nested.Colour.Blue => nameof(Outer.Nested.Colour.Blue),
            _ => value.ToString(),
        };
}
```

Unfortunately, this falls down if you have a generic outer type, e.g. `Outer<T>`. Replacing `Outer` with `Outer<T>` in the above snippet results in an `EnumExtensions` class that doesn't compile:

```
public static partial class EnumExtensions
{
    public static string ToStringFast(this Outer<T>.Nested.Colour value) // 👈 Not valid C#
    // ...
}
```

There are a couple of ways to handle this, but in most cases, we need to understand the whole hierarchy of types. We can't simply "replicate" the hierarchy for our extension class (extension methods can be defined in nested types), but if you're extending types in other ways, this may well solve your problem. For example, I have a source generator project that adds members to `struct` types call [StronglyTypedId](https://github.com/andrewlock/StronglyTypedId). If you decorate a nested struct like this:

```
public partial record Outer
{
    public partial class Generic<T> where T: new()
    {
        public partial struct Nested
        {
            [StronglyTypedId]
            public partial readonly struct TestId
            {
            }
        }
    }
}
```

then we need to generate code similar to the following, that replicates the hierarchy:

```
public partial record Outer
{
    public partial class Generic<T> where T: new()
    {
        public partial struct Nested
        {
            public partial readonly struct TestId
            {
                public TestId (int value) => Value = value;
                public int Value { get; }
                // ... etc
            }
        }
    }
}
```

This avoids us needing to add special handling for generic types or anything like that, and is generally very versatile. It's the same approach [the `LoggerMessage` generator uses](https://andrewlock.net/exploring-dotnet-6-part-8-improving-logging-performance-with-source-generators/) to implement high-performance logging in .NET 6.

To implement this in our source generator, we'll need a helper (that we'll call `ParentClass`), to hold the details of each "parent" type of the nested target (`Colour`). We need to record 3 pieces of information:

*   The `keyword` of the type, i.e. `class`/`stuct`/`record`
*   The `name` of the type, i.e. `Outer`, `Nested`, `Generic<T>`
*   Any constraints on a generic type i.e. `where T: new()`

We also need to record the parent/child reference between classes. We could use a `stack`/`queue` for this, but the implementation below uses a linked list approach instead, where each `ParentClass` contains a reference to its child:

```
internal class ParentClass
{
    public ParentClass(string keyword, string name, string constraints, ParentClass? child)
    {
        Keyword = keyword;
        Name = name;
        Constraints = constraints;
        Child = child;
    }

    public ParentClass? Child { get; }
    public string Keyword { get; }
    public string Name { get; }
    public string Constraints { get; }
}
```

Starting from the `enum` declaration itself, we can build up the linked list of `ParentClass`es, using code similar to the following. As before, this code works for any type (`class`/`struct` etc):

```
static ParentClass? GetParentClasses(BaseTypeDeclarationSyntax typeSyntax)
{
    // Try and get the parent syntax. If it isn't a type like class/struct, this will be null
    TypeDeclarationSyntax? parentSyntax = typeSyntax.Parent as TypeDeclarationSyntax;
    ParentClass? parentClassInfo = null;

    // Keep looping while we're in a supported nested type
    while (parentSyntax != null && IsAllowedKind(parentSyntax.Kind()))
    {
        // Record the parent type keyword (class/struct etc), name, and constraints
        parentClassInfo = new ParentClass(
            keyword: parentSyntax.Keyword.ValueText,
            name: parentSyntax.Identifier.ToString() + parentSyntax.TypeParameterList,
            constraints: parentSyntax.ConstraintClauses.ToString(),
            child: parentClassInfo); // set the child link (null initially)

        // Move to the next outer type
        parentSyntax = (parentSyntax.Parent as TypeDeclarationSyntax);
    }

    // return a link to the outermost parent type
    return parentClassInfo;

}

// We can only be nested in class/struct/record
static bool IsAllowedKind(SyntaxKind kind) =>
    kind == SyntaxKind.ClassDeclaration ||
    kind == SyntaxKind.StructDeclaration ||
    kind == SyntaxKind.RecordDeclaration;
```

This code builds up the list starting from the type closest to our target type. So for our previous example, this creates a ParentClass hierarchy that is equivalent to this:

```
var parent = new ParentClass(
    keyword: "record",
    name: "Outer",
    constraints: "",
    child: new ParentClass(
        keyword: "class",
        name: "Generic<T>",
        constraints: "where T: new()",
        child: new ParentClass(
            keyword: "struct",
            name: "Nested",
            constraints: "",
            child: null
        )
    )
);
```

We can then reconstruct this hierarchy in our source generator when generating the output. The following shows a simple way to use both the `ParentClass` hierarchy and the extracted namespace from the previous section:

```
static public GetResource(string nameSpace, ParentClass? parentClass)
{
    var sb = new StringBuilder();

    // If we don't have a namespace, generate the code in the "default"
    // namespace, either global:: or a different <RootNamespace>
    var hasNamespace = !string.IsNullOrEmpty(nameSpace)
    if (hasNamespace)
    {
        // We could use a file-scoped namespace here which would be a little impler, 
        // but that requires C# 10, which might not be available. 
        // Depends what you want to support!
        sb
            .Append("namespace ")
            .Append(nameSpace)
            .AppendLine(@"
    {");
    }

    // Loop through the full parent type hiearchy, starting with the outermost
    while (parentClass is not null)
    {
        sb
            .Append("    partial ")
            .Append(parentClass.Keyword) // e.g. class/struct/record
            .Append(' ')
            .Append(parentClass.Name) // e.g. Outer/Generic<T>
            .Append(' ')
            .Append(parentClass.Constraints) // e.g. where T: new()
            .AppendLine(@"
        {");
        parentsCount++; // keep track of how many layers deep we are
        parentClass = parentClass.Child; // repeat with the next child
    }

    // Write the actual target generation code here. Not shown for brevity
    sb.AppendLine(@"public partial readonly struct TestId
    {
    }");

    // We need to "close" each of the parent types, so write
    // the required number of '}'
    for (int i = 0; i < parentsCount; i++)
    {
        sb.AppendLine(@"    }");
    }

    // Close the namespace, if we had one
    if (hasNamespace)
    {
        sb.Append('}').AppendLine();
    }

    return sb.ToString();
}
```

The above example isn't a complete example, and won't work for every situation, but it shows one possible approach which may work for you, as I've found it useful in several situations.

## [Summary](http://andrewlock.net/creating-a-source-generator-part-5-finding-a-type-declarations-namespace-and-type-hierarchy/#summary)

In this post I showed how to calculate two specific features useful in source generators: the namespace of a type declaration syntax, and the nested type hierarchy of a type declaration syntax. These won't always be necessary, but they can be useful for handling complexities like generic parent types, or for ensuring you generate your code in the same namespace as the original.

[![Image 1: Customising generated code with marker attributes](https://andrewlock.net/content/images/2021/source_gen_banner.png)Previous Customising generated code with marker attributes: Creating a source generator - Part 4](https://andrewlock.net/creating-a-source-generator-part-4-customising-generated-code-with-marker-attributes/)[![Image 2: Saving source generator output in source control](https://andrewlock.net/content/images/2022/source_gen_emit_banner.png)Next Saving source generator output in source control: Creating a source generator - Part 6](https://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/)

Andrew Lock | .Net Escapades

![Image 3](https://andrewlock.net/assets/img/icons/apple/apple-touch-icon-180x180.png)Want an email when

there's new posts?
