# Part 4 — Customising Generated Code With Marker Attributes

> Source: [Creating a source generator - Part 4: Customising generated code with marker attributes](https://andrewlock.net/creating-a-source-generator-part-4-customising-generated-code-with-marker-attributes/)
> Andrew Lock, .NET Escapades
> Original article (c) Andrew Lock, all rights reserved. This file is an original summary, not a copy.

## Why read this

Sooner or later a generator needs per-target configuration: a different class name, a flag, an opt-out. The natural knob is the marker attribute you already use to select targets — give it properties or constructor parameters, then read them back through the semantic model. Send the reader here when they are wiring a marker attribute into an `IIncrementalGenerator` for the first time, or when they have an attribute in place but do not know how to pull the caller's arguments out of `AttributeData` safely.

## Key points

- **Ship the attribute from the generator itself.** Register it in `Initialize` with `context.RegisterPostInitializationOutput(ctx => ctx.AddSource("EnumExtensionsAttribute.g.cs", SourceText.From(attributeSource, Encoding.UTF8)))`. Post-initialization output is added to the compilation *before* the rest of the pipeline runs, so the attribute is a real symbol your own semantic queries can resolve. Source emitted from `RegisterSourceOutput` is not visible that way — this ordering property is the whole reason post-init output exists.
- **The attribute is plain C# text.** In the running example it lives in namespace `NetEscapades.EnumGenerators`, is named `EnumExtensionsAttribute`, derives from `System.Attribute`, and is constrained with `[AttributeUsage(AttributeTargets.Enum)]`. Fully-qualify `System.Attribute` / `System.AttributeUsage` inside the generated file so it compiles regardless of the consumer's usings.
- **Add the knob.** Part 4's change is a `public string ExtensionClassName { get; set; }` property so each decorated enum can name its own generated extensions class, plus (later in the post) a constructor overload so the same value can be passed positionally.
- **Select candidates as before.** `context.SyntaxProvider.CreateSyntaxProvider(predicate, transform)`: the predicate is a cheap syntactic check (an `EnumDeclarationSyntax` whose `AttributeLists` are non-empty); the transform resolves each `AttributeSyntax` through the semantic model and compares its containing type's display string to the attribute's fully-qualified name. Combine with `compilation.GetTypeByMetadataName("NetEscapades.EnumGenerators.EnumExtensionsAttribute")` and bail out early if that returns `null`.
- **Read the arguments from `AttributeData`.** Walk `enumSymbol.GetAttributes()`, skip anything whose `AttributeClass` is not equal to your attribute symbol under `SymbolEqualityComparer.Default`, then inspect two collections on the surviving `AttributeData`:
  - `ConstructorArguments` — an `ImmutableArray<TypedConstant>`, positional, ordered.
  - `NamedArguments` — `ImmutableArray<KeyValuePair<string, TypedConstant>>`, keyed by property name (`"ExtensionClassName"`).
- **`TypedConstant` is the unit of value.** Check `Kind` first: `TypedConstantKind.Error` means the user's code does not compile (typo'd property, wrong type), so abandon generation for that target rather than emitting garbage. Otherwise read `.Value` and cast, or use `.Value?.ToString()`.
- **Order of precedence.** Establish a default (`"EnumExtensions"` in the example), then let constructor arguments overwrite it, then named arguments overwrite those — named arguments are written after positional ones in source, so applying them last matches user expectation.
- **Carry the result forward as data, not symbols.** The extracted class name is stored on the value-equatable model type (`EnumToGenerate`) that flows to `RegisterSourceOutput`, which then emits one extensions class per enum instead of one shared class. Keeping symbols out of the pipeline model is what preserves incrementality.

Minimal shape of the read (original illustration, not the article's code):

```csharp
var className = "EnumExtensions"; // default
foreach (var arg in attributeData.NamedArguments)
{
    if (arg.Value.Kind == TypedConstantKind.Error) return null; // don't generate for broken input
    if (arg.Key == "ExtensionClassName" && arg.Value.Value is string s) className = s;
}
```

## Pitfalls

- Emitting the attribute with `RegisterSourceOutput` instead of `RegisterPostInitializationOutput`: the type will not be resolvable when your own pipeline asks the semantic model about it.
- Trusting `TypedConstant.Value` without checking `Kind`. Half-typed or invalid code reaches the generator constantly in the IDE; an unchecked cast turns a red squiggle into a generator crash or a nonsense file.
- Assuming a single shape of usage. Once the attribute has both constructor parameters and settable properties, you must merge both sources of truth — and the combinatorics grow with each overload you add.
- Forgetting the fallback when no argument is supplied; a null or empty class name produces uncompilable output.
- Matching attributes by simple name only. Compare against the resolved symbol (`SymbolEqualityComparer.Default`) or a fully-qualified metadata name so a same-named attribute from another namespace does not trigger generation.
- Not covered by this post, but worth knowing: an attribute injected via post-init lands in *every* assembly that references the generator, which matters for `public` vs `internal` visibility and for cross-assembly usage. Andrew has separate posts on that 'marker attribute' problem (parts 7, 8 and 15 here). Also, on Roslyn 4.3.1+ (.NET 7 SDK), `context.SyntaxProvider.ForAttributeWithMetadataName(...)` replaces the hand-written predicate/transform pair shown here and is dramatically faster.

## In his words

> "And if you have multiple properties, and multiple constructors, then things become more complicated again."

— Andrew Lock, [Creating a source generator - Part 4](https://andrewlock.net/creating-a-source-generator-part-4-customising-generated-code-with-marker-attributes/)

## Read the full article

[Creating a source generator - Part 4: Customising generated code with marker attributes](https://andrewlock.net/creating-a-source-generator-part-4-customising-generated-code-with-marker-attributes/) — Andrew Lock, .NET Escapades.
