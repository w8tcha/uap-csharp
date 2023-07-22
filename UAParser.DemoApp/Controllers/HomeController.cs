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

    public HomeController(IUserAgentParser parser, ILogger<HomeController> logging)
    {
        this.logger = logging ?? throw new ArgumentNullException(nameof(logging));
        this.userAgentParser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    [HttpGet]
    public string Get()
    {
        this.logger.LogInformation("Inside GET action method");

        var clientInfo = this.userAgentParser.ClientInfo;

        var userAgent = this.HttpContext.Request.Headers["User-Agent"];

        return
            $".NET 7.0 APP. Browser:{clientInfo.Browser.Family}, Version: {clientInfo.Browser.Version}, Device type: {clientInfo.Device.Family}, OS: {clientInfo.OS}, Original User-Agent: {userAgent}";
    }
}
