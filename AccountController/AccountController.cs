using AuthNetCore.BL.IBL;
using AuthNetCore.Data.Models.DTos;
using AuthNetCore.Data.Models.EModels;
using AuthNetCore.Utilities.BaseControllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AccountController
{
    [ApiVersion("1.0")]
    public class AccountController : AuthNetCoreControllerBase<AccountController>
    {
        private readonly IAccountServiceBL _accountServiceBL;
        public AccountController(ILogger<AccountController> logger, IAccountServiceBL accountServiceBL)
            : base(logger)
        {
            _accountServiceBL = accountServiceBL;
        }

        [HttpPost("CustomerRegistration")]
        [SwaggerOperation(
            Summary = "",
            Description = "")]
        [SwaggerResponse((int)HttpStatusCode.OK, "Succeded.")]
        [SwaggerResponse((int)HttpStatusCode.BadRequest, ".")]
        [SwaggerResponse((int)HttpStatusCode.InternalServerError, ".")]
        public async Task<ActionResult> AccountRegistrationAsync([FromBody] AccountRegistration accountRegistration)
        {
            if (accountRegistration == null)
            {
                return BadRequest("Invalid registration details.");
            }

            try
            {
                var (accountRegistered, token) = await _accountServiceBL.AccountRegisterAsync(accountRegistration);
                return Ok(accountRegistered);
            }
            catch (System.Exception)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while processing the registration.");
            }
        }

        [HttpPost("CustomerLoginAcces")]
        [SwaggerOperation(
            Summary = "",
            Description = ".")]
        [SwaggerResponse((int)HttpStatusCode.OK, "Succeded.")]
        [SwaggerResponse((int)HttpStatusCode.BadRequest, ".")]
        [SwaggerResponse((int)HttpStatusCode.InternalServerError, ".")]
        public async Task<ActionResult> AccountLoginAccessAsync(AccountLogin accountLogin)
        {
            if (accountLogin == null)
            {
                return BadRequest("Invalid login details.");
            }

            try
            {
                var (accountLoged , token) = await _accountServiceBL.AccountAuthenticateAsync(accountLogin);

                if (accountLoged == null)
                {
                    return BadRequest("Invalid login credentials.");
                }

                return Ok(accountLoged);
            }
            catch (System.Exception)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while processing the login.");
            }
        }

        [HttpDelete("CustomerDeleteAcces")]
        [SwaggerOperation(
            Summary = "",
            Description = ".")]
        [SwaggerResponse((int)HttpStatusCode.OK, "Succeded.")]
        [SwaggerResponse((int)HttpStatusCode.BadRequest, ".")]
        [SwaggerResponse((int)HttpStatusCode.InternalServerError, ".")]
        public async Task<ActionResult> AccountDeleteAsync([FromQuery] string tokenString)
        {
            if (string.IsNullOrEmpty(tokenString))
            {
                return BadRequest("Token is required.");
            }

            try
            {
                await _accountServiceBL.AccountDeleteAsync(tokenString);
                return Ok("Account successfully deleted.");
            }
            catch (System.Exception)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while processing the account deletion.");
            }
        }

        [HttpPost("CustomerLogoutAcces")]
        [SwaggerOperation(
            Summary = "",
            Description = ".")]
        [SwaggerResponse((int)HttpStatusCode.OK, "Succeded.")]
        [SwaggerResponse((int)HttpStatusCode.BadRequest, ".")]
        [SwaggerResponse((int)HttpStatusCode.InternalServerError, ".")]
        public async Task<ActionResult> AccountLogoutAsync([FromQuery] string tokenString)
        {
            if (string.IsNullOrEmpty(tokenString))
            {
                return BadRequest("Token is required.");
            }

            try
            {
                bool isRevoked = await _accountServiceBL.RevokeTokenAsync(tokenString);

                if (isRevoked)
                {
                    return Ok("Successfully logged out.");
                }

                return BadRequest("Token is invalid or already revoked.");
            }
            catch (System.Exception)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while processing the logout.");
            }
        }
    }
}
