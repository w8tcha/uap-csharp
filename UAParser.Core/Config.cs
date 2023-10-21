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

namespace UAParser;

using System;
using System.Text.RegularExpressions;

using UAParser.Objects;

/// <summary>
/// Class Config.
/// </summary>
internal class Config
{
    /// <summary>
    /// The options
    /// </summary>
    // ReSharper disable once InconsistentNaming
    private readonly ParserOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="Config"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    internal Config(ParserOptions options)
    {
        this._options = options;
    }

    // ReSharper disable once InconsistentNaming
    public Func<string, OS> OSSelector(OSSelector selector)
    {
        var regex = this.Regex(selector.regex, "OS");
        var os = selector.os_replacement;
        var v1 = selector.os_v1_replacement;
        var v2 = selector.os_v2_replacement;
        var v3 = selector.os_v3_replacement;
        var v4 = selector.os_v4_replacement;
        return Parsers.OS(regex, os, v1, v2, v3, v4);
    }

    public Func<string, Browser> UserAgentSelector(UserAgentSelector selector)
    {
        var regex = this.Regex(selector.regex, "User agent");
        var family = selector.family_replacement;
        var v1 = selector.v1_replacement;
        var v2 = selector.v2_replacement;
        var v3 = selector.v3_replacement;
        return Parsers.UserAgent(regex, family, v1, v2, v3);
    }

    public Func<string, Device> DeviceSelector(DeviceSelector selector)
    {
        var regex = this.Regex(selector.regex, "Device", selector.regex_flag);
        var device = selector.device_replacement;
        var brand = selector.brand_replacement;
        var model = selector.model_replacement;
        return Parsers.Device(regex, device, brand, model);
    }

    private Regex Regex(string regex, string key, string regexFlag = null)
    {
        var pattern = regex
                      ?? throw new ArgumentNullException(
                          $"{key} is missing regular expression specification.");

        if (pattern.Contains(@"\_"))
        {
            pattern = pattern.Replace(@"\_", "_");
        }

        // Singleline: User agent strings do not contain newline characters. RegexOptions.Singleline improves performance.
        // CultureInvariant: The interpretation of a user agent never depends on the current locale.
        var options = RegexOptions.Singleline | RegexOptions.CultureInvariant;

        if (regexFlag is not null)
        {
            options |= RegexOptions.IgnoreCase;
        }

        if (this._options.UseCompiledRegex)
        {
            options |= RegexOptions.Compiled;
        }

        return new Regex(pattern, options, this._options.MatchTimeOut);
    }
}
