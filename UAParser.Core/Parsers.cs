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

using UAParser.Extensions;
using UAParser.Objects;

internal static class Parsers
{
    // ReSharper disable once InconsistentNaming
    public static Func<string, OS> OS(
        Regex regex,
        string osReplacement,
        string v1Replacement,
        string v2Replacement,
        string v3Replacement,
        string v4Replacement)
    {
        // For variable replacements to be consistent the order of the linq statements are important ($1
        // is only available to the first 'from X in Replace(..)' and so forth) so a bit of conditional
        // is required to get the creations to work. This is backed by unit tests
        if (v1Replacement is not "$1")
        {
            return Create(
                regex,
                from family in Replace(osReplacement, "$1")
                from v1 in Replace(v1Replacement, "$2")
                from v2 in Replace(v2Replacement, "$3")
                from v3 in Replace(v3Replacement, "$4")
                from v4 in Replace(v4Replacement, "$5")
                select new OS(family, v1, v2, v3, v4));
        }

        if (v2Replacement is "$2")
        {
            return Create(
                regex,
                from v1 in Replace(v1Replacement, "$1")
                from v2 in Replace(v2Replacement, "$2")
                from v3 in Replace(v3Replacement, "$3")
                from v4 in Replace(v4Replacement, "$4")
                from family in Replace(osReplacement, "$5")
                select new OS(family, v1, v2, v3, v4));
        }

        return Create(
            regex,
            from v1 in Replace(v1Replacement, "$1")
            from family in Replace(osReplacement, "$2")
            from v2 in Replace(v2Replacement, "$3")
            from v3 in Replace(v3Replacement, "$4")
            from v4 in Replace(v4Replacement, "$5")
            select new OS(family, v1, v2, v3, v4));
    }

    public static Func<string, Device> Device(
        Regex regex,
        string familyReplacement,
        string brandReplacement,
        string modelReplacement)
    {
        return Create(
            regex,
            from family in ReplaceAll(familyReplacement)
            from brand in ReplaceAll(brandReplacement)
            from model in ReplaceAll(modelReplacement)
            select new Device(family, brand, model));
    }

    public static Func<string, Browser> UserAgent(
        Regex regex,
        string familyReplacement,
        string majorReplacement,
        string minorReplacement,
        string patchReplacement)
    {
        return Create(
            regex,
            from family in Replace(familyReplacement, "$1")
            from v1 in Replace(majorReplacement, "$2")
            from v2 in Replace(minorReplacement, "$3")
            from v3 in Replace(patchReplacement, "$4")
            select new Browser(family, v1, v2, v3));
    }

    private static Func<Match, IEnumerator<int>, string> Replace(string replacement)
    {
        return replacement is not null ? Select(_ => replacement) : Select(v => v);
    }

    private static Func<Match, IEnumerator<int>, string> Replace(
        string replacement,
        string token)
    {
        return replacement is not null && replacement.Contains(token)
                   ? Select(
                       s => s is not null
                                ? replacement.ReplaceFirstOccurrence(token, s)
                                : replacement)
                   : Replace(replacement);
    }

    private static readonly string[] AllReplacementTokens = {
                                                                    "$1", "$2", "$3", "$4",
                                                                    "$5", "$6", "$7", "$8",
                                                                    "$9"
                                                                };

    private static Func<Match, IEnumerator<int>, string> ReplaceAll(string replacement)
    {
        if (replacement is null)
        {
            return Select(v => v);
        }

        return (m, _) =>
            {
                var finalString = replacement;

                if (!finalString.Contains('$')) return finalString;

                var groups = m.Groups;
                for (var i = 0; i < AllReplacementTokens.Length; i++)
                {
                    var tokenNumber = i + 1;
                    var token = AllReplacementTokens[i];
                    if (finalString.Contains(token))
                    {
                        var replacementText = string.Empty;

                        if (tokenNumber <= groups.Count)
                        {
                            var group = groups[tokenNumber];
                            if (group.Success)
                            {
                                replacementText = group.Value;
                            }
                        }

                        finalString = ReplaceFunction(finalString, replacementText, token);
                    }

                    if (!finalString.Contains('$'))
                        break;
                }

                return finalString;
            };

        static string ReplaceFunction(
            string replacementString,
            string matchedGroup,
            string token)
        {
            return matchedGroup is not null
                       ? replacementString.ReplaceFirstOccurrence(token, matchedGroup)
                       : replacementString;
        }
    }

    private static Func<Match, IEnumerator<int>, T> Select<T>(Func<string, T> selector)
    {
        return (m, num) =>
            {
                if (!num.MoveNext())
                {
                    throw new InvalidOperationException();
                }

                var groups = m.Groups;
                var group = groups[num.Current];

                return selector(
                    num.Current <= groups.Count && group.Success
                        ? group.Value
                        : null);
            };
    }

    private static Func<string, T> Create<T>(
        Regex regex,
        Func<Match, IEnumerator<int>, T> binder)
    {
        return input =>
            {
                try
                {
                    var m = regex.Match(input);
                    var num = Generate(1, n => n + 1);
                    return m.Success ? binder(m, num) : default;
                }
                catch (RegexMatchTimeoutException)
                {
                    // we'll simply swallow this exception and return the default (non-matched)
                    return default;
                }
            };
    }

    private static IEnumerator<T> Generate<T>(T initial, Func<T, T> next)

    {
        for (var state = initial; ; state = next(state))
        {
            yield return state;
        }
    }
}
