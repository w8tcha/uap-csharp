UserAgent Parser (based on ua_parser C# Library)
======================

This is the ASP.NET Core implementation of [ua-parser](https://github.com/tobie/ua-parser). You can find the latest binaries on NuGet [here](https://www.nuget.org/packages/UAParser/).

[![build dotnet](https://github.com/w8tcha/uap-csharp/actions/workflows/build.yml/badge.svg)](https://github.com/w8tcha/uap-csharp/actions/workflows/build.yml)

The implementation uses the shared regex patterns and overrides from regexes.yaml (found in [uap-core](https://github.com/ua-parser/uap-core)). The assembly embeds the latest regex patterns (enabled through a node module) which are loaded into the default parser. You can create a parser with more updated regex patterns by using the static methods on `Parser` to pass in specific patterns in yaml format.

## Build and Run Tests
------
You can then build and run the tests by

````
dotnet restore

dotnet test
````

Update the embedded regexes
------
To pull the latest regexes into the project:

````
    npm install
	grunt
````

## How to use ?

**Step 1:**
Install the [UAParser.Core nuget package](https://www.nuget.org/packages/UAParser.Core/)


````
Install-Package Shyjus.BrowserDetector
````

**Step 2:** Enable the browser detection service inside the `ConfigureServices` method of `Startup.cs`.

```c#
public void ConfigureServices(IServiceCollection services)
{
    // Add user agent service
    services.AddUserAgentParser();

    services.AddMvc();
}
```

**Step 3:** Inject `IUserAgentParser` to your controller class or view file or middleware and access the `ClientInfo` property.

Example usage in controller code

```c#
public class HomeController : Controller
{
    private readonly IUserAgentParser userAgentParser;
    public HomeController(IUserAgentParser browserDetector)
    {
        this.userAgentParser = browserDetector;
    }
    public IActionResult Index()
    {
        var clientInfo = this.userAgentParser.ClientInfo;
        // Use ClientInfo object as needed.

        return View();
    }
}
```

Example usage in view code

```c#
@inject UAParser.Interfaces.IUserAgentParser browserDetector

<h2> @browserDetector.Browser.Family </h2>
<h3> @browserDetector.Browser.Version </h3>
<h3> @browserDetector.Browser.OS.ToString() </h3>
<h3> @browserDetector.Browser.Device.ToString() </h3>

```

Example usage in custom middlware

You can inject the `IUserAgentParser` to the `InvokeAsync` method.

```c#
public class MyCustomMiddleware
{
    private RequestDelegate next;
    public MyCustomMiddleware(RequestDelegate next)
    {
        this.next = next;
    }
    public async Task InvokeAsync(HttpContext httpContext, IUserAgentParser parser)
    {
        var clientInfo = parser.ClientInfo;

        if (clientInfo.Browser.Family == "Edge")
        {
            await httpContext.Response.WriteAsync("Have you tried the new chromuim based edge ?");
        }
        else
        {
            await this.next.Invoke(httpContext);
        }
    }
}
```

Authors:
-------

  * Søren Enemærke [@sorenenemaerke](https://twitter.com/sorenenemaerke) / [github](https://github.com/enemaerke)
  * Atif Aziz [@raboof](https://twitter.com/raboof) / [github](https://github.com/atifaziz)
