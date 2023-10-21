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

namespace UAParser.Extensions;

using System;

internal static class StringExtensions
{
    /// <summary>
    /// Replaces the first occurrence.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <param name="search">The search.</param>
    /// <param name="replacement">The replacement.</param>
    /// <returns>System.String.</returns>
    /// <exception cref="System.ArgumentNullException">input</exception>
    public static string ReplaceFirstOccurrence(this string input, string search, string replacement)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var index = input.IndexOf(search, StringComparison.Ordinal);
        return index >= 0
                   ? $"{input[..index]}{replacement}{input[(index + search.Length)..]}"
                   : input;
    }
}
