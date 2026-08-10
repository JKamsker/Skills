Title: Supporting multiple .NET SDK versions in a source generator: Creating a source generator - Part 14

URL Source: http://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/

Published Time: 2025-02-04T09:00:00.0000000

Markdown Content:
February 04, 2025 ~18 min read

[Creating a source generator - Part 14](https://andrewlock.net/series/creating-a-source-generator/)

This is the fourteenth post in the series: [Creating a source generator](https://andrewlock.net/series/creating-a-source-generator/).

1.   [Part 1 - Creating an incremental generator](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/)
2.   [Part 2 - Testing an incremental generator with snapshot testing](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/)
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
14.   Part 14 - Supporting multiple .NET SDK versions in a source generator (this post) 
15.   [Part 15 - Solving the source generator 'marker attribute' problem in .NET 10](https://andrewlock.net/exploring-dotnet-10-preview-features-4-solving-the-source-generator-marker-attribute-problem-in-dotnet-10/)

In [my previous post](https://andrewlock.net/supporting-multiple-sdk-versions-in-analyzers-and-source-generators/) I described why you might want (or need) to target multiple versions of the .NET SDK in a source generator. In this post I show how I applied this to my _NetEscapades.EnumGenerators_ source generator, so that I could support features only available in the .NET 8/9 SDK, while still supporting users stuck on the .NET 7 SDK.

## [.NET SDK versions, _Microsoft.CodeAnalysis.CSharp_, and NuGet package layouts](http://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/#-net-sdk-versions-microsoft-codeanalysis-csharp-and-nuget-package-layouts)

In [my previous post](https://andrewlock.net/supporting-multiple-sdk-versions-in-analyzers-and-source-generators/) I provided a long description of how and why you might need to target multiple versions of the .NET SDK. This post directly follows on from that one, so if you haven't, I suggest reading that one first.

In summary, when you create a source generator, you reference a specific version of the [Microsoft.CodeAnalysis.CSharp](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp) NuGet package. The [version of this package](https://learn.microsoft.com/en-us/visualstudio/extensibility/roslyn-version-support) you choose defines the minimum version of [Visual Studio, MSBuild, and the .NET SDK](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs) that your source generator will work with. The higher the version, the more Roslyn APIs you can use, but the higher the version of the .NET SDK the consumer must be using.

In the .NET 6 SDK, Microsoft updated the logic that finds analyzers/source generators in a NuGet file to allow versioning a source generator by Roslyn API version. That means you can ship a source generator that provides basic functionality when used with the .NET 6 SDK (for example) but which provides _additional_ functionality when used with the .NET 7 or .NET 8 SDKs, for example. You do this by shipping multiple versions of the generator in the NuGet package, each compiled against a different version of [Microsoft.CodeAnalysis.CSharp](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp):

![Image 1: The (simplified) layout of the System.Text.Json 6.0.0 package](https://andrewlock.net/content/images/2025/system_text_json_4.png)

The .NET SDK then loads the highest supported version of the analyzer, based on the version of the Roslyn API that it supports.

I covered most of the details of why you might want to multi-target multiple roslyn versions in your package in [the previous post](https://andrewlock.net/supporting-multiple-sdk-versions-in-analyzers-and-source-generators/), so in this post I'll show how I added multi-targeting support to my _NetEscapades.EnumGenerators_ source generator.

## [Adding multi-targeting support for _NetEscapades.EnumGenerators_](http://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/#adding-multi-targeting-support-for-netescapades-enumgenerators)

The [NetEscapades.EnumGenerators](https://www.nuget.org/packages/NetEscapades.EnumGenerators/) package is a source generator for making working with enums faster. I [recently added experimental support for interceptors to the package](https://andrewlock.net/recent-updates-for-netescapades-enumgenerators-interceptors/), but this required using a newer version of the Roslyn API than I was previously. The interceptor API I needed was only available in .NET SDK version `8.0.400` (version `4.11` of the Roslyn API in _Microsoft.CodeAnalysis.CSharp_), whereas I was currently targeting `4.4`.

The easy approach would have been to simply update the version of _Microsoft.CodeAnalysis.CSharp_ to `4.11.0` to give access to the `4.11` Roslyn API. However, doing so would have meant that anyone currently using the package with a .NET 7 SDK or a .NET 8 SDK below `8.0.400` would have been broken.

Rather than inconvenience people, I decided to have a go at multi-targeting. This would ensure that down-version users could use the basic functionality, but the interceptor support would not be available. Meanwhile, anyone using `8.0.400` or higher of the .NET SDK _would_ be able to enable the interceptor functionality. The best of both worlds!

The approach I took to multi-targeting was heavily based on [the approach taken by the built-in generators](https://github.com/dotnet/runtime/pull/59074/files#diff-258ed32f98847603de7425cdcd362915271c61390268af3e6c75b4335b817471) like the _System.Text.Json_ generator and the _Microsoft.Extensions.Logging_ generator when they first added multi-targeting support.

I implemented the bulk of the support in [a single PR](https://github.com/andrewlock/NetEscapades.EnumGenerators/pull/101), and it's broadly the same approach I show in this post.

### [The original: prior to multi-targeting](http://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/#the-original-prior-to-multi-targeting)

To set the stage, _prior_ to multi-targeting, the _NetEscapades.EnumGenerators_ source generator targeted version `4.4` of the Roslyn API, by referencing `4.4.0` of the _Microsoft.CodeAnalysis.CSharp_ NuGet package. This API version corresponds to the .NET 7 SDK, and gives access to the [recommended `ForAttributeWithMetadataName` API](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/#1-use-the-net-7-api-forattributewithmetadataname).

The solution consisted of two main projects:

*   `NetEscapades.EnumGenerators`: The main source generator project
*   `NetEscapades.EnumGenerators.Attributes`: A library project containing the "marker" attributes that drive the source generator

There's also a bunch of test projects, some of the main ones are described below. I've described these approaches in [more detail](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/)[previously](https://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/) in [this series](https://andrewlock.net/series/creating-a-source-generator/):

*   `NetEscapades.EnumGenerators.Tests`: Unit tests for the generator functionality, and snapshot tests comparing the generated output
*   `NetEscapades.EnumGenerators.IntegrationTests`: "Integration" test project that references the source generator project directly, and runs a bunch of tests. This ensures the generated output _actually_ compiles, and gives the expected values when executed, for example.
*   `NetEscapades.EnumGenerators.Nuget.IntegrationTests`: Runs the same tests as the above project, but instead of referencing the source generator directly, it uses a version of the generator that has been fully packed into a NuGet package. This is the closest to a "real life" scenario you can get to.

There are a bunch of other tests for additional variations, but I'll ignore those for brevity:

![Image 2: The solution layout](https://andrewlock.net/content/images/2025/multi_targeting_01.png)

Now we have the starting point, lets begin the migration to support multiple Roslyn versions.

### [Splitting the source generator project](http://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/#splitting-the-source-generator-project)

Prior to supporting the multi-targeting, the _NetEscapades.EnumGenerators_ _.csproj_ file looked as shown below. This includes a variety of properties and items. Some of the properties (`<TargetFramework>`, `<PackageReference>`) are related to _building_ the source generator dll, some parts are related to properties of the final NuGet package (`<PackageId>`, `<Description>`), and some are related to controlling the contents of the final NuGet package (`<PackageReadmeFile>`, `PackagePath` attributes):

```
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <Nullable>enable</Nullable>
    <ImplicitUsings>true</ImplicitUsings>
    <PackageId>NetEscapades.EnumGenerators</PackageId>
    <Description>A source generator for creating helper extension methods on enums using a [EnumExtensions] attribute</Description>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>

  <!-- These are references required by the source generator -->
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.3" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.4.0" PrivateAssets="all" />
    <ProjectReference Include="..\NetEscapades.EnumGenerators.Attributes\NetEscapades.EnumGenerators.Attributes.csproj" PrivateAssets="All" />
  </ItemGroup>

  <!-- These tweak and control the contents of the NuGet package -->
  <ItemGroup>
    <None Include="../../README.md" Pack="true" PackagePath="\" />

    <!-- Pack the source generator and attributes dll -->
    <None Include="$(OutputPath)\$(AssemblyName).dll" Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
    <None Include="$(OutputPath)\NetEscapades.EnumGenerators.Attributes.dll" Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />

    <!-- Pack the attributes file to be referenced by the target project -->
    <None Include="$(OutputPath)\NetEscapades.EnumGenerators.Attributes.dll" Pack="true" PackagePath="lib\netstandard2.0" Visible="true" />
    <None Include="$(OutputPath)\NetEscapades.EnumGenerators.Attributes.xml" Pack="true" PackagePath="lib\netstandard2.0" Visible="true" />
    <None Include="$(OutputPath)\NetEscapades.EnumGenerators.Attributes.dll" Pack="true" PackagePath="lib\net451" Visible="true" />
    <None Include="$(OutputPath)\NetEscapades.EnumGenerators.Attributes.xml" Pack="true" PackagePath="lib\net451" Visible="true" />
  </ItemGroup>
</Project>
```

To support multi-targeting, we need to make some significant changes to this project:

*   Split the source generator project into multiple projects, each referencing different version of _Microsoft.CodeAnalysis.CSharp_. 
    *   One project references version `4.4.0`. This is the "lower" bound for our SDK support, and corresponds to the .NET 7 SDK.
    *   Another project references version `4.11.0`. This is the "enhanced" SDK support, which we need for interceptor support, and corresponds to `8.0.4xx` of the .NET SDK.

*   Update the projects to share most of the implementation. Update the implementation in the `4.11` project with the additional functionality.
*   Create a separate project that is used solely to _pack_ the two source generator dlls into the final NuGet package. You could also use a nuspec file for this, but I prefer to use a csproj instead.

The final result looks something like this in the solution explorer:

![Image 3: The updated solution explorer, with 3 new projects](https://andrewlock.net/content/images/2025/multi_targeting_02.png)

So the original _NetEscapades.EnumGenerators_ project has gone, and we have _three_ projects in its place:

*   `NetEscapades.EnumGenerators.Rolsyn4_04`: The version of the generator that references _Microsoft.CodeAnalysis.CSharp_`4.4.0`.
*   `NetEscapades.EnumGenerators.Rolsyn4_11`: The version of the generator that references _Microsoft.CodeAnalysis.CSharp_`4.11.0`.
*   `NetEscapades.EnumGenerators.Pack`: A project focused solely on collecting the output artifacts from the other source projects and producing the final NuGet package.

Note that the three projects are peers in the solution explorer, but the files are all side-by-side in the same folder in the filesystem, rather than being in separate folders:

![Image 4: The filesystem of the updated solution, showing the files are side-by-side](https://andrewlock.net/content/images/2025/multi_targeting_03.png)

Now lets look at the project files, as well as some ancillary files.

### [Exploring the new project files](http://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/#exploring-the-new-project-files)

We'll start by looking at the `NetEscapades.EnumGenerators.Rolsyn4_04` and `NetEscapades.EnumGenerators.Rolsyn4_11`_.csproj_ files. These are quite simple, basically just defining the version of the Roslyn API they require and then importing a separate `.targets` file.

The `Rolsyn4_04` project looks like this:

```
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RoslynApiVersion>4.4.0</RoslynApiVersion>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <Import Project="NetEscapades.EnumGenerators.Build.targets" />

</Project>
```

and the `Rolsyn4_11` project looks like this:

```
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RoslynApiVersion>4.11.0</RoslynApiVersion>
    <DefineConstants>$(DefineConstants);INTERCEPTORS</DefineConstants>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <Import Project="NetEscapades.EnumGenerators.Build.targets" />

</Project>
```

Note that I've defined an additional constant in the `Rolsyn4_11` project, so that I can use `#if INTERCEPTORS` in the source generator code, and share the same files between both projects.

The bulk of the project definition happens in the _NetEscapades.EnumGenerators.Build.targets_ file (the name is chosen arbitrarily). This includes most of the same property definitions as we had before, with a couple of tweaks, which I've highlighted in comments below:

```
<Project>

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <PackageId>NetEscapades.EnumGenerators</PackageId>

    <!-- Explicitly define these, so that both projects produce dlls with the same name -->
    <AssemblyName>$(PackageId)</AssemblyName>
    <RootNamespace>$(PackageId)</RootNamespace>
    <Nullable>enable</Nullable>
    <ImplicitUsings>true</ImplicitUsings>
    <PackageId>NetEscapades.EnumGenerators</PackageId>
    <Description>A source generator for creating helper extension methods on enums using a [EnumExtensions] attribute</Description>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.4" PrivateAssets="all" />
    <!-- Use the RoslynApiVersion version defined in the project's .csproj -->
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="$(RoslynApiVersion)" PrivateAssets="all" />
    <ProjectReference Include="..\NetEscapades.EnumGenerators.Attributes\NetEscapades.EnumGenerators.Attributes.csproj" PrivateAssets="All" />
  </ItemGroup>

</Project>
```

As you can see, this `.targets` file _only_ includes values for controlling how the dlls are built. There's nothing related to actually packaging the file here. That's all handled in the `.Pack` project, which admittedly, feels a bit of a hack. The _.csproj_ for the `.Pack` project looks like the following (heavily commented to try to make sense of it all!):

```
<Project Sdk="Microsoft.NET.Sdk">

  <!-- This project is just used to _pack_ the NuGet, containing both the other analyzers -->
  <!-- We still import the same targets file to ensure we have the same values. There are -->
  <!-- probably other, better ways of doing this (e.g. using Directory.Build.props), but -->
  <!-- this is where I eventually ended up, and it works, so I haven't bothered experimenting -->
  <PropertyGroup>
    <RoslynApiVersion>4.11.0</RoslynApiVersion>
    <IsPackable>true</IsPackable>
  </PropertyGroup>

  <Import Project="NetEscapades.EnumGenerators.Build.targets" />

  <!-- These project references to each of the "real" projects ensures everything builds -->
  <!-- in the correct order etc. We set ReferenceOutputAssembly="false" though -->
  <ItemGroup>
    <ProjectReference Include="..\NetEscapades.EnumGenerators\NetEscapades.EnumGenerators.Roslyn4_04.csproj" 
      PrivateAssets="All" ReferenceOutputAssembly="false" />
    <ProjectReference Include="..\NetEscapades.EnumGenerators\NetEscapades.EnumGenerators.Roslyn4_11.csproj"
      PrivateAssets="All" ReferenceOutputAssembly="false" />
  </ItemGroup>

  <ItemGroup>
    <!-- We don't want to actually build anything in this project -->
    <Compile Remove="**\*.cs" />
    <None Include="../../README.md" Pack="true" PackagePath="\" />

    <!-- Pack the Roslyn4_04 project output into the analyzers/dotnet/roslyn4.4/cs folder -->
    <!-- and the Roslyn4_11 project output into the analyzers/dotnet/roslyn4.11/cs folder -->
    <None Include="$(OutputPath)\..\..\NetEscapades.EnumGenerators.Roslyn4_04\$(ArtifactsPivots)\$(AssemblyName).dll"
          Pack="true" PackagePath="analyzers/dotnet/roslyn4.4/cs" Visible="false" />
    <None Include="$(OutputPath)\..\..\NetEscapades.EnumGenerators.Roslyn4_11\$(ArtifactsPivots)\$(AssemblyName).dll"
          Pack="true" PackagePath="analyzers/dotnet/roslyn4.11/cs" Visible="false" />

    <!-- Pack the attributes dll for both Roslyn versions for referencing by the generator directly -->
    <None Include="$(OutputPath)\NetEscapades.EnumGenerators.Attributes.dll" Pack="true"
          PackagePath="analyzers/dotnet/roslyn4.4/cs" Visible="false" />
    <None Include="$(OutputPath)\NetEscapades.EnumGenerators.Attributes.dll" Pack="true"
          PackagePath="analyzers/dotnet/roslyn4.11/cs" Visible="false" />

    <!-- Pack the attributes dll for referencing by the target project-->
    <None Include="$(OutputPath)\NetEscapades.EnumGenerators.Attributes.dll" Pack="true" PackagePath="lib\netstandard2.0" Visible="false" />
    <None Include="$(OutputPath)\NetEscapades.EnumGenerators.Attributes.xml" Pack="true" PackagePath="lib\netstandard2.0" Visible="false" />
    <None Include="$(OutputPath)\NetEscapades.EnumGenerators.Attributes.dll" Pack="true" PackagePath="lib\net451" Visible="false" />
    <None Include="$(OutputPath)\NetEscapades.EnumGenerators.Attributes.xml" Pack="true" PackagePath="lib\net451" Visible="false" />

  </ItemGroup>
</Project>
```

Phew, that's a lot, but it is _mostly_ just a way to define a nuspec file while reusing values between the dlls. A simple `dotnet pack` and we have everything we need!

Of course, as we're now _building_ for multiple Roslyn versions, we really should _test_ for those different versions too, right?

## [Testing multi-targeting support](http://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/#testing-multi-targeting-support)

Testing is where things really start to get a little hairy. Prior to the multi-targeting support I had 3 main test projects, as I described previously:

*   `NetEscapades.EnumGenerators.Tests`
*   `NetEscapades.EnumGenerators.IntegrationTests`
*   `NetEscapades.EnumGenerators.Nuget.IntegrationTests`

However, if I really wanted to be sure that everything was working as expected, I decided I would need to run the tests _both_ with the `4.4` Roslyn API and with the `4.11` Roslyn API. Which means running all the tests _twice_ with two different versions of the .NET SDK.

But on top of that the _unit test_ project, `NetEscapades.EnumGenerators.Tests`, needs to reference one of the _NetEscapades.EnumGenerators_ projects (`Roslyn4_04` or `Roslyn4_11`) directly, which means you actually need _two_ versions of this project (or at least, that's what I did).

> This uses a "normal" project reference, so you can reference _either_ version of the projects, the version of the SDK you're using for the unit testing/snapshot testing isn't critical.

For the integration tests I took a slightly different approach. These test projects reference the _NetEscapades.EnumGenerators_ projects as an _analyzer_. In this case we _can't_ load the `Roslyn4_11` project unless we're using a supported version of the SDK, so it makes things a bit trickier. In the end, I ended up creating several new test projects:

*   `NetEscapades.EnumGenerators.Tests.Roslyn4_04`: Runs all the unit/snapshot tests for the `4.4` version of the analyzer.
*   `NetEscapades.EnumGenerators.Tests.Roslyn4_11`: Runs all the unit/snapshot tests for the `4.11` version of the analyzer, including some additional snapshot tests for the interceptor support.
*   `NetEscapades.EnumGenerators.IntegrationTests`: References the `4.4` version of the generator, runs tests that are common to both implementations.
*   `NetEscapades.EnumGenerators.Interceptors.IntegrationTests`: References _either_ the `4.4` or `4.11` version of the generator, _depending on the currently active .NET SDK version_, but only runs additional interceptor tests when using the `4.11` version
*   `NetEscapades.EnumGenerators.NuGet.IntegrationTests`: References the final generated NuGet package, and runs tests common to both implementations.
*   `NetEscapades.EnumGenerators.Interceptors.IntegrationTests`: References the final generated NuGet package, but only runs additional interceptor tests when using the `4.11` version.

If all that extra duplication sounds like a pain, you're not wrong 😅 Still, for full coverage, it's pretty much necessary. The following sections show how I handled the MSBuild/csproj mess.

### [Splitting the snapshot test project](http://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/#splitting-the-snapshot-test-project)

The `NetEscapades.EnumGenerators.Tests` project is [where I do the snapshot testing](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/) of the generators, and is the main feedback loop when I'm developing a source generator. As I wanted to be able to quickly test both versions of the generator, all while developing locally, I split the project in two, each referencing a different version of _Microsoft.CodeAnalysis.CSharp_ and of the generator project.

As before, I reduced the duplication in the project files by moving the bulk of the definition into a `.targets` file, and referencing it from two different _.csproj_ files. The `NetEscapades.EnumGenerators.Tests.Roslyn4_04` .csproj file looks like this:

```
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RoslynApiVersion>4.4.0</RoslynApiVersion>
  </PropertyGroup>

  <Import Project="NetEscapades.EnumGenerators.Tests.Project.targets" />

  <ItemGroup>
    <ProjectReference Include="..\..\src\NetEscapades.EnumGenerators\NetEscapades.EnumGenerators.Roslyn4_04.csproj" />
  </ItemGroup>

</Project>
```

The `Roslyn4_11` version is the same, but references the `Roslyn4_11` versions of the generator, sets `RoslynApiVersion` to `4.11.0`, and defines an additional MSBuild constant, `INTERCEPTORS`, for use in `#if` within test files . Both of the files reference the `.targets` file shown below, but the only real important part is the use of `$(RoslynApiVersion)`, which is taken from the projects file:

```
<Project>

  <PropertyGroup>
    <AssemblyName>NetEscapades.EnumGenerators.Tests</AssemblyName>
    <RootNamespace>$(AssemblyName)</RootNamespace>
    <TargetFrameworks>netcoreapp3.1;net6.0;net7.0;net8.0</TargetFrameworks>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <!-- Use the Roslyn API version passed in from the project file  -->
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="$(RoslynApiVersion)" PrivateAssets="all" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="16.11.0" />
    <PackageReference Include="System.ComponentModel.Annotations" Version="5.0.0" Condition="'$(TargetFramework)' == 'net48'" />
    <PackageReference Include="Verify.Xunit" Version="14.3.0" />
    <PackageReference Include="xunit" Version="2.4.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.3">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="3.1.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\NetEscapades.EnumGenerators.Attributes\NetEscapades.EnumGenerators.Attributes.csproj" />
  </ItemGroup>

</Project>
```

Both of these test projects can run on any support .NET SDK version, because the generator directly uses the `Compilation` types provided in the _Microsoft.CodeAnalysis.CSharp_ package, rather than relying on it being provided by the .NET SDK itself. That's different from the integration test projects, which _do_ rely on the SDK.

### [Multi-targeting the integration test project for multiple .NET SDK versions](http://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/#multi-targeting-the-integration-test-project-for-multiple-net-sdk-versions)

The integration test projects for my source generators reference the generator projects, but add the generator with `OutputItemType="Analyzer"`, as described [in a previous post](https://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/). This lets you test your generator _similar_ to how it will be used "in production", but without needing to create and reference a NuGet package locally, which is a bit of a pain.

In integration tests, your source generator is running _in_ the compiler, so you need to be careful about the Roslyn APIs. You can only reference and test functionality exposed by the `Roslyn4_11` project if you're using a high enough SDK version. To achieve the testing I wanted, I needed to conditionally reference a different project (`Roslyn4_04` or `Roslyn4_11`) _based on the version of the .NET SDK I was building with_.

It turns out, I've never had to do that before. I've had _minimum_ .NET SDK version requirements, but needing to run with two different SDK versions isn't something I'd tried before. Luckily, it turns out that MSBuild exposes the .NET SDK version you're using as a property. I used this in my test project _.csproj_ to set a simple MSBuild property and a constant, which I could use further on in the _.csproj_ file, and in `#if` conditions in the code.

This post is already long, so I've just shown the pertinent parts of the _.csproj_ file below:

```
<!-- Are we building with .NET SDK 8.0.400 or greater? -->
<PropertyGroup Condition="$([MSBuild]::VersionGreaterThanOrEquals('$(NETCoreSdkVersion)', '8.0.400'))">
  <!-- If so, set some properties and constants -->
  <UsingModernDotNetSdk>true</UsingModernDotNetSdk>
  <DefineConstants>$(DefineConstants);INTERCEPTORS</DefineConstants>
</PropertyGroup>

<!-- This project can only be loaded when we have a high enough compiler version -->
<ItemGroup Condition="$(UsingModernDotNetSdk) == true">
  <!-- Can use the 4_11 project -->
  <ProjectReference Include="..\..\src\NetEscapades.EnumGenerators\NetEscapades.EnumGenerators.Roslyn4_11.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>

<ItemGroup Condition="$(UsingModernDotNetSdk) != true">
  <!-- Have to use the 4_04 project instead-->
  <ProjectReference Include="..\..\src\NetEscapades.EnumGenerators\NetEscapades.EnumGenerators.Roslyn4_04.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

That's the biggest difference in this project; the test files make judicious use of `#if INTERCEPTORS` to both test the behaviour where interceptors _are_ available (in `4.11`) but also where they're _not_ available (`4.4`).

The final piece of the puzzle I want to touch on are how I set up the testing for this in CI.

## [Setting up a CI build in GitHub Actions](http://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/#setting-up-a-ci-build-in-github-actions)

Prior to the above changes, I already had a relatively simple, but thorough, GitHub Actions CI pipeline set up for building and testing the package, which looked something like the following (I've reduced some of the unimportant aspects):

```
name: BuildAndPack

# Build pushes and PRs
on:
  push:
    branches:
      - main
    tags:
      - '*'
  pull_request:
    branches:
      - '*'

jobs:
  # Build and test on multiple OS
  build-and-test:
    strategy:
      matrix:
        include:
          - os: windows
            vm: windows-latest
          - os: linux
            vm: ubuntu-latest
          - os: macos
            vm: macos-13

    name: ${{ matrix.os}}
    runs-on: ${{ matrix.vm}}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            8.0.402
            6.0.x
            3.1.x
      # Build, Test, Pack, Test the NuGet package, and (optionally) push to NuGet
      - name: Run './build.cmd Clean Test TestPackage PushToNuGet'
        run: ./build.cmd Clean Test TestPackage PushToNuGet
        env:
          NuGetToken: ${{ secrets.NUGET_TOKEN }}
```

Previously, this whole build was using the `8.0.402` .NET SDK, but with my changes I wanted to ensure I could run _some_ of the tests with an earlier version of the .NET SDK. Specifically, and for simplicity, I decided to _only_ run the NuGet package tests on both SDKs in CI. Consequently, I updated the build pipeline to be something like the following (again, simplified, but with the changes highlighted):

```
name: BuildAndPack

# Build pushes and PRs
on:
  push:
    branches:
      - main
    tags:
      - '*'
  pull_request:
    branches:
      - '*'

jobs:
  # Build and test on multiple OS
  build-and-test:
    strategy:
      matrix:
        include:
          - os: windows
            vm: windows-latest
          - os: linux
            vm: ubuntu-latest
          - os: macos
            vm: macos-13

    name: ${{ matrix.os}}
    runs-on: ${{ matrix.vm}}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          # NOTE: installing the 8.0.110 SDK too
          dotnet-version: |
            8.0.402
            8.0.110
            6.0.x
            3.1.x
      # Force the build to use the 8.0.402 SDK specifically
      - run: dotnet new globaljson --sdk-version "8.0.402" --force

      # Run the build, test, pack, and package test with the 8.0.402 SDK
      - name: Run './build.cmd Clean Test TestPackage'
        run: ./build.cmd Clean Test TestPackage

      # Force the build to use an earlier .NET SDK
      - run: dotnet new globaljson --sdk-version "8.0.110" --force

      # Run the TestPackage stage with the earlier SDK (skipping dependent steps)
      - name: Run './build.cmd TestPackage PushToNuGet --skip'
        run: ./build.cmd TestPackage PushToNuGet --skip
        env:
          NuGetToken: ${{ secrets.NUGET_TOKEN }}
```

I _could_ have chosen to run _all_ the integration tests with the earlier SDK, but it didn't seem worth the effort, as I share [the same actual test files between both the integration tests and package tests](https://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/). So with that, I'm done!

## [Was it worth it? Should you do it?](http://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/#was-it-worth-it-should-you-do-it-)

As you can tell by the length of the post, there's a _lot_ of moving pieces required to support multiple Roslyn API versions in a source generator. This post is actually quite brief compared to the changes I found I needed to make in my project, as I haven't touched on a bunch of additional points.

> A prime example of work not covered here is tracking your tests in CI. I wanted to make sure I didn't "lose" any tests with these changes, and to understand exactly what I was testing under what SDK. I used the `EnricoMi/publish-unit-test-result-action@v2` GitHub Action for this, but tracking it properly required a bunch of gnarly `#if`/`#elif` that I didn't go into in this post, but [which you can see in the repo](https://github.com/andrewlock/NetEscapades.EnumGenerators/blob/main/tests/NetEscapades.EnumGenerators.Interceptors.IntegrationTests/InterceptorTests.cs#L8-L28) if you like pain😆

So…was it worth it? Well, it depends. If you don't _have_ to do this, then don't. It's a _lot_ of work and added complexity to a project, without a big pay off. _In general_ there aren't a lot of reasons _not_ to just update your .NET SDK, so there _shouldn't_ be a big call for supporting older versions of the SDK.

> Ironically, I have one in my day job—.NET 8 dropped support for the unsupported (but still heavily used) CentOS 7 Linux distro (and related distros), which means we _can't_ upgrade the SDK for the portions of the build that need to run on CentOS 7🙁

In my case, I wanted to increase the .NET SDK requirement so I could add a _preview_ of an _optional_ component. That didn't seem a big enough justification for forcing _everyone_ to update their .NET SDK, so multi-targeting _somewhat_ made sense. Honestly, I mostly took the multi-targeting approach to see how bad it would be. And the verdict is: it's Pretty Bad™😅

## [Summary](http://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/#summary)

In this post I showed how you could update an existing source generator project to add multi-targeting support for multiple Roslyn API versions. This ensures your source generator can run on the widest possible range of .NET SDK versions and that features which require a newer SDK version can "light up" when it's available. Unfortunately this process is somewhat complex.

In this post I described the changes I made to the project files of my _NetEscapades.EnumGenerators_ source generator project when I added Roslyn API multi-targeting support. I split the main source generator API into two, to produce two different dlls targeting different versions of the _Microsoft.CodeAnalysis.CSharp_ package. To test the changes, I split my unit testing projects in two, each testing a different version of the source generator project, and updated the integration tests to support working with multiple versions of the .NET SDK.

Overall multi-targeting multiple versions of the .NET SDK adds quite a lot of complexity to the various projects you need, and I wouldn't recommend it unless you definitely need to.

[![Image 5: Supporting multiple .NET SDK versions in analyzers and source generators](https://andrewlock.net/content/images/2025/system_text_json_banner.png)Previous Supporting multiple .NET SDK versions in analyzers and source generators](https://andrewlock.net/supporting-multiple-sdk-versions-in-analyzers-and-source-generators/)[![Image 6: Preventing client-side cross-site-scripting vulnerabilities with Trusted Types](https://andrewlock.net/content/images/2025/trusted_types_banner.png)Next Preventing client-side cross-site-scripting vulnerabilities with Trusted Types](https://andrewlock.net/preventing-client-side-cross-site-scripting-vulnerabilities-with-trusted-types/)
