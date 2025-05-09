using AuthNetCore.DAL.IDAL;
using AuthNetCore.Data.Models.DTOs;
using AuthNetCore.Data.Models.Entities;
using AuthNetCore.Data.Models.EntityFrameworkModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthNetCore.DAL
{
    public partial class AccountService : IPasswordRecovery
    {
        public async Task<AccountDto> PasswordRecoveryAsync(string email)
        {
            try
            {
                var (accountId, accountDto) = await GetAccountDtoIdByEmail(email);
                string token = await GetAccountSessionTokenCompromisedAsync(accountId);

                if (!await SenderEMailRecipentAsync(email, accountId, token))
                    throw new Exception("Was not possible to send the email to the destination.");

                return ReturnAccountDtoObject(accountDto);
            }
            catch (Exception ex)
            {
                throw new Exception("Error during password recovery: " + ex.Message);
            }
        }

        public async Task<bool> ResetPasswordAsync(AccountResetPassword accountResetPassword)
        {
            using var transaction = await _authNetCoreDbContext.Database.BeginTransactionAsync();
            try
            {
                if (accountResetPassword.email == null ||
                    accountResetPassword.password == null ||
                    accountResetPassword.passwordConfirmation == null)
                    return false;

                var (id, accountDto) = await GetAccountDtoIdByEmail(accountResetPassword.email);
                if (accountDto.Id == 0) return false;
                await RemoveAccountCredentialsAsync(id);
                await StablishAccountCredentialsAsync(accountResetPassword.passwordConfirmation, id, accountDto);

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception("Error during password reset: " + ex.Message);
            }
        }

        #region FieldsValidation Helper Methods

        private void ValidateCredentialsNotNullOrEmpty(AccountRegistration accountRegistration)
        {

            if (string.IsNullOrEmpty(accountRegistration.Email))
                throw new Exception("Email is required to create an account.");

            if (string.IsNullOrEmpty(accountRegistration.Password) || string.IsNullOrEmpty(accountRegistration.ConfirmPassword))
                throw new Exception("Password and Confirm password are required to create an account.");

            if (!string.Equals(accountRegistration.Password, accountRegistration.ConfirmPassword, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Password and Confirm password are not the same.");
        }

        private void ValidateAccountDataNotNullOrEmpty(AccountRegistration accountRegistration)
        {
            if (string.IsNullOrEmpty(accountRegistration.Name))
                throw new Exception("Name is required to create an account.");

            if (string.IsNullOrEmpty(accountRegistration.LastName))
                throw new Exception("LastName is required to create an account.");

            if (accountRegistration.BirthDate == DateOnly.MinValue && accountRegistration.BirthDate > DateOnly.FromDateTime(DateTime.Now))
                throw new Exception("BirthDate must be greater than or equal to DateOnly.MinValue.");
        }

        #endregion

        #region DataBase Helper Methods

        private async Task<AccountAuth?> GetAccountAuthByAccountIdAsync(int accountId)
        {
            return await _authNetCoreDbContext
                         .AccountAuth
                         .FirstOrDefaultAsync(a => a.AccountId == accountId);
        }

        private async Task<Account?> GetAccountByEmailAsync(string email)
        {
            try
            {
                return await _authNetCoreDbContext
                    .Accounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Email == email);
            }
            catch (Exception ex)
            {
                throw new Exception("Error fetching account by email: " + ex.Message);
            }
        }

        private async Task<(int, Account)> GetAccountDtoIdByEmail(string email)
        {
            try
            {
                Account? accountDto = await _authNetCoreDbContext
                    .Accounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(acc => acc.Email == email);

                if (accountDto == null) return (0, new Account());
                return (accountDto.Id, accountDto);
            }
            catch (Exception ex)
            {
                throw new Exception("Error fetching account ID by email: " + ex.Message);
            }
        }

        private async Task<string> GetAccountSessionTokenCompromisedAsync(int id)
        {
            try
            {
                AccountSession? accountSessionData = await _authNetCoreDbContext
                    .AccountSessions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(accSData => accSData.AccountId == id);

                if (accountSessionData == null) return string.Empty;
                return accountSessionData.Token;
            }
            catch (Exception ex)
            {
                throw new Exception("Error fetching compromised session token: " + ex.Message);
            }
        }

        private async Task<List<AccountSession>> GetAccountSessionById(int id)
        {
            List<AccountSession> sessions = await _authNetCoreDbContext
                .AccountSessions
                .AsNoTracking()
                .Where(s => s.AccountId == id)
                .ToListAsync();

            return sessions;
        }

        private void RemoveAccountAuth(AccountAuth accountAuth)
        {
            _authNetCoreDbContext
                .AccountAuth
                .Remove(accountAuth);
        }

        private void RemoveAccountSessions(IEnumerable<AccountSession> accountSessions)
        {
            _authNetCoreDbContext
                .AccountSessions
                .RemoveRange(accountSessions);
        }

        #endregion
    }
}
