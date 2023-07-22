namespace UAParser.Objects;

using System;
using System.Text.RegularExpressions;

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
