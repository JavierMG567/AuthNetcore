using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AuthNetCore.Utilities.BaseControllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class AuthNetCoreControllerBase<TController> : ControllerBase
    {
        protected readonly ILogger<TController> _logger;

        public AuthNetCoreControllerBase(ILogger<TController> logger)
        {
            _logger = logger;
        }
    }
}
