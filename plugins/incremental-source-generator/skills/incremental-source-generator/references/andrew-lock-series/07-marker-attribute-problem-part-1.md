# Part 7 — The marker attribute problem (part 1): where should the attribute live?

> Source: [Solving the source generator 'marker attribute' problem, part 1](https://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/)
> Andrew Lock, .NET Escapades
> Original article (c) Andrew Lock, all rights reserved. This file is an original summary, not a copy.

## Why read this

Almost every generator is driven by a marker attribute (`[LoggerMessage]`, `[StronglyTypedId]`, your own), and the naive answer — emit the attribute from the generator itself — quietly breaks for anyone whose consumers have project-to-project references. Send a user here when they are about to add a marker attribute via `RegisterPostInitializationOutput`, when a consumer reports **CS0436** after adding the generator to two projects in a reference chain, or when they are deciding how to package the attribute alongside the analyzer. This part frames the problem and surveys the three candidate designs; the follow-up post covers what Andrew actually shipped.

## Key points

**The decision being made.** A generator needs to recognise user code. The attribute type it looks for has to be *defined* somewhere so user code compiles. There are only three places it can come from, and the post walks all three.

**Option 1 — the generator emits the attribute into the user's compilation.**
This is the pattern the Roslyn source generator cookbook shows: hook post-initialization and add a fixed source file.

```csharp
public void Initialize(IncrementalGeneratorInitializationContext context)
{
    context.RegisterPostInitializationOutput(static ctx =>
        ctx.AddSource("MyExampleAttribute.g.cs", AttributeSourceText));
    // ...then build the value provider pipeline
}
```

It works perfectly in a single project and fails as soon as two projects in one solution both reference the generator. Project A compiles its own `HelloWorld.MyExampleAttribute`; Project B references the same generator *and* references A, so B now has a locally-generated type plus an imported type with the identical fully-qualified name. The compiler reports **CS0436**: the type declared in the generated file conflicts with the imported type of the same name. This is the central failure the post is built around.

**Why `internal` is a mitigation, not a fix.** Emitting the attribute as `internal` rather than `public` means A's copy is not visible to B, so the ambiguity disappears — until someone uses `[InternalsVisibleTo]`. IVT re-exposes internals across the assembly boundary and the conflict comes straight back. Andrew points out this is not a theoretical edge case: his day-job `AssemblyInfo.cs` carries 22 `[InternalsVisibleTo]` declarations, and the issue was raised by real users. Crucially, there is no clean workaround a *consumer* can apply, which is what makes it a library-author problem rather than a user problem.

**CS0436 is a warning, not an error.** That is why the problem escapes notice: builds keep succeeding. It becomes a hard failure only for consumers with `TreatWarningsAsErrors` (or an equivalent warnings-as-errors policy) turned on, so a generator can ship broken for months.

**Option 2 — tell users to declare the attribute themselves.**
This is viable because a generator matches attributes by name, not by symbol identity — it does not care which assembly declares the type. The BCL leans on exactly this: attributes such as `[DoesNotReturn]` are routinely hand-declared (usually behind an `#if` for older target frameworks) rather than shipped everywhere. The downsides are practical rather than technical: users must transcribe an attribute that may have several constructor overloads, optional properties and enums; any change to that surface is a breaking change users must apply by hand; and version skew between a user's copy and the generator's expectations becomes your support burden.

**Option 3 — ship the attribute in a real referenced dll.**
Put the marker attribute in an assembly the consumer references. Exactly one definition exists, so multi-project reference chains are fine, and the attribute's API can evolve through normal NuGet versioning. The precedent cited is the xunit ecosystem, where the main package pulls in a companion analyzer package; `Microsoft.Extensions.Logging` instead ships attribute and generator together in one package. The remaining question — how to package a dll and an analyzer together so both land correctly — is the thread picked up in the next part.

## Pitfalls

- Treating the cookbook's post-initialization pattern as universally safe. It is fine for an app or a single project; it is a trap for a *library* whose consumers reference each other.
- Reaching for `internal` and considering the matter closed. `[InternalsVisibleTo]` silently undoes it, and it is far more common in real codebases than people assume.
- Ignoring CS0436 because the build is green. Downstream teams with warnings-as-errors experience it as a build break you cannot fix for them.
- Handing users a complicated attribute to hand-write. The more constructor parameters, properties and enums it has, the more the ask-users-to-define-it approach costs you in support and locks your API surface.
- Assuming the generator must bind to one canonical attribute symbol. If users may declare their own, match on the fully-qualified metadata name instead.
- Deciding attribute placement late. It affects your package layout, your versioning story and your public API — it is an up-front design call, not a packaging detail.

## In his words

> "The C# compiler uses the \"add it yourself\" approach. It doesn't care where the attribute is defined, as long as it's defined somewhere."

— Andrew Lock, [Solving the source generator 'marker attribute' problem, part 1](https://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/)

> "Source generators are really like fancy analyzers, so many of the same patterns should apply."

— Andrew Lock, [Solving the source generator 'marker attribute' problem, part 1](https://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/)

## Read the full article

The post walks each option with the real compiler output and the reasoning behind rejecting the easy answers — worth reading in full before committing to a marker-attribute strategy:
[andrewlock.net — Creating a source generator, Part 7](https://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/)
