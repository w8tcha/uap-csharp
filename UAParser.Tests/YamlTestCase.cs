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

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public abstract class YamlTestCase
{
    public string UserAgent { get; set; }

    public abstract void Verify(ClientInfo clientInfo);

    protected void AssertMatch<T>(T expected, T actual, string type)
    {
        if (typeof(T) == typeof(string))
        {
            var exp = expected as string;
            var act = actual as string;

            if (string.IsNullOrEmpty(exp) && string.IsNullOrEmpty(act))
                return;
        }

        Assert.True(expected.Equals(actual),
            $"{type} did not match. (expected:{expected} actual:{actual})  in {this.UserAgent}");
    }
}
