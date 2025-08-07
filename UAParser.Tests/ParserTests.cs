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

using UAParser.Objects;

using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

#pragma warning disable IDE1006 // Naming Styles
public class ParserTests
{
    [Fact]
    public void can_get_default_parser()
    {
        var parser = Parser.GetDefault();
        Assert.NotNull(parser);
    }

    [Fact]
    public void can_get_parser_from_input()
    {
        var yamlContent = this.GetTestResources("UAParser.Tests.Regexes.regexes.yaml");

        var parser = FromYaml(yamlContent);
        Assert.NotNull(parser);
    }

    [Fact]
    public void can_utilize_regex_timeouts()
    {
        var yamlContent = this.GetTestResources("UAParser.Tests.Regexes.backtracking.yaml");

        var parser = FromYaml(
            yamlContent,
            new ParserOptions { MatchTimeOut = TimeSpan.FromSeconds(1), });

        // this loads a backtracking-sensible regular expression, and we'll attempt to match it with
        // a long string that should trigger the backtracking,
        const string input = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa>";

        var start = DateTime.UtcNow;
        var match = parser.ParseUserAgent(input);
        Assert.Equal(Parser.Other, match.Family);
        var duration = DateTime.UtcNow.Subtract(start);

        // without the match timeout in place, the regex will do massive backtracking and would run for
        // a very long time (at least on my machine). I will attempt to assert on the duration
        // even though I realize that this is potentially a brittle approach
        Assert.True(
            duration < TimeSpan.FromSeconds(3),
            $"The match takes longer than 3 seconds (took {duration}). The MatchTimeOut should have stopped it at 1 second, but this may just be a brittle test due to e.g. shared resources on a CI server");
    }

    /// <summary>
    /// Returns a <see cref="Parser"/> instance based on the regex definitions in a json string
    /// </summary>
    /// <param name="yaml">a string containing yaml definitions of reg-ex</param>
    /// <param name="parserOptions">specifies the options for the parser</param>
    /// <returns>A <see cref="Parser"/> instance parsing user agent strings based on the regexes defined in the json string</returns>
    private static Parser FromYaml(string yaml, ParserOptions parserOptions = null)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance).Build();

        var regexList = deserializer.Deserialize<Selectors>(yaml);

        return new Parser(regexList, parserOptions);
    }
}

#pragma warning restore IDE1006 // Naming Styles
