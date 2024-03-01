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

namespace UAParser.Objects;

using System;

/// <summary>
/// Options available for the parser
/// </summary>
public sealed class ParserOptions
{
    /// <summary>
    /// If true, will use compiled regular expressions for slower startup time
    /// but higher throughput. The default is false.
    /// </summary>
    public bool UseCompiledRegex { get; set; }

    /// <summary>
    /// Allows for specifying the maximum time spent on regular expressions,
    /// serving as a fail safe for potential infinite backtracking. The default is
    /// set to Regex.InfiniteMatchTimeout
    /// </summary>
    public TimeSpan MatchTimeOut { get; set; } = Regex.InfiniteMatchTimeout;
}
