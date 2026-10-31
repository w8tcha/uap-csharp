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
using System.Globalization;

using Microsoft.AspNetCore.Http;

using UAParser.Objects;

/// <summary>
/// Support for User-Agent Client Hints, used to refine results that the
/// User-Agent string cannot express, such as telling Windows 11 apart from Windows 10.
/// </summary>
/// <remarks>
/// Windows 11 browsers still send <c>Windows NT 10.0</c> in the User-Agent string. Chromium based
/// browsers report the real version in the <c>Sec-CH-UA-Platform-Version</c> header, but only after
/// the server asked for it with an <c>Accept-CH</c> response header
/// (see <c>UseUserAgentClientHints</c>). A major platform version of 13 or higher means Windows 11.
/// See https://learn.microsoft.com/microsoft-edge/web-platform/how-to-detect-win11
/// </remarks>
public static class ClientHints
{
    /// <summary>The low-entropy hint carrying the platform name, e.g. <c>"Windows"</c>.</summary>
    public const string PlatformHeader = "Sec-CH-UA-Platform";

    /// <summary>The high-entropy hint carrying the platform version, e.g. <c>"15.0.0"</c>.</summary>
    public const string PlatformVersionHeader = "Sec-CH-UA-Platform-Version";

    private const string Windows = "Windows";

    /// <summary>The first <c>Sec-CH-UA-Platform-Version</c> major version reported by Windows 11.</summary>
    private const int FirstWindows11PlatformMajor = 13;

    /// <summary>
    /// Returns the client info with its OS refined by the client hints of the request, or the
    /// unchanged client info when the request carries no applicable hints.
    /// </summary>
    internal static ClientInfo Apply(ClientInfo clientInfo, IHeaderDictionary headers)
    {
        if (clientInfo is null || !IsWindows11(headers))
        {
            return clientInfo;
        }

        // Only upgrade what the User-Agent string reports as Windows 10, so a hint can never turn
        // an unrelated or older OS (or a spoofed User-Agent string) into Windows 11.
        var os = clientInfo.OS;
        if (os is not { Family: Windows, Major: "10" })
        {
            return clientInfo;
        }

        return new ClientInfo(
            clientInfo.String,
            new OS(os.Family, "11", null, null, null),
            clientInfo.Device,
            clientInfo.Browser);
    }

    /// <summary>
    /// Whether the client hints identify the platform as Windows 11.
    /// </summary>
    internal static bool IsWindows11(IHeaderDictionary headers) =>
        string.Equals(Unquote(headers[PlatformHeader]), Windows, StringComparison.OrdinalIgnoreCase)
        && TryGetMajorVersion(Unquote(headers[PlatformVersionHeader]), out var major)
        && major >= FirstWindows11PlatformMajor;

    private static bool TryGetMajorVersion(string version, out int major)
    {
        major = 0;

        if (string.IsNullOrEmpty(version))
        {
            return false;
        }

        var dot = version.IndexOf('.');
        var majorPart = dot < 0 ? version : version[..dot];

        return int.TryParse(majorPart, NumberStyles.None, CultureInfo.InvariantCulture, out major);
    }

    /// <summary>Client hint values are structured-header strings, i.e. wrapped in double quotes.</summary>
    private static string Unquote(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        value = value.Trim();

        return value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
    }
}
