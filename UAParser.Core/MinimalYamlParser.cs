//
// Copyright 2014 Atif Aziz
// Portions Copyright 2012 Søren Enemærke
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

namespace UAParser;

using System;
using System.Collections.Generic;

/// <summary>
/// Just enough string parsing to recognize the regexes.yaml file format. Introduced to remove
/// dependency on large Yaml parsing lib. Note that a unittest ensures compatibility
/// by ensuring regexes and properties are read similar to using the full yaml lib
/// </summary>
internal class MinimalYamlParser
{
    internal class Mapping
    {
        private Dictionary<string, string> _lastEntry;

        public Mapping()
        {
            this.Sequences = new List<Dictionary<string, string>>();
        }

        public List<Dictionary<string, string>> Sequences { get; }

        public void BeginSequence()
        {
            this._lastEntry = new Dictionary<string, string>();
            this.Sequences.Add(this._lastEntry);
        }

        public void AddToSequence(string key, string value)
        {
            this._lastEntry[key] = value;
        }
    }

    private readonly Dictionary<string, Mapping> _mappings = new();

    public MinimalYamlParser(string yamlString)
    {
        this.ReadIntoMappingModel(yamlString);
    }

    internal IDictionary<string, Mapping> Mappings => this._mappings;

    private void ReadIntoMappingModel(string yamlInputString)
    {
        // line splitting using various splitting characters
        var lines = yamlInputString.Split(new[] { Environment.NewLine, "\r", "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        var lineCount = 0;
        Mapping activeMapping = null;

        foreach (var line in lines)
        {
            lineCount++;
            if (line.Trim().StartsWith("#")) // skipping comments
                continue;
            if (line.Trim().Length == 0)
                continue;

            // is this a new mapping entity
            if (line[0] != ' ')
            {
                var indexOfMappingColon = line.IndexOf(':');
                if (indexOfMappingColon == -1)
                    throw new ArgumentException(
                        $"YamlParsing: Expecting mapping entry to contain a ':', at line {lineCount}");
                var name = line[..indexOfMappingColon].Trim();
                activeMapping = new Mapping();
                this._mappings.Add(name, activeMapping);
                continue;
            }

            // reading scalar entries into the active mapping
            if (activeMapping == null)
                throw new ArgumentException(
                    $"YamlParsing: Expecting mapping entry to contain a ':', at line {lineCount}");

            var seqLine = line.Trim();
            if (seqLine[0] == '-')
            {
                activeMapping.BeginSequence();
                seqLine = seqLine[1..];
            }

            var indexOfColon = seqLine.IndexOf(':');
            if (indexOfColon == -1)
                throw new ArgumentException(
                    $"YamlParsing: Expecting scalar mapping entry to contain a ':', at line {lineCount}");

            var key = seqLine[..indexOfColon].Trim();
            var value = ReadQuotedValue(seqLine[(indexOfColon + 1)..].Trim());
            activeMapping.AddToSequence(key, value);
        }
    }

    private static string ReadQuotedValue(string value)
    {
        if (value.StartsWith("'") && value.EndsWith("'"))
            return value[1..^1];
        if (value.StartsWith("\"") && value.EndsWith("\""))
            return value[1..^1];
        return value;
    }

    public IEnumerable<Dictionary<string, string>> ReadMapping(string mappingName)
    {
        if (!this._mappings.TryGetValue(mappingName, out var mapping))
        {
            yield break;
        }

        foreach (var s in mapping.Sequences)
        {
            yield return s;
        }
    }
}
