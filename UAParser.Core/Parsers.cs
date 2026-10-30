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

using UAParser.Extensions;
using UAParser.Objects;

internal static class Parsers
{
    /// <summary>
    /// Binds a part of a match. Every binder that reads the next regex group advances
    /// <paramref name="group"/>, so the order in which binders run determines which group they read.
    /// </summary>
    private delegate T Binder<out T>(Match match, ref int group);

    private static readonly string[] AllReplacementTokens =
    [
        "$1", "$2", "$3", "$4",
        "$5", "$6", "$7", "$8",
        "$9"
    ];

    // ReSharper disable once InconsistentNaming
    public static Func<string, OS> OS(
        Regex regex,
        string osReplacement,
        string v1Replacement,
        string v2Replacement,
        string v3Replacement,
        string v4Replacement)
    {
        // For variable replacements to be consistent the order in which the binders run is important
        // ($1 is only available to the first binder and so forth) so a bit of conditional is required
        // to get the creations to work. This is backed by unit tests
        if (v2Replacement is "$1")
        {
            var v2 = Replace(v2Replacement, "$1");
            var v1 = Replace(v1Replacement, "$1");
            var v3 = Replace(v3Replacement, "$3");
            var v4 = Replace(v4Replacement, "$4");
            var family = Replace(osReplacement, "$5");

            return Create(
                regex,
                (Match m, ref int g) =>
                    {
                        var minor = v2(m, ref g);
                        var major = v1(m, ref g);
                        var patch = v3(m, ref g);
                        var patchMinor = v4(m, ref g);
                        return new OS(family(m, ref g), major, minor, patch, patchMinor);
                    });
        }

        if (v1Replacement is not "$1")
        {
            var family = Replace(osReplacement, "$1");
            var v1 = Replace(v1Replacement, "$2");
            var v2 = Replace(v2Replacement, "$3");
            var v3 = Replace(v3Replacement, "$4");
            var v4 = Replace(v4Replacement, "$5");

            return Create(
                regex,
                (Match m, ref int g) => new OS(family(m, ref g), v1(m, ref g), v2(m, ref g), v3(m, ref g), v4(m, ref g)));
        }

        if (v2Replacement is "$2")
        {
            var v1 = Replace(v1Replacement, "$1");
            var v2 = Replace(v2Replacement, "$2");
            var v3 = Replace(v3Replacement, "$3");
            var v4 = Replace(v4Replacement, "$4");
            var family = Replace(osReplacement, "$5");

            return Create(
                regex,
                (Match m, ref int g) =>
                    {
                        var major = v1(m, ref g);
                        var minor = v2(m, ref g);
                        var patch = v3(m, ref g);
                        var patchMinor = v4(m, ref g);
                        return new OS(family(m, ref g), major, minor, patch, patchMinor);
                    });
        }

        return OSWithFamilySecond(regex, osReplacement, v1Replacement, v2Replacement, v3Replacement, v4Replacement);
    }

    // ReSharper disable once InconsistentNaming
    private static Func<string, OS> OSWithFamilySecond(
        Regex regex,
        string osReplacement,
        string v1Replacement,
        string v2Replacement,
        string v3Replacement,
        string v4Replacement)
    {
        var v1 = Replace(v1Replacement, "$1");
        var family = Replace(osReplacement, "$2");
        var v2 = Replace(v2Replacement, "$3");
        var v3 = Replace(v3Replacement, "$4");
        var v4 = Replace(v4Replacement, "$5");

        return Create(
            regex,
            (Match m, ref int g) =>
                {
                    var major = v1(m, ref g);
                    return new OS(family(m, ref g), major, v2(m, ref g), v3(m, ref g), v4(m, ref g));
                });
    }

    public static Func<string, Device> Device(
        Regex regex,
        string familyReplacement,
        string brandReplacement,
        string modelReplacement)
    {
        var family = ReplaceAll(familyReplacement);
        var brand = ReplaceAll(brandReplacement);
        var model = ReplaceAll(modelReplacement);

        return Create(
            regex,
            (Match m, ref int g) => new Device(family(m, ref g), brand(m, ref g), model(m, ref g)));
    }

    public static Func<string, Browser> UserAgent(
        Regex regex,
        string familyReplacement,
        string majorReplacement,
        string minorReplacement,
        string patchReplacement)
    {
        var family = Replace(familyReplacement, "$1");
        var v1 = Replace(majorReplacement, "$2");
        var v2 = Replace(minorReplacement, "$3");
        var v3 = Replace(patchReplacement, "$4");

        return Create(
            regex,
            (Match m, ref int g) => new Browser(family(m, ref g), v1(m, ref g), v2(m, ref g), v3(m, ref g)));
    }

    private static Binder<string> Replace(string replacement)
    {
        return replacement is not null ? Select(_ => replacement) : Select(v => v);
    }

    private static Binder<string> Replace(
        string replacement,
        string token)
    {
        return replacement is not null && replacement.Contains(token)
            ? Select(s => s is not null
                ? replacement.ReplaceFirstOccurrence(token, s)
                : replacement)
            : Replace(replacement);
    }

    private static Binder<string> ReplaceAll(string replacement)
    {
        if (replacement is null)
        {
            return Select(v => v);
        }

        return (Match m, ref int _) =>
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

    private static Binder<T> Select<T>(Func<string, T> selector)
    {
        return (Match m, ref int group) =>
        {
            group++;

            var groups = m.Groups;
            var matched = groups[group];

            return selector(
                group <= groups.Count && matched.Success
                    ? matched.Value
                    : null);
        };
    }

    private static Func<string, T> Create<T>(
        Regex regex,
        Binder<T> binder)
    {
        return input =>
        {
            try
            {
                var m = regex.Match(input);
                if (!m.Success)
                    return default;

                var group = 0;
                return binder(m, ref group);
            }
            catch (RegexMatchTimeoutException)
            {
                // we'll simply swallow this exception and return the default (non-matched)
                return default;
            }
        };
    }
}
