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

/// <summary>
/// Represents a user agent, commonly a browser
/// </summary>
public sealed class Browser
{
    /// <summary>
    /// Construct a UserAgent instance
    /// </summary>
    public Browser(string family, string major, string minor, string patch)
    {
        this.Family = family;
        this.Major = major;
        this.Minor = minor;
        this.Patch = patch;
        this.Version = VersionString.Format(this.Major, this.Minor, this.Patch);
    }

    /// <summary>
    /// The family of user agent
    /// </summary>
    public string Family { get; }

    /// <summary>
    /// Major version of the user agent, if available
    /// </summary>
    public string Major { get; }

    /// <summary>
    /// Minor version of the user agent, if available
    /// </summary>
    public string Minor { get; }

    /// <summary>
    /// Patch version of the user agent, if available
    /// </summary>
    public string Patch { get; }

    /// <summary>
    /// Gets the version.
    /// </summary>
    /// <value>The version.</value>
    public string Version { get; }

    /// <summary>
    /// The user agent as a readable string
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return $"{this.Family}{(!string.IsNullOrEmpty(this.Version) ? $" {this.Version}"
                                    : null)}";
    }
}
