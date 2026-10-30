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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

#pragma warning disable IDE1006 // Naming Styles
public class PrefilterTests
{
    [Test]
    public void prefiltered_parser_matches_evaluating_every_regex()
    {
        var selectors = LoadEmbeddedSelectors();
        var parser = new Parser(selectors, null);

        var config = new Config(new ParserOptions());
        var userAgentParsers = selectors.user_agent_parsers.Select(config.UserAgentSelector).ToArray();
        var osParsers = selectors.os_parsers.Select(config.OSSelector).ToArray();
        var deviceParsers = selectors.device_parsers.Select(config.DeviceSelector).ToArray();

        var userAgents = new TestResourceTests()
            .GetTestCases("UAParser.Tests.TestResources.test_ua.yaml", "test_cases", m => m["user_agent_string"])
            .Concat(new TestResourceTests().GetTestCases("UAParser.Tests.TestResources.test_os.yaml", "test_cases", m => m["user_agent_string"]))
            .Concat(new TestResourceTests().GetTestCases("UAParser.Tests.TestResources.test_device.yaml", "test_cases", m => m["user_agent_string"]))
            .Where(ua => ua is not null)
            .Distinct()
            .ToList();

        // case variants check that case-sensitive and case-insensitive patterns are prefiltered correctly
        var inputs = userAgents
            .Concat(userAgents.Where((_, i) => i % 10 == 0).Select(ua => ua.ToUpperInvariant()))
            .Concat(userAgents.Where((_, i) => i % 10 == 5).Select(ua => ua.ToLowerInvariant()))
            .Concat(EdgeCases)
            .ToList();

        var failures = new ConcurrentBag<string>();

        Parallel.ForEach(
            inputs,
            ua =>
                {
                    var browser = FirstMatch(userAgentParsers, ua)?.ToString() ?? Parser.Other;
                    var os = FirstMatch(osParsers, ua)?.ToString() ?? Parser.Other;
                    var device = FirstMatch(deviceParsers, ua);

                    if (parser.ParseUserAgent(ua).ToString() != browser)
                        failures.Add($"user agent of {ua}");

                    if (parser.ParseOS(ua).ToString() != os)
                        failures.Add($"os of {ua}");

                    var parsedDevice = parser.ParseDevice(ua);
                    if (parsedDevice.Family != (device?.Family ?? Parser.Other)
                        || parsedDevice.Brand != (device is null ? string.Empty : device.Brand)
                        || parsedDevice.Model != (device is null ? string.Empty : device.Model))
                    {
                        failures.Add($"device of {ua}");
                    }
                });

        inputs.Count.Should().BeGreaterThan(15000);
        failures.Should().BeEmpty();
    }

