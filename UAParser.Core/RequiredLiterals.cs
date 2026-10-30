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
using System.Linq;

/// <summary>
/// Extracts literal substrings that every match of a regular expression must contain.
/// </summary>
/// <remarks>
/// <para>
/// The result is a list of requirements that must all be met. Each requirement is a set of
/// alternative literals of which at least one must occur in the input. Literals are lower-cased
/// (ASCII only) so they can be searched case-insensitively. The extraction is conservative: any
/// construct that is not understood yields no requirement, never a wrong one.
/// </para>
/// <para>
/// Parts of a pattern that only match a small, finite set of strings (literals, small character
/// classes, alternations of those) are tracked as such a set, and adjacent sets are combined, so
/// <c>[BL]C\d</c> yields the requirement "bc0" or "bc1" or ... or "lc9".
/// </para>
/// </remarks>
internal static class RequiredLiterals
{
    /// <summary>
    /// The maximum number of strings a requirement may consist of.
    /// </summary>
    private const int MaxSetSize = 64;

    /// <summary>
    /// The maximum number of prefixes of a pattern anchored at the start of the input.
    /// </summary>
    private const int MaxPrefixes = 512;

    /// <summary>
    /// The maximum number of requirements an alternation is expanded into.
    /// </summary>
    private const int MaxCombinations = 16;

    /// <summary>
    /// The maximum number of repetitions of a finite set that is expanded, as in <c>x{0,2}</c>.
    /// </summary>
    private const int MaxRepetitions = 8;

    private const string ZeroWidthEscapes = "bBAzZG";

    private const string UnboundedClassEscapes = "DwWsS";

    private static readonly HashSet<string> EmptyString = [string.Empty];

    private static readonly char[] Digits = [.. "0123456789"];

    // the sets are never modified, so the sets of single (ASCII) characters can be shared
    private static readonly HashSet<string>[] SingleChars =
        [.. Enumerable.Range(0, 128).Select(c => new HashSet<string> { ((char)c).ToString() })];

    /// <summary>
    /// Returns the required literals of the pattern, or <c>null</c> if none could be determined.
    /// </summary>
    public static List<string[]> Extract(string pattern) => Analyze(pattern).Requirements;

    /// <summary>
    /// Returns the strings one of which a match must start with if the pattern is anchored at the
    /// start of the input, or <c>null</c> if the pattern is not anchored or no prefix is known.
    /// </summary>
    public static string[] ExtractPrefixes(string pattern) => Analyze(pattern).Prefixes;

    /// <summary>
    /// Returns both the required literals (see <see cref="Extract"/>) and the prefixes (see
    /// <see cref="ExtractPrefixes"/>) of the pattern.
    /// </summary>
    public static (List<string[]> Requirements, string[] Prefixes) Analyze(string pattern)
    {
        var alternatives = Parse(pattern);
        if (alternatives is null)
            return (null, null);

        var requirements = Alternation(alternatives).Requirements;

        string[] prefixes = null;
        if (alternatives is [[{ IsStartAnchor: true }, ..] sequence])
        {
            var leading = LeadingStrings(sequence.Skip(1));
            prefixes = leading.Contains(string.Empty) ? null : [.. leading];
        }

        return (requirements.Count > 0 ? requirements : null, prefixes);
    }

    /// <summary>
    /// Orders requirements from most to least selective, assuming longer literals and fewer
    /// alternatives are rarer.
    /// </summary>
    public static IEnumerable<string[]> Ranked(this IEnumerable<string[]> requirements)
    {
        return requirements
            .OrderByDescending(r => r.Min(l => l.Length))
            .ThenBy(r => r.Length);
    }

