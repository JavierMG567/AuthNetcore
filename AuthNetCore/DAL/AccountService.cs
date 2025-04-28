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

        public async Task<(AccountDto, string)> AccountAuthenticateAsync(AccountLogin accountLogin)
        {
            if (string.IsNullOrWhiteSpace(accountLogin.Email) || string.IsNullOrWhiteSpace(accountLogin.Password))
                return (new AccountDto(), string.Empty); 

            AccountDto? account = await GetAccountByEmailAsync(accountLogin.Email);

            if (account == null) 
                return (new AccountDto(), string.Empty);

            if (account.IsLocked) 
                return (new AccountDto(), string.Empty);

            await UnlockAccountAsync(account);

            if (account.IsLocked)
                return (new AccountDto(), string.Empty);

            AccountAuth? auth = await GetAccountAuthByAccountIdAsync(account.Id);

            if(!await PasswordStash(auth, accountLogin, account))
                return (new AccountDto(), string.Empty);

            return await HandleSuccessfulLoginAsync(account);
        }

        public async Task<(AccountDto?, string)> AccountRegisterAsync(AccountRegistration accountRegistration)
        {
            AccountDto? account = await CreateAccountAsync(accountRegistration);
            if (account == null) return (new AccountDto(), string.Empty);
            string token = await StablishAccountCredentialsAsync(accountRegistration.Password, account.Id, account);

            return (account, token);
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
            if (accountDto.Id == 0) return false;

            AccountSessionsDto accountSessions = await GetAccountSessionById(id);
            if (accountSessions == null) return false;

            RemoveAccountSession(accountSessions);
            AccountAuth? accountAuth = await GetAccountAuthByAccountIdAsync(id);
            if (accountAuth == null) return false;

            RemoveAccountAuth(accountAuth);
            await StablishAccountCredentialsAsync(accountResetPassword.passwordConfirmation, id, accountDto);

            return true;
        }

        public async Task<bool> RevokeTokenAsync(string token)
        {
            AccountSessionsDto? session = await _authNetCoreDbContext
                .AccountSessions
                .FirstOrDefaultAsync(acc => acc.Token == token);

            if (session == null || session.IsRevoked) return false;

            session.IsRevoked = true;
            await AddingTokenToBlackListAsync(session.AccountId, token);
            await _authNetCoreDbContext.SaveChangesAsync();

            return true;
        }

        #region AccountService Helper Methods

        private async Task AddingTokenToBlackListAsync(int id, string token)
        {
            BlackListTokenDto blackListTokenItem = new BlackListTokenDto
            {
                AccountId = id,
                Token = token,
                RevokeTokenDateTime = DateTime.Now
            };
            await _authNetCoreDbContext.BlackListToken.AddAsync(blackListTokenItem);
        }

        private async Task<AccountDto?> CreateAccountAsync(AccountRegistration accountRegistration)
        {
            if (string.IsNullOrEmpty(accountRegistration.Name))
                return new AccountDto();

            if (string.IsNullOrEmpty(accountRegistration.LastName))
                return new AccountDto();

            if (string.IsNullOrEmpty(accountRegistration.Email))
                return new AccountDto();

            if (accountRegistration.BirthDate == DateOnly.MinValue && accountRegistration.BirthDate > DateOnly.FromDateTime(DateTime.Now))
                return new AccountDto();

            AccountDto? account = await GetAccountByEmailAsync(accountRegistration.Email);

            if (account != null)
            {
                account = new AccountDto
                {
                    Id = accountRegistration.Id,
                    Name = accountRegistration.Name,
                    LastName = accountRegistration.LastName,
                    Email = accountRegistration.Email,
                    BirthDate = accountRegistration.BirthDate
                };

                await _authNetCoreDbContext.Account.AddAsync(account);
                await _authNetCoreDbContext.SaveChangesAsync();
            }

            return account;
        }

        private async Task CreateAccountAuthAsync(string password, int accountId)
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

        private async Task CreateAccountSessionAsync(int accountId, string token)
        {
            AccountSessionsDto accountSession = new AccountSessionsDto
            {
                AccountId = accountId,
                Token = token,
                IsRevoked = false,
                Created = DateTime.Now
            };
            await _authNetCoreDbContext.AccountSessions.AddAsync(accountSession);
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

        private int GetAccountIdFromToken(string tokenString)
        {
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            JwtSecurityToken token = tokenHandler.ReadJwtToken(tokenString);
            Claim accountIdClaim = token.Claims.FirstOrDefault(acc => acc.Type == "account_id") ?? throw new Exception("Invalid token.");
            return Convert.ToInt32(accountIdClaim.Value);
        }

        private async Task<AccountAuth?> GetAccountAuthByAccountIdAsync(int accountId)
        {
            return await _authNetCoreDbContext.AccountAuth.FirstOrDefaultAsync(a => a.AccountId == accountId);
        }

        private async Task<AccountDto?> GetAccountByEmailAsync(string email)
        {
            return await _authNetCoreDbContext.Account.FirstOrDefaultAsync(a => a.Email == email);
        }

        private async Task<(int, AccountDto)> GetAccountDtoIdByEmail(string email)
        {
            AccountDto? accountDto = await _authNetCoreDbContext
                .Account
                .FirstOrDefaultAsync(acc => acc.Email == email);

            if (accountDto == null) return (0, new AccountDto());
            return (accountDto.Id, accountDto);
        }

        private async Task<string> GetAccountSessionTokenCompromisedAsync(int id)
        {
            AccountSessionsDto? accountSessionData = await _authNetCoreDbContext
                .AccountSessions
                .FirstOrDefaultAsync(accSData => accSData.AccountId == id);

            if (accountSessionData == null) return string.Empty;
            return accountSessionData.Token;
        }

        private async Task<AccountSessionsDto> GetAccountSessionById(int id)
        {
            AccountSessionsDto? accountSessionData = await _authNetCoreDbContext
                .AccountSessions
                .FirstOrDefaultAsync(accSData => accSData.AccountId == id);

            if (accountSessionData == null) return new AccountSessionsDto();

            return accountSessionData;
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

        private async Task<(AccountDto, string)> HandleSuccessfulLoginAsync(AccountDto account)
        {
            string token = GenerateJwtToken(account);
            await CreateAccountSessionAsync(account.Id, token);
            ResetFailedLoginAttempts(account);

            return (account, token);
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
            account.LockoutEnd = DateTime.Now.AddMinutes(12);
        }

        private async Task<bool> PasswordStash(AccountAuth? auth, AccountLogin accountLogin, AccountDto account)
        {
            if (auth == null || !SecurityHelper.VerifyPassword(
                    accountLogin.Password,
                    auth.PasswordHash,
                    auth.PasswordSalt)
            )
            {
                await HandleFailedLoginAttemptAsync(account);
                return false;
            }
            return true;
        }

        private void RemoveAccountAuth(AccountAuth accountAuth)
        {
            _authNetCoreDbContext.AccountAuth.Remove(accountAuth);
        }

        private void RemoveAccountSession(AccountSessionsDto accountSessionsDto)
        {
            _authNetCoreDbContext.AccountSessions.Remove(accountSessionsDto);
        }

        private void ResetFailedLoginAttempts(AccountDto account)
        {
            account.FailedLoginAttempts = 0;
            _authNetCoreDbContext.Update(account);
            _authNetCoreDbContext.SaveChangesAsync();
        }

        private async Task<bool> SenderEMailRecipentAsync(string email, int id, string token)
        {
            if (!EmailServiceHelper.SendPasswordRecoveryEmail(email)) return false;
            await AddingTokenToBlackListAsync(id, token);
            return true;
        }

        private async Task<string> StablishAccountCredentialsAsync(string password, int accountId, AccountDto accountDto)
        {
            await CreateAccountAuthAsync(password, accountId);
            string token = GenerateJwtToken(accountDto);
            await CreateAccountSessionAsync(accountId, token);

            return token;
        }

        private async Task UnlockAccountAsync(AccountDto account)
        {
            if (account.IsLocked && account.LockoutEnd.HasValue)
            {
                if (DateTime.UtcNow >= account.LockoutEnd.Value)
                {
                    account.IsLocked = false;
                    account.FailedLoginAttempts = 0;
                    account.LockoutEnd = null;
                    _authNetCoreDbContext.Account.Update(account);
                    await _authNetCoreDbContext.SaveChangesAsync();
                }
            }
        }

        #endregion
    }
}
