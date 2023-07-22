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
        var parser = Parser.FromYaml(yamlContent);
        Assert.NotNull(parser);
    }

    [Fact]
    public void can_utilize_regex_timeouts()
    {
        var yamlContent = this.GetTestResources("UAParser.Tests.Regexes.backtracking.yaml");
        var parser = Parser.FromYaml(
            yamlContent,
            new ParserOptions { MatchTimeOut = TimeSpan.FromSeconds(1), });

        // this loads a backtracking-sensible regular expression and we'll attempt to match it with
        // a long string that should trigger the backtracking,
        var input = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa>";

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
}
