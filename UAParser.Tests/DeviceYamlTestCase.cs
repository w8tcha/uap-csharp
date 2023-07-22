namespace UAParser.Tests;

using System.Collections.Generic;

using Xunit;

using UAParser.Objects;

public class DeviceYamlTestCase : YamlTestCase
{
    public static DeviceYamlTestCase ReadFromMap(Dictionary<string, string> map)
    {
        var tc = new DeviceYamlTestCase
                     {
                         UserAgent = map["user_agent_string"],
                         Family = map["family"],

                     };
        return tc;
    }

    public string Family { get; set; }

    public override void Verify(ClientInfo clientInfo)
    {
        Assert.NotNull(clientInfo);
        this.AssertMatch(this.Family, clientInfo.Device.Family, "Family");
    }
}

public class OSYamlTestCase : YamlTestCase
{
    public static OSYamlTestCase ReadFromMap(Dictionary<string, string> map)
    {
        var tc = new OSYamlTestCase
                     {
                         UserAgent = map["user_agent_string"],
                         Family = map["family"],
                         Major = map["major"],
                         Minor = map["minor"],
                         Patch = map["patch"],
                         PatchMinor = map["patch_minor"]
                     };
        return tc;
    }

    public string Family { get; set; }
    public string Major { get; set; }
    public string Minor { get; set; }
    public string Patch { get; set; }
    public string PatchMinor { get; set; }

    public override void Verify(ClientInfo clientInfo)
    {
        Assert.NotNull(clientInfo);
        this.AssertMatch(this.Family, clientInfo.OS.Family, "Family");
        this.AssertMatch(this.Major, clientInfo.OS.Major, "Major");
        this.AssertMatch(this.Minor, clientInfo.OS.Minor, "Minor");
        this.AssertMatch(this.Patch, clientInfo.OS.Patch, "Patch");
        this.AssertMatch(this.PatchMinor, clientInfo.OS.PatchMinor, "PatchMinor");

    }
}

public class UserAgentYamlTestCase : YamlTestCase
{
    public static UserAgentYamlTestCase ReadFromMap(Dictionary<string, string> map)
    {
        var tc = new UserAgentYamlTestCase
                     {
                         UserAgent = map["user_agent_string"],
                         Family = map["family"],
                         Major = map["major"],
                         Minor = map["minor"],
                         Patch = map["patch"],
                     };
        return tc;
    }

    public string Family { get; set; }
    public string Major { get; set; }
    public string Minor { get; set; }
    public string Patch { get; set; }

    public override void Verify(ClientInfo clientInfo)
    {
        Assert.NotNull(clientInfo);
        this.AssertMatch(this.Family, clientInfo.Browser.Family, "Family");
        this.AssertMatch(this.Major, clientInfo.Browser.Major, "Major");
        this.AssertMatch(this.Minor, clientInfo.Browser.Minor, "Minor");
        this.AssertMatch(this.Patch, clientInfo.Browser.Patch, "Patch");

    }
}
