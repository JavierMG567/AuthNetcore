using AuthNetCore.Data.Models.DTOs;
using AuthNetCore.Data.Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthNetCore.BL.IBL
{
    public interface IAccountServiceBL
    {
        Task<(AccountDto, string)> AccountRegisterAsync(AccountRegistration accountRegistration);
        Task<(AccountDto, string)> AccountAuthenticateAsync(AccountLogin accountLogin);
        Task<bool> RevokeTokenAsync(string token);
        Task<AccountDto> PasswordRecoveryAsync(string email);
        Task AccountDeleteAsync(string tokenString);
    }
}
