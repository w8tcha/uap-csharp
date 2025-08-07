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

namespace UAParser.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using YamlDotNet.RepresentationModel;

#pragma warning disable IDE1006 // Naming Styles
public class TestResourceTests
{
    [Fact]
    public void can_run_device_tests()
    {
        this.RunTests(
            "UAParser.Tests.TestResources.test_device.yaml",
            DeviceYamlTestCase.ReadFromMap);
    }

    [Fact]
    public void can_run_additional_os_tests()
    {
        this.RunTests(
            "UAParser.Tests.TestResources.additional_os_tests.yaml",
            OSYamlTestCase.ReadFromMap);
    }

    [Fact]
    public void can_run_firefox_user_agent_string_tests()
    {
        this.RunTests(
            "UAParser.Tests.TestResources.firefox_user_agent_strings.yaml",
            UserAgentYamlTestCase.ReadFromMap);
    }

    [Fact]
    public void can_run_pgts_browser_list_tests()
    {
        this.RunTests(
            "UAParser.Tests.TestResources.pgts_browser_list.yaml",
            UserAgentYamlTestCase.ReadFromMap);
    }

    [Fact]
    public void can_run_user_agent_parser_tests()
    {
        this.RunTests(
            "UAParser.Tests.TestResources.test_ua.yaml",
            UserAgentYamlTestCase.ReadFromMap);
    }

    [Fact]
    public void can_run_user_agent_parser_os_tests()
    {
        this.RunTests("UAParser.Tests.TestResources.test_os.yaml", OSYamlTestCase.ReadFromMap);
    }

    internal void RunTests<TTestCase>(
        string resourceName,
        Func<Dictionary<string, string>, TTestCase> testCaseFunction)
        where TTestCase : YamlTestCase
    {
        var testCases = this.GetTestCases(resourceName, "test_cases", testCaseFunction);

        RunTestCases(testCases);
    }

    private static void RunTestCases<TTestCase>(List<TTestCase> testCases)
        where TTestCase : YamlTestCase
    {
        var parser = Parser.GetDefault();
        Assert.NotEmpty(testCases);

        var sb = new StringBuilder();
        for (var i = 0; i < testCases.Count; i++)
        {
            var tc = testCases[i];
            if (tc is null)
                continue;

            var clientInfo = parser.Parse(tc.UserAgent);
            try
            {
                tc.Verify(clientInfo);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"test case {i + 1}: {ex.Message}");
            }
        }

        Assert.True(0 == sb.Length, $"Failed tests: {Environment.NewLine}{sb}");
    }

    public List<TTestCase> GetTestCases<TTestCase>(
        string resourceName,
        string yamlNodeName,
        Func<Dictionary<string, string>, TTestCase> testCaseFunction)
    {
        var yamlContent = this.GetTestResources(resourceName);
        var yaml = new YamlStream();
        yaml.Load(new StringReader(yamlContent));

        // reading overall configurations
        var regexConfigNode = (YamlMappingNode)yaml.Documents[0].RootNode;
        var regexConfig = regexConfigNode.Children.ToDictionary(
            entry => ((YamlScalarNode)entry.Key).Value,
            entry => entry.Value);

        var testCasesNode = (YamlSequenceNode)regexConfig[yamlNodeName];
        var testCases = testCasesNode.ConvertToDictionaryList().Select(
            configMap => !configMap.ContainsKey("js_ua")
                             ? // we deliberately skip tests with js-user agents
                             testCaseFunction(configMap)
                             : default).ToList();
        return testCases;
    }
}

#pragma warning restore IDE1006 // Naming Styles
