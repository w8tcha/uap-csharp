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

internal static class InternalExtensions
{
    internal static List<Dictionary<string, string>> ConvertToDictionaryList(this YamlSequenceNode yamlNode)
    {
        return yamlNode.OfType<YamlMappingNode>().Select(ConvertToDictionary).ToList();
    }

    internal static Dictionary<string, string> ConvertToDictionary(this YamlMappingNode yamlNode)
    {
        var dic = new Dictionary<string, string>();
        foreach (var key in yamlNode.Children.Keys)
        {
            dic[key.ToString()] = yamlNode.Children[key].ToString();
        }

        return dic;
    }

    internal static string GetTestResources(this object _, string name)
    {
        using var s = typeof(TestResourceTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(
                $"Could not locate an embedded test resource with name: {name}");
        using var sr = new StreamReader(s, Encoding.UTF8);
        return sr.ReadToEnd();
    }
}
