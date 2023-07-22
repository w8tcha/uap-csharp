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
using System.IO;
using System.Linq;

using YamlDotNet.RepresentationModel;

public class YamlParsing
{
    [Fact]
    public void can_parse_same_regexes_using_minimal_yaml_parser()
    {
        // read in the yaml file in fully functional yaml parser
        var yamlContent = this.GetTestResources("UAParser.Tests.Regexes.regexes.yaml");
        Assert.NotNull(yamlContent);
        Assert.NotEqual(string.Empty, yamlContent);

        var yaml = new YamlStream();
        yaml.Load(new StringReader(yamlContent));

        // read into the minimal parser
        var minimal = new MinimalYamlParser(yamlContent);

        var entries =
            from doc in yaml.Documents
            select doc.RootNode as YamlMappingNode into rn
            where rn != null
            from e in rn.Children
            select new
                       {
                           Key = e.Key as YamlScalarNode,
                           Value = e.Value as YamlSequenceNode
                       } into e
            where e.Key != null && e.Value != null
            select e;

        var config = entries.ToDictionary(e => e.Key.Value,
            e => e.Value,
            StringComparer.OrdinalIgnoreCase);

        foreach (var kvPair in config)
        {
            var configNode = kvPair.Value;
            var valueDic = from node in configNode ?? Enumerable.Empty<YamlNode>()
                           select node as YamlMappingNode
                           into node
                           where node != null
                           select node.Children
                               .Where(e => e is { Key: YamlScalarNode, Value: YamlScalarNode })
                               .GroupBy(e => e.Key.ToString(), e => e.Value.ToString(), StringComparer.OrdinalIgnoreCase)
                               .ToDictionary(e => e.Key, e => e.Last(), StringComparer.OrdinalIgnoreCase)
                           into cm
                           select cm;

            var name = kvPair.Key;
            var minimalLookupList = minimal.ReadMapping(name).ToList();
            var yamlDictionaryList = valueDic.ToList();

            Assert.Equal(yamlDictionaryList.Count, minimalLookupList.Count);
            for (var i = 0; i < yamlDictionaryList.Count; i++)
            {
                var yamlDic = yamlDictionaryList[i];
                var minimalLookup = minimalLookupList[i];

                foreach (var seqKVPair in yamlDic)
                {
                    Assert.True(minimalLookup.ContainsKey(seqKVPair.Key), seqKVPair.Key);
                    var lookupResult = minimalLookup[seqKVPair.Key];
                    Assert.Equal(seqKVPair.Value, lookupResult);
                }
            }
        }
    }
}
