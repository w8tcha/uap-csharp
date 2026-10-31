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

using System.Threading;

/// <summary>
/// A regular expression that starts out interpreted and is replaced by a compiled one in the
/// background once it has been used often.
/// </summary>
/// <remarks>
/// Most patterns are rarely or never evaluated, so compiling all of them up front would be wasted
/// effort, while the few frequently used ones benefit from compilation. Compiled and interpreted
/// regular expressions produce the same results.
/// </remarks>
internal sealed class TieredRegex
{
    /// <summary>
    /// The number of uses after which the regular expression is compiled.
    /// </summary>
    internal const int CompileThreshold = 256;

    private const int Counting = 0;

    private const int Compiling = 1;

    private Regex _regex;

    private int _uses;

    private int _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="TieredRegex"/> class.
    /// </summary>
    /// <param name="regex">The regular expression.</param>
    /// <param name="compileWhenFrequentlyUsed">Whether to compile the regular expression once it is used often.</param>
    public TieredRegex(Regex regex, bool compileWhenFrequentlyUsed)
    {
        this._regex = regex;
        this._state = compileWhenFrequentlyUsed && (regex.Options & RegexOptions.Compiled) == 0 ? Counting : Compiling;
    }

    /// <summary>
    /// Gets whether the regular expression in use is compiled.
    /// </summary>
    internal bool IsCompiled => (Volatile.Read(ref this._regex).Options & RegexOptions.Compiled) != 0;

    public Match Match(string input)
    {
        var regex = Volatile.Read(ref this._regex);

        // the counter is not updated atomically: lost updates only delay the compilation
        if (this._state == Counting && ++this._uses >= CompileThreshold
            && Interlocked.CompareExchange(ref this._state, Compiling, Counting) == Counting)
        {
            ThreadPool.UnsafeQueueUserWorkItem(static tiered => tiered.Compile(), this, preferLocal: false);
        }

        return regex.Match(input);
    }

    private void Compile()
    {
        var regex = this._regex;

        try
        {
            var compiled = new Regex(regex.ToString(), regex.Options | RegexOptions.Compiled, regex.MatchTimeout);
            Volatile.Write(ref this._regex, compiled);
        }
        catch
        {
            // keep using the interpreted regular expression
        }
    }
}
