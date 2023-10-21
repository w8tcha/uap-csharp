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

namespace UAParser.Benchmarks;

using System.Collections.Generic;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

using DeviceDetectorNET;

using MyCSharp.HttpUserAgentParser;
using MyCSharp.HttpUserAgentParser.Providers;

using UAParser.Objects;

[ShortRunJob]
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class LibraryComparisonBenchmarks
{
    public record TestData(string Label, string UserAgent)
    {
        public override string ToString() => this.Label;
    }

    [ParamsSource(nameof(GetTestUserAgents))]
    public TestData Data { get; set; }

    public static IEnumerable<TestData> GetTestUserAgents()
    {
        yield return new TestData("Chrome Win10", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/90.0.4430.212 Safari/537.36");
        yield return new TestData("Google-Bot", "APIs-Google (+https://developers.google.com/webmasters/APIs-Google.html)");
    }

    [Benchmark(Baseline = true, Description = "MyCSharp")]
    [BenchmarkCategory("Basic")]
    public HttpUserAgentInformation MyCSharpBasic()
    {
        var info = HttpUserAgentParser.Parse(this.Data.UserAgent);
        return info;
    }

    private static readonly HttpUserAgentParserCachedProvider MyCSharpCachedProvider = new();

    [Benchmark(Baseline = true, Description = "MyCSharp")]
    [BenchmarkCategory("Cached")]
    public HttpUserAgentInformation MyCSharpCached()
    {
        return MyCSharpCachedProvider.Parse(this.Data.UserAgent);
    }

    [Benchmark(Description = "UAParser")]
    [BenchmarkCategory("Basic")]
    public async Task<ClientInfo> UaParserBasicAsync()
    {
        var parser = await Parser.GetDefaultAsync();

        var info = await parser.ParseAsync(this.Data.UserAgent);

        return info;
    }

    private static readonly Parser UaParser = Parser.GetDefault(new ParserOptions { UseCompiledRegex = true });

    [Benchmark(Description = "UAParser")]
    [BenchmarkCategory("Cached")]
    public async Task<ClientInfo> UaParserCachedAsync()
    {
        var info = await UaParser.ParseAsync(this.Data.UserAgent, true);
        return info;
    }

    [Benchmark(Description = "DeviceDetector.NET")]
    [BenchmarkCategory("Basic")]
    public object DeviceDetectorNetBasic()
    {
        DeviceDetector dd = new(this.Data.UserAgent);
        dd.Parse();

        var info = new
                       {
                           Client = dd.GetClient(),
                           OS = dd.GetOs(),
                           Device = dd.GetDeviceName(),
                           Brand = dd.GetBrandName(),
                           Model = dd.GetModel()
                       };

        return info;
    }
}
