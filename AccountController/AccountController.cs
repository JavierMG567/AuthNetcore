using AuthNetCore.Utilities.BaseControllers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccountController
{
    public class AccountController : AuthNetCoreControllerBase<AccountController>
    {
        public AccountController(ILogger<AccountController> logger)
            : base(logger)
        {
            
        }
    }
}
