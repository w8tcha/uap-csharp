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
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Caching.Memory;

#pragma warning disable IDE1006 // Naming Styles
public class ParserBehaviorTests
{
    private const string ChromeOnWindows =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.6099.109 Safari/537.36";

    private const string SafariOnIPhone =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1 Mobile/15E148 Safari/604.1";

    private const string Googlebot =
        "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";

    [Test]
    public void default_parser_parses_desktop_browser()
    {
        var clientInfo = Parser.GetDefault().Parse(ChromeOnWindows);

        clientInfo.String.Should().Be(ChromeOnWindows);
        clientInfo.Browser.Family.Should().Be("Chrome");
        clientInfo.Browser.Major.Should().Be("120");
        clientInfo.OS.Family.Should().Be("Windows");
        clientInfo.OS.Major.Should().Be("10");
        clientInfo.Device.Family.Should().Be(Parser.Other);
        clientInfo.Device.IsSpider.Should().BeFalse();
    }

    [Test]
    public void default_parser_parses_mobile_device()
    {
        var clientInfo = Parser.GetDefault().Parse(SafariOnIPhone);

        clientInfo.Browser.Family.Should().Be("Mobile Safari");
        clientInfo.OS.Family.Should().Be("iOS");
        clientInfo.OS.Major.Should().Be("17");
        clientInfo.OS.Minor.Should().Be("1");
        clientInfo.Device.Family.Should().Be("iPhone");
        clientInfo.Device.Brand.Should().Be("Apple");
        clientInfo.Device.Model.Should().Be("iPhone");
    }

    [Test]
    public void default_parser_detects_spiders()
    {
        var clientInfo = Parser.GetDefault().Parse(Googlebot);

        clientInfo.Device.IsSpider.Should().BeTrue();
        clientInfo.Browser.Family.Should().Be("Googlebot");
    }

    [Test]
    public void unknown_user_agent_falls_back_to_other()
    {
        var clientInfo = Parser.GetDefault().Parse("definitely-not-a-user-agent");

        clientInfo.Browser.Family.Should().Be(Parser.Other);
        clientInfo.Browser.Major.Should().BeNull();
        clientInfo.OS.Family.Should().Be(Parser.Other);
        clientInfo.Device.Family.Should().Be(Parser.Other);
        clientInfo.Device.Brand.Should().BeEmpty();
        clientInfo.Device.Model.Should().BeEmpty();
    }

    [Test]
    public void parse_with_cache_returns_cached_instance()
    {
        var parser = new Parser(SimpleSelectors(), null);

        var first = parser.Parse("MyBrowser/1.2.3", useCache: true);
        var second = parser.Parse("MyBrowser/1.2.3", useCache: true);
        var uncached = parser.Parse("MyBrowser/1.2.3");

        second.Should().BeSameAs(first);
        uncached.Should().NotBeSameAs(first);
        uncached.Browser.ToString().Should().Be(first.Browser.ToString());
    }

    [Test]
    public void parse_with_cache_uses_supplied_memory_cache()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var parser = new Parser(SimpleSelectors(), null, cache);

        var clientInfo = parser.Parse("MyBrowser/1.2.3", useCache: true);

