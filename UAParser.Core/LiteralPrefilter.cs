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
using System.Numerics;
using System.Text;

/// <summary>
/// Skips regular expressions that cannot match an input because a literal they require is absent.
/// </summary>
/// <remarks>
/// The required literals of all patterns are searched with a single Aho-Corasick scan over the
/// input. Each pattern is "anchored" on its most selective requirement, so only the patterns
/// anchored on a literal that was found become candidates. Candidates are then checked against
/// their remaining requirements and evaluated in their original order, which keeps the
/// first-match-wins semantics. Patterns are only ever skipped when they provably cannot match.
/// </remarks>
internal sealed class LiteralPrefilter
{
    /// <summary>
    /// The largest buffer that should be allocated on the stack.
    /// </summary>
    public const int MaxStackBufferLength = 1024;

    /// <summary>
    /// Single characters occur in nearly every user agent, so they hardly filter anything while
    /// costing time on every occurrence.
    /// </summary>
    private const int MinNeedleLength = 2;

    private readonly byte[] _charClass = new byte[128];

    private readonly int _classCount;

    // The automaton. The root and the states one character deep come first and have complete
    // (dense) transition rows. Deeper states only store their own edges and fall back to their
    // failure state; they are numbered depth-first, so the states of a literal are adjacent in
    // memory. Transitions are encoded as (state << 1) | 1 if the state has an output, else state << 1.
    private readonly int _denseCount;

    private readonly int[] _dense;

    private readonly SparseState[] _sparse;

    // (transition << 8) | character class
    private readonly int[] _edges;

    private readonly int[] _needleAt;

    private readonly int[] _outputLink;

    private readonly int _needleWords;

    private readonly int _patternWords;

    private readonly int[][] _patternsByAnchor;

    private readonly int[][][] _requirements;

    private readonly Dictionary<char, string[]>[] _prefixes;

    private readonly ulong[] _unanchored;

    private readonly ulong[] _ignoreCase;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiteralPrefilter"/> class.
    /// </summary>
    /// <param name="patterns">The patterns, in evaluation order, and whether they ignore case.</param>
    public LiteralPrefilter(IReadOnlyList<(string Pattern, bool IgnoreCase)> patterns)
    {
        this._patternWords = (patterns.Count + 63) >> 6;
        this._unanchored = new ulong[this._patternWords];
        this._ignoreCase = new ulong[this._patternWords];
        this._requirements = new int[patterns.Count][][];
        this._prefixes = new Dictionary<char, string[]>[patterns.Count];

        var needleIds = new Dictionary<string, int>(StringComparer.Ordinal);
        var patternRequirements = new List<int[]>[patterns.Count];

        for (var p = 0; p < patterns.Count; p++)
        {
            // dropping a literal from an "any of" requirement would make it too strict, so
            // requirements with literals the ASCII-only scan cannot find are dropped as a whole
            var (requirements, prefixes) = RequiredLiterals.Analyze(patterns[p].Pattern);

            patternRequirements[p] = requirements?
                .Where(r => r.Length > 0 && r.All(n => n.Length >= MinNeedleLength && IsAscii(n)))
                .Select(r => r.Select(NeedleId).Distinct().ToArray())
                .ToList();

            this._prefixes[p] = prefixes?
                .GroupBy(prefix => prefix[0])
                .ToDictionary(g => g.Key, g => g.ToArray());
        }

        // How many patterns require a literal approximates how common it is in user agents, e.g.
        // ") AppleWebKit" is required by many device patterns and occurs in most user agents.
        // Every pattern is anchored on the requirement whose literals are the least common, so
        // that as few patterns as possible become candidates.
        // Short literals are weighted as more common as they are more likely to occur by chance.
        var needles = needleIds.OrderBy(n => n.Value).Select(n => n.Key).ToList();
        var frequency = new int[needleIds.Count];
        foreach (var needle in patternRequirements.Where(r => r is not null).SelectMany(r => r.SelectMany(n => n).Distinct()))
        {
            frequency[needle] += needles[needle].Length < 4 ? 1 << (4 - needles[needle].Length) : 1;
        }

        var anchors = Enumerable.Range(0, needleIds.Count).Select(_ => new List<int>()).ToList();

        for (var p = 0; p < patterns.Count; p++)
        {
            var requirements = patternRequirements[p];

            if (patterns[p].IgnoreCase)
                SetBit(this._ignoreCase, p);

            if (requirements is not { Count: > 0 })
            {
                SetBit(this._unanchored, p);
                continue;
            }

            var anchor = requirements.MinBy(r => r.Sum(n => frequency[n]));
            foreach (var needle in anchor)
            {
                anchors[needle].Add(p);
            }

            this._requirements[p] = [.. requirements.Where(r => r != anchor)];
        }

        this._patternsByAnchor = [.. anchors.Select(a => a.ToArray())];
        this._needleWords = (needleIds.Count + 63) >> 6;

        this._classCount = this.BuildCharClasses(needles);
        (this._denseCount, this._dense, this._sparse, this._edges, this._needleAt, this._outputLink) =
            this.BuildAutomaton(needles);

        int NeedleId(string needle)
        {
            if (!needleIds.TryGetValue(needle, out var id))
            {
                id = needleIds.Count;
                needleIds.Add(needle, id);
            }

            return id;
        }
    }

