# Bonus: Solving the marker attribute problem in .NET 10 (`AddEmbeddedAttributeDefinition()`)

> Source: [Exploring the .NET 10 preview - Solving the source generator 'marker attribute' problem in .NET 10](https://andrewlock.net/exploring-dotnet-10-preview-features-4-solving-the-source-generator-marker-attribute-problem-in-dotnet-10/)
> Andrew Lock, .NET Escapades
> Original article (c) Andrew Lock, all rights reserved. This file is an original summary, not a copy.

## Why read this

Almost every generator needs a marker attribute, and the two classic ways of shipping one both hurt: emitting an `internal` attribute via `RegisterPostInitializationOutput` collides across projects wired together with `[InternalsVisibleTo]`, and shipping a separate attributes assembly means an extra project plus fiddly NuGet packaging. Roslyn 4.14 (.NET 10 / SDK 9.0.300) adds a supported way for *your* generator to mark its emitted types with the compiler's own `[Embedded]` attribute, which kills the collision without a second assembly. Send the reader here when they are choosing how to ship marker attributes, or when they are staring at CS0436 in a multi-project solution.

## Key points

### The symptom

- **CS0436 in a multi-project solution.** Generating an `internal` marker attribute into every consuming project is fine in isolation. Add `[InternalsVisibleTo]` between two of those projects and the compiler now sees the same type name, in the same namespace, from two sources at once. It reports **CS0436**, whose text is of the form *the type X in \<generated file\> conflicts with the imported type X in \<assembly\>*. It is only a warning and the generated code still compiles, but the noise is the visible symptom of a real ambiguity.

### The diagnosis

- **The generated attribute is a normal type, so it leaks.** Nothing about `RegisterPostInitializationOutput` makes emitted source private to the compilation that produced it. `internal` is the only protection, and `[InternalsVisibleTo]` is exactly the switch that removes it.
- **The compiler solved this for itself years ago.** Roslyn already synthesises types into compilations — collection-expression helpers, and attributes like `NullableAttribute`, `NullableContextAttribute`, `IsReadOnlyAttribute` and `RefSafetyRulesAttribute` when the target runtime does not supply them. It has the same multi-project collision risk you do, and it avoids it by stamping those types with an internal `Microsoft.CodeAnalysis.EmbeddedAttribute`. The marker means, in effect, *this type does not escape the current compilation* — a referencing assembly never imports it, even under IVT.

### The fix

- **Roslyn 4.14 opens that mechanism to generator authors.** `IncrementalGeneratorPostInitializationContext` gains **`AddEmbeddedAttributeDefinition()`**, which emits a correctly shaped `Microsoft.CodeAnalysis.EmbeddedAttribute` for you. You then apply that attribute to the marker types you emit. Two lines, both in post-initialization:

  ```csharp
  // inside IIncrementalGenerator.Initialize
  context.RegisterPostInitializationOutput(static postInit =>
  {
      postInit.AddEmbeddedAttributeDefinition();

      const string marker = """
          namespace Telemetry.Generated;

          [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
          internal sealed class TrackedAttribute : global::System.Attribute;
          """;

      postInit.AddSource("TrackedAttribute.g.cs", marker);
  });
  ```

- **Post-initialization is not optional.** Both the `EmbeddedAttribute` definition and your marker attribute have to be in the compilation before the attribute-driven pipeline (`ForAttributeWithMetadataName`) matches user code against them. Emitting either from the main pipeline is too late.
- **Always fully qualify.** Write `global::Microsoft.CodeAnalysis.EmbeddedAttribute` in emitted source so it resolves no matter what namespace the user's code sits in. Andrew notes this is often unnecessary but occasionally bites, so he always does it.
- **If you hand-write the definition instead**, it must match the shape the compiler expects exactly: type `Microsoft.CodeAnalysis.EmbeddedAttribute`, `internal`, a `class`, `sealed`, non-`static`, inheriting from `System.Attribute`, with an `internal` or `public` parameterless constructor, and usable on any type declaration (class, struct, interface, enum, delegate). `AddEmbeddedAttributeDefinition()` exists so you do not have to get that right by hand.

### The cost

- **Toolchain floor.** The API needs `Microsoft.CodeAnalysis.CSharp` **4.14.0** or later in your generator. The consequence lands on your *users*: installing your package then requires **.NET SDK 9.0.300+ or .NET 10 preview 4+**, and **Visual Studio 17.14+**. That is a support-matrix decision, not just a build detail.
- **Decision rule.** New generator, or an audience you know is on a current SDK: use `AddEmbeddedAttributeDefinition()` and document the requirement. Existing generator with a working attributes DLL and older consumers: leave it alone — you would be making a breaking change without an active problem to solve.
- **The two approaches compose.** If you accept the SDK floor you can do both: embed the marker attributes, and keep a shared DLL only for the public helper types that genuinely have to be visible downstream.

## Pitfalls

- **Embedded types cannot appear in your public API surface.** This is the big one. If your generator emits a `public` API that takes or returns a generated helper type — an options enum passed to a generated extension method, for example — that helper must be a genuinely visible type. Marking it `[Embedded]` makes it invisible outside the generating compilation, so a caller in another project cannot construct the argument and the public method becomes uncallable. Marker attributes are fine; shared public helper types are not.
- **Raising the Roslyn reference to 4.14 is a breaking change for consumers** on older SDKs. Do not bump it casually in a widely-installed package.
- **Switching an existing generator to embedded attributes churns for no gain** if the shared-DLL packaging already works — and the shared DLL can carry non-attribute code, which embedding cannot.
- **Don't hand-roll a near-miss `EmbeddedAttribute`.** Any deviation from the required shape means the compiler will not treat it as the real thing, and you are back to CS0436 with no obvious cause.
- **`[Embedded]` is per-compilation, not per-project-tree.** *Current project* is the shorthand; the real boundary is the compilation, which matters for multi-targeting and for tests that compile generated output separately.

## In his words

> Applying `[Embedded]` to a type ensures that it's not visible outside the _current_ project (more accurately, the current compilation).

— Andrew Lock, [.NET Escapades](https://andrewlock.net/exploring-dotnet-10-preview-features-4-solving-the-source-generator-marker-attribute-problem-in-dotnet-10/)

> If you're already using the "shared" dll approach, then you may not have much to gain by switching to `AddEmbeddedAttributeDefinition()`.

— Andrew Lock, [.NET Escapades](https://andrewlock.net/exploring-dotnet-10-preview-features-4-solving-the-source-generator-marker-attribute-problem-in-dotnet-10/)

## Related in the series

Parts 7 and 8 of the "Creating a source generator" series cover the original statement of this problem and the shared-attributes-DLL workaround this post is measured against. Read those first if you need the packaging mechanics; read this one to decide whether you still need them.

## Read the full article

[Exploring the .NET 10 preview - Solving the source generator 'marker attribute' problem in .NET 10](https://andrewlock.net/exploring-dotnet-10-preview-features-4-solving-the-source-generator-marker-attribute-problem-in-dotnet-10/) — Andrew Lock, .NET Escapades.