        cache.TryGetValue("MyBrowser/1.2.3", out ClientInfo cached).Should().BeTrue();
        cached.Should().BeSameAs(clientInfo);
    }

    [Test]
    public async Task parse_async_without_cache_returns_parsed_info()
    {
        var parser = new Parser(SimpleSelectors(), null);

        var clientInfo = await parser.ParseAsync("MyBrowser/1.2.3");

        clientInfo.Browser.ToString().Should().Be("MyBrowser 1.2.3");
        clientInfo.OS.Family.Should().Be(Parser.Other);
    }

    [Test]
    public async Task parse_async_with_cache_returns_cached_instance()
    {
        var parser = new Parser(SimpleSelectors(), null);

        var first = await parser.ParseAsync("MyBrowser/1.2.3", useCache: true);
        var second = await parser.ParseAsync("MyBrowser/1.2.3", useCache: true);

        second.Should().BeSameAs(first);
        parser.Parse("MyBrowser/1.2.3", useCache: true).Should().BeSameAs(first);
    }

    [Test]
    public async Task get_default_async_returns_working_parser()
    {
        var parser = await Parser.GetDefaultAsync();

        parser.Should().NotBeSameAs(Parser.GetDefault());
        parser.Parse(ChromeOnWindows).Browser.Family.Should().Be("Chrome");
    }

    [Test]
    public void from_json_creates_parser()
    {
        var parser = Parser.FromJson(JsonSerializer.Serialize(SimpleSelectors()));

        parser.ParseUserAgent("MyBrowser/4.5.6").ToString().Should().Be("MyBrowser 4.5.6");
    }

    [Test]
    public async Task from_json_async_creates_parser()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(SimpleSelectors())));

        var parser = await Parser.FromJsonAsync(stream, new ParserOptions { UseCompiledRegex = true });

        parser.ParseUserAgent("MyBrowser/4.5.6").ToString().Should().Be("MyBrowser 4.5.6");
    }

    [Test]
    public void compiled_regex_parser_produces_same_results()
    {
        var compiled = Parser.GetDefault(new ParserOptions { UseCompiledRegex = true });

        compiled.Parse(SafariOnIPhone).ToString().Should().Be(Parser.GetDefault().Parse(SafariOnIPhone).ToString());
    }

    [Test]
    public void selectors_are_built_once_at_construction()
    {
        // a missing regex is reported while constructing the parser, which proves that the
        // regular expressions are not lazily rebuilt on every parse call
        var selectors = SimpleSelectors();
        selectors.os_parsers.Add(new OSSelector());

        var act = () => new Parser(selectors, null);

        act.Should().Throw<ArgumentNullException>().WithParameterName("OS");
    }

    [Test]
    public void missing_user_agent_regex_throws()
    {
        var selectors = SimpleSelectors();
        selectors.user_agent_parsers.Add(new UserAgentSelector());

        var act = () => new Parser(selectors, null);

        act.Should().Throw<ArgumentNullException>().WithParameterName("User agent");
    }

    [Test]
    public void missing_device_regex_throws()
    {
        var selectors = SimpleSelectors();
        selectors.device_parsers.Add(new DeviceSelector());

        var act = () => new Parser(selectors, null);

        act.Should().Throw<ArgumentNullException>().WithParameterName("Device");
    }

    [Test]
    public void escaped_underscores_in_regex_are_supported()
    {
        var selectors = EmptySelectors();
        selectors.user_agent_parsers.Add(new UserAgentSelector { regex = @"(My\_App)/(\d+)" });

        var parser = new Parser(selectors, null);

        parser.ParseUserAgent("My_App/7").ToString().Should().Be("My_App 7");
    }

    [Test]
    public void first_matching_selector_wins()
    {
        var selectors = EmptySelectors();
        selectors.user_agent_parsers.Add(new UserAgentSelector { regex = "(First)", family_replacement = "One" });
        selectors.user_agent_parsers.Add(new UserAgentSelector { regex = "(First)", family_replacement = "Two" });

        var parser = new Parser(selectors, null);

        parser.ParseUserAgent("First").Family.Should().Be("One");
    }

    [Test]
    public void user_agent_replacements_are_applied()
    {
        var selectors = EmptySelectors();
        selectors.user_agent_parsers.Add(
            new UserAgentSelector
                {
                    regex = @"(Brw)/(\d+)\.(\d+)",
                    family_replacement = "Custom $1",
                    v1_replacement = "v$2",
                    v2_replacement = "9",
                    v3_replacement = "$4"
                });

        var browser = new Parser(selectors, null).ParseUserAgent("Brw/3.4");

        browser.Family.Should().Be("Custom Brw");
        browser.Major.Should().Be("v3");
        browser.Minor.Should().Be("9");

        // $4 references a group which does not exist, so the replacement is kept as is
        browser.Patch.Should().Be("$4");
    }

    [Test]
    public void os_without_replacements_uses_groups_in_order()
    {
        var os = ParseOS(new OSSelector { regex = @"(MyOS) (\d+)\.(\d+)\.(\d+)\.(\d+)" }, "MyOS 1.2.3.4");

        os.ToString().Should().Be("MyOS 1.2.3.4");
    }

    [Test]
    public void os_family_replacement_uses_first_group()
    {
        var os = ParseOS(
            new OSSelector { regex = @"(Foo) (\d+)", os_replacement = "Bar $1" },
            "Foo 5");

        os.Family.Should().Be("Bar Foo");
        os.Major.Should().Be("5");
    }

    [Test]
    public void os_version_replacements_referencing_first_and_second_group()
    {
        var os = ParseOS(
            new OSSelector
                {
                    regex = @"Win (\d+)\.(\d+)",
                    os_replacement = "Windows",
                    os_v1_replacement = "$1",
                    os_v2_replacement = "$2"
                },
            "Win 6.1");

        os.Family.Should().Be("Windows");
        os.Major.Should().Be("6");
        os.Minor.Should().Be("1");
    }

    [Test]
    public void os_v1_replacement_referencing_first_group()
    {
        var os = ParseOS(
            new OSSelector { regex = @"Ver (\d+)", os_replacement = "MyOS", os_v1_replacement = "$1" },
            "Ver 12");

        os.Family.Should().Be("MyOS");
        os.Major.Should().Be("12");
        os.Minor.Should().BeNull();
    }

    [Test]
    public void os_v2_replacement_referencing_first_group()
    {
        var os = ParseOS(
            new OSSelector
                {
                    regex = @"X (\d+)",
                    os_replacement = "XOS",
                    os_v1_replacement = "10",
                    os_v2_replacement = "$1"
                },
            "X 3");

        os.Family.Should().Be("XOS");
        os.Major.Should().Be("10");
        os.Minor.Should().Be("3");
    }

    [Test]
    public void os_selector_not_matching_returns_other()
    {
        var os = ParseOS(new OSSelector { regex = "NeverMatches" }, "Something");

        os.Family.Should().Be(Parser.Other);
    }

    [Test]
    public void device_replacements_substitute_all_tokens()
    {
        var device = ParseDevice(
            new DeviceSelector
                {
                    regex = @"(Pixel) (\d+)",
                    device_replacement = "$1 $2",
                    brand_replacement = "Google",
                    model_replacement = "$1$2"
                },
            "Pixel 7");

        device.Family.Should().Be("Pixel 7");
        device.Brand.Should().Be("Google");
        device.Model.Should().Be("Pixel7");
    }

    [Test]
    public void device_replacement_with_missing_group_is_emptied()
    {
        var device = ParseDevice(
            new DeviceSelector { regex = @"(Pixel)( Pro)?", device_replacement = "$1$2 $9" },
            "Pixel");

        device.Family.Should().Be("Pixel");
    }

    [Test]
    public void device_without_replacements_uses_first_group()
    {
        var device = ParseDevice(new DeviceSelector { regex = @"(Nexus \d+)" }, "Android; Nexus 5 Build");

        device.Family.Should().Be("Nexus 5");
        device.Brand.Should().BeNull();
        device.Model.Should().BeNull();
    }

    [Test]
    public void device_regex_flag_enables_case_insensitive_matching()
    {
        var selector = new DeviceSelector { regex = "(kindle)", device_replacement = "Kindle", regex_flag = "i" };

        ParseDevice(selector, "KINDLE").Family.Should().Be("Kindle");

        selector.regex_flag = null;
        ParseDevice(selector, "KINDLE").Family.Should().Be(Parser.Other);
    }

    private static OS ParseOS(OSSelector selector, string userAgent)
    {
        var selectors = EmptySelectors();
        selectors.os_parsers.Add(selector);
        return new Parser(selectors, null).ParseOS(userAgent);
    }

    private static Device ParseDevice(DeviceSelector selector, string userAgent)
    {
        var selectors = EmptySelectors();
        selectors.device_parsers.Add(selector);
        return new Parser(selectors, null).ParseDevice(userAgent);
    }

    private static Selectors EmptySelectors()
    {
        return new Selectors
                   {
                       user_agent_parsers = new List<UserAgentSelector>(),
                       os_parsers = new List<OSSelector>(),
                       device_parsers = new List<DeviceSelector>()
                   };
    }

    private static Selectors SimpleSelectors()
    {
        var selectors = EmptySelectors();
        selectors.user_agent_parsers.Add(new UserAgentSelector { regex = @"(MyBrowser)/(\d+)\.(\d+)\.(\d+)" });
        return selectors;
    }
}

#pragma warning restore IDE1006 // Naming Styles
