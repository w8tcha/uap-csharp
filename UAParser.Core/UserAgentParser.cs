//
// Copyright Atif Aziz, Søren Enemærke
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

namespace UAParser;

using System;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

using UAParser.Interfaces;
using UAParser.Objects;

/// <summary>
/// A class to get browser and platform information.
/// </summary>
public sealed class UserAgentParser : IUserAgentParser
{
    /// <summary>The client information</summary>
    private readonly Lazy<IUAParserOutput> clientInfo;

    private readonly IHttpContextAccessor httpContextAccessor;

    private readonly IMemoryCache cache;

    private const string UserAgent = "User-Agent";

    /// <summary>
    /// Initializes a new instance of the <see cref="UserAgentParser"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="memoryCache">The memory cache.</param>
    public UserAgentParser(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.clientInfo = this.GetBrowserLazy();
        this.cache = memoryCache;
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
        var uaParser = Parser.GetDefault(new ParserOptions { UseCompiledRegex = true } , this.cache);

        return this.httpContextAccessor.HttpContext?.Request.Headers.TryGetValue(UserAgent, out var uaHeader)
                   is true
                   ? uaParser.Parse(uaHeader[0], true)
                   : null;
    }
}
