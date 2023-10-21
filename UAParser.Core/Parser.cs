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

    private readonly Func<string, OS> osParser;

    private readonly Func<string, Device> deviceParser;

    private readonly Func<string, Browser> userAgentParser;

    private static IMemoryCache cache;

    public Parser(Selectors regexList, ParserOptions options)
    {
        var config = new Config(options ?? new ParserOptions());

        this.userAgentParser = CreateParser(
            regexList.user_agent_parsers.Select(config.UserAgentSelector),
            new Browser(Other, null, null, null));
        this.osParser = CreateParser(
            regexList.os_parsers.Select(config.OSSelector),
            new OS(Other, null, null, null, null));
        this.deviceParser = CreateParser(
            regexList.device_parsers.Select(config.DeviceSelector),
            new Device(Other, string.Empty, string.Empty));
    }

    /// <summary>
    /// Returns a <see cref="Parser"/> instance based on the embedded regex definitions.
    /// <remarks>The embedded regex definitions may be outdated. Consider passing in external json definitions using <see cref="Parser.FromJson"/></remarks>
    /// </summary>
    /// <param name="parserOptions">specifies the options for the parser</param>
    /// <param name="memoryCache">the memory cache</param>
    /// <returns></returns>
    public static Parser GetDefault(ParserOptions parserOptions = null, IMemoryCache memoryCache = null)
    {
        using var stream = typeof(Parser)
            .GetTypeInfo()
            .Assembly.GetManifestResourceStream("UAParser.regexes.json");
        using var reader = new StreamReader(stream);

        var regexList = JsonSerializer.Deserialize<Selectors>(reader.ReadToEnd());

        cache = memoryCache ?? new MemoryCache(new MemoryCacheOptions());

        return new Parser(regexList, parserOptions);
    }

    /// <summary>
    /// Returns a <see cref="Parser"/> instance based on the embedded regex definitions.
    /// <remarks>The embedded regex definitions may be outdated. Consider passing in external json definitions using <see cref="Parser.FromJsonAsync"/></remarks>
    /// </summary>
    /// <param name="parserOptions">specifies the options for the parser</param>
    /// <param name="memoryCache">the memory cache</param>
    /// <returns></returns>
    public static async Task<Parser> GetDefaultAsync(ParserOptions parserOptions = null, IMemoryCache memoryCache = null)
    {
        await using var stream = typeof(Parser)
            .GetTypeInfo()
            .Assembly.GetManifestResourceStream("UAParser.regexes.json");
        using var reader = new StreamReader(stream);

        var regexList = await JsonSerializer.DeserializeAsync<Selectors>(stream);

        cache = memoryCache ?? new MemoryCache(new MemoryCacheOptions());

        return new Parser(regexList, parserOptions);
    }

    /// <summary>
    /// Load Parser with regex definitions from a JSON string
    /// </summary>
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
        if (useCache)
        {
            return cache.GetOrCreate(
                userAgent,
                _ =>
                    {
                        var os = this.ParseOS(userAgent);
                        var device = this.ParseDevice(userAgent);
                        var ua = this.ParseUserAgent(userAgent);

                        return new ClientInfo(userAgent, os, device, ua);
                    });
        }

        var os = this.ParseOS(userAgent);
        var device = this.ParseDevice(userAgent);
        var ua = this.ParseUserAgent(userAgent);

        return new ClientInfo(userAgent, os, device, ua);
    }

    /// <summary>
    /// Parse a user agent string and obtain all client information
    /// </summary>
    /// <param name="userAgent">The user agent string.</param>
    /// <param name="useCache">if set to <c>true</c> [use cache].</param>
    /// <returns>ClientInfo.</returns>
    public async Task<ClientInfo> ParseAsync(string userAgent, bool useCache = false)
    {
        if (useCache)
        {
            return await cache.GetOrCreateAsync(
                userAgent,
                _ =>
                    {
                        var os = this.ParseOS(userAgent);
                        var device = this.ParseDevice(userAgent);
                        var ua = this.ParseUserAgent(userAgent);

                        return Task.FromResult(new ClientInfo(userAgent, os, device, ua));
                    });
        }

        var os = this.ParseOS(userAgent);
        var device = this.ParseDevice(userAgent);
        var ua = this.ParseUserAgent(userAgent);

        return new ClientInfo(userAgent, os, device, ua);
    }

    /// <summary>
    /// Parse a user agent string and obtain the OS information
    /// </summary>
    /// <param name="userAgent">The user agent string.</param>
    // ReSharper disable once InconsistentNaming
    public OS ParseOS(string userAgent)
    {
        return this.osParser(userAgent);
    }

    /// <summary>
    /// Parse a user agent string and obtain the device information
    /// </summary>
    /// <param name="userAgent">The user agent string.</param>
    public Device ParseDevice(string userAgent)
    {
        return this.deviceParser(userAgent);
    }

    /// <summary>
    /// Parse a user agent string and obtain the UserAgent information
    /// </summary>
    /// <param name="userAgent">The user agent string.</param>
    public Browser ParseUserAgent(string userAgent)
    {
        return this.userAgentParser(userAgent);
    }

    /// <summary>
    /// Creates the parser.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="parsers">The parsers.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <returns>Func&lt;System.String, T&gt;.</returns>
    private static Func<string, T> CreateParser<T>(
        IEnumerable<Func<string, T>> parsers,
        T defaultValue)
        where T : class
    {
        return CreateParser(parsers, defaultValue, t => t);
    }

    private static Func<string, TResult> CreateParser<T, TResult>(
        IEnumerable<Func<string, T>> parsers,
        T defaultValue,
        Func<T, TResult> selector)
        where T : class
    {
        return ua =>
            selector(parsers.Select(p => p(ua)).FirstOrDefault(m => m is not null) ?? defaultValue);
    }
}