    /// <summary>
    /// The number of buffer elements <see cref="Scan"/> needs.
    /// </summary>
    public int BufferLength => this._needleWords + this._patternWords;

    /// <summary>
    /// Scans the input once for the required literals of all patterns.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <param name="buffer">A buffer of <see cref="BufferLength"/> elements holding the result.</param>
    public PrefilterScan Scan(string input, Span<ulong> buffer)
    {
        ArgumentNullException.ThrowIfNull(input);

        var foundNeedles = buffer[..this._needleWords];
        var candidates = buffer.Slice(this._needleWords, this._patternWords);

        foundNeedles.Clear();
        this._unanchored.CopyTo(candidates);

        // ignore-case patterns compare non-ASCII characters using Unicode case equivalences
        // (e.g. the Kelvin sign matches 'k'), which the ASCII-only scan does not model
        var isAscii = Ascii.IsValid(input);
        if (!isAscii)
        {
            for (var i = 0; i < candidates.Length; i++)
                candidates[i] |= this._ignoreCase[i];
        }

        this.Scan(input, foundNeedles, candidates);

        return new PrefilterScan(input, foundNeedles, candidates, isAscii);
    }

    /// <summary>
    /// Returns the result of the first parser that matches the input, evaluating only the
    /// parsers whose pattern may match.
    /// </summary>
    /// <param name="scan">The scan of the input.</param>
    /// <param name="offset">The index of the pattern of the first parser.</param>
    /// <param name="parsers">The parsers, in the same order as their patterns.</param>
    public T FirstMatch<T>(in PrefilterScan scan, int offset, Func<string, T>[] parsers)
        where T : class
    {
        if (parsers.Length == 0)
            return null;

        var last = offset + parsers.Length - 1;

        for (var word = offset >> 6; word <= last >> 6; word++)
        {
            var bits = scan.Candidates[word];

            if (word == offset >> 6)
                bits &= ulong.MaxValue << (offset & 63);

            if (word == last >> 6)
                bits &= ulong.MaxValue >> (63 - (last & 63));

            while (bits != 0)
            {
                var pattern = (word << 6) + BitOperations.TrailingZeroCount(bits);
                bits &= bits - 1;

                if (!this.MeetsRequirements(pattern, scan.Input, scan.FoundNeedles, scan.IsAscii))
                    continue;

                var result = parsers[pattern - offset](scan.Input);
                if (result is not null)
                    return result;
            }
        }

        return null;
    }

    private static bool IsAscii(string needle) => Ascii.IsValid(needle);

    private static void SetBit(Span<ulong> bits, int index) => bits[index >> 6] |= 1UL << index;

    private static bool IsSet(ReadOnlySpan<ulong> bits, int index) => (bits[index >> 6] & (1UL << index)) != 0;

