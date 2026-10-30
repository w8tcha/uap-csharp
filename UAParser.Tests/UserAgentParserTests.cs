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
using System.Linq;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

using UAParser.Extensions;
using UAParser.Interfaces;

#pragma warning disable IDE1006 // Naming Styles
public class UserAgentParserTests
{
    private const string FirefoxOnLinux =
        "Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:121.0) Gecko/20100101 Firefox/121.0";

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

    private static UserAgentParser CreateParser(string userAgent, IMemoryCache cache = null)
    {
        var accessor = new HttpContextAccessor { HttpContext = CreateHttpContext(userAgent) };

        return new UserAgentParser(accessor, cache ?? new MemoryCache(new MemoryCacheOptions()));
    }

    private static DefaultHttpContext CreateHttpContext(string userAgent)
    {
        var context = new DefaultHttpContext();

        if (userAgent is not null)
        {
            context.Request.Headers.UserAgent = userAgent;
        }

        return context;
    }
}

#pragma warning restore IDE1006 // Naming Styles