    private static List<List<Node>> Parse(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return null;

        try
        {
            var pos = 0;
            var alternatives = ParseAlternation(pattern, ref pos);
            return pos == pattern.Length ? alternatives : null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static Info Alternation(List<List<Node>> alternatives)
    {
        var infos = alternatives.Select(Sequence).ToList();
        if (infos.Count == 1)
            return infos[0];

        if (infos.All(i => i.Exact is not null))
        {
            var union = infos.SelectMany(i => i.Exact).ToHashSet();
            if (union.Count <= MaxSetSize)
                return Info.ForExact(union);
        }

        // At least one alternative has to match. Picking one requirement of every alternative and
        // combining them gives an "any of" requirement, and since (A and B) or C equals
        // (A or C) and (B or C) every such combination is required. The best two requirements of
        // each alternative are combined as long as the number of combinations stays small.
        var ranked = infos.Select(i => i.Requirements.Ranked().Take(2).ToList()).ToList();
        if (ranked.Any(r => r.Count == 0))
            return Info.None;

        if (ranked.Aggregate(1L, (count, r) => count * r.Count) > MaxCombinations)
            ranked = [.. ranked.Select(r => r.Take(1).ToList())];

        var combinations = new List<HashSet<string>> { new() };
        foreach (var requirements in ranked)
        {
            combinations = requirements.Count == 1
                               ? [.. combinations.Select(c => { c.UnionWith(requirements[0]); return c; })]
                               : [.. combinations.SelectMany(c => requirements.Select(r => new HashSet<string>(c.Concat(r))))];
        }

        return new Info(null, [.. combinations.Select(c => c.ToArray())]);
    }

    private static Info Sequence(List<Node> sequence)
    {
        var requirements = new List<string[]>();
        var run = EmptyString;
        var exact = true;

        void Flush()
        {
            // a run that may be empty does not require anything
            if (!run.Contains(string.Empty))
                requirements.Add([.. run]);

            run = EmptyString;
        }

        foreach (var node in sequence)
        {
            var info = Quantified(node, out var repeated);

            if (info.Exact is null)
            {
                Flush();
                exact = false;
                requirements.AddRange(info.Requirements);
                continue;
            }

            var combined = Concat(run, info.Exact, MaxSetSize);
            if (combined is null)
            {
                Flush();
                exact = false;
                combined = info.Exact;
            }

            run = combined;

            // further copies of a repeated node may follow, so nothing can be appended to the run
            if (repeated)
            {
                Flush();
                exact = false;
            }
        }

        var whole = run;
        Flush();
        return new Info(exact ? whole : null, requirements);
    }

    /// <summary>
    /// The strings one of which the sequence must start with.
    /// </summary>
    private static HashSet<string> LeadingStrings(IEnumerable<Node> sequence)
    {
        var run = EmptyString;

        foreach (var node in sequence)
        {
            if (node.Kind == NodeKind.Group && node.Min >= 1 && Atom(node).Exact is null)
            {
                var leading = node.Alternatives.SelectMany(LeadingStrings).ToHashSet();
                return Concat(run, leading, MaxPrefixes) ?? run;
            }

            var info = Quantified(node, out var repeated);
            var combined = info.Exact is null ? null : Concat(run, info.Exact, MaxPrefixes);

            if (combined is null)
                return run;

            run = combined;

            if (repeated)
                return run;
        }

        return run;
    }

    private static Info Quantified(Node node, out bool repeated)
    {
        repeated = false;
        var info = Atom(node);

        if (node is { Min: 1, Max: 1 })
            return info;

        // a small, bounded repetition of a finite set is a finite set as well, e.g. " {0,2}"
        // matches "", " " or "  "
        if (info.Exact is not null && node.Max <= MaxRepetitions)
        {
            var repetitions = Repetitions(info.Exact, node.Min, node.Max);
            if (repetitions is not null)
                return Info.ForExact(repetitions);
        }

        if (node.Min == 0)
            return Info.None;

        if (info.Exact is not null)
            repeated = true;

        return info;
    }

    /// <summary>
    /// All strings made of <paramref name="min"/> to <paramref name="max"/> strings of the set, or
    /// <c>null</c> if there are too many.
    /// </summary>
    private static HashSet<string> Repetitions(HashSet<string> set, int min, int max)
    {
        var copies = EmptyString;
        var all = new HashSet<string>();

        for (var count = 0; count <= max; count++)
        {
            if (count >= min)
                all.UnionWith(copies);

            if (all.Count > MaxSetSize)
                return null;

            if (count < max && (copies = Concat(copies, set, MaxSetSize)) is null)
                return null;
        }

        return all;
    }

    private static Info Atom(Node node)
    {
        return node.Kind switch
        {
            NodeKind.Set => Info.ForExact(node.Strings),
            NodeKind.Empty => Info.ForExact(EmptyString),
            NodeKind.Group => Alternation(node.Alternatives),
            _ => Info.None
        };
    }

    private static HashSet<string> Concat(HashSet<string> left, HashSet<string> right, int limit)
    {
        if ((long)left.Count * right.Count > limit)
            return null;

        var result = new HashSet<string>(left.Count * right.Count);
        foreach (var l in left)
        {
            foreach (var r in right)
                result.Add(l + r);
        }

        return result;
    }

    private static List<List<Node>> ParseAlternation(string pattern, ref int pos)
    {
        var alternatives = new List<List<Node>> { ParseSequence(pattern, ref pos) };

        while (pos < pattern.Length && pattern[pos] == '|')
        {
            pos++;
            alternatives.Add(ParseSequence(pattern, ref pos));
        }

        return alternatives;
    }

    private static List<Node> ParseSequence(string pattern, ref int pos)
    {
        var sequence = new List<Node>();

        while (pos < pattern.Length && pattern[pos] is not ('|' or ')'))
        {
            var node = ParseAtom(pattern, ref pos);
            ParseQuantifier(pattern, ref pos, node);
            sequence.Add(node);
        }

        return sequence;
    }

    private static Node ParseAtom(string pattern, ref int pos)
    {
        var c = pattern[pos];

        switch (c)
        {
            case '(':
                return ParseGroup(pattern, ref pos);
            case '[':
                return ParseCharacterClass(pattern, ref pos);
            case '\\':
                return ParseEscape(pattern, ref pos);
            case '^':
                pos++;
                return Node.StartAnchor;
            case '$':
                pos++;
                return Node.Empty;
            case '.':
                pos++;
                return Node.Other;
            case '*' or '+' or '?':
                throw new NotSupportedException();
            case '{' when TryParseQuantifier(pattern, pos, out _, out _, out _):
                throw new NotSupportedException();
            default:
                pos++;
                return c < 128 ? Node.ForChars([c]) : Node.Other;
        }
    }

    private static Node ParseGroup(string pattern, ref int pos)
    {
        pos++;
        var lookaround = false;

        if (Peek(pattern, pos) == '?')
        {
            switch (Peek(pattern, pos + 1))
            {
                case ':' or '>':
                    pos += 2;
                    break;
                case '=' or '!':
                    pos += 2;
                    lookaround = true;
                    break;
                case '<' when Peek(pattern, pos + 2) is '=' or '!':
                    pos += 3;
                    lookaround = true;
                    break;
                case '<':
                    pos = SkipPast(pattern, pos + 2, '>');
                    break;
                case '\'':
                    pos = SkipPast(pattern, pos + 2, '\'');
                    break;
                default:
                    // inline options, comments and conditionals are not supported
                    throw new NotSupportedException();
            }
        }

        var alternatives = ParseAlternation(pattern, ref pos);

        if (Peek(pattern, pos) != ')')
            throw new NotSupportedException();

        pos++;

        // a lookaround does not consume any input
        return lookaround ? Node.Empty : Node.ForGroup(alternatives);
    }

    private static Node ParseEscape(string pattern, ref int pos)
    {
        var escaped = Peek(pattern, pos + 1);
        pos += 2;

        if (escaped == 'd')
            return Node.ForChars(Digits);

        if (ZeroWidthEscapes.Contains(escaped))
            return Node.Empty;

        if (UnboundedClassEscapes.Contains(escaped))
            return Node.Other;

        if (char.IsAsciiLetterOrDigit(escaped))
            throw new NotSupportedException();

        return escaped is not '\0' and < (char)128 ? Node.ForChars([escaped]) : throw new NotSupportedException();
    }

    private static Node ParseCharacterClass(string pattern, ref int pos)
    {
        pos++;

        var negated = Peek(pattern, pos) == '^';
        if (negated)
            pos++;

        var members = new List<char>();
        var first = true;

        while (pos < pattern.Length && (pattern[pos] != ']' || first))
        {
            first = false;
            var c = pattern[pos++];

            if (c == '[' && members.Count > 0 && pattern[pos - 2] == '-')
            {
                // character class subtraction
                throw new NotSupportedException();
            }

            if (c == '\\')
            {
                var escaped = Peek(pattern, pos++);
                if (escaped == 'd')
                {
                    members.AddRange("0123456789");
                    continue;
                }

                if (char.IsAsciiLetterOrDigit(escaped))
                {
                    // other classes like \w or \s, or escapes like \x41: not tracked
                    members = null;
                    SkipToClassEnd(pattern, ref pos);
                    break;
                }

                c = escaped;
            }

            if (Peek(pattern, pos) == '-' && Peek(pattern, pos + 1) is not (']' or '\0' or '\\' or '['))
            {
                var last = pattern[pos + 1];
                pos += 2;
                if (last - c > MaxSetSize)
                {
                    members = null;
                    SkipToClassEnd(pattern, ref pos);
                    break;
                }

                for (var r = c; r <= last; r++)
                    members.Add(r);

                continue;
            }

            members.Add(c);
        }

        if (Peek(pattern, pos) != ']')
            throw new NotSupportedException();

        pos++;

        if (negated || members is null || members.Any(m => m >= 128))
            return Node.Other;

        // the search is case-insensitive, so [Ss] is the same as "s"
        var chars = members.Select(char.ToLowerInvariant).Distinct().ToArray();
        return chars.Length <= MaxSetSize ? Node.ForChars(chars) : Node.Other;
    }

    private static void SkipToClassEnd(string pattern, ref int pos)
    {
        while (pos < pattern.Length && pattern[pos] != ']')
        {
            if (pattern[pos] == '\\')
                pos++;

            pos++;
        }
    }

    private static void ParseQuantifier(string pattern, ref int pos, Node node)
    {
        int min, max;

        switch (Peek(pattern, pos))
        {
            case '*':
                (min, max) = (0, int.MaxValue);
                pos++;
                break;
            case '+':
                (min, max) = (1, int.MaxValue);
                pos++;
                break;
            case '?':
                (min, max) = (0, 1);
                pos++;
                break;
            case '{' when TryParseQuantifier(pattern, pos, out min, out max, out var length):
                pos += length;
                break;
            default:
                return;
        }

        node.Min = min;
        node.Max = max;

        // lazy quantifier
        if (Peek(pattern, pos) == '?')
            pos++;
    }

    private static bool TryParseQuantifier(string pattern, int pos, out int min, out int max, out int length)
    {
        min = max = length = 0;

        var end = pattern.IndexOf('}', pos);
        if (end < 0)
            return false;

        var body = pattern.AsSpan(pos + 1, end - pos - 1);
        var comma = body.IndexOf(',');
        var minPart = comma < 0 ? body : body[..comma];
        var maxPart = comma < 0 ? body : body[(comma + 1)..];

        if (!int.TryParse(minPart, out min) || minPart.ContainsAnyExceptInRange('0', '9'))
            return false;

        if (maxPart.IsEmpty)
        {
            max = int.MaxValue;
        }
        else if (!int.TryParse(maxPart, out max) || maxPart.ContainsAnyExceptInRange('0', '9'))
        {
            return false;
        }

        length = end - pos + 1;
        return true;
    }

    private static int SkipPast(string pattern, int pos, char terminator)
    {
        var end = pattern.IndexOf(terminator, pos);
        return end >= 0 ? end + 1 : throw new NotSupportedException();
    }

    private static char Peek(string pattern, int pos)
    {
        return pos < pattern.Length ? pattern[pos] : '\0';
    }

    private enum NodeKind
    {
        /// <summary>A single character out of a small set.</summary>
        Set,

        /// <summary>A group of alternatives.</summary>
        Group,

        /// <summary>A zero-width assertion, such as an anchor or a lookaround.</summary>
        Empty,

        /// <summary>Anything else.</summary>
        Other
    }

    /// <summary>
    /// What is known about the strings a part of a pattern matches: either the exact, finite set
    /// of strings, or only requirements.
    /// </summary>
    private sealed class Info(HashSet<string> exact, List<string[]> requirements)
    {
        private List<string[]> _requirements = requirements;

        public static Info None => new(null, []);

        public HashSet<string> Exact { get; } = exact;

        // only built when needed, as most exact parts are merged into larger ones
        public List<string[]> Requirements =>
            this._requirements ??= this.Exact.Contains(string.Empty) ? [] : [[.. this.Exact]];

        public static Info ForExact(HashSet<string> exact) => new(exact, null);
    }

    private sealed class Node
    {
        public static Node Other => new() { Kind = NodeKind.Other };

        public static Node Empty => new() { Kind = NodeKind.Empty };

        public static Node StartAnchor => new() { Kind = NodeKind.Empty, IsStartAnchor = true };

        public NodeKind Kind { get; private init; }

        public bool IsStartAnchor { get; private init; }

        /// <summary>
        /// The characters of a <see cref="NodeKind.Set"/>, as strings. Never modified.
        /// </summary>
        public HashSet<string> Strings { get; private init; }

        public List<List<Node>> Alternatives { get; private init; }

        public int Min { get; set; } = 1;

        public int Max { get; set; } = 1;

        public static Node ForChars(IReadOnlyCollection<char> chars) =>
            new()
                {
                    Kind = NodeKind.Set,
                    Strings = chars.Count == 1
                                  ? SingleChars[char.ToLowerInvariant(chars.First())]
                                  : [.. chars.Select(c => char.ToLowerInvariant(c).ToString())]
                };

        public static Node ForGroup(List<List<Node>> alternatives) =>
            new() { Kind = NodeKind.Group, Alternatives = alternatives };
    }
}
