Title: Creating a source generator

URL Source: http://andrewlock.net/series/creating-a-source-generator/

Published Time: 2021-12-09T10:00:00.0000000

Markdown Content:
# Creating a source generator

![Image 2: blog post image](http://andrewlock.net/content/images/2021/source_gen_banner.jpg)

[![Image 3: Andrew Lock avatar](http://andrewlock.net/content/images/logo-small.jpg)](http://andrewlock.net/about/)
# [Andrew Lock | .NET Escapades](http://andrewlock.net/)[Andrew Lock](http://andrewlock.net/)

*   [Home](http://andrewlock.net/)
*   [About](http://andrewlock.net/about/)
*   [Subscribe](http://andrewlock.net/series/creating-a-source-generator/#)
*   [Dark](http://andrewlock.net/series/creating-a-source-generator/# "Switch to dark theme")[Light](http://andrewlock.net/series/creating-a-source-generator/# "Switch to light theme")

*   [](https://www.facebook.com/NETescapades "Like .NET Escapades on Facebook")
*   [](https://twitter.com/andrewlocknet "Follow @andrewlocknet on Twitter")
*   [](https://bsky.app/profile/andrewlock.bsky.social "Follow @andrewlock.bsky.social on Bluesky")
*   [](https://hachyderm.io/@andrewlock "Follow @andrewlock@hachyderm.io on Mastadon")
*   [](https://uk.linkedin.com/in/andrewdlock "Andrew Lock on LinkedIn")
*   [](https://github.com/andrewlock "Andrew lock on Github")
*   [](http://andrewlock.net/rss.xml "Subscribe to RSS")

**Sponsored by**[**Dometrain Courses**](https://dometrain.com/dometrain-pro/?ref=andrew-lock&promo=banner&coupon_code=ANDREW30)—Get 30% off [**Dometrain Pro**](https://dometrain.com/dometrain-pro/?ref=andrew-lock&promo=banner&coupon_code=ANDREW30) with code [**ANDREW30**](https://dometrain.com/dometrain-pro/?ref=andrew-lock&promo=banner&coupon_code=ANDREW30) and access the best courses for .NET Developers

December 09, 2021

*   [.NET Core](http://andrewlock.net/tag/net-core/)
*   [C#](http://andrewlock.net/tag/c/)
*   [Source Generators](http://andrewlock.net/tag/source-generators/)

# Series: Creating a source generator

 Share on: 
*   [](https://www.facebook.com/sharer/sharer.php?u=https%3A%2F%2Fandrewlock.net%2Fseries%2Fcreating-a-source-generator%2F&t=Creating+a+source+generator "Share on Facebook")
*   [](https://twitter.com/intent/tweet?source=https%3A%2F%2Fandrewlock.net%2Fseries%2Fcreating-a-source-generator%2F&text=Creating+a+source+generator:https%3A%2F%2Fandrewlock.net%2Fseries%2Fcreating-a-source-generator%2F "Tweet")
*   [](https://twitter.com/intent/tweet?source=https%3A%2F%2Fandrewlock.net%2Fseries%2Fcreating-a-source-generator%2F&text=Creating+a+source+generator:https%3A%2F%2Fandrewlock.net%2Fseries%2Fcreating-a-source-generator%2F "Share on Bluesky")
*   [](http://www.reddit.com/submit?url=https%3A%2F%2Fandrewlock.net%2Fseries%2Fcreating-a-source-generator%2F&title=Creating+a+source+generator "Submit to Reddit")
*   [](http://www.linkedin.com/shareArticle?mini=true&url=https%3A%2F%2Fandrewlock.net%2Fseries%2Fcreating-a-source-generator%2F&title=Creating+a+source+generator&source=https%3A%2F%2Fandrewlock.net%2Fseries%2Fcreating-a-source-generator%2F "Share on LinkedIn")

In this series I show how to create an incremental source generator, using the APIs introduced in .NET 6. I cover the basics of using the API, testing, design issues, and how to add the ability for users to control the generated source code.

Posts in this series (new posts will be listed here as they're written):

1.   [Part 1 - Creating an incremental generator](http://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/)
2.   [Part 2 - Testing an incremental generator with snapshot testing](http://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/)
3.   [Part 3 - Integration testing and NuGet packaging](http://andrewlock.net/creating-a-source-generator-part-3-integration-testing-and-packaging/)
4.   [Part 4 - Customising generated code with marker attributes](http://andrewlock.net/creating-a-source-generator-part-4-customising-generated-code-with-marker-attributes/)
5.   [Part 5 - Finding a type declaration's namespace and type hierarchy](http://andrewlock.net/creating-a-source-generator-part-5-finding-a-type-declarations-namespace-and-type-hierarchy/)
6.   [Part 6 - Saving source generator output in source control](http://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/)
7.   [Part 7 - Solving the source generator 'marker attribute' problem - Part 1](http://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/)
8.   [Part 8 - Solving the source generator 'marker attribute' problem - Part 2](http://andrewlock.net/creating-a-source-generator-part-8-solving-the-source-generator-marker-attribute-problem-part2/)
9.   [Part 9 - Avoiding performance pitfalls in incremental generators](http://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/)
10.   [Part 10 - Testing your incremental generator pipeline outputs are cacheable](http://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/)
11.   [Part 11 - Implementing an interceptor with a source generator](http://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/)
12.   [Part 12 - Reading compilation options and the C# version in source generators](http://andrewlock.net/creating-a-source-generator-part-12-reading-compilation-options-and-csharp-version-in-source-generators/)
13.   [Part 13 - Accessing MSBuild properties and user configuration from source generators](http://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/)
14.   [Part 14 - Supporting multiple .NET SDK versions in a source generator](http://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/)
15.   [Part 15 - Solving the source generator 'marker attribute' problem in .NET 10](http://andrewlock.net/exploring-dotnet-10-preview-features-4-solving-the-source-generator-marker-attribute-problem-in-dotnet-10/)

Follow me 
*   [](https://www.facebook.com/NETescapades "Like .NET Escapades on Facebook")
*   [](https://twitter.com/andrewlocknet "Follow @andrewlocknet on Twitter")
*   [](https://hachyderm.io/@andrewlock "Follow @andrewlock@hachyderm.io on Mastadon")
*   [](https://uk.linkedin.com/in/andrewdlock "Andrew Lock on LinkedIn")
*   [](https://github.com/andrewlock "Andrew lock on Github")
*   [](http://andrewlock.net/rss.xml "Subscribe to RSS")

Follow me 
*   [![Image 4: Buy Me A Coffee](http://andrewlock.net/assets/img/coffee.png)](https://www.buymeacoffee.com/andrewlock)
*   [![Image 5: Donate with PayPal](http://andrewlock.net/assets/img/paypal.png)](https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=M5VJREL5PTWNC&currency_code=GBP&source=url)

Loading...

[![Image 6: 30% off with code ANDREW30 on Dometrain Pro](http://andrewlock.net/content/images/a/nickchapsas2025.jpg)](https://dometrain.com/dometrain-pro/?ref=andrew-lock&promo=banner&coupon_code=ANDREW30)

[![Image 7: ASP.NET Core in Action, Third Edition](http://andrewlock.net/content/images/aspnetcoreinaction3e.png) My new book _ASP.NET Core in Action, Third Edition_ is available now! It supports .NET 7.0, and is available as an eBook or paperback.](http://mng.bz/5mRz)

Enjoy this blog? 
*   [![Image 8: Buy Me A Coffee](http://andrewlock.net/assets/img/coffee.png)](https://www.buymeacoffee.com/andrewlock)
*   [![Image 9: Donate with PayPal](http://andrewlock.net/assets/img/paypal.png)](https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=M5VJREL5PTWNC&currency_code=GBP&source=url)

 © 2026 Andrew Lock | .NET Escapades. All Rights Reserved. | [Image credits](http://andrewlock.net/credits/)
## Tags

 Andrew Lock | .Net Escapades ![Image 10: close](http://andrewlock.net/assets/img/icons-close.svg)

![Image 11](http://andrewlock.net/assets/img/icons/apple/apple-touch-icon-180x180.png)Want an email when

there's new posts?Subscribe

Stay up to the date with the latest posts!

Oops! Check your details and try again.

Thanks! Check your email for confirmation.

Subscribe
