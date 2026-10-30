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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Caching.Memory;

using UAParser.Objects;

/// <summary>
/// Represents a parser of a user agent string
/// </summary>
public sealed class Parser
{
    /// <summary>
    /// The constant string value used to signal an unknown match for a given property or value. This
    /// is by default the string "Other".
    /// </summary>
    public const string Other = "Other";

    private static readonly object DefaultParserLock = new object();
    private static Parser _defaultParser;
    private static Task<Parser> _defaultParserTask;

    private static readonly Browser OtherBrowser = new(Other, null, null, null);

    private static readonly OS OtherOS = new(Other, null, null, null, null);

    private static readonly Device OtherDevice = new(Other, string.Empty, string.Empty);

    private readonly Func<string, Browser>[] _userAgentParsers;

    private readonly Func<string, OS>[] _osParsers;

    private readonly Func<string, Device>[] _deviceParsers;

    // A single prefilter covers the user agent, OS and device patterns (in this order), so that
    // parsing all three only scans the input once.
    private readonly LiteralPrefilter _prefilter;

    private readonly int _osOffset;

    private readonly int _deviceOffset;

    private readonly IMemoryCache _cache;

    // Used when no memory cache is supplied. Completed tasks are stored so that cache hits do not
    // allocate, neither for Parse nor for ParseAsync.
    private readonly ConcurrentDictionary<string, Task<ClientInfo>> _localCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="Parser"/> class.
    /// </summary>
    /// <param name="regexList">The regex list.</param>
    /// <param name="options">The options.</param>
    /// <param name="memoryCache">The memory cache used when parsing with <c>useCache: true</c>. If omitted, a private cache is created for this instance.</param>
    public Parser(Selectors regexList, ParserOptions options, IMemoryCache memoryCache = null)
    {
        var config = new Config(options ?? new ParserOptions());

        this._cache = memoryCache;
        this._localCache = memoryCache is null ? new ConcurrentDictionary<string, Task<ClientInfo>>() : null;

        // The selectors are materialized so that the regular expressions are built once per parser
        // instead of on every parse call.
        this._userAgentParsers = [.. regexList.user_agent_parsers.Select(config.UserAgentSelector)];
        this._osParsers = [.. regexList.os_parsers.Select(config.OSSelector)];
        this._deviceParsers = [.. regexList.device_parsers.Select(config.DeviceSelector)];

        this._osOffset = this._userAgentParsers.Length;
        this._deviceOffset = this._osOffset + this._osParsers.Length;
        this._prefilter = new LiteralPrefilter(
        [
            .. regexList.user_agent_parsers.Select(s => (s.regex, false)),
            .. regexList.os_parsers.Select(s => (s.regex, false)),
            .. regexList.device_parsers.Select(s => (s.regex, s.regex_flag is not null))
        ]);
    }

    /// <summary>
    /// Returns a <see cref="Parser"/> instance based on the embedded regex definitions.
    /// </summary>
    /// <remarks>
    /// Parser construction is relatively expensive because the embedded yaml definitions must be parsed and
    /// the regex structures must be built. Reuse the returned parser instead of creating one per request.
    /// The parameterless overload returns a cached default parser for this reason. The embedded regex definitions
    /// may be outdated, so consider passing in external yaml definitions using <see cref="Parser.FromJson"/>.
    /// </remarks>
    /// <param name="parserOptions">specifies the options for the parser</param>
    /// <param name="memoryCache">the memory cache</param>
    /// <returns></returns>
    public static Parser GetDefault(ParserOptions parserOptions = null, IMemoryCache memoryCache = null)
    {
        if (parserOptions != null)
            return CreateDefaultParser(parserOptions, memoryCache);

        if (_defaultParser != null)
            return _defaultParser;

        lock (DefaultParserLock)
        {
            _defaultParser ??= CreateDefaultParser(null, memoryCache);
        }

        return _defaultParser;
    }

    private static Parser CreateDefaultParser(ParserOptions parserOptions, IMemoryCache memoryCache = null)
    {
        using var stream = typeof(Parser)
            .GetTypeInfo()
            .Assembly.GetManifestResourceStream("UAParser.regexes.json");
        using var reader = new StreamReader(stream);

        var regexList = JsonSerializer.Deserialize<Selectors>(reader.ReadToEnd());

        return new Parser(regexList, parserOptions, memoryCache);
    }

    /// <summary>
    /// Returns a <see cref="Parser"/> instance based on the embedded regex definitions.
    /// <remarks>The embedded regex definitions may be outdated. Consider passing in external json definitions using <see cref="Parser.FromJsonAsync"/></remarks>
    /// </summary>
    /// <param name="parserOptions">specifies the options for the parser</param>
    /// <param name="memoryCache">the memory cache</param>
    /// <returns></returns>
    public static Task<Parser> GetDefaultAsync(ParserOptions parserOptions = null, IMemoryCache memoryCache = null)
    {
        if (parserOptions != null || memoryCache != null)
            return CreateDefaultParserAsync(parserOptions, memoryCache);

        return _defaultParserTask ??= Task.FromResult(GetDefault());
    }

    private static async Task<Parser> CreateDefaultParserAsync(ParserOptions parserOptions, IMemoryCache memoryCache)
    {
        await using var stream = typeof(Parser)
            .GetTypeInfo()
            .Assembly.GetManifestResourceStream("UAParser.regexes.json");

        var regexList = await JsonSerializer.DeserializeAsync<Selectors>(stream);

        return new Parser(regexList, parserOptions, memoryCache);
    }

