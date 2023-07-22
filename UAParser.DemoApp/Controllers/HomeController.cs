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

namespace UAParser.DemoApp.Controllers;

using System;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using UAParser.Interfaces;

[ApiController]
[Route("[controller]")]
public class HomeController : ControllerBase
{
    private readonly ILogger<HomeController> logger;
    
    private readonly IUserAgentParser userAgentParser;

    public HomeController(IUserAgentParser parser, ILogger<HomeController> logging)
    {
        this.logger = logging ?? throw new ArgumentNullException(nameof(logging));
        this.userAgentParser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    [HttpGet]
    public string Get()
    {
        this.logger.LogInformation("Inside GET action method");

        var clientInfo = this.userAgentParser.ClientInfo;

        var userAgent = this.HttpContext.Request.Headers["User-Agent"];

        return
            $".NET 7.0 APP. Browser:{clientInfo.Browser.Family}, Version: {clientInfo.Browser.Version}, Device type: {clientInfo.Device.Family}, OS: {clientInfo.OS}, Original User-Agent: {userAgent}";
    }
}