    private void Scan(string input, Span<ulong> foundNeedles, Span<ulong> candidates)
    {
        var state = 0;

        foreach (var c in input)
        {
            var charClass = c < 128 ? this._charClass[c] : 0;
            var transition = state < this._denseCount
                                 ? this._dense[state * this._classCount + charClass]
                                 : this.SparseTransition(state, charClass);
            state = transition >> 1;

            if ((transition & 1) == 0)
                continue;

            for (var s = this._needleAt[state] >= 0 ? state : this._outputLink[state]; s != 0; s = this._outputLink[s])
            {
                var needle = this._needleAt[s];
                if (IsSet(foundNeedles, needle))
                    continue;

                SetBit(foundNeedles, needle);
                foreach (var pattern in this._patternsByAnchor[needle])
                    SetBit(candidates, pattern);
            }
        }
    }

    private int SparseTransition(int state, int charClass)
    {
        while (state >= this._denseCount)
        {
            var sparse = this._sparse[state - this._denseCount];

            for (var edge = sparse.FirstEdge; edge < sparse.FirstEdge + sparse.EdgeCount; edge++)
            {
                var encoded = this._edges[edge];
                if ((byte)encoded == charClass)
                    return encoded >> 8;
            }

            state = sparse.Failure;
        }

        return this._dense[state * this._classCount + charClass];
    }

