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

namespace UAParser.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

using UAParser.Extensions;
using UAParser.Interfaces;

#pragma warning disable IDE1006 // Naming Styles
public class UserAgentParserTests
{
    private const string FirefoxOnLinux =
        "Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:121.0) Gecko/20100101 Firefox/121.0";

    private const string ChromeOnWindows =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";

    [Test]
    public void client_info_is_parsed_from_user_agent_header()
    {
        var parser = CreateParser(FirefoxOnLinux);

        var clientInfo = parser.ClientInfo;

        clientInfo.Should().NotBeNull();
        clientInfo.String.Should().Be(FirefoxOnLinux);
        clientInfo.Browser.Family.Should().Be("Firefox");
        clientInfo.Browser.Major.Should().Be("121");
        clientInfo.OS.Family.Should().Be("Ubuntu");
    }

    [Test]
    public void client_info_is_evaluated_once_per_instance()
    {
        var parser = CreateParser(FirefoxOnLinux);

        parser.ClientInfo.Should().BeSameAs(parser.ClientInfo);
    }

    [Test]
    public void client_info_is_cached_in_memory_cache()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var first = CreateParser(FirefoxOnLinux, cache).ClientInfo;
        var second = CreateParser(FirefoxOnLinux, cache).ClientInfo;

        second.Should().BeSameAs(first);
    }

    [Test]
    public void client_info_is_null_without_user_agent_header()
    {
        var parser = CreateParser(null);

        parser.ClientInfo.Should().BeNull();
    }

    [Test]
    public void client_info_is_null_without_http_context()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var parser = new UserAgentParser(new HttpContextAccessor(), cache);

        parser.ClientInfo.Should().BeNull();
    }

    [Test]
    public void add_user_agent_parser_registers_services()
    {
        var services = new ServiceCollection();

        services.AddUserAgentParser().Should().BeSameAs(services);

        services.Should().ContainSingle(
            d => d.ServiceType == typeof(IUserAgentParser) && d.ImplementationType == typeof(UserAgentParser)
                                                          && d.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(
            d => d.ServiceType == typeof(IHttpContextAccessor) && d.Lifetime == ServiceLifetime.Singleton);
    }

    [Test]
    public void add_user_agent_parser_does_not_duplicate_http_context_accessor()
    {
        var services = new ServiceCollection();
        services.AddHttpContextAccessor();

        services.AddUserAgentParser();

        services.Count(d => d.ServiceType == typeof(IHttpContextAccessor)).Should().Be(1);
    }

    [Test]
    public void add_user_agent_parser_throws_on_null_services()
    {
        var act = () => ((IServiceCollection)null).AddUserAgentParser();

        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Test]
    public void user_agent_parser_can_be_resolved_from_container()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddUserAgentParser();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = CreateHttpContext(FirefoxOnLinux);

        using var scope = provider.CreateScope();
        var parser = scope.ServiceProvider.GetRequiredService<IUserAgentParser>();

        parser.Should().BeOfType<UserAgentParser>();
        parser.ClientInfo.Browser.Family.Should().Be("Firefox");
    }

    [TestCase("\"15.0.0\"")]
    [TestCase("\"13.0.0\"")]
    [TestCase("19.0.0")]
    public void windows_11_is_detected_from_platform_version_hint(string platformVersion)
    {
        var parser = CreateParser(ChromeOnWindows, hints: ("\"Windows\"", platformVersion));

        var os = parser.ClientInfo.OS;

        os.Family.Should().Be("Windows");
        os.Major.Should().Be("11");
        os.Minor.Should().BeNull();
        os.ToString().Should().Be("Windows 11");
        parser.ClientInfo.Browser.Family.Should().Be("Chrome");
        parser.ClientInfo.String.Should().Be(ChromeOnWindows);
    }

    [TestCase("\"10.0.0\"")]
    [TestCase("\"1.0.0\"")]
    [TestCase("\"0.3.0\"")]
    [TestCase("\"\"")]
    [TestCase("\"abc\"")]
    [TestCase("\"-15.0.0\"")]
    [TestCase(null)]
    public void windows_10_is_kept_without_windows_11_platform_version_hint(string platformVersion)
    {
        var parser = CreateParser(ChromeOnWindows, hints: ("\"Windows\"", platformVersion));

        parser.ClientInfo.OS.Major.Should().Be("10");
    }

    [Test]
    public void windows_10_is_kept_without_client_hints()
    {
        CreateParser(ChromeOnWindows).ClientInfo.OS.Major.Should().Be("10");
    }

    [Test]
    public void platform_version_hint_is_ignored_for_other_platforms()
    {
        var parser = CreateParser(ChromeOnWindows, hints: ("\"macOS\"", "\"15.0.0\""));

        parser.ClientInfo.OS.Major.Should().Be("10");
    }

    [Test]
    public void windows_hint_does_not_change_non_windows_10_user_agents()
    {
        var parser = CreateParser(FirefoxOnLinux, hints: ("\"Windows\"", "\"15.0.0\""));

        parser.ClientInfo.OS.Family.Should().Be("Ubuntu");
    }

    [Test]
    public void cached_result_is_not_affected_by_client_hints()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var windows11 = CreateParser(ChromeOnWindows, cache, ("\"Windows\"", "\"15.0.0\"")).ClientInfo;
        var windows10 = CreateParser(ChromeOnWindows, cache).ClientInfo;

        windows11.OS.Major.Should().Be("11");
        windows10.OS.Major.Should().Be("10");
    }

    [TestCase(true, "Sec-CH-UA-Platform-Version")]
    [TestCase(false, null)]
    public void client_hint_headers_are_requested(bool critical, string expectedCriticalCh)
    {
        var headers = new HeaderDictionary();

        UserAgentClientHintsApplicationBuilderExtensions.AddClientHintHeaders(headers, critical);

        headers["Accept-CH"].ToString().Should().Be("Sec-CH-UA-Platform-Version");
        headers["Critical-CH"].ToString().Should().Be(expectedCriticalCh ?? string.Empty);
    }

    [Test]
    public void client_hint_headers_are_appended_to_existing_accept_ch()
    {
        var headers = new HeaderDictionary { ["Accept-CH"] = "Sec-CH-UA-Model" };

        UserAgentClientHintsApplicationBuilderExtensions.AddClientHintHeaders(headers, critical: false);

        headers["Accept-CH"].ToString().Should().Be("Sec-CH-UA-Model,Sec-CH-UA-Platform-Version");
    }

    [Test]
    public async Task use_user_agent_client_hints_adds_headers_to_response()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        app.UseUserAgentClientHints();
        var pipeline = app.Build();

        var context = new DefaultHttpContext();
        var responseFeature = new StartableResponseFeature();
        context.Features.Set<IHttpResponseFeature>(responseFeature);

        await pipeline(context);
        await responseFeature.StartAsync();

        context.Response.Headers["Accept-CH"].ToString().Should().Be("Sec-CH-UA-Platform-Version");
        context.Response.Headers["Critical-CH"].ToString().Should().Be("Sec-CH-UA-Platform-Version");
    }

    [Test]
    public void use_user_agent_client_hints_throws_on_null_app()
    {
        var act = () => ((IApplicationBuilder)null).UseUserAgentClientHints();

        act.Should().Throw<ArgumentNullException>().WithParameterName("app");
    }

    private static UserAgentParser CreateParser(
        string userAgent,
        IMemoryCache cache = null,
        (string Platform, string PlatformVersion)? hints = null)
    {
        var accessor = new HttpContextAccessor { HttpContext = CreateHttpContext(userAgent, hints) };

        return new UserAgentParser(accessor, cache ?? new MemoryCache(new MemoryCacheOptions()));
    }

    private static DefaultHttpContext CreateHttpContext(
        string userAgent,
        (string Platform, string PlatformVersion)? hints = null)
    {
        var context = new DefaultHttpContext();

        if (userAgent is not null)
        {
            context.Request.Headers.UserAgent = userAgent;
        }

        if (hints is { } h)
        {
            context.Request.Headers[ClientHints.PlatformHeader] = h.Platform;

            if (h.PlatformVersion is not null)
            {
                context.Request.Headers[ClientHints.PlatformVersionHeader] = h.PlatformVersion;
            }
        }

        return context;
    }

    /// <summary>
    /// The default response feature never fires OnStarting callbacks; this one does on demand.
    /// </summary>
    private sealed class StartableResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onStarting = [];

        public override void OnStarting(Func<object, Task> callback, object state) =>
            this._onStarting.Add((callback, state));

        public async Task StartAsync()
        {
            foreach (var (callback, state) in this._onStarting)
            {
                await callback(state);
            }
        }
    }
}

#pragma warning restore IDE1006 // Naming Styles
