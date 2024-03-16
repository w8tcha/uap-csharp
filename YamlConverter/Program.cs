
using System.Text;
using System.Text.Json;

using UAParser;
using UAParser.Objects;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace YamlConverter;

/// <summary>
/// Class Program.
/// </summary>
public static class Program
{
    private static void Main()
    {
        var yamlContent = GetYamlContent(@"..\..\..\..\UAParser.Core\regexes.yaml");
        var selectors = FromYaml(yamlContent);

        var jsonString = JsonSerializer.Serialize(selectors);

        File.WriteAllText(@"..\..\..\..\UAParser.Core\regexes.json", jsonString);
    }

    /// <summary>
    /// Gets the content of the yaml.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>System.String.</returns>
    internal static string GetYamlContent(string file)
    {
        using var fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None);
        using var sr = new StreamReader(fs, Encoding.UTF8);

        return sr.ReadToEnd();
    }

    /// <summary>
    /// Returns a <see cref="Parser"/> instance based on the regex definitions in a json string
    /// </summary>
    /// <param name="yaml">a string containing yaml definitions of reg-ex</param>
    /// <returns>A <see cref="Parser"/> instance parsing user agent strings based on the regexes defined in the json string</returns>
    private static Selectors FromYaml(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance).Build();

        return deserializer.Deserialize<Selectors>(yaml);
    }
}