    private bool MeetsRequirements(int pattern, string input, ReadOnlySpan<ulong> foundNeedles, bool isAscii)
    {
        if (!isAscii && IsSet(this._ignoreCase, pattern))
            return true;

        if (!this.HasPrefix(pattern, input))
            return false;

        var requirements = this._requirements[pattern];
        if (requirements is null)
            return true;

        foreach (var alternatives in requirements)
        {
            var found = false;
            foreach (var needle in alternatives)
            {
                if (IsSet(foundNeedles, needle))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;
        }

        return true;
    }

    private bool HasPrefix(int pattern, string input)
    {
        var prefixes = this._prefixes[pattern];
        if (prefixes is null)
            return true;

        if (input.Length == 0 || !prefixes.TryGetValue(char.ToLowerInvariant(input[0]), out var candidates))
            return false;

        // an indexed loop instead of Any() avoids allocating a closure on this hot path
        for (var i = 0; i < candidates.Length; i++)
        {
            if (input.StartsWith(candidates[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Maps every character used by a needle to a dense class. Upper-case ASCII letters share the
    /// class of their lower-case counterpart; all other characters map to class 0.
    /// </summary>
    private int BuildCharClasses(List<string> needles)
    {
        var classCount = 1;

        foreach (var c in needles.SelectMany(n => n).Distinct().Order())
        {
            this._charClass[c] = (byte)classCount++;
        }

        for (var c = 'A'; c <= 'Z'; c++)
        {
            this._charClass[c] = this._charClass[c + 32];
        }

        return classCount;
    }

    private (int DenseCount, int[] Dense, SparseState[] Sparse, int[] Edges, int[] NeedleAt, int[] OutputLink)
        BuildAutomaton(List<string> needles)
    {
        // the edges of the trie, keyed by (state << 8) | character class
        var edgeOf = new Dictionary<int, int>();
        var needleAt = new List<int> { -1 };

        for (var id = 0; id < needles.Count; id++)
        {
            var state = 0;
            foreach (var c in needles[id])
            {
                var key = (state << 8) | this._charClass[c];
                if (!edgeOf.TryGetValue(key, out var next))
                {
                    next = needleAt.Count;
                    needleAt.Add(-1);
                    edgeOf.Add(key, next);
                }

                state = next;
            }

            needleAt[state] = id;
        }

        var stateCount = needleAt.Count;

        // the children of state s are childClass/childTarget[childStart[s]..childStart[s + 1]],
        // ordered by character class
        var keys = edgeOf.Keys.Order().ToArray();
        var childStart = new int[stateCount + 1];
        var childClass = new byte[keys.Length];
        var childTarget = new int[keys.Length];

        for (var i = 0; i < keys.Length; i++)
        {
            childStart[(keys[i] >> 8) + 1]++;
            childClass[i] = (byte)keys[i];
            childTarget[i] = edgeOf[keys[i]];
        }

        for (var state = 0; state < stateCount; state++)
            childStart[state + 1] += childStart[state];

        var failure = new int[stateCount];
        var outputLink = new int[stateCount];
        var queue = new Queue<int>(childTarget[childStart[0]..childStart[1]]);

        // breadth-first, so the failure state of every state is complete before it is used
        while (queue.Count > 0)
        {
            var state = queue.Dequeue();

            for (var edge = childStart[state]; edge < childStart[state + 1]; edge++)
            {
                var child = childTarget[edge];
                var fallback = state == 0 ? 0 : Goto(failure[state], childClass[edge]);
                failure[child] = fallback;
                outputLink[child] = needleAt[fallback] >= 0 ? fallback : outputLink[fallback];
                queue.Enqueue(child);
            }
        }

        // number the root and its children first, then all other states depth-first
        var order = new List<int>(stateCount) { 0 };
        order.AddRange(childTarget[childStart[0]..childStart[1]]);
        var denseCount = order.Count;

        var stack = new Stack<int>();
        for (var i = denseCount - 1; i > 0; i--)
            PushChildren(order[i]);

        while (stack.Count > 0)
        {
            var state = stack.Pop();
            order.Add(state);
            PushChildren(state);
        }

        var number = new int[stateCount];
        for (var i = 0; i < order.Count; i++)
            number[order[i]] = i;

        int Encode(int state) =>
            (number[state] << 1) | (needleAt[state] >= 0 || outputLink[state] != 0 ? 1 : 0);

        var dense = new int[denseCount * this._classCount];
        for (var i = 0; i < denseCount; i++)
        {
            for (var charClass = 0; charClass < this._classCount; charClass++)
                dense[i * this._classCount + charClass] = Encode(Goto(order[i], (byte)charClass));
        }

        var sparse = new SparseState[stateCount - denseCount];
        var edges = new List<int>();
        for (var i = denseCount; i < stateCount; i++)
        {
            var state = order[i];
            sparse[i - denseCount] = new SparseState(
                edges.Count,
                childStart[state + 1] - childStart[state],
                number[failure[state]]);

            for (var edge = childStart[state]; edge < childStart[state + 1]; edge++)
                edges.Add((Encode(childTarget[edge]) << 8) | childClass[edge]);
        }

        var numberedNeedleAt = new int[stateCount];
        var numberedOutputLink = new int[stateCount];
        for (var state = 0; state < stateCount; state++)
        {
            numberedNeedleAt[number[state]] = needleAt[state];
            numberedOutputLink[number[state]] = number[outputLink[state]];
        }

        return (denseCount, dense, sparse, [.. edges], numberedNeedleAt, numberedOutputLink);

        // children are pushed in reverse, so that they are numbered in order
        void PushChildren(int state)
        {
            for (var edge = childStart[state + 1] - 1; edge >= childStart[state]; edge--)
                stack.Push(childTarget[edge]);
        }

        int Goto(int state, byte charClass)
        {
            while (true)
            {
                if (edgeOf.TryGetValue((state << 8) | charClass, out var next))
                    return next;

                if (state == 0)
                    return 0;

                state = failure[state];
            }
        }
    }

    /// <summary>
    /// A state below the first level of the automaton: its edges and its failure state.
    /// </summary>
    private readonly record struct SparseState(int FirstEdge, int EdgeCount, int Failure);
}

/// <summary>
/// The result of scanning an input with a <see cref="LiteralPrefilter"/>.
/// </summary>
internal readonly ref struct PrefilterScan(string input, ReadOnlySpan<ulong> foundNeedles, ReadOnlySpan<ulong> candidates, bool isAscii)
{
    public string Input { get; } = input;

    public ReadOnlySpan<ulong> FoundNeedles { get; } = foundNeedles;

    public ReadOnlySpan<ulong> Candidates { get; } = candidates;

    public bool IsAscii { get; } = isAscii;
}
