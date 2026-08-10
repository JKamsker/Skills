Title: Integration testing and NuGet packaging: Creating a source generator - Part 3

URL Source: http://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/

Published Time: 2021-12-21T10:00:00.0000000

Markdown Content:
December 21, 2021 ~9 min read

[Creating a source generator - Part 3](https://andrewlock.net/series/creating-a-source-generator/)

This is the third post in the series: [Creating a source generator](https://andrewlock.net/series/creating-a-source-generator/).

1.   [Part 1 - Creating an incremental generator](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/)
2.   [Part 2 - Testing an incremental generator with snapshot testing](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/)
3.   Part 3 - Integration testing and NuGet packaging (this post) 
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
14.   [Part 14 - Supporting multiple .NET SDK versions in a source generator](https://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/)
15.   [Part 15 - Solving the source generator 'marker attribute' problem in .NET 10](https://andrewlock.net/exploring-dotnet-10-preview-features-4-solving-the-source-generator-marker-attribute-problem-in-dotnet-10/)

In [the first post in this series](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/), I described how to create a .NET 6 incremental source generator, and [in the second post](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/) I described how to unit-test your generators using snapshot testing with [Verify](https://github.com/VerifyTests/Verify). These are essential first steps to building a source generator, and the snapshot testing provides a quick, easily-debuggable testing approach.

Another essential part of testing your package is _integration_ testing. By that, I mean testing the source generator as it will be used in practice, as part of a project's compilation process. Similarly, if you are going to ship your source generator as a NuGet package, then you should test that the NuGet package is working correctly when used by consuming projects.

In this post, I'm going to do 3 things for the source generator I've been creating in this series:

*   Create an integration test project
*   Create a NuGet package
*   Test the NuGet package in an integration test project

Everything in this post builds on the work in the earlier posts, so make sure to refer back to those if you find something confusing!

1.   [Create the integration test project](http://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/#1-create-the-integration-test-project)
2.   [Add the integration test](http://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/#2-add-the-integration-test)
3.   [Create a NuGet package](http://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/#3-create-a-nuget-package)
4.   [Creating a local NuGet package source with a custom NuGet config](http://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/#4-creating-a-local-nuget-package-source-with-a-custom-nuget-config)
5.   [Add a NuGet package test project](http://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/#6-add-a-nuget-package-test-project)
6.   [Run the NuGet package integration test](http://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/#7-run-the-nuget-package-integration-test)

## [1. Create the integration test project](http://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/#1-create-the-integration-test-project)

The first step is to create the integration test project. The following script creates a new xunit test project, adds it to the solution, and adds a reference to the source generator project:

```
dotnet new xunit -o ./tests/NetEscapades.EnumGenerators.IntegrationTests
dotnet sln add ./tests/NetEscapades.EnumGenerators.IntegrationTests
dotnet add ./tests/NetEscapades.EnumGenerators.IntegrationTests reference ./src/NetEscapades.EnumGenerators
```

This creates a normal project reference between the test project and the source generator project, something like this following:

```
<ProjectReference Include="..\..\src\NetEscapades.EnumGenerators\NetEscapades.EnumGenerators.csproj" />
```

Unfortunately, for source generator (or analyzer) projects, you need to tweak this element slightly so that it works correctly. Specifically, you need to add the `OutputItemType` and `ReferenceOutputAssembly` attributes

*   `OutputItemType="Analyzer"` tells the compiler to load the project as part of the compilation process.
*   `ReferenceOutputAssembly="false"` tells the project _not_ to reference the source generator project's dll

This gives a project reference similar to the following:

```
<ProjectReference Include="..\..\src\NetEscapades.EnumGenerators\NetEscapades.EnumGenerators.csproj"
    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

With these changes, your integration test project should look something like the following:

```
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <!-- 👇 This project reference is added by the script...-->
    <ProjectReference Include="..\..\src\NetEscapades.EnumGenerators\NetEscapades.EnumGenerators.csproj"
                      OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
                <!-- 👆 But you need to add these attributes yourself-->
  </ItemGroup>

  <!-- 👇 Added in the template by default -->
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="16.11.0" />
    <PackageReference Include="xunit" Version="2.4.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.3">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="3.1.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

</Project>
```

With the project file sorted, let's add some basic tests to confirm the source generator is working correctly!

## [2. Add the integration test](http://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/#2-add-the-integration-test)

The first thing we need for our tests is an `enum` for the source generator to create an extension class. The following is a super basic one, but note that I've given it the `[Flags]` attribute for extra complexity. This isn't _necessary_ but it's a slightly more complex example for our tests which we want to make sure to handle:

```
using System;

namespace NetEscapades.EnumGenerators.IntegrationTests;

[EnumExtensions]
[Flags]
public enum Colour
{
    Red = 1,
    Blue = 2,
    Green = 4,
}
```

For our initial tests, we're simply going to confirm two things:

1.   The source generator generates an extension method called `ToStringFast()` when an `enum` is decorated with the `[EnumExtensions]` attribute.
2.   The result of calling `ToStringFast()` is the same as calling `ToString()`.

The following test does just that. It tests 5 different values for the `Colour``enum`, including:

*   Valid values (`Color.Red`)
*   Invalid values (`(Colour)15`)
*   Composite value (`Colour.Green | Colour.Blue`)

This test confirms both that the extension exists (otherwise it wouldn't compile) and that we get the expected results for all the above values:

```
using Xunit;

namespace NetEscapades.EnumGenerators.IntegrationTests;

public class EnumExtensionsTests
{
    [Theory]
    [InlineData(Colour.Red)]
    [InlineData(Colour.Green)]
    [InlineData(Colour.Green | Colour.Blue)]
    [InlineData((Colour)15)]
    [InlineData((Colour)0)]
    public void FastToStringIsSameAsToString(Colour value)
    {
        var expected = value.ToString();
        var actual = value.ToStringFast();

        Assert.Equal(expected, actual);
    }
}
```

And that's it. We can run all our tests by running `dotnet test` on the solution.

> Note that if you make changes to your source generator you may need to close and re-open your IDE before your integration test project respects the changes.

If you're creating a source generator for a specific project, then this level of integration test may be all you need. However, if you're planning on sharing the source generator more widely, then you will likely want to create a NuGet package.

## [3. Create a NuGet package](http://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/#3-create-a-nuget-package)

Creating a NuGet package from a source generator is _similar_ to a NuGet package for a standard library, but the _contents_ of the NuGet package will be laid out differently. Specifically, you have to:

*   Ensure the build output ends up in the _analyzers/dotnet/cs_ folder in the NuGet package.
*   Ensure the dll _doesn't_ end up in the "normal" folder in the NuGet package.

For the first point, ensure you have the following `<ItemGroup>` in your project:

```
<ItemGroup>
  <None Include="$(OutputPath)\$(AssemblyName).dll" Pack="true" 
      PackagePath="analyzers/dotnet/cs" Visible="false" />
</ItemGroup>
```

This will ensure the source generator assembly is packed into the correct location in the NuGet package, so that the compiler will load it as an analyzer/source generator.

You should also set the property `IncludeBuildOutput` to `false`, so that the consuming project doesn't get a reference to the source generator dll itself:

```
<PropertyGroup>
  <IncludeBuildOutput>false</IncludeBuildOutput>
</PropertyGroup>
```

With that, you can simply `dotnet pack` the project. In the following example, I set the version number to `0.1.0-beta`, and ensure the output is put into the folder _./artifacts_:

```
dotnet pack -c Release -o ./artifacts -p:Version=0.1.0-beta
```

This will produce a NuGet package called something like:

```
NetEscapades.EnumGenerators.0.1.0-beta.nupkg
```

If you open the package in NuGet package explorer, the layout should be as shown in the following image, with the dll inside the _analyzers/dotnet/cs_ folder, with no other dlls/folders included.

![Image 1: The NuGet package layout](https://andrewlock.net/content/images/2021/nuget_tests.png)

Now, testing this package is where things get tricky. We don't want to push the NuGet package to a repository before we've tested it. We _also_ don't want to "pollute" our local NuGet cache with this package. That requires jumping through a few hoops.

## [4. Creating a local NuGet package source with a custom NuGet config](http://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/#4-creating-a-local-nuget-package-source-with-a-custom-nuget-config)

First off, we need to create a local NuGet package source. By default, when you run `dotnet restore`, packages are restored from nuget.org, but we want to ensure our NuGet test project uses the local test package. That means we need to configure a custom restore source.

The typical way to do this is to create a _nuget.config_ file, and list the additional sources. You can include both remote sources (like nuget.org, or private NuGet feeds like myget.org) _and_ "local" sources, which are just a folder of NuGet packages. That latter option is exactly what we want.

However, for our testing we don't necessarily want to create a config file with the "default" _nuget.config_ name, as otherwise that source would be used for restoring _everything_ in our solution. Ideally, we only want to use the local source with our beta package for this one NuGet integration test project. To achieve that, we give our config file a different name so that it's not used automatically, and we will explicitly specify this name where necessary.

The following script creates a _nuget.config_ file, renames it to _nuget.integration-tests.config_, and adds the _./artifacts_ directory as a nuget source called `local-packages` (where we packaged our test NuGet package):

```
dotnet new nugetconfig 
mv nuget.config nuget.integration-tests.config
dotnet nuget add source ./artifacts -n local-packages --configfile nuget.integration-tests.config
```

The resulting _nuget.integration-tests.config_ file looks like this:

```
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <!--To inherit the global NuGet package sources remove the <clear/> line below -->
    <clear />
    <add key="nuget" value="https://api.nuget.org/v3/index.json" />
    <add key="local-packages" value="./artifacts" />
  </packageSources>
</configuration>
```

Now we have the config file it's time to create our NuGet-integration-test project.

## [6. Add a NuGet package test project](http://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/#6-add-a-nuget-package-test-project)

In the first part of this post we created an integration test project, to confirm the source generator worked correctly when running "inline" in the compiler. For the NuGet package tests I'm going to use a little MSBuild trickery to use exactly the same test files in the NuGet package test as the "normal" integration test, to reduce the duplication and ensure consistency.

The following script creates a new xunit test project, and adds a reference to our test NuGet package:

```
dotnet new xunit -o ./tests/NetEscapades.EnumGenerators.NugetIntegrationTests
dotnet add ./tests/NetEscapades.EnumGenerators.NugetIntegrationTests package NetEscapades.EnumGenerators --version 0.1.0-beta
```

Note that we're _not_ adding this project to the solution file, so it's not part of the normal restore/build/test dev cycle. This simplifies several things, and as we already have the integration test, that's not a big problem. This project tests that the NuGet package is being created correctly, but I think it's fine to only do that as part of the CI process.

> Another option is to add the project to the solution, but remove the project from all the solution's build configurations.

After running the above script, we need to make some manual changes to the _.csproj_ file to include all the C# files from the "normal" integration test project in this NuGet integration test project. To do that, we can use a `<Compile>` element, with a wildcard for the `Include` attribute referencing the other project's _.cs_ files. The resulting project file should look something like this:

```
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <!-- 👇 Add the temporary package -->
  <ItemGroup>
    <PackageReference Include="NetEscapades.EnumGenerators" Version="0.1.0-beta" />
  </ItemGroup>

  <!-- 👇 Link in all the files from the integration test project, so we run the same tests -->
  <ItemGroup>
    <Compile Include="..\NetEscapades.EnumGenerators.IntegrationTests\*.cs" Link="%(Filename)%(Extension)"/>
  </ItemGroup>

  <!-- Standard packages from the template -->
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="16.11.0" />
    <PackageReference Include="xunit" Version="2.4.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.3">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="3.1.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

</Project>
```

And that's it. This project literally consists of a project file and the _nuget.integration-tests.config_ config file. All that remains is to run it.

## [7. Run the NuGet package integration test](http://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/#7-run-the-nuget-package-integration-test)

Unfortunately, running the project is another case where we need to be careful. We don't want to "pollute" our machine NuGet caches with the test NuGet package, and we need to use our custom _nuget.config_ file. That requires running the `restore`/`build`/`test` steps all separately, passing the required command switches where necessary.

To make sure we don't pollute our NuGet caches with the test package, we restore NuGet packages to a local folder, _./packages_. This will use a lot more drive space and network during restore (as you'll be pulling NuGet packages that are already cached elsewhere to this folder). But trust me, it's absolutely worth doing. If you don't, you're setting yourself up for confusing errors down the line, when you can't update your test package, or some completely separate project starts using your test package!

The following script runs the `restore`/`build`/`test` for the NuGet integration test project. It assumes that you've already built the NuGet package as described in section 3.

```
# Restore the project using the custom config file, restoring packages to a local folder
dotnet restore ./tests/NetEscapades.EnumGenerators.NugetIntegrationTests --packages ./packages --configfile "nuget.integration-tests.config" 

# Build the project (no restore), using the packages restored to the local folder
dotnet build ./tests/NetEscapades.EnumGenerators.NugetIntegrationTests -c Release --packages ./packages --no-restore

# Test the project (no build or restore)
dotnet test ./tests/NetEscapades.EnumGenerators.NugetIntegrationTests -c Release --no-build --no-restore
```

If all has gone well, your tests should pass, and you can be confident that the NuGet package you created is correctly built and ready for distribution!

## [Summary](http://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/#summary)

In this post I showed how to create an integration test project for a source generator by using a project reference, running the generator as part of the compiler, just as you would for a normal reference. I also showed how to create a NuGet package for your source generator, and how to create an integration test for your package, to ensure it's been created correctly. This latter process is more complicated, as you have to be careful not to pollute your local NuGet package caches with the test package.

[![Image 2: Testing an incremental generator with snapshot testing](https://andrewlock.net/content/images/2021/snapshot_source_gen_banner.png)Previous Testing an incremental generator with snapshot testing: Creating a source generator - Part 2](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/)[![Image 3: Customising generated code with marker attributes](https://andrewlock.net/content/images/2021/source_gen_banner.png)Next Customising generated code with marker attributes: Creating a source generator - Part 4](https://andrewlock.net/creating-a-source-generator-part-4-customising-generated-code-with-marker-attributes/)

Andrew Lock | .Net Escapades

![Image 4](https://andrewlock.net/assets/img/icons/apple/apple-touch-icon-180x180.png)Want an email when

there's new posts?
