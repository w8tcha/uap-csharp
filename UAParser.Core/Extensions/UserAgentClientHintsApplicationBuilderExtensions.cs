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

namespace UAParser.Extensions;

using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Extension methods for requesting User-Agent Client Hints from browsers.
/// </summary>
public static class UserAgentClientHintsApplicationBuilderExtensions
{
    private const string AcceptCh = "Accept-CH";

    private const string CriticalCh = "Critical-CH";

    /// <summary>
    /// Asks browsers to send the <c>Sec-CH-UA-Platform-Version</c> client hint, which
    /// <see cref="UserAgentParser"/> uses to detect Windows 11.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/> to add the middleware to.</param>
    /// <param name="critical">
    /// When <see langword="true"/> (the default), the hint is also sent as <c>Critical-CH</c>, so the
    /// browser retries the very first navigation with the hint included. Otherwise the hint is only
    /// available from the second request on.
    /// </param>
    /// <returns>The <see cref="IApplicationBuilder"/> so that additional calls can be chained.</returns>
    /// <remarks>
    /// Client hints are only sent over HTTPS and only by Chromium based browsers.
    /// </remarks>
    public static IApplicationBuilder UseUserAgentClientHints(this IApplicationBuilder app, bool critical = true)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                AddClientHintHeaders(context.Response.Headers, critical);
                return Task.CompletedTask;
            });

            return next(context);
        });
    }

    internal static void AddClientHintHeaders(IHeaderDictionary headers, bool critical)
    {
        headers.Append(AcceptCh, ClientHints.PlatformVersionHeader);

        if (critical)
        {
            headers.Append(CriticalCh, ClientHints.PlatformVersionHeader);
        }
    }
}
