namespace UAParser;

using System.Linq;

internal static class VersionString
{
    public static string Format(params string[] parts)
    {
        return string.Join(".", parts.Where(v => !string.IsNullOrEmpty(v)).ToArray());
    }
}