    /// <summary>
    /// Load Parser with regex definitions from a JSON string
    /// </summary>
    /// <remarks>
    /// Parser construction is relatively expensive because the yaml definitions must be parsed and
    /// the regex structures must be built. Reuse the returned parser instead of creating one per request.
    /// </remarks>
    /// <param name="jsonString">The json string.</param>
    /// <param name="parserOptions">The parser options.</param>
    /// <returns>Parser.</returns>
    public static Parser FromJson(string jsonString, ParserOptions parserOptions = null)
    {
        var regexList = JsonSerializer.Deserialize<Selectors>(jsonString);

        return new Parser(regexList, parserOptions);
    }

    /// <summary>
    /// Load Parser with regex definitions from a JSON string
    /// </summary>
    /// <param name="jsonStream">The json stream.</param>
    /// <param name="parserOptions">The parser options.</param>
    /// <returns>Parser.</returns>
    public static async Task<Parser> FromJsonAsync(Stream jsonStream, ParserOptions parserOptions = null)
    {
        var regexList = await JsonSerializer.DeserializeAsync<Selectors>(jsonStream);

        return new Parser(regexList, parserOptions);
    }

    /// <summary>
    ///  Parse a user agent string and obtain all client information
    /// </summary>
    /// <param name="userAgent">The user agent string.</param>
    /// <param name="useCache">if set to <c>true</c> [use cache].</param>
    /// <returns>ClientInfo.</returns>
    public ClientInfo Parse(string userAgent, bool useCache = false)
    {
        if (!useCache)
            return this.ParseUncached(userAgent);

        if (this._localCache is not null)
            return this.GetOrAddLocal(userAgent).Result;

        if (this._cache.TryGetValue(userAgent, out ClientInfo cached))
            return cached;

        return this._cache.Set(userAgent, this.ParseUncached(userAgent));
    }

    /// <summary>
    /// Parse a user agent string and obtain all client information
    /// </summary>
    /// <param name="userAgent">The user agent string.</param>
    /// <param name="useCache">if set to <c>true</c> [use cache].</param>
    /// <returns>ClientInfo.</returns>
    public Task<ClientInfo> ParseAsync(string userAgent, bool useCache = false)
    {
        try
        {
            // parsing is CPU bound, so the result is always available synchronously
            return useCache && this._localCache is not null
                       ? this.GetOrAddLocal(userAgent)
                       : Task.FromResult(this.Parse(userAgent, useCache));
        }
        catch (Exception ex)
        {
            return Task.FromException<ClientInfo>(ex);
        }
    }

    /// <summary>
    /// Parse a user agent string and obtain the OS information
    /// </summary>
    /// <param name="userAgent">The user agent string.</param>
    // ReSharper disable once InconsistentNaming
    public OS ParseOS(string userAgent)
    {
        var length = this._prefilter.BufferLength;
        var scan = this._prefilter.Scan(
            userAgent,
            length <= LiteralPrefilter.MaxStackBufferLength ? stackalloc ulong[length] : new ulong[length]);

        return this._prefilter.FirstMatch(scan, this._osOffset, this._osParsers) ?? OtherOS;
    }

    /// <summary>
    /// Parse a user agent string and obtain the device information
    /// </summary>
    /// <param name="userAgent">The user agent string.</param>
    public Device ParseDevice(string userAgent)
    {
        var length = this._prefilter.BufferLength;
        var scan = this._prefilter.Scan(
            userAgent,
            length <= LiteralPrefilter.MaxStackBufferLength ? stackalloc ulong[length] : new ulong[length]);

        return this._prefilter.FirstMatch(scan, this._deviceOffset, this._deviceParsers) ?? OtherDevice;
    }

    /// <summary>
    /// Parse a user agent string and obtain the UserAgent information
    /// </summary>
    /// <param name="userAgent">The user agent string.</param>
    public Browser ParseUserAgent(string userAgent)
    {
        var length = this._prefilter.BufferLength;
        var scan = this._prefilter.Scan(
            userAgent,
            length <= LiteralPrefilter.MaxStackBufferLength ? stackalloc ulong[length] : new ulong[length]);

        return this._prefilter.FirstMatch(scan, 0, this._userAgentParsers) ?? OtherBrowser;
    }

    private ClientInfo ParseUncached(string userAgent)
    {
        var length = this._prefilter.BufferLength;
        var scan = this._prefilter.Scan(
            userAgent,
            length <= LiteralPrefilter.MaxStackBufferLength ? stackalloc ulong[length] : new ulong[length]);

        var os = this._prefilter.FirstMatch(scan, this._osOffset, this._osParsers) ?? OtherOS;
        var device = this._prefilter.FirstMatch(scan, this._deviceOffset, this._deviceParsers) ?? OtherDevice;
        var ua = this._prefilter.FirstMatch(scan, 0, this._userAgentParsers) ?? OtherBrowser;

        return new ClientInfo(userAgent, os, device, ua);
    }

    private Task<ClientInfo> GetOrAddLocal(string userAgent)
    {
        ArgumentNullException.ThrowIfNull(userAgent);

        return this._localCache.TryGetValue(userAgent, out var cached)
                   ? cached
                   : this._localCache.GetOrAdd(
                       userAgent,
                       static (ua, parser) => Task.FromResult(parser.ParseUncached(ua)),
                       this);
    }
}
