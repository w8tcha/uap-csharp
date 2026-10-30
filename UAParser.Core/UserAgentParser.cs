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
    private readonly Lazy<IUAParserOutput> _clientInfo;

    private readonly IHttpContextAccessor _httpContextAccessor;

    private readonly IMemoryCache _cache;

    private const string UserAgent = "User-Agent";

    private static readonly object CompiledParserLock = new object();

    // Building the compiled-regex parser is expensive (regex compilation for hundreds of
    // patterns), so it is built once per process and reused across requests instead of on
    // every scoped UserAgentParser instance.
    private static Parser _compiledParser;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserAgentParser"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="memoryCache">The memory cache.</param>
    public UserAgentParser(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache)
    {
        this._httpContextAccessor = httpContextAccessor;
        this._clientInfo = this.GetBrowserLazy();
        this._cache = memoryCache;
    }

    /// <summary>
    /// Gets the browser information.
    /// </summary>
    public IUAParserOutput ClientInfo => this._clientInfo.Value;

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
        if (this._httpContextAccessor.HttpContext?.Request.Headers.TryGetValue(UserAgent, out var uaHeader)
            is not true)
        {
            return null;
        }

        var userAgent = uaHeader[0];
        var uaParser = GetCompiledParser();

        // The injected cache is used here rather than inside the shared parser, so every instance
        // honors its own cache instead of the one passed to the first instance.
        return this._cache.GetOrCreate(userAgent, _ => uaParser.Parse(userAgent));
    }

    private static Parser GetCompiledParser()
    {
        if (_compiledParser != null)
            return _compiledParser;

        lock (CompiledParserLock)
        {
            _compiledParser ??= Parser.GetDefault(new ParserOptions { UseCompiledRegex = true });
        }

        return _compiledParser;
    }
}
