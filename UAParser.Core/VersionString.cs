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
using System.Text;

internal static class VersionString
{
    public static string Format(params ReadOnlySpan<string> parts)
    {
        var builder = new StringBuilder();

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
                continue;

            if (builder.Length > 0)
                builder.Append('.');

            builder.Append(part);
        }

        return builder.ToString();
    }
}
