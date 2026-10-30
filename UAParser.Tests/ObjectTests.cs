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

using UAParser.Extensions;

#pragma warning disable IDE1006 // Naming Styles
public class ObjectTests
{
    [Test]
    public void browser_formats_version_and_to_string()
    {
        var browser = new Browser("Firefox", "120", "0", "1");

        browser.Version.Should().Be("120.0.1");
        browser.ToString().Should().Be("Firefox 120.0.1");
    }

    [Test]
    public void browser_to_string_omits_missing_version()
    {
        var browser = new Browser("Other", null, null, null);

        browser.Version.Should().BeEmpty();
        browser.ToString().Should().Be("Other");
    }

    [Test]
    public void browser_version_skips_empty_parts()
    {
        var browser = new Browser("Chrome", "100", null, "5");

        browser.Version.Should().Be("100.5");
    }

    [Test]
    public void os_to_string_includes_all_version_parts()
    {
        var os = new OS("Windows", "10", "0", "19045", "1");

        os.ToString().Should().Be("Windows 10.0.19045.1");
        os.PatchMinor.Should().Be("1");
    }

    [Test]
    public void os_to_string_omits_missing_version()
    {
        var os = new OS("Other", null, null, null, null);

        os.ToString().Should().Be("Other");
    }

    [Test]
    public void device_trims_values()
    {
        var device = new Device("  iPhone ", " Apple ", " iPhone ");

        device.Family.Should().Be("iPhone");
        device.Brand.Should().Be("Apple");
        device.Model.Should().Be("iPhone");
        device.ToString().Should().Be("iPhone");
    }

    [Test]
    public void device_allows_null_brand_and_model()
    {
        var device = new Device("Generic", null, null);

        device.Brand.Should().BeNull();
        device.Model.Should().BeNull();
    }

    [TestCase("Spider", true)]
    [TestCase("spider", true)]
    [TestCase("iPhone", false)]
    public void device_detects_spiders(string family, bool expected)
    {
        new Device(family, null, null).IsSpider.Should().Be(expected);
    }

    [Test]
    public void client_info_exposes_parts_and_to_string()
    {
        var os = new OS("Mac OS X", "14", "1", null, null);
        var device = new Device("Mac", "Apple", "Mac");
        var browser = new Browser("Safari", "17", "1", null);

        var clientInfo = new ClientInfo("ua", os, device, browser);

        clientInfo.String.Should().Be("ua");
        clientInfo.OS.Should().BeSameAs(os);
        clientInfo.Device.Should().BeSameAs(device);
        clientInfo.Browser.Should().BeSameAs(browser);
        clientInfo.ToString().Should().Be("Mac OS X 14.1 Mac Safari 17.1");
    }

    [Test]
    public void parser_options_have_expected_defaults()
    {
        var options = new ParserOptions();

        options.UseCompiledRegex.Should().BeFalse();
        options.MatchTimeOut.Should().Be(System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
    }

    [TestCase("a $1 b $1", "$1", "x", "a x b $1")]
    [TestCase("no token", "$1", "x", "no token")]
    [TestCase("$1", "$1", "", "")]
    public void replace_first_occurrence_replaces_only_first_match(
        string input,
        string search,
        string replacement,
        string expected)
    {
        input.ReplaceFirstOccurrence(search, replacement).Should().Be(expected);
    }

    [Test]
    public void replace_first_occurrence_throws_on_null_input()
    {
        var act = () => ((string)null).ReplaceFirstOccurrence("$1", "x");

        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void version_string_joins_non_empty_parts()
    {
        VersionString.Format("1", "", null, "4").Should().Be("1.4");
        VersionString.Format().Should().BeEmpty();
    }
}

#pragma warning restore IDE1006 // Naming Styles
