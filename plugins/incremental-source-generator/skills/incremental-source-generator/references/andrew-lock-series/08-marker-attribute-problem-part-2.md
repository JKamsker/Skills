Title: Solving the source generator 'marker attribute' problem - Part 2: Creating a source generator - Part 8

URL Source: http://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/

Published Time: 2022-02-01T10:00:00.0000000

Markdown Content:
February 01, 2022 ~10 min read

[Creating a source generator - Part 8](https://andrewlock.net/series/creating-a-source-generator/)

This is the eighth post in the series: [Creating a source generator](https://andrewlock.net/series/creating-a-source-generator/).

1.   [Part 1 - Creating an incremental generator](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/)
2.   [Part 2 - Testing an incremental generator with snapshot testing](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/)
3.   [Part 3 - Integration testing and NuGet packaging](https://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/)
4.   [Part 4 - Customising generated code with marker attributes](https://andrewlock.net/creating-a-source-generator-part-4-customising-generated-code-with-marker-attributes/)
5.   [Part 5 - Finding a type declaration's namespace and type hierarchy](https://andrewlock.net/creating-a-source-generator-part-5-finding-a-type-declarations-namespace-and-type-hierarchy/)
6.   [Part 6 - Saving source generator output in source control](https://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/)
7.   [Part 7 - Solving the source generator 'marker attribute' problem - Part 1](https://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/)
8.   Part 8 - Solving the source generator 'marker attribute' problem - Part 2 (this post) 
9.   [Part 9 - Avoiding performance pitfalls in incremental generators](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/)
10.   [Part 10 - Testing your incremental generator pipeline outputs are cacheable](https://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/)
11.   [Part 11 - Implementing an interceptor with a source generator](https://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/)
12.   [Part 12 - Reading compilation options and the C# version in source generators](https://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/)
13.   [Part 13 - Accessing MSBuild properties and user configuration from source generators](https://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/)
14.   [Part 14 - Supporting multiple .NET SDK versions in a source generator](https://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/)
15.   [Part 15 - Solving the source generator 'marker attribute' problem in .NET 10](https://andrewlock.net/exploring-dotnet-10-preview-features-4-solving-the-source-generator-marker-attribute-problem-in-dotnet-10/)

In the previous post I described marker attributes, how they're used by source generators, and the problem with deciding how they should be referenced in a user's project. In this post I describe some of the approaches I tried, along with the final approach I decided on.

## [Referencing marker attributes in an external dll](http://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/#referencing-marker-attributes-in-an-external-dll)

As a quick recap, marker attributes are simple attributes that are used to control which types a source generator should use for code generation, and provide a way to pass options to the source generator.

For example, [my _StronglyTypedId_ project](https://github.com/andrewlock/StronglyTypedId) allows you to decorate a `struct` with a `[StronglyTypedId]` attribute. The source generator uses the presence of that attribute to trigger generation of type converters and properties for the struct.

Similarly the `[LoggerMessage]` attribute in _Microsoft.Extensions.Logging.Abstractions_ is [used to generate efficient log infrastructure](https://andrewlock.net/exploring-dotnet-6-part-8-improving-logging-performance-with-source-generators/).

The question is, where should the marker attributes live? In the previous post I described three options:

1.   Added to the compilation by the source generator.
2.   Manually created by users.
3.   Included in a referenced dll.

Option 1. is the standard approach, but it doesn't work when users are using `[InternalsVisibleTo]`, as you can end up defining the same type multiple times. In this post, I explore variations on option 3. These variations are pretty much in the same order I tried them while [trying to solve this problem for myself](https://github.com/andrewlock/StronglyTypedId/issues/38).

## [1. Directly referencing the build output](http://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/#1-directly-referencing-the-build-output)

The first option is kind of brilliant in its simplicity. Typically the analyzer/source generator dll isn't referenced in the normal way when you add the generator package to a project. With this approach, we change that!

The beauty of this one is how simple it is. Simply create the attributes inside your source generator project, and remove the `<IncludeBuildOutput>false</IncludeBuildOutput>` override that you typically have in source generators. For example:

```
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <!-- 👇 don't include this, so the dll ends up in the build output-->
    <!-- <IncludeBuildOutput>false</IncludeBuildOutput> -->
  </PropertyGroup>

  <!-- Standard source generator references -->
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.3" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.0.1" PrivateAssets="all" />
  </ItemGroup>

  <!-- Package the build output into the "analyzer" slot in the NuGet package -->
  <ItemGroup>
    <None Include="$(OutputPath)\$(AssemblyName).dll" Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
  </ItemGroup>
</Project>
```

I only had to make a single tweak to the generator project, so far so good! After we pack this into a NuGet package, the dll will be added to both the _analyzers/dotnet/cs_ path (required for source generators) **and** in the normal _lib_ folder, for direct reference by the consuming project:

![Image 1: Example layout](https://andrewlock.net/content/images/2021/attributes_03.png)

Consumers of the NuGet package will all reference the marker attributes contained in your generator dll, so there's no problems with conflicting types. Problem solved!

If you're referencing the source generator project within the same solution, either for testing purposes, or because you have a solution-specific generator you'll need to set `ReferenceOutputAssembly="true"` in the `<ProjectReference>` element of the consuming project. For example:

```
<ItemGroup>
  <ProjectReference Include="..\StronglyTypedId\StronglyTypedId.csproj" 
    OutputItemType="Analyzer" 
    ReferenceOutputAssembly="true" /> <!-- 👈 This is normally false -->
</ItemGroup>
```

So that's it, problem solved right? Well…maybe. But I don't really like this approach. Your generator dll is now part of the user's references, which just feels icky. There's also potential issues around the _Microsoft.CodeAnalysis.CSharp_ dependencies etc. For example, in my testing, while my projects would build ok, there were a host of warnings about mismatched versions of _System.Collections.Immutable_:

```
warning MSB3277: Found conflicts between different versions of "System.Collections.Immutable" that could not be resolved.
warning MSB3277: There was a conflict between "System.Collections.Immutable, Version=1.2.5.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a" and "System.Collections.Immutable, Version=5.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a".
```

None of my projects were directly referencing _System.Collections.Immutable_ but it's a transitive reference used by the generator, hence the issues. The potential for issues was just too large for my liking, so I put this one aside, and tried a different approach.

## [2. Creating a separate NuGet package for the dll only](http://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/#2-creating-a-separate-nuget-package-for-the-dll-only)

Instead of referencing the source generator dll, and all the associated dependencies that relies on, we really want a tiny dll that contains _only_ the marker attributes (and associated types). The logical step then is to create a NuGet package that just contains these marker types. We can then add a dependency to the generator project, so that when you add the attributes project to a consuming project, the generator project is automatically added to the consuming package too.

My main concern with this approach wasn't really related to technical difficulties. Instead, my concerns rested more around naming, and things feeling ugly.

> As it turns out, I did have some technical difficulties with this, but this was more to the specifics of my project I think, so I don't consider it a real hurdle.

For example, take my _StronglyTypedId_ project. Should the "marker attributes" package be called _StronglyTypedId.Attributes_, and the "generator" package called _StronglyTypedId_? That seems likely that users are going to add the _StronglyTypedId_ package, and then not understand why the generator doesn't appear to be working (as they don't have any references to the marker attributes).

Alternatively, I could call the marker-attributes package _StronglyTypedId_ and call the source generator package _StronglyTypedId.Generator_. That feels like the hierarchy works better, but still feels like someone is going to add the generator package without the attributes. It's the generator they want after all, the attributes are a by-product! Documentation is great, but people don't read it 😉

## [3. Making the additional attributes package optional](http://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/#3-making-the-additional-attributes-package-optional)

The previous solution felt like it was _nearly_ the right one, but I didn't like the fact users always had to think about two different packages. While fiddling with this I realised I was trying to solve a problem for, potentially, a small subset of users of the project, and maybe that should drive my approach.

As I mentioned in [the previous post](https://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/), there's a "standard" way to use marker attributes with source generators: the source generator adds them itself as part of the initialization phase. This works well _except_ in the case where users have `[InternalsVisibleTo]` attributes, and are using the source generator in multiple projects.

In which case, I decided, why not use the source-generator initialization phase to add the attributes automatically, and provide a separate attributes package for users that run into trouble?

This would mean that 99% of users would just have a single package, using the auto-added attributes as normal, and not have to worry about the other one. The main generator package would be called _StronglyTypedId_ and the supplementary attributes package would be called _StronglyTypedId.Attributes_. The hierarchy feels right, and people are (hopefully) driven towards the right package.

The problem with this approach, is that users that run into `[InternalsVisibleTo]` need a way of "turning" off the auto-added attributes. The best way I could think of doing that, was to wrap the generated attribute code in an `#if/#endif`. For example, something like the following:

```
#if !STRONGLY_TYPED_ID_EXCLUDE_ATTRIBUTES

using System;
namespace StronglyTypedIds
{
    [AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    [System.Diagnostics.Conditional("STRONGLY_TYPED_ID_USAGES")]
    internal sealed class StronglyTypedIdAttribute : Attribute
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
#endif
```

By default, the variable `STRONGLY_TYPED_ID_EXCLUDE_ATTRIBUTES` would not be set, so the attributes would be part of the compilation. If a user runs into the `[InternalsVisibleTo]` problem, they could define this constant in their project, and the embedded generated attributes would no longer be part of the compilation. They could instead then reference the _StronglyTypedId.Attributes_ package to use the generator

```
<Project Sdk="Microsoft.NET.Sdk">
   
   <PropertyGroup>
     <OutputType>Exe</OutputType>
     <TargetFramework>net6.0</TargetFramework>
    <!--  Define the MSBuild constant    -->
     <DefineConstants>STRONGLY_TYPED_ID_EXCLUDE_ATTRIBUTES</DefineConstants>
   </PropertyGroup>

  <PackageReference Include="StronglyTypedId" Version="1.0.0" PrivateAssets="All"/>
  <PackageReference Include="StronglyTypedId.Attributes" Version="1.0.0" PrivateAssets="All" />
 
 </Project>
```

The main advantage of this approach is that _most_ users don't have to worry about the extra package. It's only when you have a problem that you need to dig into it, at which point you're more motivated to read the docs 😉

## [4. Pack the dll into the generator package](http://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/#4-pack-the-dll-into-the-generator-package)

It was shortly after implementing and shipping the previous approach that I realised I'd missed a trick. Instead of requiring users to install a separate package to resolve the problem, I could just package the attributes dll inside the generator package, and skip the auto-embedding of the marker attributes entirely.

> This is the same approach used by the `[LoggerMessage]` generator. I face-palmed when I realised I'd finally arrived at this point, given I'd been referring to that project as a reference 🤦‍♂️

The net result is a NuGet package layout that looks like the following, with the _StronglyTypedId.dll_ "generator" dll in the _analyzers/dotnet/cs_ folder, so it's used for generation, and the marker attributes dll _StronglyTypedId.Attributes.dll_ in the _lib_ folder, that will be directly referenced by user code.

> Note that in my case I also want to reference the marker attributes from within my generator code, so _StronglyTypedId.Attributes.dll_ is packed in _analyzers/dotnet/cs_ too - that likely won't be necessary for all source generator projects.

![Image 2: The layout of the NuGet package, with multiple dlls](https://andrewlock.net/content/images/2021/attributes_04.png)

Achieving this layout required a little bit of csproj magic to make sure `dotnet pack` put the dlls in the right place, but nothing too arcane.

```
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IncludeBuildOutput>false</IncludeBuildOutput>
  </PropertyGroup>

  <!-- Standard source generator references -->
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.3" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.0.1" PrivateAssets="all" />
  </ItemGroup>

  <!-- Reference the attributes from the generator to compile against them -->
  <!-- Ensure we specify PrivateAssets so the NuGet doesn't have any dependencies -->
  <ItemGroup>
    <ProjectReference Include="..\StronglyTypedIds.Attributes\StronglyTypedIds.Attributes.csproj" PrivateAssets="All" /> 
  </ItemGroup>

  <ItemGroup>
    <!-- Pack the generator dll in the analyzers/dotnet/cs path -->
    <None Include="$(OutputPath)\$(AssemblyName).dll" Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
    
    <!-- Pack the attributes dll in the analyzers/dotnet/cs path -->
    <None Include="$(OutputPath)\StronglyTypedIds.Attributes.dll" Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />

    <!-- Pack the attributes dll in the lib\netstandard2.0 path -->
    <None Include="$(OutputPath)\StronglyTypedIds.Attributes.dll" Pack="true" PackagePath="lib\netstandard2.0" Visible="true" />
  </ItemGroup>

</Project>
```

There's probably "better" ways to do this, but this worked so it'll do for me.

When it comes to referencing the NuGet package, you don't need to do anything special:

```
<ItemGroup>
  <PackageReference Include="StronglyTypedId" Version="1.0.0" PrivateAssets="all" />
</ItemGroup>
```

I used `PrivateAssets="all"` here to prevent downstream projects also getting a reference to the source generator, but that's entirely optional. One thing to be aware of is that this will result in the marker attribute dll _StronglyTypedId.Attributes.dll_ appearing in the project's bin folder. However, the attributes themselves [are decorated with the conditional](https://andrewlock.net/conditional-compilation-for-ignoring-method-calls-with-the-conditionalattribute/#applying-the-conditional-attribute-to-classes), so there's no runtime dependency on the dll.

You can ensure the dll _doesn't_ get copied to the output [by setting `ExcludeAssets="runtime"` on the `<PackageReference>` element](https://docs.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#controlling-dependency-assets):

```
<ItemGroup>
  <PackageReference Include="StronglyTypedId" Version="1.0.0" 
    PrivateAssets="all" ExcludeAssets="runtime" />
</ItemGroup>
```

This will still let you compile against the marker attributes, but the dll won't be in your _bin_ folder.

If you're referencing the source generator project from inside the same solution you will need to add a normal `<PackageReference>` to the attributes project too. In my case, it was a little more complicated as I needed _both_ the source generator and the destination project to have a reference to the attributes dll.

> Source generators live in their own little bubble in terms of references. Even though the consuming project has a reference to the attributes project, the source generator won't have access to it, or any other reference in the consuming project.

It's all a bit confusing, but [for the source generator project to access the attributes dll in the consuming project, you need to tell the consuming project to _treat the attributes project as an analyzer_.](https://github.com/dotnet/roslyn/discussions/47517#discussioncomment-1633510) The source generator "analyzer" can then reference it and generate correctly. Because we want the consuming project to _also_ reference the marker attributes dll, we must set `ReferenceOutputAssembly="true"`.

```
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <!-- Rererence the source generator project -->
    <ProjectReference Include="..\StronglyTypedIds\StronglyTypedIds.csproj"
        OutputItemType="Analyzer" 
        ReferenceOutputAssembly="false" /> <!-- Don't reference the generator dll -->

    <!-- Rererence the attributes project "treat as an analyzer"-->
    <ProjectReference Include="..\StronglyTypedIds.Attributes\StronglyTypedIds.Attributes.csproj" 
        OutputItemType="Analyzer" 
        ReferenceOutputAssembly="true" /> <!-- We DO reference the attributes dll -->
  </ItemGroup>
</Project>
```

With this final setup, I think we have the best of all worlds:

*   Only a single NuGet package to worry about
*   No issues when users are using `[InternalsVisibleTo]`
*   Users can exclude the marker dll from their build output using `ExcludeAssets="runtime"`
*   Users can do `dotnet add package StronglyTypedId` and it will just work, the extra `<PackageReference>` properties are purely optional

## [Bonus: embed the attributes if you want!](http://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/#bonus-embed-the-attributes-if-you-want-)

For StronglyTypedId, I actually went one step further and allowed users to opt-in to embedding the attributes in their project's dll using the source generator by setting an MSBuild variable `STRONGLY_TYPED_ID_EMBED_ATTRIBUTES`. The attributes are always added to the compilation, but they aren't available unless this is set:

```
#if STRONGLY_TYPED_ID_EMBED_ATTRIBUTES

using System;

namespace StronglyTypedIds
{
    [AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    [System.Diagnostics.Conditional("STRONGLY_TYPED_ID_USAGES")]
    internal sealed class StronglyTypedIdAttribute : Attribute
    {
        // ...
    }
}
#endif
```

If users _do_ turn this on, then initially they'll get duplicate type problems, as you will have the "internal" types embedded by the source generator, as well as the public types in the attribute dll. To solve this, you can add `compile` to the `ExcludeAssets` for the package:

```
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <!-- Define this constant so the embedded attributes are activated -->
    <DefineConstants>STRONGLY_TYPED_ID_EMBED_ATTRIBUTES</DefineConstants>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="StronglyTypedId" Version="1.0.0" 
        ExcludeAssets="compile;runtime" PrivateAssets="all" />
        <!-- Add this  ☝ so you don't compile against the marker attribute dll -->
  </ItemGroup>
</Project>
```

Now, I can't really think of _why_ someone would want to do that, but seeing as I already had the code written for the original approach, I left it there for anyone that needs it! 😄

## [Summary](http://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/#summary)

In this post I describe the journey I went through deciding how to handle marker attributes for my source generator. I described 4 main approaches: Directly referencing the source generator dll in the consuming project; creating two independent NuGet packages; making the marker attribute NuGet package optional using conditional compilation; and embedding the marker attribute dll and generator dll in the same NuGet package. The final option seemed like the best approach, and gives the smoothest experience for users.

[![Image 3: Solving the source generator 'marker attribute' problem - Part 1](https://andrewlock.net/content/images/2021/attributes_banner.png)Previous Solving the source generator 'marker attribute' problem - Part 1: Creating a source generator - Part 7](https://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/)[![Image 4: NetEscapades.EnumGenerators: a source generator for enum performance](https://andrewlock.net/content/images/2022/enumgenerators_banner.png)Next NetEscapades.EnumGenerators: a source generator for enum performance](https://andrewlock.net/netescapades-enumgenerators-a-source-generator-for-enum-performance/)

Andrew Lock | .Net Escapades

![Image 5](https://andrewlock.net/assets/img/icons/apple/apple-touch-icon-180x180.png)Want an email when

there's new posts?
