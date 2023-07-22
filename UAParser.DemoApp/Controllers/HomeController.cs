namespace DemoApp.NET7.Controllers;

using System;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using UAParser.Interfaces;

[ApiController]
[Route("[controller]")]
public class HomeController : ControllerBase
{
    private readonly ILogger<HomeController> logger;
    
    private readonly IUserAgentParser userAgentParser;

    public HomeController(IUserAgentParser browserDetector, ILogger<HomeController> logging)
    {
        this.logger = logging ?? throw new ArgumentNullException(nameof(logging));
        this.userAgentParser = browserDetector ?? throw new ArgumentNullException(nameof(browserDetector));
    }

    [HttpGet]
    public string Get()
    {
        this.logger.LogInformation("Inside GET action method");

        var browser = this.userAgentParser.ClientInfo;

        var userAgent = this.HttpContext.Request.Headers["User-Agent"];

        return
            $".NET 7.0 APP. Browser:{browser.Browser.Family}, Version: {browser.Browser.Version}, Device type: {browser.Device.Family}, OS: {browser.OS}, Original User-Agent: {userAgent}";
    }
}
