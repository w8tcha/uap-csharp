using System;

using Microsoft.AspNetCore.Http;

using UAParser.Interfaces;
using UAParser.Objects;

namespace UAParser;

/// <summary>
/// A class to get browser and platform information.
/// </summary>
public sealed class UserAgentParser : IUserAgentParser
{
    private readonly Lazy<IUAParserOutput> clientInfo;

    private readonly IHttpContextAccessor httpContextAccessor;

    private const string UserAgent = "User-Agent";

    /// <summary>
    /// Initializes a new instance of the <see cref="BrowserDetector"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The IHttpContextAccessor instance.</param>
    public UserAgentParser(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.clientInfo = this.GetBrowserLazy();
    }

    /// <summary>
    /// Gets the browser information.
    /// </summary>
    public IUAParserOutput ClientInfo => this.clientInfo.Value;

    private Lazy<IUAParserOutput> GetBrowserLazy()
    {
        return new Lazy<IUAParserOutput>(this.GetBrowser);
    }

    /// <summary>
    /// Gets the IBrowser instance.
    /// </summary>
    /// <returns>The IBrowser instance.</returns>
    private ClientInfo GetBrowser()
    {
        // get a parser with the embedded regex patterns
        var uaParser = Parser.GetDefault();

        return this.httpContextAccessor.HttpContext?.Request.Headers.TryGetValue(UserAgent, out var uaHeader)
               == true
                   ? uaParser.Parse(uaHeader[0])
                   : default;
    }
}
