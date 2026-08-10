# Part 11 — Implementing an interceptor with a source generator

> [Creating a source generator - Part 11: Implementing an interceptor with a source generator](https://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/)
> Andrew Lock, .NET Escapades
> Original article (c) Andrew Lock, all rights reserved. This file is an original summary, not a copy.

## Why read this

Normal source generators can only *add* code; they cannot change a call the user already wrote. Interceptors close that gap: the generator marks a specific call site and the compiler rewrites that one call to point at a generated method instead. Send the reader here when they want an existing API call transparently replaced with a faster or AOT-friendly version without touching the caller's source, or when they are trying to work out what the `InterceptsLocationAttribute` `data` blob is and how to produce it correctly.

## Key points

- **What an interceptor is.** A method decorated with `[InterceptsLocation]` that names one exact call site in the user's source. At compile time the compiler redirects that call to the intercepting method. The motivating use case is AOT: replacing a reflection-based call with generated code.
- **The attribute.** `System.Runtime.CompilerServices.InterceptsLocationAttribute`. The current constructor is `InterceptsLocationAttribute(int version, string data)`. Emitted usage looks like `[InterceptsLocation(version: 1, data: "…base64…")]`.
- **The `data` blob is opaque and content-derived.** It packs a 16-byte xxHash128 checksum of the intercepted file, the position within that file, and the file name (used only for error reporting), base64-encoded. It is not human-readable and not something you assemble by hand.
- **Producing the location.** Call the Roslyn extension `GetInterceptableLocation(…)` on the `SemanticModel` for the invocation node. It returns an `InterceptableLocation`, whose members you use directly:
  - `Version` — the `version` argument (currently `1`)
  - `Data` — the base64 `data` argument
  - `GetDisplayLocation()` — a human-readable `File.cs(line,col)` string, useful for a comment above the generated method or for diagnostics
- **This API is still gated behind an experimental diagnostic.** Wrap the call in `#pragma warning disable RSEXPERIMENTAL002` (the article notes this is expected to change).
- **Sketch of the generator side** (original illustration, not the article's code):

  ```csharp
  #pragma warning disable RSEXPERIMENTAL002
  var loc = ctx.SemanticModel.GetInterceptableLocation(invocation);
  #pragma warning restore RSEXPERIMENTAL002
  // then emit: [InterceptsLocation(loc.Version, "…loc.Data…")]
  ```

- **The generator emits the attribute definition too.** The post generates `InterceptsLocationAttribute` into `System.Runtime.CompilerServices` itself, under `#nullable enable`, decorated with `[global::System.Diagnostics.Conditional("DEBUG")]` and `AttributeUsage(AttributeTargets.Method, AllowMultiple = true)` — the same marker-attribute-emission pattern used elsewhere in the series.
- **Opt-in is per namespace, via MSBuild.** The consuming project lists the namespace containing the intercepting methods in the `<InterceptorsNamespaces>` MSBuild property. Without that entry the interceptor simply does not apply. (The older preview-era property name was `<InterceptorsPreviewNamespaces>`.)
- **The worked example** is the running enum theme from the series: intercepting `ToString()` on an enum so it resolves to a generated fast path — an extension method shaped like `MyEnumExtensionsToString(this global::System.Enum value)` rather than the boxing, reflection-driven framework implementation.
- **Pipeline shape.** Register a syntax provider whose predicate is deliberately cheap (an invocation whose member name is `ToString`), then do the real work in the transform: use the semantic model to confirm the symbol is the method you actually want to intercept, and only then compute the interceptable location.
- **Status.** The article reports interceptors as a stable language feature from the .NET 9.0.2xx SDK onward, with the `[Experimental]` attribute removed from the APIs, and the old positional `[InterceptsLocation(path, line, column)]` constructor officially deprecated. In-box users so far are ASP.NET Core and the configuration binder generator.

## Pitfalls

- **Do not emit the old three-argument form.** `[InterceptsLocation(filePath, line, character)]` is deprecated; anything you find in older blog posts or samples that computes a path/line/column by hand is now the wrong shape. Use `GetInterceptableLocation`.
- **Do not hand-roll or hardcode the `data` string.** Because it embeds a checksum of the intercepted file's content, a value copied from a previous build stops matching as soon as the file changes.
- **Forgetting the MSBuild opt-in produces silent no-ops.** Generation succeeds, the attribute is emitted, and the call is simply never intercepted. Check `<InterceptorsNamespaces>` before debugging the generator.
- **The experimental diagnostic will fail the build** if your generator project treats warnings as errors and you have not suppressed `RSEXPERIMENTAL002` around the call.
- **`GetInterceptableLocation` requires a recent Roslyn reference.** Referencing it raises the floor on which SDK and IDE versions can load your generator — the multi-targeting tradeoff covered in Part 14 applies directly here.
- **Caching implication worth thinking about** (a consequence of the checksum encoding above, not a claim the article makes): a location value is tied to the exact content of the intercepted file, so it changes on essentially every edit to that file. Keep it out of any pipeline stage whose value you expect to stay stable across keystrokes.

## In his words

> "You also still need to \"opt-in\" to individual interceptors by adding the namespace of the interceptor in the &lt;InterceptorsNamespaces&gt; MSBuild property."

— Andrew Lock, [.NET Escapades](https://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/)

> "Interceptors have been used both by ASP.NET Core and by the configuration binder source generator to provide support for AOT"

— Andrew Lock, [.NET Escapades](https://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/)

## Read the full article

[Creating a source generator - Part 11: Implementing an interceptor with a source generator](https://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/) — Andrew Lock, .NET Escapades.
