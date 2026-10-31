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
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;

#pragma warning disable IDE1006 // Naming Styles
public class TieredRegexTests
{
    private const string Input = "Mozilla/5.0 Chrome/120.0.6099.109";

    [Test]
    public void frequently_used_regex_is_compiled_in_the_background()
    {
        var tiered = new TieredRegex(new Regex(@"Chrome/(\d+)\.(\d+)"), compileWhenFrequentlyUsed: true);

        for (var i = 0; i < TieredRegex.CompileThreshold - 1; i++)
            tiered.Match(Input);

        tiered.IsCompiled.Should().BeFalse();

        tiered.Match(Input);
        WaitUntil(() => tiered.IsCompiled);

        var match = tiered.Match(Input);
        match.Success.Should().BeTrue();
        match.Groups[1].Value.Should().Be("120");
        match.Groups[2].Value.Should().Be("0");
    }

    [Test]
    public void regex_is_not_compiled_when_disabled()
    {
        var tiered = new TieredRegex(new Regex("Chrome"), compileWhenFrequentlyUsed: false);

        for (var i = 0; i < TieredRegex.CompileThreshold * 2; i++)
            tiered.Match(Input);

        Thread.Sleep(100);
        tiered.IsCompiled.Should().BeFalse();
    }

    [Test]
    public void compiled_regex_keeps_options_and_timeout()
    {
        var timeout = TimeSpan.FromSeconds(3);
        var tiered = new TieredRegex(
            new Regex("chrome", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, timeout),
            compileWhenFrequentlyUsed: true);

        for (var i = 0; i < TieredRegex.CompileThreshold; i++)
            tiered.Match(Input);

        WaitUntil(() => tiered.IsCompiled);

        tiered.Match(Input).Success.Should().BeTrue();
    }

    [Test]
    public void parser_compiles_frequently_used_regexes()
    {
        var parser = Parser.GetDefault(new ParserOptions());
        const string userAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.6099.109 Safari/537.36";

        var expected = parser.Parse(userAgent).ToString();

        for (var i = 0; i < TieredRegex.CompileThreshold * 2; i++)
            parser.Parse(userAgent).ToString().Should().Be(expected);

        Thread.Sleep(200);
        parser.Parse(userAgent).ToString().Should().Be(expected);
    }

    private static void WaitUntil(Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition() && stopwatch.Elapsed < TimeSpan.FromSeconds(10))
            Thread.Sleep(10);

        condition().Should().BeTrue();
    }
}

#pragma warning restore IDE1006 // Naming Styles
