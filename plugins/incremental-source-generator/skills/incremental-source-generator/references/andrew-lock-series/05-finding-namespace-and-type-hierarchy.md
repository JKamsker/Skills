# Part 5 — Finding a type declaration's namespace and type hierarchy

> Source: [Creating a source generator, part 5: Finding a type declaration's namespace and type hierarchy](https://andrewlock.net/creating-a-source-generator-part-5-finding-a-type-declarations-namespace-and-type-hierarchy/)
> Andrew Lock, .NET Escapades
> Original article (c) Andrew Lock, all rights reserved. This file is an original summary, not a copy.

## Why read this

Your generator has a `BaseTypeDeclarationSyntax` in hand and now has to emit code that sits in the *same* namespace and the *same* nesting as the original declaration — otherwise the generated `partial` never joins up with the user's type. This post gives two small, reusable syntax-walking helpers: one that resolves a namespace (block-scoped, file-scoped, nested, or global), and one that rebuilds the chain of containing types so the emitted code can re-open every enclosing `class`/`struct`/`record` correctly. Send a reader here whenever generated partials fail to merge, when a nested type breaks the generator, or when they are tempted to build a type reference by string-concatenating `Outer.Inner`.

## Key points

- **The namespace helper** has the shape `static string GetNamespace(BaseTypeDeclarationSyntax syntax)` and returns `string.Empty` when the type lives in the global namespace. `BaseTypeDeclarationSyntax` is the right parameter type because it covers classes, structs, records *and* enums (`EnumDeclarationSyntax` derives from it, but not from `TypeDeclarationSyntax`).
- **Walk `SyntaxNode.Parent` upward.** Start at `syntax.Parent` into a `potentialNamespaceParent` variable and keep reassigning `potentialNamespaceParent = potentialNamespaceParent.Parent` while it is non-null and is neither a `NamespaceDeclarationSyntax` nor a `FileScopedNamespaceDeclarationSyntax`. This skips over any intervening type declarations.
- **`BaseNamespaceDeclarationSyntax` is the unifying type.** Both `NamespaceDeclarationSyntax` (`namespace Foo { }`) and `FileScopedNamespaceDeclarationSyntax` (`namespace Foo;`, C# 10) derive from it, so one pattern match handles both, and `.Name.ToString()` gives the declared name.
- **Nested block namespaces need a second loop.** After finding the innermost namespace node, keep checking whether *its* `Parent` is a `NamespaceDeclarationSyntax`; if so, prepend that outer name plus a `.` and move up again. File-scoped namespaces cannot nest, so only `NamespaceDeclarationSyntax` matters in this outer loop.
- **The hierarchy helper** is `static ParentClass? GetParentClasses(BaseTypeDeclarationSyntax typeSyntax)`, returning a small linked list. `ParentClass` carries four things: `Keyword` (string), `Name` (string), `Constraints` (string), and `Child` (`ParentClass?`).
- **Only certain parents count.** An `IsAllowedKind(SyntaxKind kind)` predicate accepts `SyntaxKind.ClassDeclaration`, `SyntaxKind.StructDeclaration` and `SyntaxKind.RecordDeclaration`. The walk casts the parent to `TypeDeclarationSyntax` and keeps going while the kind is allowed.
- **Capture the parent's declaration verbatim-ish, from syntax, not from a symbol name:**
  - `parentSyntax.Keyword.ValueText` → `"class"` / `"struct"` / `"record"`, so you re-emit the same construct.
  - `parentSyntax.Identifier.ToString() + parentSyntax.TypeParameterList` → `Outer<T>` including arity.
  - `parentSyntax.ConstraintClauses.ToString()` → `where T : new()` etc., which C# requires you to repeat on every partial declaration of a generic type.
- **Ordering falls out of the construction.** Each newly discovered (more outer) parent becomes the new head with `Child` set to the previously built node, so the returned head is the *outermost* type and you can simply follow `Child` inward when writing. No separate reversal step.
- **Emission shape:** open the namespace if non-empty; walk the `ParentClass` chain writing `partial {Keyword} {Name} {Constraints}` followed by `{` for each level, counting them into `parentsCount`; write your real generated member; then close `parentsCount` braces; then close the namespace brace.
- **Every enclosing level must be declared `partial`** in the generated file, and the user's own outer types must be `partial` too — not just the target type.
- Andrew points at Microsoft's `LoggerMessage` generator as prior art for this pattern.
- *(Not in the article, but useful when you already have symbols:)* if your transform produced an `INamedTypeSymbol`, `symbol.ContainingNamespace.IsGlobalNamespace` / `.ToDisplayString()` and `symbol.ContainingType` give the same information from the semantic model. Either way, flatten to plain strings before they enter the incremental pipeline.

## Pitfalls

- **Don't build `Outer<T>.Nested` as a name.** Concatenating containing type names produces something that is not valid where you need it (a generic outer type makes it outright illegal). Re-declaring the nesting is the only correct approach — that is the whole reason for the linked list.
- **Dropping type parameters or constraint clauses.** A partial declaration of `Outer<T>` must repeat `<T>` and its `where` clauses on every part; omit either and the consumer's build fails, often with a confusing constraint-mismatch error.
- **Emitting a file-scoped namespace in generated output** forces C# 10 on every consuming project. Block-scoped `namespace X { }` in generated code is the safer default; whether that matters depends on the language versions you intend to support.
- **Forgetting the global-namespace case.** An empty namespace string must mean "write no namespace block at all", not `namespace  {`.
- **Miscounting closing braces.** Namespace and parent-type braces are separate counts; the `parentsCount` bookkeeping exists precisely so the two do not drift.
- **`IsAllowedKind` as written omits some kinds.** Nested `record struct` parents surface as `SyntaxKind.RecordStructDeclaration`, and `interface` parents as `SyntaxKind.InterfaceDeclaration`; neither is in the article's list. If you support them, extend the predicate — and note that for `RecordDeclarationSyntax` you may also need `ClassOrStructKeyword` to reconstruct `record struct` faithfully.
- **Enums are not containers.** An enum can be your generation *target* (hence `BaseTypeDeclarationSyntax`), but it can never be a parent in the chain, and it cannot be `partial` — so enum-driven generators emit into a separate class rather than into the enum itself.

## In his words

His `GetNamespace()` sample carries code comments noting that a type with no namespace returns an
empty string, which is how the "default namespace" case is handled. (Paraphrased: those are comments
inside his snippet, not prose.)

On extending the approach to nested types:

> "Unfortunately, this falls down if you have a generic outer type, e.g. Outer<T>."

— Andrew Lock, [.NET Escapades](https://andrewlock.net/creating-a-source-generator-part-5-finding-a-type-declarations-namespace-and-type-hierarchy/)

## Read the full article

<https://andrewlock.net/creating-a-source-generator-part-5-finding-a-type-declarations-namespace-and-type-hierarchy/>
