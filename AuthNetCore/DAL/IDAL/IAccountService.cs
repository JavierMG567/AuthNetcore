using AuthNetCore.Data.Models.DTos;
using AuthNetCore.Data.Models.EModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthNetCore.DAL.IDAL
{
    public interface IAccountService
    {
        Task<(AccountDto, string)> AccountAuthenticateAsync(AccountLogin accountLogin);
        Task<(AccountDto?, string)> AccountRegisterAsync(AccountRegistration accountRegistration);
        Task AccountDeleteAsync(string tokenString);
        Task<AccountDto> PasswordRecoveryAsync(string email);
        Task<bool> ResetPasswordAsync(AccountResetPassword accountResetPassword);
        Task<bool> RevokeTokenAsync(string token);
    }
}
