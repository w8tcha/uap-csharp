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

using System.Reflection;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

// Needed for DeviceDetector.NET
// https://github.com/totpero/DeviceDetector.NET/issues/44

var config = ManualConfig.Create(DefaultConfig.Instance)
    .WithOptions(ConfigOptions.DisableOptimizationsValidator);

BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args, config);

// Run dotnet run -c Release -- --job short --filter *LibraryComparisonBenchmarks*
