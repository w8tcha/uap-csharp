namespace UAParser.Extensions;

using System;

internal static class StringExtensions
{
    public static string ReplaceFirstOccurence(this string input, string search, string replacement)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        var index = input.IndexOf(search, StringComparison.Ordinal);
        return index >= 0
                   ? $"{input[..index]}{replacement}{input[(index + search.Length)..]}"
                   : input;
    }
}
