# Part 10 — Testing that your incremental generator pipeline outputs are cacheable

> [Creating a source generator, Part 10: Testing your incremental generator pipeline outputs are cacheable](https://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/)
> Andrew Lock, .NET Escapades
> Original article (c) Andrew Lock, all rights reserved. This file is an original summary, not a copy.

## Why read this

Broken incrementality is invisible from the outside. A generator whose pipeline models silently fail value equality still emits perfectly correct code — it just re-runs the whole pipeline on every keystroke and drags the IDE down with it. There is no compiler warning for this and no obvious symptom in normal testing. This article turns "is my pipeline actually cached?" into an ordinary unit-test assertion: enable Roslyn's step tracking, run the generator twice, and check the run reason recorded for every output of every stage you named. Send the user here when they are writing generator tests, when they suspect an equality bug in a model, or when they want a CI guardrail that fires the moment someone leaks a `Compilation`, `ISymbol`, or `SyntaxNode` into the pipeline.

## Key points

**Symptom to test for.** On a second run over a semantically identical compilation, a healthy pipeline recomputes nothing past the first stage that produced an equal value. A broken one recomputes everything. The observable difference is the `IncrementalStepRunReason` attached to each step output — that is the entire diagnostic surface, and it only exists if you ask the driver to record it.

**Step tracking is off by default — turn it on.** Build a `GeneratorDriverOptions` with `trackIncrementalGeneratorSteps: true` (paired with `disabledOutputs: IncrementalGeneratorOutputKind.None` so nothing is suppressed) and pass it as the `driverOptions` argument to `CSharpGeneratorDriver.Create(...)`. Wrap your `IIncrementalGenerator` with `.AsSourceGenerator()` when handing it over. Without the flag the tracked-step dictionary comes back empty and every assertion you write passes vacuously.

**Name the stages you want to assert over.** `WithTrackingName(string)` is an extension on `IncrementalValueProvider<T>` and `IncrementalValuesProvider<T>` — it returns a provider, so you chain it onto intermediate stages: the result of `ForAttributeWithMetadataName`/`CreateSyntaxProvider`, and the outputs of `Where`, `Select`, `Collect`, `Combine`. Keep the names unique and hold them as `const string` fields on a small internal `TrackingNames` class so the generator and the test helper cannot drift apart.

**You cannot name the terminal output step, and you do not need to.** `RegisterSourceOutput` and `RegisterImplementationSourceOutput` return `void`, so there is no provider left to tag. Roslyn records the output step for you in a *separate* dictionary — `GeneratorRunResult.TrackedOutputSteps` — keyed by the well-known names `"SourceOutput"` and `"ImplementationSourceOutput"`. Those entries never appear in `TrackedSteps`. Scope your cacheability assertion to `TrackedSteps` entries whose key is one of your own tracking names; asserting over `TrackedOutputSteps` is a separate, stricter check about whether the driver had to re-emit the file at all.

**Run twice, on the driver the first run handed back.** `driver.RunGenerators(compilation)` returns a *new* `GeneratorDriver` that carries the cache state. Capture that return value and call `RunGenerators` on **it** for the second pass. Feed the second pass `compilation.Clone()` so the compilation is a different object instance — defeating trivial reference equality — while the semantic content is unchanged. Calling `CSharpGeneratorDriver.Create` a second time, or re-using your original driver variable, throws the cache away and makes the test meaningless.

**Read results through `GetRunResult()`, then filter.** `GeneratorDriverRunResult.Results` yields one `GeneratorRunResult` per generator. `GeneratorRunResult.TrackedSteps` is an `ImmutableDictionary<string, ImmutableArray<IncrementalGeneratorRunStep>>`. It contains Roslyn's own built-in input steps alongside yours — the well-known input names include `"Compilation"`, `"ParseOptions"`, `"AdditionalTexts"`, `"AnalyzerConfigOptions"`, and `"MetadataReferences"` — and several of those will never report as cached. Filter the dictionary down to keys your `TrackingNames` class knows about before asserting anything.

**Assert on the run reasons of the second run only.** Each `IncrementalGeneratorRunStep` exposes `.Name`, `.Inputs`, `.ElapsedTime`, and `.Outputs`, where `Outputs` is a collection of `(object Value, IncrementalStepRunReason Reason)` pairs. The enum values are `New`, `Modified`, `Unchanged`, `Cached`, and `Removed`. On the second run every output of every tracked stage must be either `Cached` (the input was unchanged, so the stored output was reused without executing the step) or `Unchanged` (the input changed but the step produced an equal value, so propagation stopped there). Any `New` or `Modified` on the second run is the signature of a model that does not implement value equality. The first run is all `New` by definition — never assert on it.

**Compare the values as well as the reasons.** Collect the output values from run one and run two, assert the step counts match, and assert the two value sequences are equal. Reasons alone can miss an `Equals` that is wrong in the other direction — one that reports equality for genuinely different models.

**Add a reflection guard against poisonous types.** Walk the object graph of each output value: public and non-public instance fields, recursing into `IEnumerable` elements, with a reference-identity `HashSet<object>` to break cycles. Fail the test if any node is a `Compilation`, `ISymbol`, or `SyntaxNode`. Those types are not value-equatable and they root large Roslyn object graphs, so holding one in a pipeline model both destroys caching and leaks memory across compilations. This is the check that catches regressions automatically, long before anyone notices the IDE getting sluggish.

**Prove the test can fail.** Deliberately put a non-equatable member on a model — a bare `object` field, or a raw `ImmutableArray<T>` whose default equality is reference-based (see part 9's `EquatableArray<T>`) — and confirm the test goes red. A cacheability test that cannot fail is worse than no test, because it manufactures confidence.

## Pitfalls

- Omitting `trackIncrementalGeneratorSteps: true`: there are no tracked steps, so every assertion passes for the wrong reason.
- Creating a fresh driver for the second run, or re-using the original driver variable instead of the one `RunGenerators` returned — no cache state carries over and the second run looks identical to the first.
- Passing the same `Compilation` instance twice instead of `Clone()`: reference equality short-circuits the comparison and the test proves nothing about your models.
- Expecting `WithTrackingName` to work on the terminal registration — it is a provider extension; `RegisterSourceOutput` has no return value to chain onto.
- Confusing `TrackedSteps` with `TrackedOutputSteps`, or asserting over unfiltered `TrackedSteps` — Roslyn's built-in input steps will trip the assertions.
- Stages using `.WithComparer(...)`: values that are equal by a custom comparer need not be `Equals`-equal, so the simple value-by-value comparison does not apply to them.
- Any `Compilation`, `ISymbol`, `SyntaxNode`, or `SemanticModel` surviving past the transform stage — extract the primitives and strings you need and drop the Roslyn types immediately.
- Enabling step tracking outside tests: it retains intermediate values and step timings purely for diagnostics, which is overhead you do not want in a shipped generator run.

## In his words

> The incremental generator APIs don't _prevent_ you from doing all sorts of things that could create terrible IDE performance issues.

— Andrew Lock, [Creating a source generator, Part 10](https://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/)

> Source generator performance is a tricky thing to understand, as it can be difficult to profile or debug generators in real-world usage.

— Andrew Lock, [Creating a source generator, Part 10](https://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/)

## Read the full article

The post develops the complete test helper step by step — driver construction, the run-result collection code, the per-step comparison, and the reflection-based object-graph walker — and finishes with a worked example of a deliberately broken model so you can watch the assertion fire:

[andrewlock.net — Creating a source generator, Part 10: Testing your incremental generator pipeline outputs are cacheable](https://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/)
