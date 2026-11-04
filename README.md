UAParser.Core
=============

[![NuGet](https://img.shields.io/nuget/v/UAParser.Core.svg)](https://nuget.org/packages/UAParser.Core)
[![build dotnet](https://github.com/w8tcha/uap-csharp/actions/workflows/build.yml/badge.svg)](https://github.com/w8tcha/uap-csharp/actions/workflows/build.yml)
[![Coverage](https://github.com/w8tcha/uap-csharp/raw/badges/badge_linecoverage.svg)](https://github.com/w8tcha/uap-csharp/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)

A User-Agent parser for ASP.NET Core and .NET, based on the [ua-parser](https://github.com/ua-parser) project.
It tells you the **browser**, **operating system** and **device** behind a User-Agent string.

It uses the shared regex definitions from [uap-core](https://github.com/ua-parser/uap-core). The latest definitions are
embedded in the assembly, so it works out of the box.

## Features

- Parses browser, OS and device (family, brand, model, version) from any User-Agent string
- ASP.NET Core integration: one call to register, then inject `IUserAgentParser` anywhere
- Optional Windows 11 detection through [User-Agent Client Hints](#detect-windows-11)
- Fast: a literal prefilter skips regexes that cannot match, and frequently used regexes are compiled in the background
- Optional result caching through `IMemoryCache`
- Load your own, newer regex definitions at runtime
- Targets .NET 9 and .NET 10

## Installation

```shell
dotnet add package UAParser.Core
```

## Usage in ASP.NET Core

### 1. Register the services

In `Program.cs`, register the parser. It needs a memory cache, so add one too:

```c#
using UAParser.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.AddUserAgentParser();
```

### 2. Inject `IUserAgentParser`

`IUserAgentParser.ClientInfo` holds the parsed User-Agent of the current request. It is `null` when the request has no
`User-Agent` header.

**In a controller**

```c#
using UAParser.Interfaces;

public class HomeController(IUserAgentParser userAgentParser) : Controller
{
    public IActionResult Index()
    {
        var clientInfo = userAgentParser.ClientInfo;

        // e.g. "Chrome 131.0.0", "Windows 10", "Other"
        ViewData["Browser"] = $"{clientInfo?.Browser.Family} {clientInfo?.Browser.Version}";

        return View();
    }
}
```

**In a Razor view or page**

```cshtml
@inject UAParser.Interfaces.IUserAgentParser parser

<h2>@parser.ClientInfo?.Browser.Family</h2>
<h3>@parser.ClientInfo?.Browser.Version</h3>
<h3>@parser.ClientInfo?.OS</h3>
<h3>@parser.ClientInfo?.Device</h3>
```

**In a middleware**

Middleware is created once, so inject the scoped `IUserAgentParser` into `InvokeAsync`, not the constructor:

```c#
public class MyCustomMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, IUserAgentParser parser)
    {
        if (parser.ClientInfo?.Device.IsSpider == true)
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(httpContext);
    }
}
```

### Detect Windows 11

Windows 11 browsers still send `Windows NT 10.0` in the User-Agent string, so the string alone reports Windows 10.
Chromium based browsers (Edge, Chrome, Opera) can report the real version through the `Sec-CH-UA-Platform-Version`
[client hint](https://learn.microsoft.com/microsoft-edge/web-platform/how-to-detect-win11), but only when the server
asks for it. Add the middleware to ask for it:

```c#
app.UseUserAgentClientHints();
```

This sends the `Accept-CH` and `Critical-CH` response headers. When the hint reports Windows 11, `ClientInfo.OS` is
`Windows 11` (`Major` = `"11"`).

- Pass `critical: false` to skip `Critical-CH`. The hint is then only available from the second request on.
- Client hints are only sent over HTTPS.
- Firefox and Safari don't send client hints, so they keep reporting Windows 10.
- If you cache responses that depend on the OS, add `Sec-CH-UA-Platform-Version` to the `Vary` header.

## Usage without ASP.NET Core

Use the `Parser` class directly to parse any User-Agent string:

```c#
using UAParser;

var parser = Parser.GetDefault();

var clientInfo = parser.Parse(
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");

Console.WriteLine(clientInfo.Browser.Family);  // Chrome
Console.WriteLine(clientInfo.Browser.Version); // 131.0.0
Console.WriteLine(clientInfo.OS);              // Windows 10
Console.WriteLine(clientInfo.Device.Family);   // Other
```

Building a parser is expensive, so create it once and reuse it. `Parser.GetDefault()` without arguments returns a shared
instance, and a parser is safe to use from multiple threads.

You can also parse only one part with `ParseUserAgent`, `ParseOS` or `ParseDevice`, or use `ParseAsync`. Pass
`useCache: true` to cache results per User-Agent string.

### Parser options

```c#
var parser = Parser.GetDefault(new ParserOptions
{
    // Compile all regexes: slower startup, higher throughput. Default: false.
    UseCompiledRegex = true,

    // Compile only the frequently used regexes in the background. Default: true.
    CompileFrequentlyUsedRegex = true,

    // Limit the time a single regex may run, as a guard against catastrophic backtracking.
    // Default: Regex.InfiniteMatchTimeout.
    MatchTimeOut = TimeSpan.FromSeconds(1)
});
```

### Use your own regex definitions

The embedded definitions are updated with each release, but you can load newer ones at runtime. The parser reads the
JSON form of `regexes.yaml`:

```c#
await using var stream = File.OpenRead("regexes.json");
var parser = await Parser.FromJsonAsync(stream);
```

## Building from source

Build and run the tests:

```shell
dotnet build UAParser.Core.slnx
dotnet test UAParser.Core.slnx
```

### Update the embedded regexes

The regex definitions come from the [uap-core](https://github.com/ua-parser/uap-core) npm package. To pull the latest
version into the project:

```shell
npm install
npm run update
```

This updates `UAParser.Core/regexes.yaml` and `UAParser.Core/regexes.json`. The CI build fails if they are out of date.

## License

Licensed under the [Apache License, Version 2.0](LICENSE).

## Authors

- Søren Enemærke [@sorenenemaerke](https://twitter.com/sorenenemaerke) / [github](https://github.com/enemaerke)
- Atif Aziz [@raboof](https://twitter.com/raboof) / [github](https://github.com/atifaziz)
