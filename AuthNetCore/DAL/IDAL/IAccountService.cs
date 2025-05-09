using AuthNetCore.Data.Models.DTOs;
using AuthNetCore.Data.Models.Entities;
using AuthNetCore.Data.Models.EntityFrameworkModels;
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
        Task<bool> RevokeTokenAsync(string token);
    }
}
