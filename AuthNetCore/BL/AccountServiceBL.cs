using AuthNetCore.BL.IBL;
using AuthNetCore.DAL.IDAL;
using AuthNetCore.Data.Models.DTos;
using AuthNetCore.Data.Models.EModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthNetCore.BL
{
    public class AccountServiceBL : IAccountServiceBL
    {
        private readonly IAccountService _accountService;
        public AccountServiceBL(IAccountService accountService)
        {
            _accountService = accountService;
        }
        public async Task<AccountDto> AccountAuthenticateAsync(AccountLogin accountLogin)
        {
            try
            {
                AccountDto account = await _accountService.AccountAuthenticateAsync(accountLogin);
                return account;
            }
            catch (Exception)
            {
                throw new Exception();
            }
        }

        public async Task AccountDeleteAsync(string tokenString)
        {
            try
            {
                await _accountService.AccountDeleteAsync( tokenString );
            }
            catch(Exception)
            {
                throw new Exception();
            }
        }

        public async Task<AccountLogin> AccountRegisterAsync(AccountRegistration accountRegistration)
        {
            try
            {
                AccountLogin account = await _accountService.AccountRegisterAsync(accountRegistration);
                return account;
            }
            catch (Exception)
            {
                throw new Exception();
            }
        }

        public async Task<bool> RevokeTokenAsync(string token)
        {
            try
            {
                bool drawRevokeInSession = await _accountService.RevokeTokenAsync(token);
                return drawRevokeInSession;
            }
            catch (Exception)
            {
                throw new Exception();
            }
        }
    }
}
