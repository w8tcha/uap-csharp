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

namespace UAParser.Objects;

using UAParser.Interfaces;

/// <summary>
/// Represents the user agent client information resulting from parsing
/// a user agent string
/// </summary>
public class ClientInfo : IUAParserOutput
{
    /// <summary>
    /// The user agent string, the input for the UAParser
    /// </summary>
    public string String { get; }

    // ReSharper disable once InconsistentNaming
    /// <summary>
    /// The OS parsed from the user agent string
    /// </summary>

    // ReSharper disable once InconsistentNaming
    public OS OS { get; }

    /// <summary>
    /// The Device parsed from the user agent string
    /// </summary>
    public Device Device { get; }

    // ReSharper disable once InconsistentNaming
    /// <summary>
    /// The User Agent parsed from the user agent string
    /// </summary>
    public Browser Browser { get; }

    /// <summary>
    /// Constructs an instance of the ClientInfo with results of the user agent string parsing
    /// </summary>
    public ClientInfo(string inputString, OS os, Device device, Browser userAgent)
    {
        this.String = inputString;
        this.OS = os;
        this.Device = device;
        this.Browser = userAgent;
    }

    /// <summary>
    /// A readable description of the user agent client information
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return $"{this.OS} {this.Device} {this.Browser}";
    }
}