    [Test]
    public void ignore_case_patterns_are_not_skipped_for_non_ascii_input()
    {
        // the Kelvin sign is a case variant of 'k' for case-insensitive matching
        const string input = "Kindle";
        var regex = new Regex("(kindle)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var prefilter = new LiteralPrefilter([("(kindle)", true)]);
        var scan = prefilter.Scan(input, new ulong[prefilter.BufferLength]);
        var result = prefilter.FirstMatch(scan, 0, [ua => regex.IsMatch(ua) ? "match" : null]);

        result.Should().Be(regex.IsMatch(input) ? "match" : null);
    }

    [Test]
    public void prefilter_skips_patterns_whose_literals_are_absent()
    {
        var evaluated = new List<int>();
        var prefilter = new LiteralPrefilter([("Chrome/(\\d+)", false), ("Firefox/(\\d+)", false), ("(\\d+)", false)]);

        var scan = prefilter.Scan("Mozilla Firefox/120", new ulong[prefilter.BufferLength]);
        prefilter.FirstMatch<string>(
            scan,
            0,
            [_ => { evaluated.Add(0); return null; }, _ => { evaluated.Add(1); return null; }, _ => { evaluated.Add(2); return null; }]);

        // the last pattern has no usable literal, so it is always evaluated
        evaluated.Should().Equal(1, 2);
    }

    [Test]
    public void prefilter_evaluates_candidates_in_pattern_order()
    {
        var prefilter = new LiteralPrefilter([("first", false), ("second", false), ("first", false)]);

        var scan = prefilter.Scan("first second", new ulong[prefilter.BufferLength]);

        prefilter.FirstMatch(scan, 0, [_ => "a", _ => "b", _ => "c"]).Should().Be("a");
        prefilter.FirstMatch(scan, 1, [_ => "b", _ => "c"]).Should().Be("b");
        prefilter.FirstMatch(scan, 2, [_ => "c"]).Should().Be("c");
    }

    [Test]
    public void prefilter_requires_prefix_of_anchored_patterns()
    {
        var prefilter = new LiteralPrefilter([("^(abc|xyz)", false)]);

        var matching = prefilter.Scan("XYZ and more", new ulong[prefilter.BufferLength]);
        prefilter.FirstMatch(matching, 0, [_ => "match"]).Should().Be("match");

        var notMatching = prefilter.Scan("and then xyz", new ulong[prefilter.BufferLength]);
        prefilter.FirstMatch(notMatching, 0, [_ => "match"]).Should().BeNull();
    }

    [Test]
    public void prefilter_rejects_null_input()
    {
        var prefilter = new LiteralPrefilter([("abc", false)]);

        Action act = () => prefilter.Scan(null, new ulong[prefilter.BufferLength]);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestCase(@"Chrome/\d", new[] { "chrome/0|chrome/1|chrome/2|chrome/3|chrome/4|chrome/5|chrome/6|chrome/7|chrome/8|chrome/9" })]
    [TestCase(@"(Chrome)/(\d+)", new[] { "chrome/", "0|1|2|3|4|5|6|7|8|9" })]
    [TestCase(@"[Ss][Pp]ider", new[] { "spider" })]
    [TestCase(@"(?:Foo|Bar)Baz", new[] { "barbaz|foobaz" })]
    [TestCase(@"a{0,2}b", new[] { "aab|ab|b" })]
    [TestCase(@"\bfoo\b", new[] { "foo" })]
    [TestCase(@"foo(?=bar)", new[] { "foo" })]
    [TestCase(@"Foo.*Bar", new[] { "foo", "bar" })]
    [TestCase(@"(Abc|De.+)", new[] { "abc|de" })]
    [TestCase(@"My\_App", new[] { "my_app" })]
    [TestCase(@"[BL]C", new[] { "bc|lc" })]
    public void required_literals_are_extracted(string pattern, string[] expected)
    {
        var requirements = RequiredLiterals.Extract(pattern);

        requirements.Select(r => string.Join("|", r.Order(StringComparer.Ordinal)))
            .Should().BeEquivalentTo(expected);
    }

    [TestCase(@"x?")]
    [TestCase(@".*")]
    [TestCase(@"(?i)abc")]
    [TestCase(@"\x41bc")]
    [TestCase(@"(abc|)")]
    [TestCase(@"[^abc]+")]
    [TestCase(@"")]
    [TestCase(null)]
    public void unsupported_or_unconstrained_patterns_have_no_requirements(string pattern)
    {
        RequiredLiterals.Extract(pattern).Should().BeNull();
    }

    [TestCase(@"^(abc|De)", new[] { "abc", "de" })]
    [TestCase(@"^Mozilla/(\d)", new[] { "mozilla/0", "mozilla/1", "mozilla/2", "mozilla/3", "mozilla/4", "mozilla/5", "mozilla/6", "mozilla/7", "mozilla/8", "mozilla/9" })]
    [TestCase(@"^(up.b|abc)", new[] { "up", "abc" })]
    public void prefixes_of_anchored_patterns_are_extracted(string pattern, string[] expected)
    {
        RequiredLiterals.ExtractPrefixes(pattern).Should().BeEquivalentTo(expected);
    }

    [TestCase(@"abc")]
    [TestCase(@"^.{0,10}abc")]
    [TestCase(@"^(abc|.x)")]
    public void patterns_without_known_prefix_have_no_prefixes(string pattern)
    {
        RequiredLiterals.ExtractPrefixes(pattern).Should().BeNull();
    }

    private static readonly string[] EdgeCases =
    [
        string.Empty,
        " ",
        "Kindle Fire",
        "Mozilla/5.0 (Linux; Android 10; İPHONE) AppleWebKit/537.36",
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_1 like Mac OS X) \U0001F600 Safari/604.1",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " + new string('a', 5000),
        "ÄÖÜ äöü ß Ω 中文 Googlebot/2.1"
    ];

    private static T FirstMatch<T>(Func<string, T>[] parsers, string userAgent)
        where T : class
    {
        foreach (var parser in parsers)
        {
            var result = parser(userAgent);
            if (result is not null)
                return result;
        }

        return null;
    }

    private static Selectors LoadEmbeddedSelectors()
    {
        using var stream = typeof(Parser).Assembly.GetManifestResourceStream("UAParser.regexes.json");
        using var reader = new StreamReader(stream!);
        return JsonSerializer.Deserialize<Selectors>(reader.ReadToEnd());
    }
}

#pragma warning restore IDE1006 // Naming Styles
