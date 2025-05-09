using AuthNetCore.Data.Models.DTOs;
using AuthNetCore.Data.Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthNetCore.DAL.IDAL
{
    public interface IPasswordRecovery
    {
        Task<AccountDto> PasswordRecoveryAsync(string email);
        Task<bool> ResetPasswordAsync(AccountResetPassword accountResetPassword);
    }
}
