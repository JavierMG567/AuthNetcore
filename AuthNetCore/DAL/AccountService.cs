using AuthNetCore.DAL.IDAL;
using AuthNetCore.Data.Access;
using AuthNetCore.Data.Models.DTos;
using AuthNetCore.Data.Models.EModels;
using AuthNetCore.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace AuthNetCore.DAL
{
    public class AccountService : IAccountService
    {
        private readonly AuthNetCoreDbContext _authNetCoreDbContext;
        private readonly JwtSettings _jwtSettings;

        public AccountService(
            AuthNetCoreDbContext authNetCoreDbContext,
            IOptions<JwtSettings> jwtSettings)
        {
            _authNetCoreDbContext = authNetCoreDbContext;
            _jwtSettings = jwtSettings.Value;
        }

        public async Task<AccountDto> AccountAuthenticateAsync(AccountLogin accountLogin)
        {
            AccountDto? account = await GetAccountByEmailAsync(accountLogin.Email);
            if (account == null || account.IsLocked) return new AccountDto();
            AccountAuth? auth = await GetAccountAuthByAccountIdAsync(account.Id);
            
            if (auth == null || !SecurityHelper.VerifyPassword(
                accountLogin.Password, 
                auth.PasswordHash, 
                auth.PasswordSalt)
            )
            {
                await HandleFailedLoginAttemptAsync(account);
                return new AccountDto();
            }

            return await HandleSuccessfulLoginAsync(account);
        }

        public async Task<AccountDto> AccountRegisterAsync(AccountRegistration accountRegistration)
        {
            AccountDto account = await CreateAccountAsync(accountRegistration);
            await StablishAccountCredentialsAsync(accountRegistration.Password, account.Id, account);
            return account;
        }

        public async Task AccountDeleteAsync(string tokenString)
        {
            int accountId = GetAccountIdFromToken(tokenString);
            AccountDto account = await _authNetCoreDbContext
                .Account
                .FindAsync(accountId) ?? throw new Exception("Account not found.");
            
            _authNetCoreDbContext.Account.Remove(account);
            await _authNetCoreDbContext.SaveChangesAsync();
        }

        public async Task<bool> RevokeTokenAsync(string token)
        {
            AccountSessionsDto? session = await _authNetCoreDbContext
                .AccountSessions
                .FirstOrDefaultAsync(acc => acc.Token == token);
            
            if (session == null || session.IsRevoked) return false;

            session.IsRevoked = true;
            await _authNetCoreDbContext.SaveChangesAsync();
            return true;
        }

        public async Task<AccountDto> PasswordRecoveryAsync(string email)
        {
            var (accountId, accountDto) = await GetAccountDtoIdByEmail(email);
            string token = await GetAccountSessionTokenCompromisedAsync(accountId);
            await SenderEMailRecipentAsync(email, accountId, token);
            return accountDto;
        }

        public async Task<bool> ResetPasswordAsync(AccountResetPassword accountResetPassword)
        {
            if( accountResetPassword.email == null || 
                accountResetPassword.password == null || 
                accountResetPassword.passwordConfirmation == null) return false;

            var (id, accountDto) = await GetAccountDtoIdByEmail(accountResetPassword.email);
            if (accountDto == null) return false;

            AccountSessionsDto accountSessions = await GetAccountSessionById(id);
            if (accountSessions == null) return false;

            RemoveAccountSession(accountSessions);
            AccountAuth? accountAuth = await GetAccountAuthByAccountIdAsync(id);
            if (accountAuth == null) return false;

            RemoveAccountAuth(accountAuth);
            await StablishAccountCredentialsAsync(accountResetPassword.passwordConfirmation, id, accountDto);

            return true;
        }

        #region Helper Methods

        private async Task<AccountDto> HandleSuccessfulLoginAsync(AccountDto account)
        {
            string token = GenerateJwtToken(account);
            await CreateAccountSessionAsync(account.Id, token);
            ResetFailedLoginAttempts(account);

            return account;
        }

        private async Task HandleFailedLoginAttemptAsync(AccountDto account)
        {
            account.FailedLoginAttempts++;
            if (account.FailedLoginAttempts >= 3)
            {
                await LockAccountAsync(account);
            }
            await _authNetCoreDbContext.SaveChangesAsync();
        }

        private async Task LockAccountAsync(AccountDto account)
        {
            AccountSessionsDto? session = await _authNetCoreDbContext.AccountSessions
                .Where(a => a.AccountId == account.Id && !a.IsRevoked)
                .FirstOrDefaultAsync();

            if (session != null)
            {
                await AddingTokenToBlackListAsync(session.Id, session.Token);
            }

            account.IsLocked = true;
        }

        private async Task StablishAccountCredentialsAsync(string password, int accountId, AccountDto accountDto)
        {
            await CreateAccountAuthAsync(password, accountId);
            string token = GenerateJwtToken(accountDto);
            await CreateAccountSessionAsync(accountId, token);
        }

        private async Task CreateAccountSessionAsync(int accountId, string token)
        {
            AccountSessionsDto accountSession = new AccountSessionsDto
            {
                AccountId = accountId,
                Token = token,
                IsRevoked = false
            };
            await _authNetCoreDbContext.AccountSessions.AddAsync(accountSession);
            await _authNetCoreDbContext.SaveChangesAsync();
        }

        private async Task<AccountDto> CreateAccountAsync(AccountRegistration accountRegistration)
        {
            AccountDto account = new AccountDto
            {
                Id = accountRegistration.Id,
                Name = accountRegistration.Name,
                LastName = accountRegistration.LastName,
                Email = accountRegistration.Email,
                BirthDate = accountRegistration.BirthDate
            };
            await _authNetCoreDbContext.Account.AddAsync(account);
            await _authNetCoreDbContext.SaveChangesAsync();

            return account;
        }

        private async Task CreateAccountAuthAsync(
            string password, 
            int accountId
        )
        {
            var (passwordHash, passwordSalt) = SecurityHelper.CreatePasswordHash(password);
            AccountAuth accountAuth = new AccountAuth
            {
                AccountId = accountId,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt
            };
            await _authNetCoreDbContext.AccountAuth.AddAsync(accountAuth);
            await _authNetCoreDbContext.SaveChangesAsync();
        }

        private string GenerateJwtToken(AccountDto account)
        {
            return SecurityHelper.GenerateJwtToken(
                account.Email,
                account.Id.ToString(),
                _jwtSettings.Key,
                _jwtSettings.Issuer,
                _jwtSettings.Audience
            );
        }

        private async Task<AccountDto?> GetAccountByEmailAsync(string email)
        {
            return await _authNetCoreDbContext.Account.FirstOrDefaultAsync(a => a.Email == email);
        }

        private async Task<AccountAuth?> GetAccountAuthByAccountIdAsync(int accountId)
        {
            return await _authNetCoreDbContext.AccountAuth.FirstOrDefaultAsync(a => a.AccountId == accountId);
        }

        private async Task<(int, AccountDto)> GetAccountDtoIdByEmail(string email)
        {
            AccountDto? accountDto = await _authNetCoreDbContext.Account.FirstOrDefaultAsync(acc => acc.Email == email);
            if (accountDto == null) return (0, new AccountDto());
            return (accountDto.Id, accountDto);
        }

        private async Task<string> GetAccountSessionTokenCompromisedAsync(int id)
        {
            AccountSessionsDto? accountSessionData = await _authNetCoreDbContext.AccountSessions.FirstOrDefaultAsync(accSData => accSData.AccountId == id);
            if (accountSessionData == null) return string.Empty;
            return accountSessionData.Token;
        }

        private async Task<AccountSessionsDto> GetAccountSessionById(int id)
        {
            AccountSessionsDto? accountSessionData = await _authNetCoreDbContext.AccountSessions.FirstOrDefaultAsync(accSData => accSData.AccountId == id);
            if (accountSessionData == null) return new AccountSessionsDto();
            return accountSessionData;
        }

        private void RemoveAccountSession(AccountSessionsDto accountSessionsDto)
        {
            _authNetCoreDbContext.AccountSessions.Remove(accountSessionsDto);
        }

        private void RemoveAccountAuth(AccountAuth accountAuth)
        {
            _authNetCoreDbContext.AccountAuth.Remove(accountAuth);
        }

        private void ResetFailedLoginAttempts(AccountDto account)
        {
            account.FailedLoginAttempts = 0;
            _authNetCoreDbContext.Update(account);
            _authNetCoreDbContext.SaveChangesAsync();
        }

        private int GetAccountIdFromToken(string tokenString)
        {
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            JwtSecurityToken token = tokenHandler.ReadJwtToken(tokenString);
            Claim accountIdClaim = token.Claims.FirstOrDefault(acc => acc.Type == "account_id") ?? throw new Exception("Invalid token.");
            return Convert.ToInt32(accountIdClaim.Value);
        }

        private async Task<bool> SenderEMailRecipentAsync(string email, int id, string token)
        {
            if (!EmailServiceHelper.SendPasswordRecoveryEmail(email)) return false;
            await AddingTokenToBlackListAsync(id, token); 
            return true;
        }

        private async Task AddingTokenToBlackListAsync(int id, string token)
        {
            BlackListTokenDto blackListTokenItem = new BlackListTokenDto
            {
                AccountId = id,
                Token = token,
            };
            await _authNetCoreDbContext.BlackListToken.AddAsync(blackListTokenItem);
        }

        #endregion
    }
}
