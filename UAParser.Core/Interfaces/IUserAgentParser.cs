namespace UAParser.Interfaces;

/// <summary>
/// An abstraction to get browser information.
/// </summary>
public interface IUserAgentParser
{
    public IUAParserOutput ClientInfo { get; }
}
