using AuthNetCore.Data.Models.DTos;
using AuthNetCore.Data.Models.EModels;
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
        Task AccountDeleteAsync(string tokenString);
    }
}
